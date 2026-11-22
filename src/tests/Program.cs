using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SoundMesh;

if(args.Length==3 && args[0]=="--duplex-emulator"){
    using var session=new AudioSender{Code="12345678"};
    using var mixer=new NetworkMixer(false){DelayMs=150};
    var reverse=new TaskCompletionSource<string>();
    session.ReverseRequested=(_,pin)=>reverse.TrySetResult(pin);
    session.Start(true,args[2]);
    Console.WriteLine("READY");
    var key=await reverse.Task.WaitAsync(TimeSpan.FromSeconds(80));
    using var input=new RemoteAudio("Emulator via ADB","127.0.0.1",int.Parse(args[1])){PreferTimestamps=true};
    await input.Start(key);mixer.Add(input);
    using var timeout=new CancellationTokenSource(15000);
    var frame=new float[960];
    while(input.FramesReceived<500){mixer.Read(frame,0,frame.Length);await Task.Delay(10,timeout.Token);}
    if(session.ClientCount!=1||session.Error!="")throw new Exception("Duplex send path interrupted");
    if(!input.Timestamped||input.Timeline.AheadMs<=0)throw new Exception("Timestamp alignment unavailable");
    await input.YieldPeer();
    Console.WriteLine("PASS Windows isolated sender and Android exchanged clock-aligned audio simultaneously for 500 inbound frames; authenticated monitor handoff acknowledged");
    return;
}
int passed = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL " + label); Console.WriteLine("PASS " + label); passed++; }
byte[] Pcm(short value) { var b = new byte[Wire.FrameBytes]; for (int i = 0; i < b.Length; i += 2) { b[i] = (byte)value; b[i + 1] = (byte)(value >> 8); } return b; }
foreach(int delayMs in new[]{100,150,200}){
    using var mix=new NetworkMixer(false){DelayMs=delayMs,Master=1};
    var first=new RemoteAudio("A","127.0.0.1"){Timestamped=true,Gain=1};
    var second=new RemoteAudio("B","127.0.0.1"){Timestamped=true,Gain=1};
    mix.Add(first);mix.Add(second);
    var rendered=new float[960];int sent=0;
    for(int tick=0;tick<400;tick++){
        long now=10000000L+tick*10000;
        first.Timeline.Push(now,Pcm((short)(1000+tick)));
        if(tick%50>=8){
            while(sent<=tick){second.Timeline.Push(10000000L+sent*10000,Pcm((short)(4000+sent)));sent++;}
        }
        mix.ReadAt(rendered,0,rendered.Length,now);
        if(tick>=delayMs/10){
            float expected=(5000+2*(tick-delayMs/10))/32768f;
            if(rendered.Any(x=>x!=expected))throw new Exception("timeline mismatch at "+tick+" delay "+delayMs);
        }
    }
    Check(second.Timeline.Gaps==0,$"two sources remain sample-aligned through 80ms bursts at {delayMs}ms");
}
{
    var timeline=new Timeline();var dest=new float[960];long baseUs=10000000;
    timeline.Push(baseUs,Pcm(1000));timeline.Push(baseUs+10000,Pcm(2000));
    timeline.Read(baseUs*48/1000,dest,960);timeline.Read(baseUs*48/1000+480,dest,960);
    timeline.Push(baseUs+20000,Pcm(3000));timeline.Push(baseUs+30000,Pcm(4000));
    timeline.Read((baseUs+20000)*48/1000,dest,960);
    Check(timeline.Late==1&&timeline.Gaps==1&&dest.All(x=>x==3000/32768f),"late packet skipped and next valid slot recovered");
    timeline.Reset();timeline.Read((baseUs+20000)*48/1000,dest,960);
    Check(dest.All(x=>x==0),"reconnect clears old timeline audio");
}
var signedStereo=Pcm(-12000);
for(int i=2;i<signedStereo.Length;i+=4){signedStereo[i]=0x60;signedStereo[i+1]=0x09;}
AudioSender.ScalePcm(signedStereo,25);
Check(BitConverter.ToInt16(signedStereo,0)==-3000 && BitConverter.ToInt16(signedStereo,2)==600,"local send gain adjusts both signed stereo channels");
AudioSender.ScalePcm(signedStereo,0);
Check(signedStereo.All(b=>b==0),"local send gain can silence outbound without altering the remote mixer");
var ring = new AudioRing(); var samples = new float[480]; ring.Push(Pcm(-16384));
Check(ring.Read(samples, 480) == 0, "C# buffer waits for pre-roll");
for (int i = 0; i < 4; i++) ring.Push(Pcm(-16384));
Check(ring.Read(samples, 480) == 480 && samples[0] == -.5f, "C# signed PCM conversion");
for (int i = 0; i < 200; i++) ring.Push(Pcm(8192));
Check(ring.Dropped > 0, "C# backlog stays bounded"); ring.Read(samples, 480);
Check(samples[0] == .25f, "C# discards stale audio");
for (int i = 0; i < 100; i++) ring.Read(samples, 480);
Check(ring.Read(samples, 480) == 0 && samples.All(x => x == 0), "C# underrun is silence");
using (var mix = new NetworkMixer(play: false))
{
    mix.Master = .5f;
    var a = new RemoteAudio("A", "127.0.0.1") { Gain = .5f };
    var b = new RemoteAudio("B", "127.0.0.1") { Gain = 1, Muted = true };
    for (int i = 0; i < 5; i++) { a.Ring.Push(Pcm(16384)); b.Ring.Push(Pcm(32767)); }
    mix.Add(a); mix.Add(b); var output = Enumerable.Repeat(42f, 500).ToArray(); mix.Read(output, 10, 480);
    Check(output[10] == .125f, "per-device gain, mute and master gain compose correctly");
    Check(output[9] == 42 && output[490] == 42, "mixer respects output offset");
    for (int i = 0; i < 64; i++) { var s = new RemoteAudio("source" + i, "127.0.0.1") { Gain = 1 }; for (int j = 0; j < 5; j++) s.Ring.Push(Pcm(32767)); mix.Add(s); }
    mix.Read(output, 10, 480);
    Check(output.Skip(10).Take(480).All(x => float.IsFinite(x) && Math.Abs(x) <= .951f), "66-source mixing stays within output range");
    mix.Master = 0; mix.Read(output, 10, 480);
    Check(output.Skip(10).Take(480).All(x => x == 0), "master mute produces silence");
}
bool oversized = false;
try { await Wire.Line(new MemoryStream(new byte[81]), CancellationToken.None); } catch (IOException) { oversized = true; }
Check(oversized, "C# rejects oversized handshake");
using (var listener = new TcpListener(IPAddress.Loopback, Wire.Port))
{
    listener.Start();
    var server = Task.Run(async () => { using var c = await listener.AcceptTcpClientAsync(); var s = c.GetStream(); await Wire.Line(s, CancellationToken.None); await Wire.WriteLine(s, "ERR", CancellationToken.None); });
    using var remote = new RemoteAudio("wrong pin", "127.0.0.1"); bool rejected = false;
    try { await remote.Start("00000000"); } catch (InvalidDataException) { rejected = true; }
    await server; Check(rejected, "incorrect pairing is rejected");
}
if (args.Length > 0)
{
    Console.WriteLine("Java test classes: " + args[0]);
    Process Java(string mode) { var info = new ProcessStartInfo(args.Length > 1 ? args[1] : "java") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true }; info.ArgumentList.Add("-cp"); info.ArgumentList.Add(args[0]); info.ArgumentList.Add("WireProbe"); info.ArgumentList.Add(mode); return Process.Start(info)!; }
    using (var listener = new TcpListener(IPAddress.Loopback, Wire.Port))
    {
        listener.Start(); using var java = Java("receive");
        using var timeout = new CancellationTokenSource(10000);
        var accepting = listener.AcceptTcpClientAsync(timeout.Token).AsTask();
        if (await Task.WhenAny(accepting, java.WaitForExitAsync(timeout.Token)) != accepting)
            throw new Exception("Java fixture exited before connecting: " + await java.StandardError.ReadToEndAsync());
        using var c = await accepting; var stream = c.GetStream();
        Check(await Wire.Line(stream, timeout.Token) == "SOUNDMESH2 12345678", "Android Java codec connects to C# sender");
        foreach (byte b in Encoding.ASCII.GetBytes("OK 48000 2\n")) await stream.WriteAsync(new byte[] { b }, timeout.Token);
        var frame = Pcm(8192);
        for (int i = 0; i < 5; i++) { await stream.WriteAsync(frame.AsMemory(0, 17), timeout.Token); await stream.WriteAsync(frame.AsMemory(17), timeout.Token); }
        c.Close(); await java.WaitForExitAsync(timeout.Token); Console.Write(await java.StandardOutput.ReadToEndAsync());
        Check(java.ExitCode == 0, "Android JVM receives fragmented C# stream and detects EOF");
    }
    using (var java = Java("send"))
    {
        using var timeout = new CancellationTokenSource(10000);
        Check(await java.StandardOutput.ReadLineAsync(timeout.Token) == "READY", "Java fixture started");
        using var remote = new RemoteAudio("Java", "127.0.0.1"); await remote.Start("12345678");
        await java.WaitForExitAsync(timeout.Token);
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 2000 && !remote.Status.StartsWith("已断开")) await Task.Delay(10);
        Check(remote.Ring.Read(samples, 480) == 480 && samples[0] == -.25f, "C# client receives Java PCM stream");
        Check(remote.Status.StartsWith("已断开"), "C# client detects peer disconnect");
        Console.Write(await java.StandardOutput.ReadToEndAsync()); Check(java.ExitCode == 0, "Java sender completed");
    }
}
// Reuse a single receiver instance across a forced disconnect and verify signed stereo content.
using(var listener=new TcpListener(IPAddress.Loopback,Wire.Port)){
    listener.Start(); using var timeout=new CancellationTokenSource(8000);
    var stereo=Pcm(8192);
    for(int i=2;i<stereo.Length;i+=4){stereo[i]=0;stereo[i+1]=0xe0;}
    var server=Task.Run(async()=>{
        for(int pass=0;pass<2;pass++){
            using var peer=await listener.AcceptTcpClientAsync(timeout.Token);
            var stream=peer.GetStream(); var hello=await Wire.Line(stream,timeout.Token);
            if(hello!="SOUNDMESH2 12345678 87654321")throw new Exception("duplex offer missing");
            await Wire.WriteLine(stream,"OK 48000 2",timeout.Token);
            for(int i=0;i<6;i++)await stream.WriteAsync(stereo,timeout.Token);
        }
    });
    using var remote=new RemoteAudio("reconnect","127.0.0.1"){Gain=.75f,Muted=true,ReverseCode="87654321"};
    await remote.Start("12345678");await server;
    while(remote.FramesReceived<12)await Task.Delay(10,timeout.Token);
    Check(remote.Muted&&remote.Gain==.75f,"forced reconnect preserves mix settings and duplex offer");
    var signal=new float[960];remote.Ring.Read(signal,signal.Length);
    Check(signal[0]==.25f&&signal[1]==-.25f,"stereo polarity survives reconnect without downmix");
}
if(args.Contains("--capture") && Environment.GetEnvironmentVariable("SOUNDMESH_CAPTURE_HELPER") is string helper){
    using var isolated=new AudioSender();isolated.Start(true,helper);
    using var receiver=new RemoteAudio("process loopback","127.0.0.1");await receiver.Start(isolated.Code);
    using var timeout=new CancellationTokenSource(4000);
    while(receiver.FramesReceived<30)await Task.Delay(15,timeout.Token);
    Check(isolated.Error=="","Windows process-excluding capture opens and streams 30 frames");
}
if (args.Contains("--capture"))
{
    using var sender = new AudioSender(); sender.Start();
    using var remote = new RemoteAudio("local capture smoke", "127.0.0.1"); await remote.Start(sender.Code);
    var watch = Stopwatch.StartNew();
    while (remote.FramesReceived < 10 && watch.ElapsedMilliseconds < 3000) await Task.Delay(10);
    Check(remote.FramesReceived >= 10 && sender.Error.Length == 0, "Windows WASAPI capture -> local TCP receiver (no audio saved)");
}
Console.WriteLine($"RESULT: {passed} C# checks passed");
