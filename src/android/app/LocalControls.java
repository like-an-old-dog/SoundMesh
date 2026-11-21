package app.soundmesh.audio;
import android.content.Context;
import android.media.AudioManager;
import android.widget.LinearLayout;

final class LocalControls {
    private final VolumeRow source,playback;
    private final AudioManager audio;
    LocalControls(Context context,LinearLayout parent){
        audio=context.getSystemService(AudioManager.class);
        source=new VolumeRow(context,parent,"本机 · 发给其他设备",v->{
            AudioService s=AudioService.current;if(s!=null)s.setLocalPercent(v);
        });
        playback=new VolumeRow(context,parent,"本机 · 系统媒体输出",v->{
            int min=audio.getStreamMinVolume(AudioManager.STREAM_MUSIC);
            int max=audio.getStreamMaxVolume(AudioManager.STREAM_MUSIC);
            if(!audio.isVolumeFixed())audio.setStreamVolume(AudioManager.STREAM_MUSIC,min+Math.round((max-min)*v/100f),0);
        });
        playback.slider.setEnabled(!audio.isVolumeFixed());
    }
    void sync(AudioService s){
        source.sync(s.localPercent());
        int min=audio.getStreamMinVolume(AudioManager.STREAM_MUSIC);
        int max=audio.getStreamMaxVolume(AudioManager.STREAM_MUSIC);
        playback.sync(Math.round(100f*(audio.getStreamVolume(AudioManager.STREAM_MUSIC)-min)/Math.max(1,max-min)));
    }
}
