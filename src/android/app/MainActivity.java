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
    final int BG=Color.rgb(242,245,240),CARD=Color.WHITE,INK=Color.rgb(26,46,42),SUB=Color.rgb(67,88,82),GREEN=Color.rgb(211,236,215);
    LinearLayout nearbyList,root,list,addCard,mixCard,shareCard,startCard;
    TextView info,count,masterValue,found;
    Button receive,send,stop,scan,showQr,beta,focus;
    CheckBox monitor;SeekBar master;
    Handler handler=new Handler(Looper.getMainLooper());
    Map<AudioService.Remote,DeviceRow> rows=new HashMap<>();
    Lan lan;boolean wantsDuplex,discovering;
    final Runnable update=new Runnable(){public void run(){refresh();handler.postDelayed(this,450);}};
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);lan=new Lan(this);
        if(state!=null)wantsDuplex=state.getBoolean("beta");
        getWindow().setStatusBarColor(BG);getWindow().setNavigationBarColor(BG);
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);
        ScrollView scroll=new ScrollView(this);scroll.setBackgroundColor(BG);scroll.setFillViewport(true);root=column();
        root.setPadding(dp(20),dp(24),dp(20),dp(36));android.widget.FrameLayout.LayoutParams layout=new android.widget.FrameLayout.LayoutParams(Math.min(getResources().getDisplayMetrics().widthPixels,dp(720)),-2,Gravity.TOP|Gravity.CENTER_HORIZONTAL);scroll.addView(root,layout);setContentView(scroll);
        if(Build.VERSION.SDK_INT>=35)root.setOnApplyWindowInsetsListener((v,i)->{android.graphics.Insets b=i.getInsets(WindowInsets.Type.systemBars());v.setPadding(dp(20)+b.left,dp(20)+b.top,dp(20)+b.right,dp(30)+b.bottom);return i;});
        root.addView(text("声汇",32,true));info=text("选择耳机所在的设备",14,false);info.setTextIsSelectable(true);root.addView(info);
        startCard=card("音频工作台");receive=button("耳机接在这里");send=button("共享本机声音");beta=button("双向空间  Beta");startCard.addView(receive);startCard.addView(send);startCard.addView(beta);
        receive.setOnClickListener(v->{wantsDuplex=false;launch("RECEIVE");});
        send.setOnClickListener(v->{wantsDuplex=false;authorize();});
        beta.setOnClickListener(v->new AlertDialog.Builder(this).setTitle("双向空间  Beta").setMessage("在每台设备开启双向空间，然后逐对连接。可在耳机所在设备监听全部声音。").setPositiveButton("进入并共享",(d,w)->{wantsDuplex=true;authorize();}).setNegativeButton("返回",null).show());
        shareCard=card("本机共享");showQr=button("显示配对二维码");shareCard.addView(showQr);showQr.setOnClickListener(v->qr());
        Button speaker=button("调整本机外放");shareCard.addView(speaker);speaker.setOnClickListener(v->new AlertDialog.Builder(this).setTitle("发送端外放").setMessage("可以降低媒体音量并在接收端试听。部分机型会同时降低采集音量。若远端也变小，可将本机声音输出到闲置的有线耳机或 USB 音频设备。").setPositiveButton("打开系统音量",(d,w)->{android.media.AudioManager m=getSystemService(android.media.AudioManager.class);m.adjustStreamVolume(android.media.AudioManager.STREAM_MUSIC,android.media.AudioManager.ADJUST_SAME,android.media.AudioManager.FLAG_SHOW_UI);}).setNegativeButton("关闭",null).show());
        addCard=card("附近设备");scan=button("查找附近设备");addCard.addView(scan);scan.setOnClickListener(v->discover());
        found=text("",12,false);addCard.addView(found);nearbyList=column();addCard.addView(nearbyList);
        Button camera=button("扫码连接");addCard.addView(camera);camera.setOnClickListener(v->{if(checkSelfPermission(Manifest.permission.CAMERA)==PackageManager.PERMISSION_GRANTED)openScanner();else requestPermissions(new String[]{Manifest.permission.CAMERA},103);});
        Button manual=button("手动配对");addCard.addView(manual);manual.setOnClickListener(v->prompt(null));
        mixCard=card("混音台");masterValue=text("总音量 80%",14,true);mixCard.addView(masterValue);master=new SeekBar(this);master.setMax(100);master.setProgress(80);mixCard.addView(master);
        master.setOnSeekBarChangeListener(change(value->{AudioService s=AudioService.current;if(s!=null)s.setVolume(value);masterValue.setText("总音量 "+value+"%");}));
        focus=button("把监听切到本机");mixCard.addView(focus);focus.setOnClickListener(v->{AudioService s=AudioService.current;if(s!=null)s.listenHere();});
        monitor=new CheckBox(this);monitor.setText("在本机监听");monitor.setChecked(true);mixCard.addView(monitor);
        monitor.setOnCheckedChangeListener((v,checked)->{AudioService s=AudioService.current;if(s!=null)s.monitor=checked;});
        Button buffer=button("播放缓冲");mixCard.addView(buffer);buffer.setOnClickListener(v->new AlertDialog.Builder(this).setTitle("播放缓冲").setItems(new String[]{"灵敏 · 25 ms","均衡 · 70 ms","稳定 · 90 ms"},(d,i)->{AudioService s=AudioService.current;if(s!=null)s.setBuffer(new int[]{25,70,90}[i]);}).show());
        count=text("",12,false);mixCard.addView(count);list=column();mixCard.addView(list);stop=button("结束当前会话");root.addView(stop);stop.setOnClickListener(v->{stopService(new Intent(this,AudioService.class));lastError="会话已结束";});
    }
    void launch(String action){lastError="";startForegroundService(new Intent(this,AudioService.class).setAction(action));}
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
    @Override protected void onSaveInstanceState(Bundle out){super.onSaveInstanceState(out);out.putBoolean("beta",wantsDuplex);}
    @Override public void onRequestPermissionsResult(int req,String[] permissions,int[] grants){super.onRequestPermissionsResult(req,permissions,grants);if(grants.length==0||grants[0]!=0){toast("权限未授予");return;}if(req==101)requestCapture();if(req==103)openScanner();}
    @Override protected void onActivityResult(int req,int result,Intent data){
        super.onActivityResult(req,result,data);if(result!=RESULT_OK||data==null)return;
        if(req==102)startForegroundService(new Intent(this,AudioService.class).setAction(wantsDuplex?"DUPLEX":"SEND").putExtra("consent",data));
        if(req==104){String raw=data.getStringExtra("pair");String[] f=raw==null?new String[0]:raw.split("/");if(f.length==6&&f[5].matches("[a-fA-F0-9]{32}"))connect(f[3],f[4],f[3],f[5]);else toast("配对码无效");}
    }
    @Override protected void onResume(){super.onResume();handler.post(update);}
    @Override protected void onPause(){handler.removeCallbacks(update);super.onPause();}
    boolean notificationAsked;
    void refresh(){
        AudioService s=AudioService.current;boolean active=s!=null,duplex=active&&"双向".equals(s.mode);
        boolean rx=active&&(duplex||"接收".equals(s.mode)),tx=active&&(duplex||"发送".equals(s.mode));
        if(rx && !notificationAsked && Build.VERSION.SDK_INT>=33 &&
            checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED){
            notificationAsked=true;
            requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},105);
        }
        boolean startScan=rx&&addCard.getVisibility()!=View.VISIBLE;
        startCard.setVisibility(active?View.GONE:View.VISIBLE);addCard.setVisibility(rx?View.VISIBLE:View.GONE);mixCard.setVisibility(rx?View.VISIBLE:View.GONE);
        shareCard.setVisibility(tx?View.VISIBLE:View.GONE);stop.setVisibility(active?View.VISIBLE:View.GONE);monitor.setVisibility(duplex?View.VISIBLE:View.GONE);focus.setVisibility(duplex?View.VISIBLE:View.GONE);
        if(startScan)discover();
        show(info,tx?(duplex?"双向空间":"共享中")+"  ·  "+Lan.primary()+"\n配对码  "+s.code+"    已接入 "+s.subscribers()+" 台":active?s.status:lastError.isEmpty()?"选择耳机所在的设备":lastError);
        if(rx){int value=Math.round(s.master*100);if(!master.isPressed()&&master.getProgress()!=value)master.setProgress(value);show(masterValue,"总音量 "+value+"%");monitor.setChecked(s.monitor);}
        List<AudioService.Remote> current=active?new ArrayList<>(s.sources):Collections.emptyList();
        for(AudioService.Remote r:new ArrayList<>(rows.keySet()))if(!current.contains(r))list.removeView(rows.remove(r).view);
        for(AudioService.Remote r:current){DeviceRow row=rows.get(r);if(row==null){row=new DeviceRow(r);rows.put(r,row);list.addView(row.view);}show(row.status,r.host+" · "+r.status+" · 缓冲 "+r.ring.bufferMs()+" ms · 断流 "+r.ring.underruns()+" 次");row.meter.setProgress(Math.round(r.level*100));}
        show(count,current.isEmpty()?"连接设备后可单独调节音量":"已添加 "+current.size()+" 台设备");
    }
    void show(TextView label,String value){if(!value.contentEquals(label.getText()))label.setText(value);}
    GradientDrawable round(int color,int radius){GradientDrawable bg=new GradientDrawable();bg.setColor(color);bg.setCornerRadius(dp(radius));return bg;}
    final class DeviceRow {
        final LinearLayout view=column();final TextView status;final ProgressBar meter;
        DeviceRow(AudioService.Remote r){
            view.setPadding(dp(12),dp(12),dp(12),dp(12));view.setBackgroundColor(BG);LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.setMargins(0,dp(10),0,dp(10));view.setLayoutParams(p);
            view.addView(text(r.name,18,true));status=text(r.host,12,false);view.addView(status);
            TextView volume=text("音量 "+Math.round(r.gain*100)+"%",13,false);view.addView(volume);SeekBar slider=new SeekBar(MainActivity.this);slider.setMax(100);slider.setProgress(Math.round(r.gain*100));view.addView(slider);
            slider.setOnSeekBarChangeListener(change(value->{r.gain=value/100f;volume.setText("音量 "+value+"%");}));
            CheckBox mute=new CheckBox(MainActivity.this);mute.setText("静音");mute.setTextColor(INK);mute.setChecked(r.muted);mute.setOnCheckedChangeListener((button,checked)->r.muted=checked);view.addView(mute);
            meter=new ProgressBar(MainActivity.this,null,android.R.attr.progressBarStyleHorizontal);meter.setMax(100);view.addView(meter);
            Button remove=button("断开设备");remove.setOnClickListener(v->{AudioService s=AudioService.current;if(s!=null)s.remove(r);});view.addView(remove);
        }
    }
    interface Value{void set(int value);}
    SeekBar.OnSeekBarChangeListener change(Value callback){return new SeekBar.OnSeekBarChangeListener(){public void onProgressChanged(SeekBar bar,int value,boolean user){if(user)callback.set(value);}public void onStartTrackingTouch(SeekBar bar){}public void onStopTrackingTouch(SeekBar bar){}};}
    LinearLayout column(){LinearLayout l=new LinearLayout(this);l.setOrientation(LinearLayout.VERTICAL);l.setLayoutParams(new LinearLayout.LayoutParams(-1,-2));return l;}
    LinearLayout card(String title){LinearLayout l=column();l.setPadding(dp(16),dp(16),dp(16),dp(16));l.setBackground(round(CARD,20));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.setMargins(0,dp(20),0,0);l.setLayoutParams(p);root.addView(l);l.addView(text(title,18,true));return l;}
    TextView text(String value,int size,boolean bold){TextView t=new TextView(this);t.setText(value);t.setTextColor(bold?INK:SUB);t.setTextSize(size);if(bold)t.setTypeface(null,Typeface.BOLD);t.setPadding(0,dp(5),0,dp(9));return t;}
    Button button(String value){Button b=new Button(this);b.setText(value);b.setTextColor(INK);b.setBackground(round(GREEN,12));b.setMinHeight(dp(44));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.setMargins(0,dp(3),0,dp(3));b.setLayoutParams(p);b.setAllCaps(false);return b;}
    EditText input(String hint){EditText e=new EditText(this);e.setHint(hint);e.setHintTextColor(SUB);e.setTextColor(INK);e.setSingleLine(true);e.setTextSize(14);return e;}
    void toast(String text){Toast.makeText(this,text,Toast.LENGTH_LONG).show();}
    int dp(int value){return Math.round(value*getResources().getDisplayMetrics().density);}
    static boolean validIp(String ip){if(!ip.matches("[0-9]{1,3}(\\.[0-9]{1,3}){3}"))return false;for(String s:ip.split("\\."))if(Integer.parseInt(s)>255)return false;return !ip.equals("0.0.0.0");}
}

