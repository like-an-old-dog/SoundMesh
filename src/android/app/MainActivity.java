package app.soundmesh.audio;
import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.*;
import android.graphics.drawable.GradientDrawable;
import android.media.projection.MediaProjectionManager;
import android.os.*;
import android.view.*;
import android.widget.*;
import java.util.*;
import com.google.zxing.BarcodeFormat;
import com.google.zxing.common.BitMatrix;
import com.google.zxing.qrcode.QRCodeWriter;

public final class MainActivity extends Activity {
    public static volatile String lastError="";
    final int BG=0xfff3f4f9,CARD=Color.WHITE,INK=0xff20243b,SUB=0xff5a6178,GREEN=0xffeeedfc,ACCENT=0xff5046c4;
    LinearLayout nearbyList,root,list,addCard,mixCard,shareCard,startCard;
    TextView info,count,masterValue,found;
    Button stop,scan,showQr,focus;
    final Button[] tabs=new Button[3];
    Button start;
    TextView sessionTitle,pairSummary;
    LinearLayout options,nav;
    int section;
    boolean wasActive;
    LocalControls localControls;
    CheckBox monitor;SeekBar master;
    Handler handler=new Handler(Looper.getMainLooper());
    Map<AudioService.Remote,DeviceRow> rows=new HashMap<>();
    Lan lan;boolean discovering;
    final Runnable update=new Runnable(){public void run(){refresh();handler.postDelayed(this,450);}};
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);lan=new Lan(this);
        getWindow().setStatusBarColor(BG);getWindow().setNavigationBarColor(BG);
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);
        ScrollView scroll=new ScrollView(this);scroll.setBackgroundColor(BG);scroll.setFillViewport(true);root=column();
        root.setPadding(dp(20),dp(24),dp(20),dp(36));android.widget.FrameLayout.LayoutParams layout=new android.widget.FrameLayout.LayoutParams(Math.min(getResources().getDisplayMetrics().widthPixels,dp(720)),-2,Gravity.TOP|Gravity.CENTER_HORIZONTAL);scroll.addView(root,layout);setContentView(scroll);
        if(Build.VERSION.SDK_INT>=35)root.setOnApplyWindowInsetsListener((v,i)->{android.graphics.Insets b=i.getInsets(WindowInsets.Type.systemBars());v.setPadding(dp(20)+b.left,dp(20)+b.top,dp(20)+b.right,dp(30)+b.bottom);return i;});
        LinearLayout brand=new LinearLayout(this);brand.setGravity(Gravity.CENTER_VERTICAL);root.addView(brand);
        ImageView mark=new ImageView(this);mark.setImageResource(R.mipmap.launcher);brand.addView(mark,dp(40),dp(40));
        TextView word=text("声汇",23,true);word.setPadding(dp(10),0,dp(8),0);brand.addView(word);brand.addView(text("SoundMesh",13,false));
        startCard=column();GradientDrawable border=round(0xff202440,22);startCard.setBackground(border);startCard.setClipToOutline(true);
        FrameLayout hero=new FrameLayout(this);startCard.addView(hero);ImageView wave=new ImageView(this);wave.setImageResource(R.drawable.session_wave);wave.setScaleType(ImageView.ScaleType.CENTER_CROP);
        hero.addView(wave,new FrameLayout.LayoutParams(-1,-1));LinearLayout content=column();content.setPadding(dp(20),dp(13),dp(20),dp(15));hero.addView(content);
        sessionTitle=text("准备融合",26,true);sessionTitle.setTextColor(Color.WHITE);content.addView(sessionTitle);
        info=text("连接同一局域网内的手机和电脑。",13,false);info.setTextColor(0xffe5e6ff);content.addView(info);
        start=button("开始融合");start.setBackground(round(0xffaff0df,13));content.addView(start);
        LinearLayout.LayoutParams heroParams=new LinearLayout.LayoutParams(-1,-2);heroParams.topMargin=dp(14);root.addView(startCard,heroParams);
        start.setOnClickListener(v->{if(AudioService.current==null)authorize();else{stopService(new Intent(this,AudioService.class));lastError="会话已结束";}});
        nav=new LinearLayout(this);String[] sections={"混音台","设备连接","输出设置"};
        for(int i=0;i<tabs.length;i++){final int number=i;tabs[i]=button(sections[i]);tabs[i].setOnClickListener(v->{section=number;refresh();});
            LinearLayout.LayoutParams tp=new LinearLayout.LayoutParams(0,dp(44),1);tp.setMargins(dp(2),dp(9),dp(2),0);nav.addView(tabs[i],tp);}
        root.addView(nav);
        shareCard=card("邀请其他设备");pairSummary=text("",13,false);pairSummary.setTextIsSelectable(true);shareCard.addView(pairSummary);showQr=button("显示配对二维码");shareCard.addView(showQr);showQr.setOnClickListener(v->qr());
        addCard=card("附近设备");scan=button("查找附近设备");addCard.addView(scan);scan.setOnClickListener(v->discover());
        found=text("",12,false);addCard.addView(found);nearbyList=column();addCard.addView(nearbyList);
        Button camera=button("扫码连接");addCard.addView(camera);camera.setOnClickListener(v->{if(checkSelfPermission(Manifest.permission.CAMERA)==PackageManager.PERMISSION_GRANTED)openScanner();else requestPermissions(new String[]{Manifest.permission.CAMERA},103);});
        Button manual=button("手动配对");addCard.addView(manual);manual.setOnClickListener(v->prompt(null));
        mixCard=card("混音台");localControls=new LocalControls(this,mixCard);masterValue=text("远端混音总音量 80%",14,true);mixCard.addView(masterValue);master=new SeekBar(this);master.setMax(100);master.setProgress(80);mixCard.addView(master);
        master.setOnSeekBarChangeListener(change(value->{AudioService s=AudioService.current;if(s!=null)s.setVolume(value);masterValue.setText("远端混音总音量 "+value+"%");}));
        options=card("输出和延迟");focus=button("把监听切到本机");options.addView(focus);focus.setOnClickListener(v->{AudioService s=AudioService.current;if(s!=null)s.listenHere();});
        monitor=new CheckBox(this);monitor.setText("在本机监听");monitor.setChecked(true);options.addView(monitor);
        monitor.setOnCheckedChangeListener((v,checked)->{AudioService s=AudioService.current;if(s!=null)s.monitor=checked;});
        buffer=button("播放缓冲 · 同步 150 ms");options.addView(buffer);buffer.setOnClickListener(v->new AlertDialog.Builder(this).setTitle("播放缓冲").setItems(new String[]{"灵敏 · 25 ms","均衡 · 70 ms","稳定 · 90 ms","同步 · 100 ms","同步 · 150 ms","同步 · 200 ms"},(d,i)->{AudioService s=AudioService.current;if(s!=null){int ms=new int[]{25,70,90,100,150,200}[i];s.setBuffer(ms);buffer.setText("播放缓冲 · "+(ms>=100?"同步 ":"")+ms+" ms");}}).show());
        count=text("",12,false);mixCard.addView(count);list=column();mixCard.addView(list);
    }
    void authorize(){if(checkSelfPermission(Manifest.permission.RECORD_AUDIO)!=PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{Manifest.permission.RECORD_AUDIO},101);else requestCapture();}
    void requestCapture(){startActivityForResult(getSystemService(MediaProjectionManager.class).createScreenCaptureIntent(),102);}
    void openScanner(){startActivityForResult(new Intent(this,ScanActivity.class),104);}
    void discover(){
        if(discovering)return;discovering=true;found.setText("查找中…");new Thread(()->{try{
            List<Lan.Peer> peers=lan.scan();runOnUiThread(()->{if(isFinishing()||isDestroyed())return;
                found.setText(peers.isEmpty()?"附近暂无设备，请在对端开启共享。":"发现 "+peers.size()+" 台设备");
                nearbyList.removeAllViews();
                for(Lan.Peer p:peers){Button select=button(p.toString());nearbyList.addView(select);select.setOnClickListener(v->{
                    String saved=lan.prefs.getString("peer."+p.id,"");
                    if(saved.isEmpty())prompt(p);else connect(p.host,saved,p.name,p.id);
                });}
            });
        }catch(Exception e){runOnUiThread(()->found.setText("发现失败，可扫码或手动配对"));}finally{runOnUiThread(()->discovering=false);}}).start();
    }
    void prompt(Lan.Peer peer){
        LinearLayout form=column();form.setPadding(dp(20),0,dp(20),0);EditText address=input("发送端 IPv4 地址"),secret=input("8 位配对码");secret.setInputType(2);
        if(peer==null)form.addView(address);form.addView(secret);
        new AlertDialog.Builder(this).setTitle(peer==null?"手动配对":peer.name).setView(form).setPositiveButton("连接",(d,w)->connect(peer==null?address.getText().toString().trim():peer.host,secret.getText().toString().trim(),peer==null?null:peer.name,peer==null?null:peer.id)).setNegativeButton("取消",null).show();
    }
    void connect(String ip,String key,String label,String id){
        if(!validIp(ip)||!key.matches("[0-9]{8}")||ip.startsWith("127.")||Arrays.asList(AudioService.addresses().split(" / ")).contains(ip)){toast("请输入对端局域网地址和 8 位配对码");return;}
        startService(new Intent(this,AudioService.class).setAction("CONNECT").putExtra("host",ip).putExtra("name",label).putExtra("peer",id).putExtra("pin",key));
    }
    void qr(){try{
        String ip=Lan.primary();if(ip.isEmpty()){toast("请先连接局域网");return;}
        String uri="soundmesh://pair/"+ip+"/"+lan.pin+"/"+lan.id;BitMatrix matrix=new QRCodeWriter().encode(uri,BarcodeFormat.QR_CODE,420,420);
        int[] pixels=new int[420*420];for(int y=0;y<420;y++)for(int x=0;x<420;x++)pixels[y*420+x]=matrix.get(x,y)?Color.BLACK:Color.WHITE;
        ImageView image=new ImageView(this);image.setImageBitmap(Bitmap.createBitmap(pixels,420,420,Bitmap.Config.ARGB_8888));new AlertDialog.Builder(this).setTitle("扫描配对").setView(image).setPositiveButton("关闭",null).show();
    }catch(Exception e){toast(e.getMessage());}}
    @Override public void onRequestPermissionsResult(int req,String[] permissions,int[] grants){super.onRequestPermissionsResult(req,permissions,grants);if(grants.length==0||grants[0]!=0){toast("权限未授予");return;}if(req==101)requestCapture();if(req==103)openScanner();}
    @Override protected void onActivityResult(int req,int result,Intent data){
        super.onActivityResult(req,result,data);if(result!=RESULT_OK||data==null)return;
        if(req==102)startForegroundService(new Intent(this,AudioService.class).setAction("FUSE").putExtra("consent",data));
        if(req==104){String raw=data.getStringExtra("pair");String[] f=raw==null?new String[0]:raw.split("/");if(f.length==6&&f[5].matches("[a-fA-F0-9]{32}"))connect(f[3],f[4],f[3],f[5]);else toast("配对码无效");}
    }
    @Override protected void onResume(){super.onResume();handler.post(update);}
    @Override protected void onPause(){handler.removeCallbacks(update);super.onPause();}
    boolean notificationAsked;
    private Button buffer;
    void refresh(){
        AudioService s=AudioService.current;boolean active=s!=null&&"融合".equals(s.mode);
        boolean rx=active,tx=active;
        if(rx && !notificationAsked && Build.VERSION.SDK_INT>=33 &&
            checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED){
            notificationAsked=true;
            requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},105);
        }
        boolean startScan=rx&&!wasActive;
        if(!active&&wasActive)section=0;
        wasActive=active;
        addCard.setVisibility(active&&section==1?View.VISIBLE:View.GONE);
        shareCard.setVisibility(active&&section==1?View.VISIBLE:View.GONE);
        mixCard.setVisibility(active&&section==0?View.VISIBLE:View.GONE);
        options.setVisibility(active&&section==2?View.VISIBLE:View.GONE);
        nav.setVisibility(active?View.VISIBLE:View.GONE);
        for(int i=0;i<tabs.length;i++){boolean selected=i==section;
            if(tabs[i].getTag()==null||!tabs[i].getTag().equals(selected)){
                tabs[i].setTag(selected);tabs[i].setBackground(round(selected?ACCENT:CARD,13));tabs[i].setTextColor(selected?Color.WHITE:INK);
            }
        }
        show(sessionTitle,active?"设备融合中":"准备融合");
        show(start,active?"结束融合":"开始融合");
        if(startScan)discover();
        show(info,active?(s.monitor?"正在本机监听":"等待接管监听")+"   ·   "+s.sources.size()+" 台远端设备":lastError.isEmpty()?"连接同一局域网内的手机和电脑。":lastError);
        if(active&&section==1)show(pairSummary,"本机地址  "+Lan.primary()+"\n配对码  "+s.code);
        if(rx){show(buffer,"播放缓冲 · "+(s.prebufferMs>=100?"同步 ":"")+s.prebufferMs+" ms");localControls.sync(s);int value=Math.round(s.master*100);if(!master.isPressed()&&master.getProgress()!=value)master.setProgress(value);show(masterValue,"远端混音总音量 "+value+"%");monitor.setChecked(s.monitor);}
        List<AudioService.Remote> current=active?new ArrayList<>(s.sources):Collections.emptyList();
        for(AudioService.Remote r:new ArrayList<>(rows.keySet()))if(!current.contains(r))list.removeView(rows.remove(r).view);
        for(AudioService.Remote r:current){DeviceRow row=rows.get(r);if(row==null){row=new DeviceRow(r);rows.put(r,row);list.addView(row.view);}row.sync(r);show(row.status,r.host+" · "+r.status+" · 缓冲 "+(r.timed&&s.prebufferMs>=100?r.timeline.aheadMs():r.ring.bufferMs())+" ms · 断流 "+(r.timed&&s.prebufferMs>=100?r.timeline.gaps:r.ring.underruns())+" 次");row.meter.setProgress(Math.round(r.level*100));}
        show(count,current.isEmpty()?"远端设备 · 尚未连接":"远端设备 · "+current.size()+" 台");
    }
    void show(TextView label,String value){if(!value.contentEquals(label.getText()))label.setText(value);}
    GradientDrawable round(int color,int radius){GradientDrawable bg=new GradientDrawable();bg.setColor(color);bg.setCornerRadius(dp(radius));return bg;}
    final class DeviceRow {
        final LinearLayout view=column();final TextView status,volume;final ProgressBar meter;final SeekBar slider;final CheckBox mute;
        void sync(AudioService.Remote r){int v=Math.round(r.gain*100);if(!slider.isPressed()&&slider.getProgress()!=v)slider.setProgress(v);show(volume,"音量 "+v+"%");mute.setChecked(r.muted);}
        DeviceRow(AudioService.Remote r){
            view.setPadding(dp(12),dp(12),dp(12),dp(12));view.setBackgroundColor(BG);LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.setMargins(0,dp(10),0,dp(10));view.setLayoutParams(p);
            view.addView(text(r.name,18,true));status=text(r.host,12,false);view.addView(status);
            volume=text("音量 "+Math.round(r.gain*100)+"%",13,false);view.addView(volume);slider=new SeekBar(MainActivity.this);slider.setMax(100);slider.setProgress(Math.round(r.gain*100));view.addView(slider);
            slider.setOnSeekBarChangeListener(change(value->{r.gain=value/100f;volume.setText("音量 "+value+"%");}));
            mute=new CheckBox(MainActivity.this);mute.setText("静音");mute.setTextColor(INK);mute.setChecked(r.muted);mute.setOnCheckedChangeListener((button,checked)->r.muted=checked);view.addView(mute);
            meter=new ProgressBar(MainActivity.this,null,android.R.attr.progressBarStyleHorizontal);meter.setMax(100);view.addView(meter);
            Button remove=button("断开设备");remove.setOnClickListener(v->{AudioService s=AudioService.current;if(s!=null)s.remove(r);});view.addView(remove);
        }
    }
    interface Value{void set(int value);}
    SeekBar.OnSeekBarChangeListener change(Value callback){return new SeekBar.OnSeekBarChangeListener(){public void onProgressChanged(SeekBar bar,int value,boolean user){if(user)callback.set(value);}public void onStartTrackingTouch(SeekBar bar){}public void onStopTrackingTouch(SeekBar bar){}};}
    LinearLayout column(){LinearLayout l=new LinearLayout(this);l.setOrientation(LinearLayout.VERTICAL);l.setLayoutParams(new LinearLayout.LayoutParams(-1,-2));return l;}
    LinearLayout card(String title){LinearLayout l=column();l.setPadding(dp(16),dp(16),dp(16),dp(16));l.setBackground(round(CARD,20));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.setMargins(0,dp(20),0,0);l.setLayoutParams(p);root.addView(l);l.addView(text(title,20,true));return l;}
    TextView text(String value,int size,boolean bold){TextView t=new TextView(this);t.setText(value);t.setTextColor(bold?INK:SUB);t.setTextSize(size);if(bold)t.setTypeface(null,Typeface.BOLD);t.setPadding(0,dp(5),0,dp(9));return t;}
    Button button(String value){Button b=new Button(this);b.setText(value);b.setTextColor(ACCENT);b.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));b.setTextSize(14);b.setBackground(new android.graphics.drawable.RippleDrawable(android.content.res.ColorStateList.valueOf(0x224f46c4),round(GREEN,12),null));b.setMinHeight(dp(44));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.setMargins(0,dp(3),0,dp(3));b.setLayoutParams(p);b.setAllCaps(false);return b;}
    EditText input(String hint){EditText e=new EditText(this);e.setHint(hint);e.setHintTextColor(SUB);e.setTextColor(INK);e.setSingleLine(true);e.setTextSize(14);return e;}
    void toast(String text){Toast.makeText(this,text,Toast.LENGTH_LONG).show();}
    int dp(int value){return Math.round(value*getResources().getDisplayMetrics().density);}
    static boolean validIp(String ip){if(!ip.matches("[0-9]{1,3}(\\.[0-9]{1,3}){3}"))return false;for(String s:ip.split("\\."))if(Integer.parseInt(s)>255)return false;return !ip.equals("0.0.0.0");}
}

