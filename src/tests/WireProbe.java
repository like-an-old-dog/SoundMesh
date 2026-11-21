import app.soundmesh.audio.AudioRing;
import app.soundmesh.audio.Wire;
import app.soundmesh.audio.MixBus;
import java.io.*;
import java.net.*;
import java.util.*;

public final class WireProbe {
    static void check(boolean test,String label){if(!test)throw new AssertionError(label);System.out.println("PASS "+label);}
    static byte[] pcm(int value){byte[] b=new byte[Wire.FRAME];for(int i=0;i<b.length;i+=2){b[i]=(byte)value;b[i+1]=(byte)(value>>8);}return b;}
    public static void main(String[] args)throws Exception {
        if(args.length==0) {
            AudioRing ring=new AudioRing();float[] out=new float[480];
            ring.push(pcm(-16384));check(ring.read(out,480)==0,"Java buffer waits for pre-roll");
            for(int i=0;i<4;i++)ring.push(pcm(-16384));
            check(ring.read(out,480)==480&&out[0]==-.5f,"Java PCM signed little-endian conversion");
            for(int i=0;i<200;i++)ring.push(pcm(8192));
            check(ring.dropped>0,"Java backlog stays bounded");ring.read(out,480);check(out[0]==.25f,"Java discards stale samples");
            for(int i=0;i<100;i++)ring.read(out,480);check(ring.read(out,480)==0&&out[0]==0,"Java underrun is silence");
            boolean oversized=false;try{Wire.line(new ByteArrayInputStream(new byte[81]));}catch(IOException e){oversized=true;}check(oversized,"Java rejects oversized handshake");
            boolean truncated=false;try{Wire.frame(new ByteArrayInputStream(new byte[100]),new byte[Wire.FRAME]);}catch(EOFException e){truncated=true;}check(truncated,"Java rejects truncated frame");
            MixBus mixer=new MixBus();List<MixBus.Input> inputs=new ArrayList<>();
            MixBus.Input a=new MixBus.Input(),b=new MixBus.Input();a.gain=.5f;b.muted=true;
            for(int i=0;i<5;i++){a.ring.push(pcm(16384));b.ring.push(pcm(32767));}inputs.add(a);inputs.add(b);
            short[] mixed=new short[960];mixer.render(inputs,.5f,mixed);
            check(mixed[0]==4095,"Android mixer gain, mute and master compose correctly");
            for(int i=0;i<64;i++){MixBus.Input channel=new MixBus.Input();channel.gain=1;for(int j=0;j<5;j++)channel.ring.push(pcm(32767));inputs.add(channel);}
            mixer.render(inputs,1,mixed);boolean limited=true;for(short sample:mixed)if(Math.abs((int)sample)>31130)limited=false;
            check(limited&&mixed[0]>30000,"Android 66-source mixing stays within output range");
            mixer.render(inputs,0,mixed);boolean silent=true;for(short sample:mixed)if(sample!=0)silent=false;
            check(silent,"Android master mute produces silence");
            return;
        }
        if(args[0].equals("receive")) {
            try(Socket socket=new Socket()){
                Wire.connect(socket,"127.0.0.1","12345678");byte[] frame=new byte[Wire.FRAME];AudioRing ring=new AudioRing();
                for(int i=0;i<5;i++){Wire.frame(socket.getInputStream(),frame);ring.push(frame);}
                float[] out=new float[480];ring.read(out,480);
                check(out[0]==.25f&&out[479]==.25f,"C# fragmented stream -> Android Java codec");
                boolean eof=false;try{Wire.frame(socket.getInputStream(),frame);}catch(EOFException e){eof=true;}check(eof,"Android codec detects peer disconnect");
            }
        }else if(args[0].equals("send")) {
            try(ServerSocket server=new ServerSocket(Wire.PORT,8,InetAddress.getByName("127.0.0.1"))){
                System.out.println("READY");System.out.flush();
                try(Socket socket=server.accept()){
                    check(Wire.line(socket.getInputStream()).equals("SOUNDMESH2 12345678"),"Java sender accepts C# handshake");
                    OutputStream out=socket.getOutputStream();
                    for(byte b:"OK 48000 2\n".getBytes("US-ASCII")){out.write(b);out.flush();}
                    byte[] frame=pcm(-8192);for(int i=0;i<5;i++){out.write(frame,0,17);out.flush();out.write(frame,17,frame.length-17);out.flush();}
                }
            }
        }
    }
}
