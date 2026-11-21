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
    static readonly Color Paper=Color.FromArgb(242,245,240), Ink=Color.FromArgb(26,46,42), Pine=Color.FromArgb(18,66,56), Sage=Color.FromArgb(211,236,215);
    readonly FlowLayoutPanel body=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(22)};
    readonly List<(RemoteAudio source,Label info,ProgressBar meter)> channels=[];
    readonly HashSet<string> pending=[];
    readonly ComboBox nearby=Choice(), profile=Choice(), routes=Choice();
    readonly TextBox host=Field("局域网 IP"), pin=Field("8 位配对码");
    readonly Label status=Caption("准备就绪"), sendState=Caption(""), summary=Caption("");
    readonly FlowLayoutPanel strips=Stack();
    readonly System.Windows.Forms.Timer timer=new(){Interval=450};
    readonly Button receiveTab=Btn("耳机监听"), sendTab=Btn("共享声音"), betaTab=Btn("双向空间  Beta");
    readonly Button search=Btn("刷新"), select=Btn("连接所选设备"), share=Btn("开始共享"), qr=Btn("显示配对二维码");
    readonly CheckBox quiet=new(){AutoSize=true,Text="共享时静音电脑当前输出"}, listen=new(){AutoSize=true,Text="在本机监听",Checked=true};
    readonly TrackBar master=new(){Minimum=0,Maximum=100,Value=80,Width=250,TickStyle=TickStyle.None};
    readonly Dictionary<string,Control> pages=[];
    FlowLayoutPanel receive=null!,send=null!,beta=null!;
    IPhoneBridge? iphone;
    AudioSender? sender; NetworkMixer? mixer; CancellationTokenSource? advertising;
    MMDevice? mutedDevice; bool wasMuted,duplex, scanning,closing; string view="监听",advertiseIssue="";
    public MainForm() {
        Text="声汇  ·  SoundMesh";BackColor=Paper;ForeColor=Ink;Font=new Font("Microsoft YaHei UI",10);
        ClientSize=new Size(800,740);MinimumSize=new Size(660,530);
        var header=new FlowLayoutPanel{Dock=DockStyle.Top,Height=78,BackColor=Pine,Padding=new(18,13,0,0)};
        var title=Caption("声汇",23,true);title.ForeColor=Color.White;header.Controls.Add(title);
        header.Controls.AddRange([receiveTab,sendTab,betaTab]);Controls.Add(body);Controls.Add(header);
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
        send=Card("共享本机声音");var action=Line();var safari=Btn("iPhone 浏览器监听");
        safari.Click+=async(_,_)=>{
            if(sender==null)ToggleShare();
            if(sender==null||routes.SelectedItem is not string ip){Tell("请先接入局域网并开启共享");return;}
            safari.Enabled=false;
            try{
                if(iphone==null){iphone=new IPhoneBridge(IPAddress.Parse(ip),sender.Code);await iphone.Start();routes.Enabled=false;}
                DisplayQr(iphone.Invitation,"用 iPhone 系统相机扫码，在 Safari 中打开");
            }catch(Exception e){if(iphone!=null)await iphone.DisposeAsync();iphone=null;Tell("网页监听启动失败 "+e.Message);}
            finally{safari.Enabled=true;}
        };
        action.Controls.AddRange([share,qr,safari]);send.Controls.Add(action);
        routes.Width=300;send.Controls.Add(routes);send.Controls.Add(sendState);send.Controls.Add(quiet);
        qr.Click+=(_,_)=>ShowQr();share.Click+=(_,_)=>ToggleShare();
        quiet.CheckedChanged+=(_,_)=>{if(sender!=null && !duplex)SetQuiet(quiet.Checked);};
        beta=Card("双向空间  Beta");beta.Controls.Add(Caption("各设备进入双向空间后，连接一次即可互相收发。三台以上请逐对连接。"));
        var focus=Btn("把监听切到本机");beta.Controls.Add(focus);
        focus.Click+=async(_,_)=>{listen.Checked=true;SetGain();int failed=0;
            foreach(var r in channels.ToArray()){try{await r.source.YieldPeer();}catch{failed++;}}
            status.Text=failed==0?"正在本机监听":"已开启本机监听，部分对端暂时无法切换";};
        beta.Controls.Add(listen);listen.CheckedChanged+=(_,_)=>SetGain();
        var mix=Card("混音台");pages["mix"]=mix;
        var sound=Line();sound.Controls.Add(Caption("总音量"));sound.Controls.Add(master);sound.Controls.Add(profile);mix.Controls.Add(sound);
        profile.Items.AddRange(["灵敏 · 25 ms","均衡 · 70 ms","稳定 · 90 ms"]);profile.SelectedIndex=1;
        profile.SelectedIndexChanged+=(_,_)=>{foreach(var r in channels)r.source.Ring.PrebufferMs=BufferMs;};
        master.ValueChanged+=(_,_)=>SetGain();mix.Controls.Add(summary);mix.Controls.Add(strips);body.Controls.Add(status);
        receiveTab.Click+=(_,_)=>Navigate("监听");sendTab.Click+=(_,_)=>Navigate("共享");betaTab.Click+=(_,_)=>Navigate("双向");
        timer.Tick+=(_,_)=>{foreach(var r in channels){r.info.Text=r.source.Status+"   ·   缓冲 "+r.source.Ring.BufferMs+" ms · 断流 "+r.source.Ring.Underruns+" 次";r.meter.Value=Math.Clamp((int)(r.source.Level*100),0,100);}
            summary.Text=channels.Count==0?"连接设备后可单独调节音量":"已添加 "+channels.Count+" 台设备";
            if(sender!=null)sendState.Text="配对码  "+sender.Code+"      已接入 "+sender.ClientCount+" 台    "+advertiseIssue+sender.Error;
        };
        ConfigureQuickMixer(header);
        Shown+=async(_,_)=>{Navigate("监听");await Scan();};FormClosing+=(_,_)=>{closing=true;StopSession();timer.Stop();};timer.Start();
    }
    int BufferMs=>profile.SelectedIndex switch{0=>25,2=>90,_=>70};
    void Navigate(string next){
        if(view!=next && (sender!=null||channels.Count>0||pending.Count>0)){
            if(pending.Count>0){Tell("正在连接，请稍候");return;}
            if(MessageBox.Show(this,"切换模式会结束当前连接。继续切换？","声汇",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
            StopSession();
        }
        view=next;duplex=next=="双向";
        receive.Visible=next!="共享";send.Visible=next!="监听";beta.Visible=duplex;pages["mix"].Visible=next!="共享";quiet.Visible=!duplex;
        foreach(var (button,on) in new[]{(receiveTab,next=="监听"),(sendTab,next=="共享"),(betaTab,duplex)})button.BackColor=on?Color.White:Sage;
        share.Text=sender==null?(duplex?"开启双向共享":"开始共享"):"停止共享";qr.Enabled=sender!=null;routes.Enabled=sender!=null;
        sendState.Text=sender==null?"开启后可从另一台设备发现本机或扫码配对。":sendState.Text;
        if(duplex&&sender==null)status.Text="先在参与设备上开启双向共享，再选取附近设备。";
    }
    void SetGain(){if(mixer!=null){mixer.Master=master.Value/100f;mixer.SetMonitoring(!duplex||listen.Checked);}}
    void StopSession(){
        if(iphone!=null){var bridge=iphone;iphone=null;_=bridge.DisposeAsync();}
        quiet.Enabled=true;advertising?.Cancel();advertising=null;advertiseIssue="";SetQuiet(false);sender?.Dispose();sender=null;mixer?.Dispose();mixer=null;channels.Clear();
        while(strips.Controls.Count>0)strips.Controls[0].Dispose();
    }
    void SetQuiet(bool enabled){
        try {
            if(enabled&&mutedDevice==null){using var enumerator=new MMDeviceEnumerator();mutedDevice=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
                wasMuted=mutedDevice.AudioEndpointVolume.Mute;mutedDevice.AudioEndpointVolume.Mute=true;}
            if(!enabled&&mutedDevice!=null){mutedDevice.AudioEndpointVolume.Mute=wasMuted;mutedDevice.Dispose();mutedDevice=null;}
        }catch(Exception e){Tell("输出设备音量不可用 "+e.Message);}
    }
    void ToggleShare(){
        if(sender!=null){StopSession();Navigate(view);return;}
        try{
            var routeList=Lan.Routes().OrderByDescending(x=>x.score).Select(x=>x.ip.ToString()).Distinct().ToArray();
            routes.Items.Clear();routes.Items.AddRange(routeList);if(routes.Items.Count>0)routes.SelectedIndex=0;
            sender=new AudioSender{Code=Lan.Pin};
            var instance=sender;
            if(duplex)sender.YieldRequested=()=>{if(!closing&&IsHandleCreated)BeginInvoke(new Action(()=>{if(sender==instance){listen.Checked=false;SetGain();}}));};
            if(duplex)sender.ReverseRequested=(ip,code)=>{if(!closing&&IsHandleCreated)BeginInvoke(new Action(async()=>{if(sender==instance&&duplex)await Connect(ip,code,ip,null,false);}));};
            if(duplex)listen.Checked=false;
            sender.Start(duplex||quiet.Checked);if(!duplex&&quiet.Checked)SetQuiet(true);
            quiet.Enabled=false;advertising=new CancellationTokenSource();var token=advertising.Token;
            _=Task.Run(async()=>{try{await Lan.Advertise(token);}catch(Exception){if(!token.IsCancellationRequested)advertiseIssue="发现端口不可用，可扫码配对。";}});
            Navigate(view);status.Text="共享已开启";
        }catch(Exception e){StopSession();Tell("共享启动失败 "+e.Message);}
        quiet.Enabled=sender==null;
    }
    async Task Scan(){
        if(scanning)return;scanning=true;search.Enabled=false;
        try {var found=await Lan.Scan();if(closing)return;nearby.Items.Clear();nearby.Items.AddRange(found.ToArray());if(found.Count>0)nearby.SelectedIndex=0;
            status.Text=found.Count==0?"附近暂无设备。请在同一 Wi-Fi 开启共享，或使用二维码配对。":"发现 "+found.Count+" 台设备";}
        catch(Exception e){Tell(e.Message);}finally{scanning=false;search.Enabled=true;}
    }
    async Task Connect(string ip,string key,string name,string? peerId,bool reverse){
        if(view=="共享"||duplex&&sender==null){Tell("请先开启双向共享");return;}
        if(!IPAddress.TryParse(ip,out var addr)||addr.AddressFamily!=AddressFamily.InterNetwork||IPAddress.IsLoopback(addr)||Lan.Routes().Any(r=>r.ip.Equals(addr))||!Regex.IsMatch(key,"^[0-9]{8}$")){Tell("请检查对端 IPv4 地址和 8 位配对码");return;}
        if(channels.Any(c=>c.source.Address==ip)||!pending.Add(ip))return;
        var source=new RemoteAudio(name,ip){ReverseCode=duplex&&reverse?sender?.Code:null};
        source.Ring.PrebufferMs=BufferMs;
        try {
            await source.Start(key);if(closing){source.Dispose();return;}
            mixer??=new NetworkMixer();SetGain();mixer.Add(source);AddStrip(source);
            if(peerId!=null)Lan.Save("peer."+peerId,key);status.Text="已连接 "+name;
        }catch(Exception e){source.Dispose();if(e is InvalidDataException&&peerId!=null)Lan.Save("peer."+peerId,"");Tell("连接失败 "+e.Message+"。请确认在同一局域网，电脑防火墙允许声汇通过专用网络。");}
        finally{pending.Remove(ip);}
    }
    void AddStrip(RemoteAudio source){
        var strip=Stack();strip.BackColor=Paper;strip.Padding=new(10);strip.Controls.Add(Caption(source.Name,13,true));var details=Caption("");strip.Controls.Add(details);
        var row=Line();var gain=new TrackBar{Maximum=100,Value=65,Width=210,TickStyle=TickStyle.None};var mute=new CheckBox{Text="静音",AutoSize=true};
        var meter=new ProgressBar{Width=75,Height=8,Margin=new(8,13,8,0)};var remove=Btn("移除");
        gain.ValueChanged+=(_,_)=>source.Gain=gain.Value/100f;mute.CheckedChanged+=(_,_)=>source.Muted=mute.Checked;
        remove.Click+=(_,_)=>{mixer?.Remove(source);channels.RemoveAll(c=>c.source==source);strip.Dispose();};
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
    FlowLayoutPanel Card(string title){var box=Stack();box.BackColor=Color.White;box.Padding=new(16);box.Margin=new(0,0,0,12);box.MinimumSize=new(560,0);box.Controls.Add(Caption(title,17,true));body.Controls.Add(box);return box;}
    static FlowLayoutPanel Stack()=>new(){AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
    static FlowLayoutPanel Line()=>new(){AutoSize=true,WrapContents=true,Margin=new(0,4,0,4)};
    static ComboBox Choice()=>new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=160,Margin=new(3,8,3,3)};
    static TextBox Field(string hint)=>new(){PlaceholderText=hint,Margin=new(4,7,4,2)};
    static Label Caption(string text,float size=10,bool bold=false)=>new(){Text=text,AutoSize=true,ForeColor=Ink,MaximumSize=new(620,0),Margin=new(4,6,8,7),Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular)};
    static Button Btn(string text){var b=new Button{Text=text,AutoSize=true,BackColor=Sage,ForeColor=Ink,FlatStyle=FlatStyle.Flat,Padding=new(7,3,7,3),Margin=new(4)};b.FlatAppearance.BorderSize=0;return b;}
}
