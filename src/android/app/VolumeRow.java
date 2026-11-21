package app.soundmesh.audio;

import android.content.Context;
import android.widget.*;
import java.util.function.IntConsumer;

final class VolumeRow {
    final LinearLayout view;
    final TextView label;
    final TextView amount;
    final SeekBar slider;
    final String name;
    VolumeRow(Context context,LinearLayout parent,String name,IntConsumer apply){
        this.name=name;
        view=new LinearLayout(context);view.setOrientation(LinearLayout.VERTICAL);
        int unit=Math.round(context.getResources().getDisplayMetrics().density*4);
        view.setPadding(0,unit*2,0,unit);
        LinearLayout header=new LinearLayout(context);header.setGravity(android.view.Gravity.CENTER_VERTICAL);view.addView(header);
        label=new TextView(context);label.setTextSize(15);label.setTextColor(0xff292b41);label.setText(name);header.addView(label,new LinearLayout.LayoutParams(0,-2,1));
        amount=new TextView(context);amount.setTextSize(14);amount.setTextColor(0xff5046c4);amount.setTypeface(android.graphics.Typeface.MONOSPACE);header.addView(amount);
        slider=new SeekBar(context);slider.setProgressTintList(android.content.res.ColorStateList.valueOf(0xff5046c4));slider.setThumbTintList(android.content.res.ColorStateList.valueOf(0xff5046c4));slider.setMinimumHeight(unit*10);slider.setMax(100);slider.setContentDescription(name);view.addView(slider);
        slider.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){
            public void onProgressChanged(SeekBar bar,int value,boolean user){
                amount.setText(value+"%");if(user)apply.accept(value);
            }
            public void onStartTrackingTouch(SeekBar bar){}
            public void onStopTrackingTouch(SeekBar bar){}
        });
        parent.addView(view);
    }
    void sync(int value){
        if(!slider.isPressed() && slider.getProgress()!=value)slider.setProgress(value);
        String text=slider.getProgress()+"%";
        if(!text.contentEquals(amount.getText()))amount.setText(text);
    }
}
