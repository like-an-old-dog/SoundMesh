using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using NAudio.CoreAudioApi;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace SoundMesh;
static class Program {
    [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MainForm()); }
}

sealed partial class MainForm : Form {
    static readonly Color Paper=Color.FromArgb(242,244,249), Ink=Color.FromArgb(30,36,62), Pine=Color.FromArgb(28,31,67), Sage=Color.FromArgb(235,235,253);
    readonly FlowLayoutPanel body=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(22)};
    readonly List<(RemoteAudio source,Label info,ProgressBar meter)> channels=[];
    readonly HashSet<string> pending=[];
    readonly Dictionary<RemoteAudio,TrackBar> peerSliders=new();
    readonly ComboBox nearby=Choice(), profile=Choice(), routes=Choice();
    readonly TextBox host=Field("局域网 IP"), pin=Field("8 位配对码");
    readonly Label status=Caption("准备就绪"), sendState=Caption(""), summary=Caption("");
    readonly FlowLayoutPanel strips=Stack();
    readonly System.Windows.Forms.Timer timer=new(){Interval=450};
    readonly Button search=Btn("刷新"), select=Btn("连接所选设备"), share=Btn("开始融合"), qr=Btn("显示配对二维码");
    readonly CheckBox listen=new(){AutoSize=true,Text="在本机监听",Checked=true};
    readonly TrackBar master=new LevelSlider(){Minimum=0,Maximum=100,Value=80,Width=250,TickStyle=TickStyle.None};
    readonly Dictionary<string,Control> pages=[];
    FlowLayoutPanel receive=null!,send=null!,routing=null!;
    readonly LocalSessions localApps=new();
    readonly TrackBar sendGain=new LevelSlider(){Maximum=100,Value=100,Width=220,TickStyle=TickStyle.None};
    readonly TrackBar localGain=new LevelSlider(){Maximum=100,Value=100,Width=220,TickStyle=TickStyle.None};
    IPhoneBridge? iphone;
    AudioSender? sender; NetworkMixer? mixer; CancellationTokenSource? advertising;
    bool scanning,closing; string advertiseIssue="";
    public MainForm() {
        Text="声汇  ·  SoundMesh";BackColor=Paper;ForeColor=Ink;Font=new Font("Microsoft YaHei UI",10);
        ClientSize=new Size(800,740);MinimumSize=new Size(660,530);
        using(var iconStream=typeof(MainForm).Assembly.GetManifestResourceStream("Brand.Icon")){using var appIcon=new Icon(iconStream!);Icon=(Icon)appIcon.Clone();}
        var header=BuildHeader();Controls.Add(body);Controls.Add(header);
        BuildSessionHero();
        receive=Card("附近设备");var line=Line();nearby.Width=310;line.Controls.AddRange([nearby,search,select]);receive.Controls.Add(line);
        var more=Line();var manual=Btn("手动配对");var import=Btn("读取二维码图片");more.Controls.AddRange([manual,import]);receive.Controls.Add(more);
        var fields=Line();host.Width=185;pin.Width=140;var join=Btn("连接");fields.Controls.AddRange([host,pin,join]);receive.Controls.Add(fields);fields.Visible=false;
        manual.Click+=(_,_)=>fields.Visible=!fields.Visible;
        join.Click+=async(_,_)=>await Connect(host.Text.Trim(),pin.Text.Trim(),host.Text.Trim(),null,true);
        import.Click+=async(_,_)=>{try{using var file=new OpenFileDialog{Filter="二维码图片|*.png;*.bmp;*.jpg;*.jpeg"};if(file.ShowDialog()==DialogResult.OK){
            using var original=new Bitmap(file.FileName);using var bmp=new Bitmap(original,new Size(620,620));var rgb=new byte[620*620*3];
            for(int y=0;y<620;y++)for(int x=0;x<620;x++){var c=bmp.GetPixel(x,y);int i=(y*620+x)*3;rgb[i]=c.R;rgb[i+1]=c.G;rgb[i+2]=c.B;}
            var result=new BarcodeReaderGeneric().Decode(new RGBLuminanceSource(rgb,620,620,RGBLuminanceSource.BitmapFormat.RGB24));
            var parts=(result?.Text??"").Split('/');
            if(parts.Length!=6||!result!.Text.StartsWith("soundmesh://pair/")||!Guid.TryParseExact(parts[5],"N",out _))throw new Exception("未识别到声汇配对码");
            await Connect(parts[3],parts[4],parts[3],parts[5],true);
        }}catch(Exception e){Tell(e.Message);}};
        search.Click+=async(_,_)=>await Scan();
        select.Click+=async(_,_)=>{if(nearby.SelectedItem is not Peer peer){Tell("先在另一台设备开启共享，再刷新列表");return;}
            string key=Lan.Read("peer."+peer.Id);if(key==""){key=AskPin(peer.Name);if(key=="")return;}
            await Connect(peer.Host,key,peer.Name,peer.Id,true);};
        send=Card("连接本机");send.Controls.Add(qr);
        var browser=Btn("邀请 iPhone 网页监听");send.Controls.Add(browser);
        browser.Click+=async(_,_)=>{
            if(sender==null||routes.SelectedItem is not string ip)return;
            var session=sender;browser.Enabled=false;
            try{
                if(iphone==null){iphone=new IPhoneBridge(IPAddress.Parse(ip),session.Code);await iphone.Start();}
                if(sender==session&&iphone!=null){routes.Enabled=false;DisplayQr(iphone.Invitation,"用 iPhone 系统相机扫码");}
            }catch(Exception e){if(iphone!=null)await iphone.DisposeAsync();iphone=null;Tell("网页监听启动失败 "+e.Message);}
            finally{browser.Enabled=true;}
        };
        routes.Width=300;send.Controls.Add(routes);send.Controls.Add(sendState);
        qr.Click+=(_,_)=>ShowQr();share.Click+=(_,_)=>ToggleShare();
        routing=Card("耳机输出");routing.Controls.Add(Caption("在耳机所在设备接管监听。三台或更多设备请逐对连接。"));
        var focus=Btn("把监听切到本机");routing.Controls.Add(focus);
        focus.Click+=async(_,_)=>{listen.Checked=true;SetGain();int failed=0;
            foreach(var r in channels.ToArray()){try{await r.source.YieldPeer();}catch{failed++;}}
            status.Text=failed==0?"正在本机监听":"已开启本机监听，部分对端暂时无法切换";};
        routing.Controls.Add(listen);listen.CheckedChanged+=(_,_)=>SetGain();
        var mix=Card("混音台");pages["mix"]=mix;
        var localRow=Line();
        var localLabel=Caption("本机应用   100%");
        AlignChannel(localLabel,localGain);localRow.Controls.AddRange([localLabel,localGain]);mix.Controls.Add(localRow);
        localGain.ValueChanged+=(_,_)=>{
            localLabel.Text="本机应用   "+localGain.Value+"%";
            try{localApps.Apply(localGain.Value);}catch(Exception e){Tell("本机应用音量不可用 "+e.Message);}
        };
        var outbound=Line();var outboundLabel=Caption("本机发送   100%");
        AlignChannel(outboundLabel,sendGain);outbound.Controls.AddRange([outboundLabel,sendGain]);mix.Controls.Add(outbound);
        sendGain.ValueChanged+=(_,_)=>{if(sender!=null)sender.OutboundPercent=sendGain.Value;
            outboundLabel.Text="本机发送   "+sendGain.Value+"%";SyncQuick();};
        var sound=Line();var incomingLabel=Caption("其他设备合计   80%");AlignChannel(incomingLabel,master);sound.Controls.AddRange([incomingLabel,master]);mix.Controls.Add(sound);
        master.ValueChanged+=(_,_)=>incomingLabel.Text="其他设备合计   "+master.Value+"%";
        var tuning=Line();tuning.Controls.Add(Caption("播放缓冲"));tuning.Controls.Add(profile);routing.Controls.Add(tuning);
        profile.Items.AddRange(["灵敏 · 25 ms","均衡 · 70 ms","稳定 · 90 ms"]);profile.SelectedIndex=1;
        profile.SelectedIndexChanged+=(_,_)=>{foreach(var r in channels)r.source.Ring.PrebufferMs=BufferMs;};
        master.ValueChanged+=(_,_)=>SetGain();mix.Controls.Add(summary);mix.Controls.Add(strips);body.Controls.Add(status);
        timer.Tick+=(_,_)=>{SyncQuick();if(localGain.Value!=100){try{localApps.Refresh();}catch(Exception e){Tell(e.Message);}}foreach(var r in channels){r.info.Text=r.source.Status+"   ·   缓冲 "+r.source.Ring.BufferMs+" ms · 断流 "+r.source.Ring.Underruns+" 次";r.meter.Value=Math.Clamp((int)(r.source.Level*100),0,100);}
            summary.Text=channels.Count==0?"还没有远端通道":"远端设备   "+channels.Count+" 台";
            if(sender!=null)sendState.Text="配对码  "+sender.Code+"      已接入 "+sender.ClientCount+" 台    "+advertiseIssue+sender.Error;
        };
        ConfigureQuickMixer(header);
        InstallNavigation(mix);
        Shown+=(_,_)=>UpdateSession();FormClosing+=(_,_)=>{closing=true;StopSession();timer.Stop();};timer.Start();
    }
    int BufferMs=>profile.SelectedIndex switch{0=>25,2=>90,_=>70};
    void UpdateSession(){
        bool active=sender!=null;
        share.Text=active?"结束融合":"开始融合";
        receive.Enabled=send.Enabled=routing.Enabled=pages["mix"].Enabled=active;
        quickButton.Enabled=active;
        sessionHeading.Text=active?"设备融合中":"准备融合";
        sessionDesc.Text=active?"连接同一局域网的设备后，将监听切到耳机所在设备。":"在手机和电脑上开始融合，让一副耳机听到多台设备。";
        share.BackColor=active?Color.FromArgb(61,65,112):Color.FromArgb(173,244,229);
        share.ForeColor=active?Color.White:Ink;
        SelectPage(active?currentPage:0);
        qr.Enabled=active;
        if(!active)status.Text="会话尚未启动";
    }
    void SetGain(){if(mixer!=null){mixer.Master=master.Value/100f;mixer.SetMonitoring(listen.Checked);}}
    void StopSession(){
        if(iphone!=null){var bridge=iphone;iphone=null;_=bridge.DisposeAsync();}
        try{localGain.Value=100;localApps.Restore();}catch(Exception e){Tell("恢复本机应用音量失败 "+e.Message);}
        advertising?.Cancel();advertising=null;advertiseIssue="";sender?.Dispose();sender=null;mixer?.Dispose();mixer=null;channels.Clear();peerSliders.Clear();sendGain.Value=100;
        while(strips.Controls.Count>0)strips.Controls[0].Dispose();
    }
    void ToggleShare(){
        if(sender!=null){StopSession();UpdateSession();return;}
        try{
            var routeList=Lan.Routes().OrderByDescending(x=>x.score).Select(x=>x.ip.ToString()).Distinct().ToArray();
            routes.Items.Clear();routes.Items.AddRange(routeList);if(routes.Items.Count>0)routes.SelectedIndex=0;
            sender=new AudioSender{Code=Lan.Pin,OutboundPercent=sendGain.Value};
            var instance=sender;
            sender.YieldRequested=()=>{if(!closing&&IsHandleCreated)BeginInvoke(new Action(()=>{if(sender==instance){listen.Checked=false;SetGain();}}));};
            sender.ReverseRequested=(ip,code)=>{if(!closing&&IsHandleCreated)BeginInvoke(new Action(async()=>{if(sender==instance)await Connect(ip,code,ip,null,false);}));};
            listen.Checked=false;
            sender.Start(isolated:true);
            advertising=new CancellationTokenSource();var token=advertising.Token;
            _=Task.Run(async()=>{try{await Lan.Advertise(token);}catch(Exception){if(!token.IsCancellationRequested)advertiseIssue="发现端口不可用，可扫码配对。";}});
            UpdateSession();_=Scan();status.Text="融合已开启";
        }catch(Exception e){StopSession();UpdateSession();Tell("融合启动失败 "+e.Message);}
    }
    async Task Scan(){
        if(scanning)return;scanning=true;search.Enabled=false;
        try {var found=await Lan.Scan();if(closing)return;nearby.Items.Clear();nearby.Items.AddRange(found.ToArray());if(found.Count>0)nearby.SelectedIndex=0;
            status.Text=found.Count==0?"附近暂无设备。请在同一 Wi-Fi 开启共享，或使用二维码配对。":"发现 "+found.Count+" 台设备";}
        catch(Exception e){Tell(e.Message);}finally{scanning=false;search.Enabled=true;}
    }
    async Task Connect(string ip,string key,string name,string? peerId,bool reverse){
        if(sender==null){Tell("请先开始融合");return;}
        var session=sender;
        if(!IPAddress.TryParse(ip,out var addr)||addr.AddressFamily!=AddressFamily.InterNetwork||IPAddress.IsLoopback(addr)||Lan.Routes().Any(r=>r.ip.Equals(addr))||!Regex.IsMatch(key,"^[0-9]{8}$")){Tell("请检查对端 IPv4 地址和 8 位配对码");return;}
        if(channels.Any(c=>c.source.Address==ip)||!pending.Add(ip))return;
        var source=new RemoteAudio(name,ip){ReverseCode=reverse?sender?.Code:null};
        source.Ring.PrebufferMs=BufferMs;
        try {
            await source.Start(key);if(closing||session!=sender){source.Dispose();return;}
            mixer??=new NetworkMixer();SetGain();mixer.Add(source);AddStrip(source);
            if(peerId!=null)Lan.Save("peer."+peerId,key);status.Text="已连接 "+name;
        }catch(Exception e){source.Dispose();if(e is InvalidDataException&&peerId!=null)Lan.Save("peer."+peerId,"");Tell("连接失败 "+e.Message+"。请确认在同一局域网，电脑防火墙允许声汇通过专用网络。");}
        finally{pending.Remove(ip);}
    }
    void AddStrip(RemoteAudio source){
        var strip=Stack();strip.BackColor=Paper;strip.Padding=new(10);strip.Controls.Add(Caption(source.Name,13,true));var details=Caption("");strip.Controls.Add(details);
        var row=Line();var gain=new LevelSlider{Maximum=100,Value=65,Width=210,TickStyle=TickStyle.None};var mute=new CheckBox{Text="静音",AutoSize=true};
        var meter=new ProgressBar{Width=75,Height=8,Margin=new(8,13,8,0)};var remove=Btn("移除");
        peerSliders[source]=gain;
        gain.ValueChanged+=(_,_)=>source.Gain=gain.Value/100f;mute.CheckedChanged+=(_,_)=>source.Muted=mute.Checked;
        remove.Click+=(_,_)=>{peerSliders.Remove(source);mixer?.Remove(source);channels.RemoveAll(c=>c.source==source);strip.Dispose();};
        row.Controls.AddRange([gain,mute,meter,remove]);strip.Controls.Add(row);strips.Controls.Add(strip);channels.Add((source,details,meter));
    }
    void ShowQr(){
        if(sender==null||routes.SelectedItem is not string ip)return;
        DisplayQr("soundmesh://pair/"+ip+"/"+sender.Code+"/"+Lan.Id,"用手机声汇扫码");
    }
    void DisplayQr(string invitation,string title){
        var bits=new QRCodeWriter().encode(invitation,BarcodeFormat.QR_CODE,300,300);
        using var bitmap=new Bitmap(300,300);for(int y=0;y<300;y++)for(int x=0;x<300;x++)bitmap.SetPixel(x,y,bits[x,y]?Color.Black:Color.White);
        using var dialog=new Form{Text=title,ClientSize=new(310,318),StartPosition=FormStartPosition.CenterParent};
        dialog.Controls.Add(new PictureBox{Dock=DockStyle.Fill,Image=bitmap,SizeMode=PictureBoxSizeMode.CenterImage});dialog.ShowDialog(this);
    }
    string AskPin(string name){using var f=new Form{Text="首次配对 · "+name,ClientSize=new(320,115),StartPosition=FormStartPosition.CenterParent};var box=Field("输入对端显示的 8 位配对码");box.Dock=DockStyle.Top;var ok=Btn("配对并记住");ok.Dock=DockStyle.Bottom;ok.DialogResult=DialogResult.OK;f.AcceptButton=ok;f.Controls.AddRange([box,ok]);return f.ShowDialog(this)==DialogResult.OK?box.Text.Trim():"";}
    void Tell(string message){status.Text=message;}
    FlowLayoutPanel Card(string title){var box=Stack();box.BackColor=Color.White;box.Padding=new(20,14,20,14);box.Margin=new(0,0,0,12);box.MinimumSize=new(610,0);box.Controls.Add(Caption(title,17,true));body.Controls.Add(box);return box;}
    static FlowLayoutPanel Stack()=>new(){AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
    static FlowLayoutPanel Line()=>new(){AutoSize=true,WrapContents=true,Margin=new(0,4,0,4)};
    static ComboBox Choice()=>new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=160,Margin=new(3,8,3,3)};
    static TextBox Field(string hint)=>new(){PlaceholderText=hint,Margin=new(4,7,4,2)};
    static Label Caption(string text,float size=10,bool bold=false)=>new(){Text=text,AutoSize=true,ForeColor=Ink,MaximumSize=new(620,0),Margin=new(4,6,8,7),Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular)};
    static Button Btn(string text){var b=new Button{Text=text,AutoSize=true,BackColor=Sage,ForeColor=Ink,FlatStyle=FlatStyle.Flat,Padding=new(7,3,7,3),Margin=new(4)};b.FlatAppearance.BorderSize=0;return b;}
}
