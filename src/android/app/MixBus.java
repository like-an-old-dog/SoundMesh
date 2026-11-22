package app.soundmesh.audio;

import java.util.Arrays;

/** Hardware-independent mixer shared by Android playback and the JVM tests. */
public final class MixBus {
    public static class Input {
        public final AudioRing ring=new AudioRing();
        final Timeline timeline=new Timeline();
        volatile boolean timed;
        public volatile float gain=.65f,level;
        public volatile boolean muted;
    }
    private final float[] sum=new float[960],scratch=new float[960];
    int delayMs;
    private final Timeline.PlayoutClock clock=new Timeline.PlayoutClock();
    private float limiter=1f;
    public void render(Iterable<? extends Input> inputs,float master,short[] pcm) {
        renderAt(inputs,master,pcm,Timeline.nowUs());
    }
    void renderAt(Iterable<? extends Input> inputs,float master,short[] pcm,long nowUs) {
        if(pcm.length!=960)throw new IllegalArgumentException("Expected 960 samples");
        Arrays.fill(sum,0f);
        long start=clock.advance(nowUs,480,delayMs);
        for(Input input:inputs) {
            if(input.timed && delayMs>=100)input.timeline.read(start,scratch,960);
            else input.ring.read(scratch,960);
            float peak=0,gain=input.muted?0:input.gain;
            for(int i=0;i<960;i++){sum[i]+=scratch[i]*gain;peak=Math.max(peak,Math.abs(scratch[i]));}
            input.level=Math.max(peak,input.level*.8f);
        }
        float peak=.001f;
        for(int i=0;i<960;i++){sum[i]*=master;peak=Math.max(peak,Math.abs(sum[i]));}
        float target=Math.min(1f,.95f/peak);limiter=target<limiter?target:Math.min(target,limiter+.005f);
        for(int i=0;i<960;i++)pcm[i]=(short)(sum[i]*limiter*32767);
    }
}
