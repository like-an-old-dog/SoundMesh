using System.Drawing.Drawing2D;
namespace SoundMesh;

// Keep the native range, keyboard and accessibility semantics while painting a larger thumb.
sealed class LevelSlider : TrackBar {
    public LevelSlider(){
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,true);
        AutoSize=false;Height=36;TickStyle=TickStyle.None;Cursor=Cursors.Hand;
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var bg=Parent?.BackColor??Color.White;
        if(bg.A==255)g.Clear(bg);
        float left=12,right=Math.Max(left+1,ClientSize.Width-12),cy=ClientSize.Height/2f;
        float end=left+(right-left)*(Value-Minimum)/Math.Max(1,Maximum-Minimum);
        using var track=new Pen(Color.FromArgb(226,228,241),6){StartCap=LineCap.Round,EndCap=LineCap.Round};
        using var active=new Pen(Enabled?Color.FromArgb(84,73,205):Color.Gray,6){StartCap=LineCap.Round,EndCap=LineCap.Round};
        g.DrawLine(track,left,cy,right,cy);if(end>left)g.DrawLine(active,left,cy,end,cy);
        g.FillEllipse(Brushes.White,end-7,cy-7,14,14);
        using var edge=new Pen(active.Color,3);g.DrawEllipse(edge,end-6,cy-6,12,12);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(2,2,Width-4,Height-4));
    }
    protected override void OnValueChanged(EventArgs e){base.OnValueChanged(e);Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Capture=true;SetAt(e.X);}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture&&e.Button==MouseButtons.Left)SetAt(e.X);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left){SetAt(e.X);Capture=false;}}
    void SetAt(int x){Value=Minimum+(int)Math.Round(Math.Clamp((x-12d)/Math.Max(1,Width-24),0,1)*(Maximum-Minimum));}
}