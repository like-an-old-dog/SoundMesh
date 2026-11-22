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
            if(!long.TryParse(await Wire.Line(s,ct),out long remote))throw new InvalidDataException("校时响应无效");
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
        long at=next==0||Math.Abs(error)>50000?estimate:next+Math.Clamp(error,-100,100);
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
    public long Gaps {get;private set;}
    public long Late {get;private set;}
    public int AheadMs {get{lock(labels)return consumed<0?0:(int)Math.Clamp((pendingAt-consumed)/48,0,2000);}}
    public Timeline(){Reset();}
    public void Reset(){
        lock(labels){Array.Fill(labels,-1L);pendingAt=consumed=-1;seen=missing=false;Gaps=Late=0;}
    }
    public void Push(long localUs,byte[] frame){
        long start=localUs*48/1000;
        lock(labels){
            if(start<0||start<=pendingAt){Late++;return;}
            if(pendingAt>=0){
                long distance=start-pendingAt;
                // Adjacent capture blocks can run on a slightly different sampling clock.
                // A larger jump is a real discontinuity and must retain its place in the mix.
                int length=distance is >=400 and <=560?(int)distance:480;
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
                if(labels[cell]==at){output[i]=samples[cell*2];output[i+1]=samples[cell*2+1];seen=true;missing=false;}
                else if(seen&&!missing){Gaps++;missing=true;}
            }
            consumed=start+count/2;
        }
    }
}
