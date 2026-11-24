package app.soundmesh.audio;

import android.app.Activity;
import android.os.Bundle;
import android.webkit.WebView;
import android.widget.Button;
import android.widget.LinearLayout;
import android.view.WindowInsets;
import android.os.Build;

public final class GuideActivity extends Activity {
    private WebView reader;
    @Override public void onCreate(Bundle state){
        super.onCreate(state);
        LinearLayout column=new LinearLayout(this);
        column.setOrientation(LinearLayout.VERTICAL);
        if(Build.VERSION.SDK_INT>=35)column.setOnApplyWindowInsetsListener((view,insets)->{
            android.graphics.Insets bars=insets.getInsets(WindowInsets.Type.systemBars());
            view.setPadding(bars.left,bars.top,bars.right,bars.bottom);return insets;
        });
        Button back=new Button(this);back.setText("返回声汇");back.setOnClickListener(v->finish());column.addView(back);
        reader=new WebView(this);reader.getSettings().setBlockNetworkLoads(true);
        reader.setWebViewClient(new android.webkit.WebViewClient());
        column.addView(reader,new LinearLayout.LayoutParams(-1,0,1));setContentView(column);
        if(state==null||reader.restoreState(state)==null)reader.loadUrl("file:///android_asset/guide.html");
    }
    @Override protected void onSaveInstanceState(Bundle state){super.onSaveInstanceState(state);reader.saveState(state);}
}
