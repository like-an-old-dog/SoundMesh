using System.Diagnostics;
using System.Buffers.Binary;
using System.Net.Sockets;
namespace SoundMesh;

public static class SyncClock {
    public static long NowUs => Stopwatch.GetElapsedTime(0).Ticks/10;
    public static async Task<long> Measure(NetworkStream s,CancellationToken ct) {
        long best=long.MaxValue,offset=0;
        for(int i=0;i<5;i++){
            long start=NowUs;
            await Wire.WriteLine(s,"PING",ct);
            if(!long.TryParse(await Wire.Line(s,ct),out long remote)||remote<0||remote>long.MaxValue/48)throw new InvalidDataException("校时响应无效");
            long end=NowUs,rtt=end-start;
            if(rtt<best){best=rtt;offset=start+rtt/2-remote;}
        }
        return offset;
    }
    public static async Task Respond(NetworkStream s,CancellationToken ct){
        for(int i=0;i<5;i++){
            if(await Wire.Line(s,ct)!="PING")throw new InvalidDataException();
            await Wire.WriteLine(s,NowUs.ToString(),ct);
        }
    }
}
// Assign sample-contiguous timestamps between discontinuities, ignoring capture callback jitter.
public sealed class FrameClock {
    long next;
    public long Stamp(long estimate){
        long error=estimate-next;
        // A backward jump indicates capture backlog. Continue from the next audio block.
        long at = next == 0 || error > 50000 ? estimate
                : error < -50000 ? next
                : next + Math.Clamp(error, -100, 100);
        next=at+10000;return at;
    }
}
public sealed class PlayoutClock {
    long next=-1;int previousDelay;
    public long Advance(long nowUs,int frames,int delayMs){
        long ideal=(nowUs-delayMs*1000L)*48/1000;
        if(next<0||delayMs!=previousDelay||Math.Abs(next-ideal)>4800)next=ideal;
        else if(Math.Abs(next-ideal)>960)next+=Math.Sign(ideal-next);
        long start=next;next+=frames;previousDelay=delayMs;return start;
    }
}
public sealed class Timeline {
    const int Capacity=48000*2;
    readonly long[] labels=new long[Capacity];
    readonly float[] samples=new float[Capacity*2];
    readonly byte[] pending=new byte[Wire.FrameBytes];
    long pendingAt=-1,consumed=-1;
    bool seen,missing;
    float lastLeft,lastRight;
    int fadeOut,fadeIn;
    public long Gaps {get;private set;}
    public long Late {get;private set;}
    public long InvalidStamps {get;private set;}
    public int AheadMs {get{lock(labels)return consumed<0?0:(int)Math.Clamp((pendingAt-consumed)/48,0,2000);}}
    public Timeline(){Reset();}
    public void Reset(){
        lock(labels){Array.Fill(labels,-1L);pendingAt=consumed=-1;seen=missing=false;lastLeft=lastRight=0;fadeIn=fadeOut=0;Gaps=Late=InvalidStamps=0;}
    }
    // Check wire input before it can advance pendingAt or enter either audio queue.
    public bool AcceptFrame(long sourceUs,long offsetUs,long arrivalUs,byte[] frame){
        if(sourceUs>=0 && (offsetUs<=0 || sourceUs<=long.MaxValue-offsetUs)){
            long localUs=sourceUs+offsetUs;
            if(localUs>=0 && localUs>=arrivalUs-2_000_000 && localUs<=arrivalUs+50_000){
                Push(localUs,frame);
                return true;
            }
        }
        lock(labels)InvalidStamps++;
        return false;
    }
    internal void Push(long localUs,byte[] frame){
        long start=localUs*48/1000;
        lock(labels){
            if(start<0||start<=pendingAt){Late++;return;}
            if(pendingAt>=0){
                long distance=start-pendingAt;
                // Adjacent capture blocks can run on a slightly different sampling clock.
                // A larger jump is a real discontinuity and must retain its place in the mix.
                int length=distance is >=470 and <=490?(int)distance:480;
                if(pendingAt+length<=consumed)Late++;
                else for(int i=0;i<length;i++){
                    long at=pendingAt+i;if(at<consumed)continue;
                    int cell=(int)(at%Capacity), fraction=i*480%length;
                    int src=i*480/length,other=Math.Min(479,src+1);
                    for(int ch=0;ch<2;ch++){
                        short a=BinaryPrimitives.ReadInt16LittleEndian(pending.AsSpan(4*src+2*ch));
                        short b=BinaryPrimitives.ReadInt16LittleEndian(pending.AsSpan(4*other+2*ch));
                        samples[cell*2+ch]=(a+(b-a)*fraction/(float)length)/32768f;
                    }
                    labels[cell]=at;
                }
            }
            pendingAt=start;frame.CopyTo(pending,0);
        }
    }
    public void Read(long start,float[] output,int count){
        Array.Clear(output,0,count);
        lock(labels){
            for(int i=0;i+1<count;i+=2){
                long at=start+i/2;if(at<0)continue;
                int cell=(int)(at%Capacity);
                if(labels[cell]==at){
                    if(missing)fadeIn=96;
                    float gain=fadeIn>0?(97-fadeIn--)/96f:1f;
                    output[i]=lastLeft=samples[cell*2]*gain;
                    output[i+1]=lastRight=samples[cell*2+1]*gain;
                    seen=true;missing=false;
                }else if(seen){
                    if(!missing){Gaps++;missing=true;fadeOut=96;}
                    if(fadeOut>0){float gain=--fadeOut/96f;output[i]=lastLeft*gain;output[i+1]=lastRight*gain;}
                }
            }
            consumed=start+count/2;
        }
    }
}
