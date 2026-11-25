package app.soundmesh.audio;
import android.app.*;
import android.content.*;
import android.graphics.*;
import android.os.*;
import android.view.*;
import android.widget.*;
import java.net.*;
import java.util.*;

public final class EnvironmentCheck extends Activity {
    final ArrayList<Double> points=new ArrayList<>();
    EditText host;TextView report;Button run;Chart chart;volatile boolean ended;
    @Override public void onCreate(Bundle state){
        super.onCreate(state);setTitle("检查环境");
        LinearLayout layout=new LinearLayout(this);layout.setOrientation(1);int pad=(int)(18*getResources().getDisplayMetrics().density);layout.setPadding(pad,pad*2,pad,pad);
        host=new EditText(this);host.setSingleLine(true);host.setHint("对端融合页上的 IPv4 地址");host.setText(getIntent().getStringExtra("host"));layout.addView(host);
        run=new Button(this);run.setText("开始 20 次通信测量");layout.addView(run);chart=new Chart(this);layout.addView(chart,new LinearLayout.LayoutParams(-1,0,1));
        report=new TextView(this);report.setText("每个点表示一次 TCP 建连和声汇应答耗时。请先让对端开始融合。");layout.addView(report);
        Button back=new Button(this);back.setText("返回声汇");back.setOnClickListener(v->finish());layout.addView(back);setContentView(layout);
        run.setOnClickListener(v->{String ip=host.getText().toString().trim();if(!MainActivity.validIp(ip)||(ip.startsWith("127.")||ip.equals("255.255.255.255")||Integer.parseInt(ip.split("\\.")[0])>=224)||Arrays.asList(AudioService.addresses().split(" / ")).contains(ip)){report.setText("请输入另一台设备的 IPv4 地址");return;}
            run.setEnabled(false);host.setEnabled(false);points.clear();chart.invalidate();new Thread(()->measure(ip)).start();});
    }
    void measure(String ip){
        for(int i=0;i<20&&!ended;i++){
            double ms=-1;String why="";boolean connected=false;
            try(Socket s=new Socket()){
                long t=SystemClock.elapsedRealtimeNanos();
                s.setTcpNoDelay(true);s.connect(new InetSocketAddress(ip,Wire.PORT),1800);connected=true;s.setSoTimeout(1800);
                Wire.send(s.getOutputStream(),"SM-DIAG1");
                if(!Wire.line(s.getInputStream()).equals("SM-READY1")){runOnUiThread(()->report.setText("TCP 可达，对端未识别检测协议，请先更新对端声汇。"));break;}
                ms=(SystemClock.elapsedRealtimeNanos()-t)/1e6;
            }catch(Exception e){why=(connected?"声汇应答":"TCP 49632")+"失败 · "+(e instanceof SocketTimeoutException?"超时":e.getMessage());}
            final double value=ms;final String detail=why;
            runOnUiThread(()->{if(ended)return;points.add(value);chart.invalidate();
                ArrayList<Double> good=new ArrayList<>();for(double v:points)if(v>=0)good.add(v);Collections.sort(good);
                if(good.isEmpty())report.setText(points.size()+" 次均失败。"+detail+"\n请两端互测，并检查 IP、防火墙及校园网设备隔离。");
                else {int n=good.size();double p95=good.get((int)Math.ceil(.95*n)-1);
                    report.setText((n<points.size()||p95>100?"有中断或高延迟，建议同步 200 ms":"可达性良好，可从同步 150 ms 开始")+String.format(Locale.ROOT,"\n成功 %d/%d　中位 %.0f ms　P95 %.0f ms　波动范围 %.0f ms",n,points.size(),good.get(n/2),p95,good.get(n-1)-good.get(0))+"\n"+detail);}
            });
            try{Thread.sleep(450);}catch(InterruptedException e){break;}
        }
        runOnUiThread(()->{if(!ended){run.setEnabled(true);host.setEnabled(true);}});
    }
    @Override public void onDestroy(){ended=true;super.onDestroy();}
    class Chart extends View{
        final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);Chart(Context c){super(c);}
        @Override protected void onDraw(Canvas c){float x0=70,y0=40,w=getWidth()-100,h=getHeight()-90;if(h<1||w<1)return;
            double max=50;for(double v:points)max=Math.max(max,v);p.setColor(0xff999aaa);p.setTextSize(24);c.drawText(Math.round(max)+" ms",2,y0,p);
            c.drawText("0",8,y0+h,p);c.drawLine(x0,y0+h,x0+w,y0+h,p);float last=-1,ly=0;
            for(int i=0;i<points.size();i++){double v=points.get(i);float x=x0+w*i/19;
                if(v<0){p.setColor(0xffc84343);c.drawText("×",x-6,y0+h/2,p);last=-1;continue;}
                float y=y0+h*(1-(float)(v/max));p.setColor(0xff5449cd);p.setStrokeWidth(4);if(last>=0)c.drawLine(last,ly,x,y,p);c.drawCircle(x,y,5,p);last=x;ly=y;}
        }
    }
}