package app.soundmesh.audio;

import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.media.*;
import android.media.projection.*;
import android.os.*;
import java.io.*;
import java.net.*;
import java.security.SecureRandom;
import java.util.*;
import java.util.concurrent.*;

public final class AudioService extends Service {
    public static volatile AudioService current;
    public static final int PORT=Wire.PORT, RATE=Wire.RATE, SAMPLES=960, FRAME=Wire.FRAME;
    public volatile String mode="", status="准备就绪", code="";
    public volatile float master=.8f;
    public volatile boolean monitor=true;
    private volatile boolean running;
    public final CopyOnWriteArrayList<Remote> sources=new CopyOnWriteArrayList<>();
    private final CopyOnWriteArrayList<Subscriber> subscribers=new CopyOnWriteArrayList<>();
    private final Map<String,ArrayDeque<Long>> attempts=new HashMap<>();
    private final ExecutorService workers=Executors.newCachedThreadPool();
    private MediaProjection projection;
    private AudioRecord recorder;
    private AudioTrack output;
    private ServerSocket listener;
    private DatagramSocket discovery;
    private Lan lan;
    public volatile int prebufferMs=70;
    private volatile boolean duplex;
    private android.net.wifi.WifiManager.WifiLock wifiLock;
    public void setBuffer(int ms){prebufferMs=ms;for(Remote remote:sources)remote.ring.prebufferMs=ms;}
    private PowerManager.WakeLock wake;
    private final Handler main=new Handler(Looper.getMainLooper());

    @Override public void onCreate() {
        super.onCreate();lan=new Lan(this);current=this;
        NotificationManager nm=getSystemService(NotificationManager.class);
        nm.createNotificationChannel(new NotificationChannel("audio","声音传输",NotificationManager.IMPORTANCE_LOW));
    }
    private String notificationText;
    private boolean volumeActions;
    private int rememberedPercent=80;
    private final Runnable refreshNotification=()->{
        if(running)getSystemService(NotificationManager.class).notify(1,notification());
    };
    private Notification.Action action(String command,String title,int id,int icon){
        Intent intent=new Intent(this,AudioService.class).setAction(command);
        PendingIntent operation=PendingIntent.getService(this,id,intent,PendingIntent.FLAG_IMMUTABLE);
        return new Notification.Action.Builder(android.graphics.drawable.Icon.createWithResource(this,icon),title,operation).build();
    }
    public void setVolume(int percent){
        master=Math.max(0,Math.min(100,percent))/100f;
        if(master>0)rememberedPercent=Math.round(master*100);
        main.removeCallbacks(refreshNotification);
        main.postDelayed(refreshNotification,100);
    }
    private Notification notification(){
        PendingIntent open=PendingIntent.getActivity(this,0,new Intent(this,MainActivity.class),PendingIntent.FLAG_IMMUTABLE);
        Notification.Builder builder=new Notification.Builder(this,"audio").setSmallIcon(android.R.drawable.ic_lock_silent_mode_off)
            .setContentTitle(volumeActions?"声汇 · 混音音量 "+Math.round(master*100)+"%":"声汇 · 正在共享")
            .setContentText(notificationText).setContentIntent(open).setOnlyAlertOnce(true).setOngoing(true);
        if(volumeActions){
            builder.addAction(action("VOL_DOWN","−5%",2,android.R.drawable.ic_media_rew))
                   .addAction(action("VOL_TOGGLE",master==0?"恢复":"静音",3,android.R.drawable.ic_lock_silent_mode))
                   .addAction(action("VOL_UP","+5%",4,android.R.drawable.ic_media_ff));
        }else builder.addAction(action("STOP","停止",1,android.R.drawable.ic_media_pause));
        return builder.build();
    }
    private void foreground(String text,int type){
        notificationText=text;
        volumeActions=(type&ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK)!=0;
        startForeground(1,notification(),type);
        if(wifiLock==null){
            android.net.wifi.WifiManager wifi=(android.net.wifi.WifiManager)getApplicationContext().getSystemService(WIFI_SERVICE);
            if(wifi!=null){wifiLock=wifi.createWifiLock(android.net.wifi.WifiManager.WIFI_MODE_FULL_LOW_LATENCY,"SoundMesh:WiFi");wifiLock.acquire();}
        }
        if(wake==null) { wake=getSystemService(PowerManager.class).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK,"SoundMesh:Audio");wake.acquire(); }
    }
    @Override public int onStartCommand(Intent intent,int flags,int startId) {
        if(intent==null)return START_NOT_STICKY;
        String action=intent.getAction();
        if("STOP".equals(action)) { stopSelf();return START_NOT_STICKY; }
        if(action!=null&&action.startsWith("VOL_")){
            if(!running){stopSelf();return START_NOT_STICKY;}
            if(volumeActions){
                int currentPercent=Math.round(master*100);
                if(action.equals("VOL_DOWN"))setVolume(currentPercent-5);
                if(action.equals("VOL_UP"))setVolume(currentPercent+5);
                if(action.equals("VOL_TOGGLE"))setVolume(currentPercent==0?rememberedPercent:0);
            }
            return START_NOT_STICKY;
        }
        try {
            if("SEND".equals(action)||"DUPLEX".equals(action)) {
                if(running)throw new IllegalStateException("请先停止当前模式");
                duplex="DUPLEX".equals(action);monitor=!duplex;
                foreground(duplex?"正在双向传输声音":"正在共享本机媒体声音",ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION|(duplex?ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK:0));
                startSending(intent.getParcelableExtra("consent"));
                if(duplex){startReceiving();mode="双向";}
            } else if("RECEIVE".equals(action)) {
                if(!running) {
                    foreground("正在融合已连接设备的声音",ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK);
                    startReceiving();
                }
            } else if("CONNECT".equals(action)) {
                if((!"接收".equals(mode)&&!duplex)||!running)throw new IllegalStateException("请先启动接收模式");
                connect(intent.getStringExtra("host"),intent.getStringExtra("pin"),intent.getStringExtra("name"),duplex,intent.getStringExtra("peer"));
            }
        } catch(Exception e) { status="无法启动："+e.getMessage();MainActivity.lastError=status;stopSelf(); }
        return START_NOT_STICKY;
    }
    private void startSending(Intent consent)throws Exception {
        projection=getSystemService(MediaProjectionManager.class).getMediaProjection(Activity.RESULT_OK,consent);
        projection.registerCallback(new MediaProjection.Callback(){@Override public void onStop(){if(running){MainActivity.lastError="系统已停止音频采集，请重新授权。";stopSelf();}}},main);
        AudioPlaybackCaptureConfiguration config=new AudioPlaybackCaptureConfiguration.Builder(projection)
            .addMatchingUsage(AudioAttributes.USAGE_MEDIA).addMatchingUsage(AudioAttributes.USAGE_GAME)
            .addMatchingUsage(AudioAttributes.USAGE_UNKNOWN).excludeUid(android.os.Process.myUid()).build();
        AudioFormat format=new AudioFormat.Builder().setSampleRate(RATE).setEncoding(AudioFormat.ENCODING_PCM_16BIT).setChannelMask(AudioFormat.CHANNEL_IN_STEREO).build();
        int min=AudioRecord.getMinBufferSize(RATE,AudioFormat.CHANNEL_IN_STEREO,AudioFormat.ENCODING_PCM_16BIT);
        recorder=new AudioRecord.Builder().setAudioFormat(format).setBufferSizeInBytes(Math.max(min,9600)).setAudioPlaybackCaptureConfig(config).build();
        if(recorder.getState()!=AudioRecord.STATE_INITIALIZED)throw new IOException("此设备无法初始化声音采集");
        listener=new ServerSocket();listener.setReuseAddress(true);listener.bind(new InetSocketAddress(PORT));
        code=lan.pin;
        running=true;mode="发送";status="发送已开启";recorder.startRecording();
        workers.execute(()->accept());
        try{discovery=new DatagramSocket(Lan.PORT);workers.execute(()->{try{lan.advertise(discovery);}catch(Exception ignored){}});}catch(SocketException e){status="发现端口忙，请使用二维码";}

        workers.execute(()->{
            try {
                while(running) {
                    byte[] frame=new byte[FRAME];int n=0;
                    while(n<FRAME&&running) { int got=recorder.read(frame,n,FRAME-n,AudioRecord.READ_BLOCKING);if(got<0)throw new IOException("声音采集错误 "+got);n+=got; }
                    if(!running)break;
                    for(Subscriber s:subscribers) {
                        if(s.blockedAt>0&&System.currentTimeMillis()-s.blockedAt>15000){s.close();continue;}
                        if(!s.frames.offer(frame)){s.frames.poll();s.frames.offer(frame);}
                    }
                }
            }catch(Exception e){fail("音频发送停止："+e.getMessage());}
        });
    }
    private void accept() {
        try {while(running){Socket client=listener.accept();workers.execute(()->serve(client));}}
        catch(Exception e){if(running)fail("连接服务停止："+e.getMessage());}
    }
    private void serve(Socket socket) {
        Subscriber sub=null;
        try {
            socket.setTcpNoDelay(true);socket.setSoTimeout(5000);socket.setSendBufferSize(65536);socket.setKeepAlive(true);
            String ip=socket.getInetAddress().getHostAddress();long now=System.currentTimeMillis();
            synchronized(attempts) {
                attempts.entrySet().removeIf(e->e.getValue().isEmpty()||now-e.getValue().peekLast()>60000);
                ArrayDeque<Long> q=attempts.get(ip);if(q==null){q=new ArrayDeque<>();attempts.put(ip,q);}
                while(!q.isEmpty()&&now-q.peekFirst()>60000)q.removeFirst();
                if(q.size()>=6)return;
            }
            InputStream in=socket.getInputStream();OutputStream out=socket.getOutputStream();
            String[] hello=Wire.line(in).split(" ");
            if(duplex&&hello.length==2&&hello[0].equals("SM2-FOCUS")&&hello[1].equals(code)){
                monitor=false;out.write("OK FOCUS\n".getBytes("US-ASCII"));return;
            }
            if(hello.length<2||hello.length>3||!hello[0].equals("SOUNDMESH2")||!hello[1].equals(code)){
                synchronized(attempts){attempts.computeIfAbsent(ip,k->new ArrayDeque<>()).add(now);}
                out.write("ERR\n".getBytes("US-ASCII"));return;
            }
            out.write("OK 48000 2\n".getBytes("US-ASCII"));
            if(duplex&&hello.length==3&&hello[2].matches("[0-9]{8}"))main.post(()->{if(running&&duplex)connect(ip,hello[2],ip,false,null);});
            sub=new Subscriber(socket);subscribers.add(sub);
            while(running&&!socket.isClosed()) {
                byte[] frame=sub.frames.poll(400,TimeUnit.MILLISECONDS);if(frame==null)frame=new byte[FRAME];
                sub.blockedAt=System.currentTimeMillis();out.write(frame);sub.blockedAt=0;
            }
        }catch(Exception ignored){}
        finally {if(sub!=null)subscribers.remove(sub);try{socket.close();}catch(Exception ignored){}}
    }
    private void startReceiving()throws Exception {
        int min=AudioTrack.getMinBufferSize(RATE,AudioFormat.CHANNEL_OUT_STEREO,AudioFormat.ENCODING_PCM_16BIT);
        output=new AudioTrack.Builder().setAudioAttributes(new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_MEDIA).setContentType(AudioAttributes.CONTENT_TYPE_MUSIC).build())
            .setAudioFormat(new AudioFormat.Builder().setSampleRate(RATE).setChannelMask(AudioFormat.CHANNEL_OUT_STEREO).setEncoding(AudioFormat.ENCODING_PCM_16BIT).build())
            .setPerformanceMode(AudioTrack.PERFORMANCE_MODE_LOW_LATENCY).setBufferSizeInBytes(Math.max(min,7680)).setTransferMode(AudioTrack.MODE_STREAM).build();
        if(output.getState()!=AudioTrack.STATE_INITIALIZED)throw new IOException("无法初始化耳机输出");
        mode="接收";running=true;status="接收已开启，可以添加设备";output.play();
        workers.execute(()->{
            android.os.Process.setThreadPriority(android.os.Process.THREAD_PRIORITY_AUDIO);
            short[] pcm=new short[SAMPLES];MixBus mixer=new MixBus();
            try {
                while(running) {
                    if(!monitor){
                        if(output.getPlayState()==AudioTrack.PLAYSTATE_PLAYING){output.pause();output.flush();}
                        mixer.render(sources,0,pcm);Thread.sleep(10);continue;
                    }
                    if(output.getPlayState()!=AudioTrack.PLAYSTATE_PLAYING)output.play();
                    mixer.render(sources,master,pcm);
                    int at=0;while(at<SAMPLES&&running){int n=output.write(pcm,at,SAMPLES-at,AudioTrack.WRITE_BLOCKING);if(n<0)throw new IOException("声音输出错误 "+n);at+=n;}
                }
            }catch(Exception e){if(running)fail("混音输出停止："+e.getMessage());}
        });
    }
    private void connect(String host,String pin,String name,boolean requestReverse,String peer) {
        for(Remote other:sources)if(other.host.equals(host))return;
        Remote r=new Remote(host,name==null||name.trim().isEmpty()?host:name.trim());
        r.ring.prebufferMs=prebufferMs;r.pin=pin;sources.add(r);
        workers.execute(()->{
            int wait=1;
            while(running&&!r.removed) {
                try(Socket socket=new Socket()) {
                    r.socket=socket;
                    if(r.removed)break;
                    Wire.connect(socket,host,pin,requestReverse?code:null);
                    if(peer!=null)lan.prefs.edit().putString("peer."+peer,pin).apply();
                    r.ring.reset();r.status="已连接 · 立体声";byte[] frame=new byte[FRAME];
                    InputStream in=socket.getInputStream();
                    while(running&&!r.removed){Wire.frame(in,frame);r.ring.push(frame);wait=1;}
                }catch(ProtocolException e){r.status=e.getMessage();if(peer!=null)lan.prefs.edit().remove("peer."+peer).apply();break;}
                catch(Exception e){if(!running||r.removed)break;r.status="连接中断 · "+wait+" 秒后重试";}
                r.level=0;
                try{Thread.sleep(wait*1000L);}catch(InterruptedException end){break;}
                wait=Math.min(8,wait*2);
            }
        });
    }
    public void listenHere(){
        monitor=true;
        workers.execute(()->{int failures=0;for(Remote remote:sources){
            try(Socket socket=new Socket()){
                socket.connect(new InetSocketAddress(remote.host,PORT),2000);socket.setSoTimeout(2000);
                socket.getOutputStream().write(("SM2-FOCUS "+remote.pin+"\n").getBytes("US-ASCII"));
                if(!Wire.line(socket.getInputStream()).equals("OK FOCUS"))failures++;
            }catch(Exception e){failures++;}
        }
        final int missed=failures;main.post(()->android.widget.Toast.makeText(this,missed==0?"正在本机监听":"本机监听已开启，部分设备离线",android.widget.Toast.LENGTH_SHORT).show());});
    }
    private void fail(String message){if(running){status=message;MainActivity.lastError=message;main.post(()->stopSelf());}}
    public void remove(Remote remote){sources.remove(remote);remote.close();}
    public int subscribers(){return subscribers.size();}
    public static String addresses() {
        ArrayList<String> result=new ArrayList<>();
        try {for(NetworkInterface ni:Collections.list(NetworkInterface.getNetworkInterfaces()))if(ni.isUp())for(InetAddress a:Collections.list(ni.getInetAddresses()))if(a instanceof Inet4Address&&!a.isLoopbackAddress())result.add(a.getHostAddress());}catch(Exception ignored){}
        return String.join(" / ",result);
    }
    @Override public void onDestroy() {
        running=false;main.removeCallbacks(refreshNotification);current=null; if(discovery!=null)discovery.close(); if(wifiLock!=null&&wifiLock.isHeld())wifiLock.release();
        if(listener!=null)try{listener.close();}catch(Exception ignored){}
        for(Subscriber s:subscribers)s.close();for(Remote r:sources)r.close();
        if(recorder!=null){try{recorder.stop();}catch(Exception ignored){}recorder.release();}
        if(output!=null){try{output.pause();output.flush();}catch(Exception ignored){}output.release();}
        if(projection!=null)projection.stop();
        workers.shutdownNow();if(wake!=null&&wake.isHeld())wake.release();stopForeground(STOP_FOREGROUND_REMOVE);super.onDestroy();
    }
    @Override public IBinder onBind(Intent intent){return null;}
    public static final class Remote extends MixBus.Input {
        public final String host,name;private String pin;public volatile Socket socket; public volatile boolean removed;
        public volatile String status="连接中…";
        Remote(String host,String name){this.host=host;this.name=name;}
        void close(){removed=true;try{if(socket!=null)socket.close();}catch(Exception ignored){}}
    }
    private static final class Subscriber {
        final Socket socket;final ArrayBlockingQueue<byte[]> frames=new ArrayBlockingQueue<>(8);volatile long blockedAt;
        Subscriber(Socket socket){this.socket=socket;}void close(){try{socket.close();}catch(Exception ignored){}}
    }
}

