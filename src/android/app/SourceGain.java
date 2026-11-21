package app.soundmesh.audio;

public final class SourceGain {
    private volatile int percent=100;
    public void set(int value){percent=Math.max(0,Math.min(100,value));}
    public int percent(){return percent;}
    public void apply(byte[] stereoPcm){
        int gain=percent;
        if(gain==100)return;
        for(int i=0;i+1<stereoPcm.length;i+=2){
            int sample=(short)((stereoPcm[i]&255)|(stereoPcm[i+1]<<8));
            int scaled=sample*gain/100;
            stereoPcm[i]=(byte)scaled;
            stereoPcm[i+1]=(byte)(scaled>>8);
        }
    }
}