using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SoundMesh;

sealed class IPhoneBridge : IAsyncDisposable {
    const int HttpPort=49634;
    readonly string secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    readonly ConcurrentDictionary<string,(string Name, string Host, string Pin)> sources=new();
    readonly CancellationTokenSource stopped=new();
    readonly IPAddress local;
    WebApplication? app;
    public string Invitation=>"http://"+local+":"+HttpPort+"/#key="+secret;
    public IPhoneBridge(IPAddress address,string pin){
        local=address;sources["pc"]=("这台电脑","127.0.0.1",pin);
    }
    bool Valid(string? supplied)=>supplied!=null&&supplied.Length==secret.Length&&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied),Encoding.ASCII.GetBytes(secret));
    static async Task<TcpClient> Connect(string host,string pin,CancellationToken token){
        var tcp=new TcpClient{NoDelay=true};
        try {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(5500);
            await tcp.ConnectAsync(host,Wire.Port,deadline.Token);
            var stream=tcp.GetStream();
            await Wire.WriteLine(stream,"SOUNDMESH2 "+pin,deadline.Token);
            if(await Wire.Line(stream,deadline.Token)!="OK 48000 2")throw new InvalidDataException("配对码不正确");
            return tcp;
        }catch{tcp.Dispose();throw;}
    }
    public async Task Start(){
        var builder=WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server=>{
            server.Listen(local,HttpPort);
            server.Limits.MaxRequestBodySize=2048;
        });
        app=builder.Build();
        app.UseWebSockets();
        app.Use(async (ctx,next)=>{
            ctx.Response.Headers.CacheControl="no-store";
            ctx.Response.Headers["Referrer-Policy"]="no-referrer";
            ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
            if(ctx.Request.Path.StartsWithSegments("/api")&&!Valid(ctx.Request.Headers.Authorization.ToString().Replace("Bearer ","",StringComparison.Ordinal))){
                ctx.Response.StatusCode=401;return;
            }
            await next(ctx);
        });
        foreach(var (url,name,type) in new[]{("/","index.html","text/html"),("/styles.css","styles.css","text/css"),("/app.js","app.js","text/javascript"),("/audio.js","audio.js","text/javascript")}){
            app.MapGet(url,()=>Results.Stream(typeof(IPhoneBridge).Assembly.GetManifestResourceStream("Web."+name)!,type+"; charset=utf-8"));
        }
        app.MapGet("/api/sources",()=>Results.Json(new{sources=sources.Select(s=>new{id=s.Key,name=s.Value.Name})}));
        app.MapGet("/api/discover",async()=>Results.Json(new{devices=(await Lan.Scan()).Select(p=>new{id=p.Id,name=p.Name,host=p.Host})}));
        app.MapPost("/api/sources", async (HttpContext ctx)=>{
            try{
                using var body=await JsonDocument.ParseAsync(ctx.Request.Body,cancellationToken:ctx.RequestAborted);
                var item=body.RootElement;string? host=item.GetProperty("host").GetString(),pin=item.GetProperty("pin").GetString();
                if(!IPAddress.TryParse(host,out var address)||address.AddressFamily!=AddressFamily.InterNetwork||!OnLink(address)||pin?.Length!=8||!pin.All(char.IsAsciiDigit))
                    return Results.BadRequest(new{error="请使用同一局域网的 IPv4 地址和八位配对码"});
                if(address.Equals(local))return Results.BadRequest(new{error="这台电脑已在混音台"});
                using var verified=await Connect(address.ToString(),pin,ctx.RequestAborted);
                var name=item.TryGetProperty("name",out var label)?label.GetString():null;
                var id=address.ToString();sources[id]=(string.IsNullOrWhiteSpace(name)?id:name,id,pin);
                return Results.Json(new{id,name=sources[id].Name});
            }catch(Exception){return Results.BadRequest(new{error="连接失败，请检查发送端、地址和配对码"});}
        });
        app.MapGet("/listen/{id}", Stream);
        await app.StartAsync();
    }
    bool OnLink(IPAddress address){
        var peer=address.GetAddressBytes();
        foreach(var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            foreach(var adapter in nic.GetIPProperties().UnicastAddresses.Where(a=>a.Address.Equals(local))){
                byte[] host=local.GetAddressBytes(), mask=adapter.IPv4Mask.GetAddressBytes();
                if(Enumerable.Range(0,4).All(i=>(host[i]&mask[i])==(peer[i]&mask[i])))return true;
            }
        return false;
    }
    async Task Stream(HttpContext ctx,string id){
        if(!ctx.WebSockets.IsWebSocketRequest||!sources.TryGetValue(id,out var source)){ctx.Response.StatusCode=404;return;}
        if(ctx.Request.Headers.Origin.ToString()!="http://"+ctx.Request.Host){ctx.Response.StatusCode=403;return;}
        using var socket=await ctx.WebSockets.AcceptWebSocketAsync();
        using var life=CancellationTokenSource.CreateLinkedTokenSource(stopped.Token,ctx.RequestAborted);
        try{
            life.CancelAfter(5000);
            var login=new byte[512];int used=0;
            while(used<login.Length){
                var r=await socket.ReceiveAsync(login.AsMemory(used),life.Token);
                if(r.MessageType!=WebSocketMessageType.Text)return;
                used+=r.Count;if(r.EndOfMessage)break;
            }
            using var json=JsonDocument.Parse(login.AsMemory(0,used));
            if(!Valid(json.RootElement.GetProperty("key").GetString()))return;
            life.CancelAfter(Timeout.Infinite);
            using var tcp=await Connect(source.Host,source.Pin,life.Token);
            await socket.SendAsync("{\"format\":\"s16le\",\"rate\":48000,\"channels\":2}"u8.ToArray(),WebSocketMessageType.Text,true,life.Token);
            var audio=tcp.GetStream();var frame=new byte[Wire.FrameBytes*2];
            while(!life.IsCancellationRequested){
                life.CancelAfter(15000);
                await audio.ReadExactlyAsync(frame,life.Token);
                await socket.SendAsync(frame.AsMemory(),WebSocketMessageType.Binary,true,life.Token);
            }
        }catch(Exception){}finally{socket.Abort();}
    }
    public async ValueTask DisposeAsync(){
        stopped.Cancel();if(app!=null){await app.StopAsync();await app.DisposeAsync();app=null;}stopped.Dispose();
    }
}