package app.soundmesh.audio;

import android.app.Activity;
import android.content.Intent;
import android.hardware.Camera;
import android.os.Bundle;
import android.view.SurfaceHolder;
import android.view.SurfaceView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;
import com.google.zxing.*;
import com.google.zxing.common.HybridBinarizer;
import com.google.zxing.qrcode.QRCodeReader;

@SuppressWarnings("deprecation")
public final class ScanActivity extends Activity implements SurfaceHolder.Callback {
    private Camera camera;
    private long lastScan;
    private boolean finished;
    public void onCreate(Bundle state){
        super.onCreate(state);
        LinearLayout layout=new LinearLayout(this);
        layout.setOrientation(LinearLayout.VERTICAL);
        TextView caption=new TextView(this);
        caption.setPadding(32,40,32,24);caption.setTextSize(18);caption.setText("将发送端的声汇配对码放在画面中央");
        layout.addView(caption);
        SurfaceView preview=new SurfaceView(this);layout.addView(preview);
        preview.getHolder().addCallback(this);setContentView(layout);
    }
    public void surfaceCreated(SurfaceHolder holder){
        try {
            camera=Camera.open();Camera.Parameters p=camera.getParameters();
            if(p.getSupportedFocusModes().contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE)){
                p.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE);camera.setParameters(p);
            }
            camera.setDisplayOrientation(90);camera.setPreviewDisplay(holder);
            camera.setPreviewCallback((data,cam)->{
                long now=android.os.SystemClock.elapsedRealtime();
                if(finished||now-lastScan<180)return;lastScan=now;
                Camera.Size size=cam.getParameters().getPreviewSize();
                try {
                    LuminanceSource luma=new PlanarYUVLuminanceSource(data,size.width,size.height,0,0,size.width,size.height,false);
                    String result=new QRCodeReader().decode(new BinaryBitmap(new HybridBinarizer(luma))).getText();
                    if(!result.startsWith("soundmesh://pair/"))return;
                    finished=true;setResult(RESULT_OK,new Intent().putExtra("pair",result));finish();
                }catch(ReaderException ignored){}
            });
            camera.startPreview();
        }catch(Exception e){Toast.makeText(this,"摄像头无法打开，可用附近设备配对",Toast.LENGTH_LONG).show();finish();}
    }
    public void surfaceChanged(SurfaceHolder h,int f,int w,int height){}
    public void surfaceDestroyed(SurfaceHolder h){if(camera!=null){camera.setPreviewCallback(null);camera.release();camera=null;}}
}

