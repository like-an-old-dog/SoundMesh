using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundMesh;

public static class Wire
{
    public const int Port = 49632, Rate = 48000, FrameBytes = 1920;
    public static async Task<string> Line(Stream stream, CancellationToken token)
    {
        var b = new byte[1]; var text = new StringBuilder();
        while (text.Length < 80)
        {
            if (await stream.ReadAsync(b, token) == 0) throw new IOException("设备已断开");
            if (b[0] == 10) return text.ToString();
            text.Append((char)b[0]);
        }
        throw new IOException("连接协议无效");
    }
    public static Task WriteLine(Stream s, string text, CancellationToken token) => s.WriteAsync(Encoding.ASCII.GetBytes(text + "\n"), token).AsTask();
}

// Bounded PCM queue: prebuffer 50 ms; discard old samples rather than accumulating delay.
public sealed class AudioRing
{
    readonly float[] data = new float[48000];
    int head, count; bool ready;
    public volatile int PrebufferMs = 40;
    int recoveryMs, stableSamples;
    public int Underruns { get; private set; }
    public int TargetMs => Math.Min(160, PrebufferMs + recoveryMs);
    public int BufferMs { get { lock(data) return count / 96; } }
    public void Reset(){lock(data){head = count = 0; ready = false; recoveryMs = stableSamples = Underruns = 0;}}
    public long Dropped { get; private set; }
    public void Push(byte[] bytes)
    {
        lock (data)
        {
            for (int i = 0; i + 1 < bytes.Length; i += 2)
            {
                if (count == data.Length) { head = (head + 1) % data.Length; count--; Dropped++; }
                data[(head + count++) % data.Length] = (short)(bytes[i] | bytes[i + 1] << 8) / 32768f;
            }
            if (count > 19200) { int skip = count - Math.Max(3840, PrebufferMs * 96); head = (head + skip) % data.Length; count -= skip; Dropped += skip; }
            if (count >= TargetMs * 96) ready = true;
        }
    }
    public int Read(float[] target, int length)
    {
        Array.Clear(target, 0, length);
        lock (data)
        {
            if (!ready) return 0;
            int n = Math.Min(count, length);
            for (int i = 0; i < n; i++) target[i] = data[(head + i) % data.Length];
            head = (head + n) % data.Length; count -= n;
            if (n < length) {
                ready = false;
                Underruns++;
                recoveryMs = Math.Min(160 - PrebufferMs, recoveryMs + 25);
                stableSamples = 0;
            } else if ((stableSamples += n) >= 96 * 30000) {
                recoveryMs = Math.Max(0, recoveryMs - 5);
                stableSamples = 0;
            }
            return n;
        }
    }
}

public sealed class RemoteAudio : IDisposable
{
    public string Name { get; }
    public string Address { get; }
    public volatile float Gain = .65f, Level;
    public volatile bool Muted;
    public string Status { get; private set; } = "正在连接";
    public AudioRing Ring { get; } = new();
    public long FramesReceived { get; private set; }
    readonly CancellationTokenSource stop = new();
    TcpClient? client;
    readonly int port;
    string? pairedCode;
    public async Task YieldPeer(){
        if(pairedCode==null)return;
        using var timeout=new CancellationTokenSource(2500);
        using var control=new TcpClient(); await control.ConnectAsync(Address,port,timeout.Token);
        var stream=control.GetStream();await Wire.WriteLine(stream,"SM2-FOCUS "+pairedCode,timeout.Token);
        if(await Wire.Line(stream,timeout.Token)!="OK FOCUS")throw new IOException("对端未开启双向模式");
    }
    public string? ReverseCode {get;set;}
    public RemoteAudio(string name, string address, int port = Wire.Port) { Name = name; Address = address; this.port=port; }

    public async Task Start(string code)
    {
        var first = await Open(code).ConfigureAwait(false);
        pairedCode=code;
        _ = Task.Run(() => Listen(first, code));
    }
    async Task<TcpClient> Open(string code)
    {
        var socket = new TcpClient { NoDelay = true };
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            deadline.CancelAfter(6000);
            await socket.ConnectAsync(Address, port, deadline.Token).ConfigureAwait(false);
            var stream = socket.GetStream();
            var request = "SOUNDMESH2 " + code + (ReverseCode == null ? "" : " " + ReverseCode);
            await Wire.WriteLine(stream, request, deadline.Token).ConfigureAwait(false);
            if(await Wire.Line(stream, deadline.Token).ConfigureAwait(false) != "OK 48000 2")
                throw new InvalidDataException("配对失败，请检查配对码并将两端升级到 0.2");
            Ring.Reset();
            Status = "已连接 · 48 kHz 立体声";
            client = socket;
            return socket;
        }
        catch { socket.Dispose(); throw; }
    }
    async Task Listen(TcpClient? socket, string code)
    {
        var frame = new byte[Wire.FrameBytes];
        int delay = 1;
        try {
            while (!stop.IsCancellationRequested) {
                try {
                    socket ??= await Open(code);
                    var stream = socket.GetStream();
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    while (!stop.IsCancellationRequested) {
                        deadline.CancelAfter(15000);
                        await stream.ReadExactlyAsync(frame, deadline.Token);
                        Ring.Push(frame); FramesReceived++; delay = 1;
                    }
                }
                catch (InvalidDataException) { Status = "配对已变更，请移除并重新配对"; break; }
                catch (Exception) when (!stop.IsCancellationRequested) {
                    Status = "已断开 · " + delay + " 秒后自动重连";
                }
                finally { socket?.Dispose(); socket = null; Level = 0; }
                await Task.Delay(delay * 1000, stop.Token);
                delay = Math.Min(8, delay * 2);
            }
        } catch (Exception) when (stop.IsCancellationRequested) { }
    }
    public void Dispose() { stop.Cancel(); client?.Dispose(); Status = "已断开"; }
}

public sealed class NetworkMixer : ISampleProvider, IDisposable
{
    readonly List<RemoteAudio> sources = new();
    readonly WaveOutEvent? output;
    public volatile float Master = .8f;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Wire.Rate, 2);
    float[] scratch = new float[8192];
    float limiterGain = 1;
    public NetworkMixer(bool play = true)
    {
        if (play)
        {
            output = new WaveOutEvent { DesiredLatency = 45, NumberOfBuffers = 3 };
            try { output.Init(this); output.Play(); } catch { output.Dispose(); throw; }
        }
    }
    bool monitoring=true;
    public void SetMonitoring(bool enabled){
        if(monitoring==enabled)return;
        monitoring=enabled;
        if(enabled){lock(sources)foreach(var s in sources)s.Ring.Reset();output?.Play();}
        else output?.Stop();
    }
    public void Add(RemoteAudio source) { lock (sources) sources.Add(source); }
    public void Remove(RemoteAudio source) { lock (sources) sources.Remove(source); source.Dispose(); }
    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        if (scratch.Length < count) scratch = new float[count];
        lock (sources)
            foreach (var s in sources)
            {
                s.Ring.Read(scratch, count);
                float level = 0, gain = s.Muted ? 0 : s.Gain;
                for (int i = 0; i < count; i++) { level = Math.Max(level, Math.Abs(scratch[i])); buffer[offset + i] += scratch[i] * gain; }
                s.Level = Math.Max(level, s.Level * .8f);
            }
        float peak = .001f;
        for (int i = 0; i < count; i++) { buffer[offset + i] *= Master; peak = Math.Max(peak, Math.Abs(buffer[offset + i])); }
        float target = Math.Min(1, .95f / peak);
        limiterGain = target < limiterGain ? target : Math.Min(target, limiterGain + .01f);
        for (int i = 0; i < count; i++) buffer[offset + i] *= limiterGain;
        return count;
    }
    public void Dispose()
    {
        output?.Stop(); output?.Dispose();
        lock (sources) { foreach (var s in sources) s.Dispose(); sources.Clear(); }
    }
}

sealed class StereoSource : ISampleProvider
{
    readonly ISampleProvider source;
    float[] input = [];
    public WaveFormat WaveFormat { get; }
    public StereoSource(ISampleProvider source) { this.source=source; WaveFormat=WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate,2); }
    public int Read(float[] buffer, int offset, int count) {
        int channels=source.WaveFormat.Channels, frames=count/2;
        if(input.Length<frames*channels) input=new float[frames*channels];
        int read=source.Read(input,0,frames*channels)/channels;
        for(int i=0;i<read;i++) {
            buffer[offset+2*i]=input[i*channels];
            buffer[offset+2*i+1]=input[i*channels+Math.Min(1,channels-1)];
        }
        return read*2;
    }
}

sealed class DesktopCapture : WasapiCapture {
    public DesktopCapture(MMDevice endpoint) : base(endpoint, false, 30) {}
    protected override AudioClientStreamFlags GetAudioClientStreamFlags() =>
        base.GetAudioClientStreamFlags() | AudioClientStreamFlags.Loopback;
}

public sealed class AudioSender : IDisposable
{
    public volatile int OutboundPercent=100;
    internal static void ScalePcm(byte[] frame,int percent){
        if(percent==100)return;
        var samples=System.Runtime.InteropServices.MemoryMarshal.Cast<byte,short>(frame);
        percent=Math.Clamp(percent,0,100);
        for(int i=0;i<samples.Length;i++)samples[i]=(short)(samples[i]*percent/100);
    }
    public Action? YieldRequested {get;set;}
    public Action<string,string>? ReverseRequested {get;set;}
    public string Code { get; init; } = RandomNumberGenerator.GetInt32(10000000, 100000000).ToString();
    public int ClientCount { get { lock (clients) return clients.Count; } }
    public string Error { get; private set; } = "";
    readonly CancellationTokenSource stop = new();
    readonly TcpListener listener = new(IPAddress.Any, Wire.Port);
    readonly List<Channel<byte[]>> clients = new();
    readonly Dictionary<string, Queue<DateTime>> attempts = new();
    WasapiCapture? capture;
    MMDevice? endpoint;
    public void Start(bool isolated = false, string? helperPath = null)
    {
        try
        {
            if(isolated) {
                var info = new ProcessStartInfo(helperPath ?? Path.Combine(AppContext.BaseDirectory,"SoundMeshCapture.exe"), Environment.ProcessId.ToString()) {
                    UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
                helper=Process.Start(info) ?? throw new IOException("无法启动独立采集");
                var ready=helper.StandardError.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(9)).GetAwaiter().GetResult();
                if(ready!="READY") throw new IOException("系统未能打开进程隔离采集，请使用 Windows 11 或普通模式。 " + ready);
                listener.Start(); _=Task.Run(Accept); _=Task.Run(ReadHelper); return;
            }
            endpoint = WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice();
            capture = new DesktopCapture(endpoint);
            var buffer = new BufferedWaveProvider(capture.WaveFormat) { BufferDuration = TimeSpan.FromMilliseconds(500), DiscardOnBufferOverflow = true, ReadFully = true };
            capture.DataAvailable += (_, e) => { if (buffer.BufferedDuration.TotalMilliseconds > 200) buffer.ClearBuffer(); buffer.AddSamples(e.Buffer, 0, e.BytesRecorded); };
            capture.RecordingStopped += (_, e) => { if (e.Exception != null) { Error = "声音采集已停止：" + e.Exception.Message; stop.Cancel(); listener.Stop(); } };
            var mono = new StereoSource(buffer.ToSampleProvider());
            var pcm = new SampleToWaveProvider16(new WdlResamplingSampleProvider(mono, Wire.Rate));
            listener.Start(); capture.StartRecording();
            _ = Task.Run(Accept); _ = Task.Run(() => Pump(pcm, buffer));
        }
        catch { Dispose(); throw; }
    }
    async Task Accept()
    {
        try { while (!stop.IsCancellationRequested) { var client = await listener.AcceptTcpClientAsync(stop.Token); _ = Serve(client); } }
        catch (Exception e) { if (!stop.IsCancellationRequested) Error = "连接服务停止：" + e.Message; }
    }
    async Task Serve(TcpClient client)
    {
        Channel<byte[]>? queue = null;
        try
        {
            client.NoDelay = true; client.SendBufferSize = 65536;
            string ip = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
            lock (attempts)
            {
                foreach (var old in attempts.Where(x => x.Value.Count == 0 || (DateTime.UtcNow - x.Value.Last()).TotalMinutes > 1).Select(x => x.Key).ToArray()) attempts.Remove(old);
                if (!attempts.TryGetValue(ip, out var times)) attempts[ip] = times = new();
                while (times.Count > 0 && (DateTime.UtcNow - times.Peek()).TotalMinutes > 1) times.Dequeue();
                if (times.Count >= 6) return;
                // Only rejected authentication attempts count towards the limit.
            }
            var stream = client.GetStream();
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); handshake.CancelAfter(5000);
            var fields=(await Wire.Line(stream,handshake.Token)).Split(' ');
            if(fields.Length==2 && fields[0]=="SM2-FOCUS" && fields[1]==Code && YieldRequested!=null){
                YieldRequested(); await Wire.WriteLine(stream,"OK FOCUS",handshake.Token);return;
            }
            if(fields.Length is <2 or >3 || fields[0]!="SOUNDMESH2" || fields[1]!=Code) {
                lock(attempts) { if(!attempts.TryGetValue(ip,out var failures)) attempts[ip]=failures=new(); failures.Enqueue(DateTime.UtcNow); }
                await Wire.WriteLine(stream,"ERR",handshake.Token); return;
            }
            await Wire.WriteLine(stream, "OK 48000 2", handshake.Token);
            if(fields.Length==3 && fields[2].Length==8 && fields[2].All(char.IsAsciiDigit)) ReverseRequested?.Invoke(ip,fields[2]);
            queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(8) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });
            lock (clients) clients.Add(queue);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            await foreach (var frame in queue.Reader.ReadAllAsync(stop.Token))
            {
                deadline.CancelAfter(15000);
                await stream.WriteAsync(frame, deadline.Token);
            }
        }
        catch { /* Disconnects are isolated to this subscriber. */ }
        finally { if (queue != null) lock (clients) clients.Remove(queue); client.Dispose(); }
    }
    Process? helper;
    async Task ReadHelper() {
        try {
            var buffer=new BufferedWaveProvider(new WaveFormat(48000,16,2)){DiscardOnBufferOverflow=true,BufferDuration=TimeSpan.FromMilliseconds(400)};
            var pump=Task.Run(()=>Pump(buffer, buffer));
            var bytes=new byte[8192];
            var stream=helper!.StandardOutput.BaseStream;
            while(!stop.IsCancellationRequested) {
                int n=await stream.ReadAsync(bytes,stop.Token);
                if(n==0) throw new IOException("隔离采集已结束");
                buffer.AddSamples(bytes,0,n);
            }
        } catch(Exception e) { if(!stop.IsCancellationRequested){Error=e.Message;stop.Cancel();listener.Stop();} }
    }
    async Task Pump(IWaveProvider pcm, BufferedWaveProvider captureBuffer)
    {
        var clock = Stopwatch.StartNew(); double next = 0;
        bool primed=false;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var frame = new byte[Wire.FrameBytes];
                double queuedMs=captureBuffer.BufferedDuration.TotalMilliseconds;
                if(queuedMs >= 35)primed=true;
                if(queuedMs < 10)primed=false;
                if(primed){pcm.Read(frame,0,frame.Length);ScalePcm(frame,OutboundPercent);}
                lock (clients) foreach (var queue in clients) queue.Writer.TryWrite(frame);
                next += 10;
                double wait = next - clock.Elapsed.TotalMilliseconds;
                if (wait > 0) await Task.Delay(TimeSpan.FromMilliseconds(wait), stop.Token);
                else if (wait < -50) next = clock.Elapsed.TotalMilliseconds;
            }
        }
        catch (Exception e) { if (!stop.IsCancellationRequested) Error = "音频发送停止：" + e.Message; }
    }
    public void Dispose() { stop.Cancel(); listener.Stop(); if(helper!=null){try{if(!helper.HasExited)helper.Kill();}catch{} helper.Dispose();helper=null;} capture?.StopRecording(); capture?.Dispose(); capture = null; endpoint?.Dispose(); endpoint=null; }
}

