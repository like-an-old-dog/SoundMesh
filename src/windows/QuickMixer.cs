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
    int previousVolume=80;
    void ConfigureQuickMixer(FlowLayoutPanel header) {
        header.Controls.Add(quickButton);
        quickButton.Click+=(_,_)=>ToggleQuick();
        master.ValueChanged+=(_,_)=>{
            if(master.Value>0) previousVolume=master.Value;
            if(quickVolume!=null) quickVolume.Value=master.Value;
            if(quickLabel!=null) quickLabel.Text="混音总音量   "+master.Value+"%";
        };
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
        if(quickWindow is {Visible:true}){quickWindow.Hide();return;}
        if(quickWindow==null){
            quickWindow=new Form {Text="声汇音量",Font=Font,BackColor=Paper,ForeColor=Ink,TopMost=true,
                FormBorderStyle=FormBorderStyle.FixedToolWindow,ShowInTaskbar=false,ClientSize=new(304,140),
                StartPosition=FormStartPosition.Manual,KeyPreview=true};
            var panel=Stack();panel.Padding=new(12);panel.Dock=DockStyle.Fill;
            quickLabel=Caption("",13,true);panel.Controls.Add(quickLabel);
            quickVolume=new TrackBar{Minimum=0,Maximum=100,Width=258,TickStyle=TickStyle.None,AccessibleName="混音总音量"};
            quickVolume.ValueChanged+=(_,_)=>master.Value=quickVolume.Value;
            panel.Controls.Add(quickVolume);
            var mute=Btn("静音 / 恢复");mute.Click+=(_,_)=>master.Value=master.Value==0?previousVolume:0;
            panel.Controls.Add(mute);quickWindow.Controls.Add(panel);
            quickWindow.FormClosing+=(_,e)=>{if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;quickWindow.Hide();}};
            quickWindow.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)quickWindow.Hide();};
        }
        quickVolume!.Value=master.Value;
        quickLabel!.Text="混音总音量   "+master.Value+"%";
        var area=Screen.FromPoint(Cursor.Position).WorkingArea;
        quickWindow.Location=new Point(area.Right-quickWindow.Width-18,area.Bottom-quickWindow.Height-18);
        quickWindow.Show();quickWindow.Activate();quickVolume.Focus();
    }
}