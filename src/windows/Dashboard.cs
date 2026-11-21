namespace SoundMesh;
sealed partial class MainForm {
    readonly Label sessionHeading=Caption("准备融合",25,true);
    readonly Label sessionDesc=Caption("",10);
    readonly Button[] pageButtons={Btn("混音台"),Btn("设备连接"),Btn("输出设置")};
    int currentPage;
    static Image Graphic(string name){
        using var stream=typeof(MainForm).Assembly.GetManifestResourceStream("Brand."+name)!;
        using var img=Image.FromStream(stream);return new Bitmap(img);
    }
    void AlignChannel(Label label,TrackBar fader){
        label.AutoSize=false;label.Size=new Size(224,34);label.TextAlign=ContentAlignment.MiddleLeft;
        label.Margin=new Padding(4,0,6,0);fader.Width=280;fader.Margin=new Padding(0);
        fader.AccessibleName=label.Text;
    }
    FlowLayoutPanel BuildHeader(){
        var header=new FlowLayoutPanel{Dock=DockStyle.Top,Height=66,BackColor=Color.White,Padding=new(22,10,0,0),WrapContents=false};
        header.Controls.Add(new PictureBox{Image=Graphic("Mark"),SizeMode=PictureBoxSizeMode.Zoom,Size=new(38,38),Margin=new(0,3,6,0)});
        header.Controls.Add(Caption("声汇",18,true));var word=Caption("SoundMesh",11);word.ForeColor=Color.FromArgb(92,98,123);word.Margin=new(0,12,32,0);header.Controls.Add(word);
        return header;
    }
    void BuildSessionHero(){
        var hero=new Panel{Width=700,Height=174,Margin=new(0,0,0,14),BackgroundImage=Graphic("Wave"),BackgroundImageLayout=ImageLayout.Stretch,BackColor=Pine};
        sessionHeading.BackColor=sessionDesc.BackColor=Color.Transparent;sessionHeading.ForeColor=Color.White;sessionDesc.ForeColor=Color.FromArgb(224,227,249);
        sessionHeading.Location=new(22,12);sessionDesc.Location=new(24,68);share.Location=new(24,108);
        share.MinimumSize=new(122,38);hero.Controls.AddRange([sessionHeading,sessionDesc,share]);body.Controls.Add(hero);
        body.ClientSizeChanged+=(_,_)=>{int width=body.ClientSize.Width-body.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-6;
            if(width<100)return;hero.Width=width;foreach(var card in new[]{send,receive,routing,pages.GetValueOrDefault("mix")})
                if(card!=null)card.MinimumSize=new(width,0);};
    }
    void InstallNavigation(Control mix){
        var navigation=Line();navigation.Margin=new(0,0,0,12);
        for(int i=0;i<pageButtons.Length;i++){int index=i;var b=pageButtons[i];b.MinimumSize=new(108,36);b.Click+=(_,_)=>SelectPage(index);navigation.Controls.Add(b);}
        body.Controls.Add(navigation);body.Controls.SetChildIndex(navigation,1);
        SelectPage(0);
    }
    void SelectPage(int index){
        currentPage=index;
        pages["mix"].Visible=index==0;receive.Visible=send.Visible=index==1;routing.Visible=index==2;
        for(int i=0;i<pageButtons.Length;i++){
            pageButtons[i].BackColor=i==index?Color.FromArgb(76,67,193):Color.White;
            pageButtons[i].ForeColor=i==index?Color.White:Ink;
        }
    }
}
