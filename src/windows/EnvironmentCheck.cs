using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
namespace SoundMesh;

sealed class EnvironmentCheck : Form {
    readonly ComboBox host=new(){Width=245};
    readonly Button start=new(){Text="测量 20 次",AutoSize=true};
    readonly Label result=new(){AutoSize=false,Dock=DockStyle.Bottom,Height=78};
    readonly Panel graph=new(){Dock=DockStyle.Fill,BackColor=Color.White};
    readonly List<double?> samples=new();
    readonly CancellationTokenSource closed=new();
    public EnvironmentCheck(IEnumerable<string> hosts){
        Text="检查环境 · 对端通信质量";ClientSize=new(620,350);Padding=new(14);Font=new Font("Microsoft YaHei UI",10);
        foreach(string ip in hosts.Distinct())host.Items.Add(ip);
        if(host.Items.Count>0)host.SelectedIndex=0;
        var row=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true};
        row.Controls.AddRange([new Label{Text="对端 IPv4",AutoSize=true},host,start]);
        Controls.Add(graph);Controls.Add(result);Controls.Add(row);
        result.Text="输入对端显示的 IP，并让对端开始融合。图中每个点是一次 TCP 建连和声汇应答耗时。";
        start.Click+=async(_,_)=>await Measure();graph.Paint+=Draw;
        FormClosing+=(_,_)=>closed.Cancel();
    }
    async Task Measure(){
        if(!IPAddress.TryParse(host.Text.Trim(),out var ip)||ip.AddressFamily!=AddressFamily.InterNetwork||
           IPAddress.IsLoopback(ip)||Lan.Routes().Any(r=>r.ip.Equals(ip))){result.Text="请输入另一台设备显示的 IPv4 地址。";return;}
        start.Enabled=host.Enabled=false;samples.Clear();string reason="";
        try{for(int i=0;i<20;i++){
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(closed.Token);timeout.CancelAfter(1800);
            var watch=Stopwatch.StartNew();bool connected=false;
            try{
                using var tcp=new TcpClient{NoDelay=true};await tcp.ConnectAsync(ip,Wire.Port,timeout.Token);connected=true;
                var stream=tcp.GetStream();await Wire.WriteLine(stream,"SM-DIAG1",timeout.Token);
                if(await Wire.Line(stream,timeout.Token)!="SM-READY1"){result.Text="TCP 可达，但对端未识别检测协议。请先更新对端声汇。";break;}
                samples.Add(watch.Elapsed.TotalMilliseconds);reason="";
            }catch(Exception e) when(!closed.IsCancellationRequested){samples.Add(null);reason=(connected?"声汇应答":"TCP 49632")+"失败 · "+NetFailure.Describe(e);}
            if(closed.IsCancellationRequested)return;
            var valid=samples.OfType<double>().Order().ToArray();int failed=samples.Count-valid.Length;
            if(valid.Length==0)result.Text=$"{samples.Count} 次均失败。{reason}\n请两端互测，检查 IP、对端融合状态、防火墙和校园网无线客户端隔离。";
            else{
                double p95=valid[(int)Math.Ceiling(valid.Length*.95)-1],median=valid[valid.Length/2];
                string grade=failed>0||p95>100?"有中断或高延迟，建议同步 200 ms 并检查 Wi-Fi":"本次可达性良好，可从同步 150 ms 开始";
                result.Text=$"{grade}\n成功 {valid.Length}/{samples.Count}  中位 {median:F0} ms  P95 {p95:F0} ms  波动范围 {valid.Last()-valid.First():F0} ms\n{reason}";
            }
            graph.Invalidate();await Task.Delay(450,closed.Token);
        }}catch(OperationCanceledException){}finally{if(!IsDisposed)start.Enabled=host.Enabled=true;}
    }
    void Draw(object? sender,PaintEventArgs e){
        var g=e.Graphics;float w=graph.Width-65,h=graph.Height-40;if(w<10||h<10)return;
        double top=Math.Max(50,samples.OfType<double>().DefaultIfEmpty(0).Max());PointF? prev=null;
        g.DrawString($"{top:F0} ms",Font,Brushes.DimGray,0,2);g.DrawString("0",Font,Brushes.DimGray,14,h);
        g.DrawRectangle(Pens.LightGray,54,12,w,h);
        using var line=new Pen(Color.SlateBlue,2);
        for(int i=0;i<samples.Count;i++){
            float x=54+i*w/19;if(samples[i] is not double value){g.DrawString("×",Font,Brushes.Firebrick,x-5,h/2);prev=null;continue;}
            var p=new PointF(x,12+h*(1-(float)(value/top)));
            if(prev is PointF last)g.DrawLine(line,last,p);g.FillEllipse(Brushes.SlateBlue,x-3,p.Y-3,6,6);prev=p;
        }
    }
}