package app.soundmesh.audio;
import java.util.Arrays;
public final class TimelineProbe {
    static byte[] packet(int value){
        byte[] out=new byte[1920];
        for(int i=0;i<1920;i+=2){out[i]=(byte)value;out[i+1]=(byte)(value>>8);}
        return out;
    }
    public static void main(String[] args){
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
