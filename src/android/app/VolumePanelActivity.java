package app.soundmesh.audio;
import android.app.Activity;
import android.os.*;
import android.view.*;
import android.widget.*;
import java.util.*;

public final class VolumePanelActivity extends Activity {
    private LocalControls local;
    private VolumeRow mix;
    private LinearLayout list;
    private final Map<AudioService.Remote,VolumeRow> rows=new LinkedHashMap<>();
    private final Handler handler=new Handler(Looper.getMainLooper());
    private final Runnable update=new Runnable(){
        public void run(){
            AudioService service=AudioService.current;
            if(service==null || !"融合".equals(service.mode)){finish();return;}
            local.sync(service);mix.sync(Math.round(service.master*100));
            for(AudioService.Remote old:new ArrayList<>(rows.keySet())){
                if(!service.sources.contains(old))list.removeView(rows.remove(old).view);
            }
            for(AudioService.Remote peer:service.sources){
                VolumeRow row=rows.get(peer);
                if(row==null){row=new VolumeRow(VolumePanelActivity.this,list,peer.name,v->peer.gain=v/100f);rows.put(peer,row);}
                row.sync(Math.round(peer.gain*100));
            }
            handler.postDelayed(this,350);
        }
    };
    public void onCreate(Bundle state){
        super.onCreate(state);setTitle("声汇音量");
        ScrollView scroll=new ScrollView(this);list=new LinearLayout(this);list.setOrientation(LinearLayout.VERTICAL);
        int pad=Math.round(16*getResources().getDisplayMetrics().density);list.setPadding(pad,pad,pad,pad);
        scroll.addView(list);setContentView(scroll);local=new LocalControls(this,list);
        mix=new VolumeRow(this,list,"远端混音总音量",v->{AudioService s=AudioService.current;if(s!=null)s.setVolume(v);});
        setFinishOnTouchOutside(true);
    }
    public void onResume(){super.onResume();handler.post(update);}
    public void onPause(){handler.removeCallbacks(update);super.onPause();}
}
