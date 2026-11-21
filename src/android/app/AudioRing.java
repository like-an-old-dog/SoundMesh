package app.soundmesh.audio;

public final class AudioRing {
    private final float[] data = new float[48000];
    private int head, count;
    private boolean ready;
    public long dropped;
    public volatile int prebufferMs=40;
    private int recoveryMs,stableSamples;
    private long underruns;
    public synchronized long underruns(){return underruns;}
    public synchronized int targetMs(){return Math.min(160,prebufferMs+recoveryMs);}
    public synchronized int bufferMs(){return count/96;}
    public synchronized void reset(){head=count=0;ready=false;recoveryMs=stableSamples=0;underruns=0;}
    public synchronized void push(byte[] bytes) {
        for (int i=0;i+1<bytes.length;i+=2) {
            if (count==data.length) { head=(head+1)%data.length;count--;dropped++; }
            data[(head+count++)%data.length]=(short)((bytes[i]&255)|(bytes[i+1]<<8))/32768f;
        }
        if (count>19200) { int skip=count-Math.max(3840,prebufferMs*96);head=(head+skip)%data.length;count-=skip;dropped+=skip; }
        if(count>=targetMs()*96) ready=true;
    }
    public synchronized int read(float[] out, int length) {
        java.util.Arrays.fill(out,0,length,0f);
        if(!ready)return 0;
        int n=Math.min(count,length);
        for(int i=0;i<n;i++)out[i]=data[(head+i)%data.length];
        head=(head+n)%data.length;count-=n;
        if(n<length){
            ready=false;
            underruns++;
            recoveryMs=Math.min(160-prebufferMs,recoveryMs+25);
            stableSamples=0;
        }else if((stableSamples+=n)>=96*30000){
            recoveryMs=Math.max(0,recoveryMs-5);
            stableSamples=0;
        }
        return n;
    }
}
