package app.soundmesh.audio;

import java.io.*;
import java.net.*;

/** The same code is used in the APK and the JVM interoperability checks. */
public final class Wire {
    public static final int PORT=49632, RATE=48000, FRAME=1920;
    public static String line(InputStream in)throws IOException {
        StringBuilder text=new StringBuilder();
        while(text.length()<80){int b=in.read();if(b<0)throw new EOFException("设备已断开");if(b==10)return text.toString();text.append((char)b);}
        throw new IOException("连接协议无效");
    }
    public static void connect(Socket socket,String host,String pin)throws IOException {
        connect(socket,host,pin,null);
    }
    public static void connect(Socket socket,String host,String pin,String reverse)throws IOException {
        socket.setTcpNoDelay(true);socket.setKeepAlive(true);
        socket.connect(new InetSocketAddress(host,PORT),6000);socket.setSoTimeout(15000);
        socket.getOutputStream().write(("SOUNDMESH2 "+pin+(reverse==null?"":" "+reverse)+"\n").getBytes("US-ASCII"));
        if(!line(socket.getInputStream()).equals("OK 48000 2"))throw new java.net.ProtocolException("配对码或版本不匹配，请重新配对");
    }
    static void send(OutputStream out,String text)throws IOException{out.write((text+"\n").getBytes(java.nio.charset.StandardCharsets.US_ASCII));}
    static void respond(InputStream in,OutputStream out)throws IOException{
        for(int i=0;i<5;i++){if(!line(in).equals("PING"))throw new IOException("校时失败");send(out,Long.toString(Timeline.nowUs()));}
    }
    static long measure(Socket socket)throws IOException{
        long best=Long.MAX_VALUE,offset=0;InputStream in=socket.getInputStream();OutputStream out=socket.getOutputStream();
        for(int i=0;i<5;i++){
            long start=Timeline.nowUs();send(out,"PING");
            long remote;
            try{remote=Long.parseLong(line(in));}catch(NumberFormatException bad){throw new IOException("校时响应无效",bad);}
            if(remote<0||remote>Long.MAX_VALUE/48)throw new IOException("校时数值越界");
            long end=Timeline.nowUs();
            if(end-start<best){best=end-start;offset=start+(end-start)/2-remote;}
        }
        return offset;
    }
    static long connectTimed(Socket socket,String host,String pin,String reverse)throws IOException{
        socket.setTcpNoDelay(true);socket.setKeepAlive(true);
        socket.connect(new InetSocketAddress(host,PORT),6000);socket.setSoTimeout(6000);
        send(socket.getOutputStream(),"SOUNDMESH3 "+pin+(reverse==null?"":" "+reverse));
        if(!line(socket.getInputStream()).equals("OK TIME3"))throw new ProtocolException("对端未启用时间戳协议");
        long offset=measure(socket);socket.setSoTimeout(15000);return offset;
    }
    public static void frame(InputStream in,byte[] frame)throws IOException {
        if(frame.length!=FRAME)throw new IOException("无效的音频帧大小");
        int read=0;while(read<frame.length){int n=in.read(frame,read,frame.length-read);if(n<0)throw new EOFException("设备已断开");read+=n;}
    }
}
