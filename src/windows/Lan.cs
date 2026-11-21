using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace SoundMesh;
record Peer(string Id,string Name,string Host) {
    public override string ToString() => Name+"   ·   "+Host;
}
static class Lan {
    const int Port=49633;
    static readonly byte[] Query=Encoding.ASCII.GetBytes("SOUNDMESH FIND 2");
    static RegistryKey Settings => Registry.CurrentUser.CreateSubKey(@"Software\SoundMesh\Pairing");
    public static string Read(string key) { using var settings=Settings; return settings.GetValue(key) as string ?? ""; }
    public static void Save(string key,string value) { using var settings=Settings; settings.SetValue(key,value); }
    public static string Local(string key,Func<string> factory) {
        string value=Read(key); if(value!="")return value; value=factory(); Save(key,value);return value;
    }
    public static readonly string Id=Local("id",()=>Guid.NewGuid().ToString("N"));
    public static string Pin=>Local("pin",()=>RandomNumberGenerator.GetInt32(10000000,100000000).ToString());
    public static IEnumerable<(IPAddress ip,IPAddress broadcast,int score)> Routes() {
        foreach(var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up)) {
            var p=nic.GetIPProperties();
            int score=p.GatewayAddresses.Any(g=>g.Address.AddressFamily==AddressFamily.InterNetwork)?10:0;
            if(nic.NetworkInterfaceType==NetworkInterfaceType.Wireless80211)score+=5;
            if(System.Text.RegularExpressions.Regex.IsMatch(nic.Description+" "+nic.Name,"vmware|virtual|hyper-v|wsl|vpn|tunnel|vethernet|tap-|tun ",System.Text.RegularExpressions.RegexOptions.IgnoreCase))score-=30;
            foreach(var addr in p.UnicastAddresses.Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))) {
                var b=addr.Address.GetAddressBytes();if(b[0]==169&&b[1]==254)continue;
                var mask=addr.IPv4Mask.GetAddressBytes();
                yield return (addr.Address,new IPAddress(b.Select((x,i)=>(byte)(x|~mask[i])).ToArray()),score);
            }
        }
    }
    public static async Task Advertise(CancellationToken token) {
        using var udp=new UdpClient(Port);
        var response=Encoding.UTF8.GetBytes("SM2|"+Id+"|"+Environment.MachineName.Replace('|',' '));
        while(!token.IsCancellationRequested) {
            var packet=await udp.ReceiveAsync(token);
            if(packet.Buffer.SequenceEqual(Query)) await udp.SendAsync(response,packet.RemoteEndPoint,token);
        }
    }
    public static async Task<List<Peer>> Scan() {
        using var udp=new UdpClient(0){EnableBroadcast=true};
        foreach(var ip in Routes().Select(r=>r.broadcast).Append(IPAddress.Broadcast).Distinct())
            try { await udp.SendAsync(Query,new IPEndPoint(ip,Port)); } catch(SocketException) {}
        var found=new Dictionary<string,Peer>();
        using var timeout=new CancellationTokenSource(1200);
        try { while(true) {
            var packet=await udp.ReceiveAsync(timeout.Token);
            var fields=Encoding.UTF8.GetString(packet.Buffer).Split('|');
            if(fields.Length==3&&fields[0]=="SM2"&&fields[1]!=Id&&Guid.TryParseExact(fields[1],"N",out _))
                found[fields[1]]=new Peer(fields[1],fields[2],packet.RemoteEndPoint.Address.ToString());
        }} catch(OperationCanceledException) {}
        return found.Values.ToList();
    }
}

