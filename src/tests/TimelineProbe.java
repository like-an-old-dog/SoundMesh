package app.soundmesh.audio;
import java.util.Arrays;
public final class TimelineProbe {
    static byte[] packet(int value){
        byte[] out=new byte[1920];
        for(int i=0;i<1920;i+=2){out[i]=(byte)value;out[i+1]=(byte)(value>>8);}
        return out;
    }
    public static void main(String[] args){
        long origin=10_000_000;
        Timeline.FrameClock clock=new Timeline.FrameClock();
        Timeline timeline=new Timeline();
        float[] output=new float[960];
        for(int i=0;i<36;i++){
            long arrival=origin+i*10_000L;
            long stamp=clock.stamp(i==0?arrival:arrival-290_000);
            if(!timeline.acceptFrame(stamp,0,arrival,packet(1000+i)))throw new AssertionError("rejected after backlog");
            if(i>0){
                timeline.read((arrival-10_000)*48/1000,output,960);
                for(float x:output)if(x!=(999+i)/32768f)throw new AssertionError("PCM gap after backlog jump");
            }
        }
        if(timeline.late!=0||timeline.gaps!=0)throw new AssertionError("backlog loss");
        clock=new Timeline.FrameClock();clock.stamp(origin);
        if(clock.stamp(origin+9950)!=origin+9950||clock.stamp(origin+200_000)!=origin+200_000)throw new AssertionError("drift trim / forward discontinuity");
        timeline.reset();timeline.acceptFrame(origin,0,origin,packet(2500));
        for(long[] t:new long[][]{{origin+60_000,0},{origin-2_010_000,0},{Long.MAX_VALUE,99},{-1,0}})
            if(timeline.acceptFrame(t[0],t[1],origin,packet(30000)))throw new AssertionError("invalid stamp accepted");
        timeline.acceptFrame(origin+5000,5000,origin+10000,packet(1000));
        timeline.read(origin*48/1000,output,960);
        if(timeline.invalidStamps!=4||timeline.late!=0)throw new AssertionError("invalid timestamp accounting");
        for(float x:output)if(x!=2500/32768f)throw new AssertionError("buffered PCM corrupted");
        System.out.println("PASS Android 290 ms backlog: zero drops, continuous PCM; bad timestamps isolated; drift trim retained");
        for(int delay:new int[]{100,150,200}){
            MixBus mix=new MixBus();mix.delayMs=delay;
            MixBus.Input a=new MixBus.Input(),b=new MixBus.Input();a.timed=b.timed=true;a.gain=b.gain=1;
            short[] pcm=new short[960];int sent=0;
            for(int tick=0;tick<400;tick++){
                long now=10000000L+tick*10000L;
                a.timeline.push(now,packet(1000+tick));
                if(tick%50>=8)while(sent<=tick){b.timeline.push(10000000L+sent*10000L,packet(4000+sent));sent++;}
                mix.renderAt(Arrays.asList(a,b),1,pcm,now);
                if(tick>=delay/10)for(short value:pcm){
                    int expected=5000+2*(tick-delay/10);
                    if(value!=(short)(expected*32767f/32768f))throw new AssertionError("wrong time slice: "+tick+"/"+delay);
                }
            }
            if(b.timeline.gaps!=0)throw new AssertionError("underrun");
            System.out.println("PASS Android 80 ms burst / aligned two-source mix at "+delay+" ms");
        }
    }
}
