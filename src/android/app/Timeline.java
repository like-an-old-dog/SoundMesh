package app.soundmesh.audio;
import java.util.Arrays;
final class Timeline {
    static long nowUs(){return System.nanoTime()/1000;}
    static final class FrameClock {
        private long next;
        long stamp(long estimate){
            long error=estimate-next;
            long at=next==0||Math.abs(error)>50000?estimate:next+Math.max(-100,Math.min(100,error));
            next=at+10000;return at;
        }
    }
    static final class PlayoutClock {
        private long next=-1;
        private int previousDelay;
        long advance(long nowUs,int frames,int delayMs){
            long ideal=(nowUs-delayMs*1000L)*48/1000;
            if(next<0||delayMs!=previousDelay||Math.abs(next-ideal)>4800)next=ideal;
            else if(Math.abs(next-ideal)>960)next+=Long.signum(ideal-next);
            long start=next;next+=frames;previousDelay=delayMs;return start;
        }
    }
    private static final int CAPACITY=48000*2;
    private final long[] labels=new long[CAPACITY];
    private final float[] samples=new float[CAPACITY*2];
    private final byte[] pending=new byte[1920];
    private long pendingAt,consumed;
    private boolean seen,missing;
    volatile long gaps,late;
    Timeline(){reset();}
    synchronized void reset(){Arrays.fill(labels,-1);pendingAt=consumed=-1;seen=missing=false;gaps=late=0;}
    synchronized int aheadMs(){return consumed<0?0:(int)Math.max(0,Math.min(2000,(pendingAt-consumed)/48));}
    private int sample(int frame,int ch){
        int i=4*frame+2*ch;return (short)((pending[i]&255)|(pending[i+1]<<8));
    }
    synchronized void push(long localUs,byte[] frame){
        long start=localUs*48/1000;
        if(start<0||start<=pendingAt){late++;return;}
        if(pendingAt>=0){
            long distance=start-pendingAt;
            int length=distance>=400&&distance<=560?(int)distance:480;
            if(pendingAt+length<=consumed)late++;
            else for(int i=0;i<length;i++){
                long at=pendingAt+i;if(at<consumed)continue;
                int cell=(int)(at%CAPACITY),fraction=i*480%length,src=i*480/length;
                int other=Math.min(479,src+1);
                for(int ch=0;ch<2;ch++){
                    int a=sample(src,ch),b=sample(other,ch);
                    samples[cell*2+ch]=(a+(b-a)*fraction/(float)length)/32768f;
                }
                labels[cell]=at;
            }
        }
        pendingAt=start;System.arraycopy(frame,0,pending,0,1920);
    }
    synchronized void read(long start,float[] output,int count){
        Arrays.fill(output,0,count,0f);
        for(int i=0;i+1<count;i+=2){
            long at=start+i/2;if(at<0)continue;
            int cell=(int)(at%CAPACITY);
            if(labels[cell]==at){output[i]=samples[cell*2];output[i+1]=samples[cell*2+1];seen=true;missing=false;}
            else if(seen&&!missing){gaps++;missing=true;}
        }
        consumed=start+count/2;
    }
}
