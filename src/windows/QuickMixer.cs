using System.Runtime.InteropServices;

namespace SoundMesh;

sealed partial class MainForm {
    const int HotkeyId = 0x534D;
    [DllImport("user32.dll", SetLastError=true)]
    static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    readonly Button quickButton=Btn("音量小窗");
    Form? quickWindow;
    TrackBar? quickVolume;
    Label? quickLabel;
    ComboBox? quickTarget;
    bool syncing;
    sealed record Target(string Name,TrackBar Slider){public override string ToString()=>Name;}
    readonly Dictionary<TrackBar,int> previousVolumes=new();
    void SyncQuick(){
        if(quickTarget==null || quickVolume==null || quickLabel==null)return;
        var targets=new List<Target>{new("远端混音总音量",master),new("本机应用",localGain),new("本机 · 发给其他设备",sendGain)};
        targets.AddRange(peerSliders.Select(p=>new Target(p.Key.Name,p.Value)));
        var previous=(quickTarget.SelectedItem as Target)?.Slider;
        syncing=true;
        try{
            if(!targets.SequenceEqual(quickTarget.Items.Cast<Target>())){
                quickTarget.Items.Clear();quickTarget.Items.AddRange(targets.ToArray());
                quickTarget.SelectedIndex=Math.Max(0,targets.FindIndex(t=>t.Slider==previous));
                foreach(var obsolete in previousVolumes.Keys.Where(k=>targets.All(t=>t.Slider!=k)).ToArray())previousVolumes.Remove(obsolete);
            }
            if(quickTarget.SelectedItem is Target t){
                quickVolume.Value=t.Slider.Value;
                quickLabel.Text=t.Name+"   "+t.Slider.Value+"%";
                if(t.Slider.Value>0)previousVolumes[t.Slider]=t.Slider.Value;
            }
        }finally{syncing=false;}
    }
    void ConfigureQuickMixer(FlowLayoutPanel header) {
        header.Controls.Add(quickButton);
        quickButton.Click+=(_,_)=>ToggleQuick();
        master.ValueChanged+=(_,_)=>SyncQuick();
        localGain.ValueChanged+=(_,_)=>SyncQuick();
        FormClosed+=(_,_)=>quickWindow?.Dispose();
    }
    protected override void OnHandleCreated(EventArgs e){
        base.OnHandleCreated(e);
        if(RegisterHotKey(Handle,HotkeyId,0x4003,(uint)Keys.M))
            quickButton.Text="音量小窗 · Ctrl+Alt+M";
        else quickButton.Text="音量小窗（快捷键被占用）";
    }
    protected override void OnHandleDestroyed(EventArgs e){
        UnregisterHotKey(Handle,HotkeyId);
        base.OnHandleDestroyed(e);
    }
    protected override void WndProc(ref Message m){
        if(m.Msg==0x0312 && m.WParam.ToInt32()==HotkeyId){ToggleQuick();return;}
        base.WndProc(ref m);
    }
    void ToggleQuick(){
        if(sender==null){Tell("开始融合后可使用音量小窗");return;}
        if(quickWindow is {Visible:true}){quickWindow.Hide();return;}
        if(quickWindow==null){
            quickWindow=new Form {Text="声汇音量",Font=Font,BackColor=Color.White,ForeColor=Ink,TopMost=true,Icon=Icon,
                FormBorderStyle=FormBorderStyle.FixedToolWindow,ShowInTaskbar=false,ClientSize=new(322,185),
                StartPosition=FormStartPosition.Manual,KeyPreview=true};
            var panel=Stack();panel.Padding=new(12);panel.Dock=DockStyle.Fill;
            quickTarget=Choice();quickTarget.Width=260;quickTarget.AccessibleName="选择设备";
            quickTarget.SelectedIndexChanged+=(_,_)=>{if(!syncing)SyncQuick();};panel.Controls.Add(quickTarget);
            quickLabel=Caption("",13,true);panel.Controls.Add(quickLabel);
            quickVolume=new LevelSlider{Minimum=0,Maximum=100,Width=258,TickStyle=TickStyle.None,AccessibleName="混音总音量"};
            quickVolume.ValueChanged+=(_,_)=>{
                if(!syncing && quickTarget.SelectedItem is Target t){t.Slider.Value=quickVolume.Value;SyncQuick();}
            };
            panel.Controls.Add(quickVolume);
            var mute=Btn("静音 / 恢复");mute.Click+=(_,_)=>{if(quickTarget.SelectedItem is Target t){
                if(t.Slider.Value!=0){previousVolumes[t.Slider]=t.Slider.Value;t.Slider.Value=0;}
                else t.Slider.Value=previousVolumes.GetValueOrDefault(t.Slider,80);
                SyncQuick();
            }};
            panel.Controls.Add(mute);quickWindow.Controls.Add(panel);
            quickWindow.FormClosing+=(_,e)=>{if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;quickWindow.Hide();}};
            quickWindow.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)quickWindow.Hide();};
        }
        SyncQuick();
        var area=Screen.FromPoint(Cursor.Position).WorkingArea;
        quickWindow.Location=new Point(area.Right-quickWindow.Width-18,area.Bottom-quickWindow.Height-18);
        quickWindow.Show();quickWindow.Activate();quickVolume!.Focus();
    }
}