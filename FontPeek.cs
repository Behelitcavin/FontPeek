// FontPeek · 글꼴 미리보기 위젯
// 빌드: .\build.ps1 (Windows 내장 .NET Framework 4.x C# 컴파일러, 외부 라이브러리·네트워크 없음)
// 설정: %LOCALAPPDATA%\FontPeek\settings.txt (창 위치·크기,고정 / 글자 크기 / 세로 / 일본어만 / 예시 문장). 검색어는 저장 안 함.
//
// 수정 참고
// - 글꼴 목록: GDI EnumFontFamiliesEx(DEFAULT_CHARSET). 비트맵(래스터) 글꼴과 '@' 세로판은 목록에서 뺀다.
// - 일본어 판별: JIS(SHIFTJIS_CHARSET) 선언 글꼴은 포함. 단 한글 문자셋도 있고 '気'가 없으면 한국 글꼴로 보고 뺀다.
//   JIS 선언이 없어도 あ·ア 글리프가 있고 한글·중국어 문자셋이 없으면 포함한다(문자셋 표시가 빠진 일본 동인 폰트).
// - 줄 간격은 글자 크기의 1.5배로 통일하고 TA_BASELINE으로 그린다(글꼴 메트릭이 커도 간격이 벌어지지 않게).
// - 줄바꿈은 모든 글꼴 공통(전각 1em·반각 0.6em 어림)이라 카드 크기가 전부 같다. GDI는 글꼴을 바꿀 때마다
//   글꼴당 ~1.5ms가 들어서(측정 함수 종류 무관) 글꼴별로 재면 140개에 0.2초 이상 걸린다. 그리기는 보이는 카드만.
// - 세로쓰기: '@글꼴' + escapement 2700 = GDI 표준 세로쓰기(글꼴의 세로용 글리프 vert 사용).
//   기준선(TA_BASELINE) 기준으로 글자 네모 틀이 대략 [x-0.12em, x+0.88em]에 그려진다.
//   단 '@' 세로판이 없거나(@굴림으로 바꿔치기됨), 있어도 ー가 안 서거나 。가 오른쪽 위로 안 가는 글꼴
//   (Noto Sans JP, 07TetsubinGothic 등)이 있어서, 세로 모드에서 카드를 처음 그릴 때 ー·。를 한 번씩 그려 검사한다.
//   검사에 실패한 글꼴은 한 글자씩 세워 그린다: 「」ー… 와 반각 문자는 90° 돌리고, 、。는 오른쪽 위로, 작은 가나는 조금 옮긴다.
// - 그리기는 GDI TextOut이라 글꼴에 없는 글자는 다른 글꼴로 대체되지 않고 □로 보인다(지원 여부 확인용).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("글꼴 미리보기")]
[assembly: System.Reflection.AssemblyProduct("FontPeek")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) 2026 Behelit · MIT License")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]

internal static class Gdi {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    internal class LOGFONT { public int height,width,escapement,orientation,weight; public byte italic,underline,strikeOut,charSet,outPrecision,clipPrecision,quality,pitchAndFamily; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string face; }
    [StructLayout(LayoutKind.Sequential)] internal struct SIZE { public int cx,cy; }
    internal delegate int EnumProc(IntPtr logFont,IntPtr metric,uint type,IntPtr param);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] internal static extern int EnumFontFamiliesExW(IntPtr hdc,LOGFONT lf,EnumProc proc,IntPtr param,uint flags);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr CreateFontIndirectW(LOGFONT lf);
    [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr hdc,IntPtr obj);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] internal static extern uint GetGlyphIndicesW(IntPtr hdc,string text,int count,[Out] ushort[] glyphs,uint flags);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] internal static extern bool GetTextExtentPoint32W(IntPtr hdc,string text,int count,out SIZE size);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] internal static extern bool TextOutW(IntPtr hdc,int x,int y,string text,int count);
    [DllImport("gdi32.dll")] internal static extern uint SetTextAlign(IntPtr hdc,uint align);
    [DllImport("gdi32.dll")] internal static extern int SetBkMode(IntPtr hdc,int mode);
    [DllImport("gdi32.dll")] internal static extern int SetTextColor(IntPtr hdc,int color);
    [DllImport("gdi32.dll")] internal static extern int SaveDC(IntPtr hdc);
    [DllImport("gdi32.dll")] internal static extern bool RestoreDC(IntPtr hdc,int saved);
    [DllImport("gdi32.dll")] internal static extern int IntersectClipRect(IntPtr hdc,int l,int t,int r,int b);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] internal static extern int SetWindowTheme(IntPtr h,string app,string id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,string l);
    // vertical: '@' 세로판(세로쓰기용) / turned: 같은 글꼴을 90° 돌린 것(세로판을 못 쓰는 글꼴의 괄호·장음용)
    internal static IntPtr Font(string face,int px,bool vertical,bool turned) {
        var lf=new LOGFONT(); lf.height=-px; lf.charSet=1; lf.quality=4; lf.face=(vertical ? "@" : "")+face;
        if(vertical || turned) lf.escapement=lf.orientation=2700;
        return CreateFontIndirectW(lf);
    }
}

internal sealed class Entry {
    internal string Name; internal bool Japanese,HasVertical; internal int VerticalCheck; // 0=아직, 1=세로판 사용, 2=한 글자씩
    internal Rectangle Box; IntPtr font; int fontKey;
    // 같은 크기·방향이면 GDI 글꼴을 다시 만들지 않는다(매번 만들면 입력할 때마다 느려짐).
    internal IntPtr Font(int px,bool vertical) {
        int key=px*2+(vertical ? 1 : 0);
        if(key!=fontKey) { if(font!=IntPtr.Zero) Gdi.DeleteObject(font); font=Gdi.Font(Name,px,vertical,false); fontKey=key; }
        return font;
    }
}

internal sealed class Board : Panel {
    internal Board() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); AutoScroll=true; }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Gdi.SetWindowTheme(Handle,"DarkMode_Explorer",null); }
}

internal static class Program {
    [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new Peek()); }
}

internal static class Startup {
    const string KeyPath="Software\\Microsoft\\Windows\\CurrentVersion\\Run", ValueName="FontPeek";
    static string Command { get { return "\""+Application.ExecutablePath+"\""; } }
    internal static bool Enabled {
        get {
            using(var key=Registry.CurrentUser.OpenSubKey(KeyPath))
                return key!=null && string.Equals(key.GetValue(ValueName) as string,Command,StringComparison.OrdinalIgnoreCase);
        }
    }
    internal static void SetEnabled(bool enabled) {
        using(var key=Registry.CurrentUser.CreateSubKey(KeyPath)) {
            if(enabled) key.SetValue(ValueName,Command,RegistryValueKind.String);
            else key.DeleteValue(ValueName,false);
        }
    }
}

internal sealed class Peek : Form {
    static readonly Color cBack=Color.FromArgb(27,33,44), cSurface=Color.FromArgb(43,52,67), cLine=Color.FromArgb(65,77,95), cMuted=Color.FromArgb(166,184,216),
        cAccent=Color.FromArgb(110,221,192), cPaper=Color.FromArgb(250,249,246), cInk=Color.FromArgb(24,24,28), cName=Color.FromArgb(120,124,134);
    const string Title="글꼴 미리보기", Fallback="あいうえお　アイウエオ　永";
    const string Turn="ー〜～「」『』（）［］｛｝〔〕【】〈〉《》＝－＜＞｜：；", Dots="、。，．", NoStart="、。，．」』）】〕〉》！？", Small="ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ";
    readonly string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FontPeek","settings.txt");
    readonly IntPtr dc=Gdi.CreateCompatibleDC(IntPtr.Zero);
    readonly List<Entry> all=new List<Entry>(); List<Entry> shown=new List<Entry>(); readonly List<string> lines=new List<string>();
    readonly TextBox box=new TextBox(); readonly Board board=new Board(); readonly TrackBar slider=new TrackBar(); readonly Label sizeLabel=new Label();
    readonly RadioButton across=new RadioButton(), down=new RadioButton(); readonly CheckBox japanese=new CheckBox(), pin=new CheckBox(); readonly TextBox search=new TextBox();
    readonly Timer delay=new Timer(); readonly ToolTip tip=new ToolTip();
    readonly ContextMenuStrip settingsMenu=new ContextMenuStrip();
    readonly Font titleFont=new Font("Malgun Gothic",10f,FontStyle.Bold), nameFont=new Font("Yu Gothic UI",8.5f);
    readonly float k; Entry hover; string countText="";

    int S(float v) { return (int)Math.Round(v*k); }

    internal Peek() {
        using(var g=Graphics.FromHwnd(IntPtr.Zero)) k=g.DpiX/96f;
        Text=Title; FormBorderStyle=FormBorderStyle.None; MaximizeBox=false; StartPosition=FormStartPosition.Manual; AutoScaleMode=AutoScaleMode.None;
        Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        BackColor=cBack; ForeColor=Color.White; Font=new Font("Malgun Gothic",9f); DoubleBuffered=true;
        MinimumSize=new Size(S(380),S(320)); ClientSize=new Size(S(460),S(640));
        var area=Screen.PrimaryScreen.WorkingArea; Location=new Point(area.Right-Width-S(28),area.Top+S(60));
        int p=S(8), w=ClientSize.Width-p*2;
        Flat("×",ClientSize.Width-p-S(30),p).Click+=delegate { Close(); };
        Flat("–",ClientSize.Width-p-S(60),p).Click+=delegate { WindowState=FormWindowState.Minimized; };
        Toggle(pin,"고정",ClientSize.Width-p-S(106),p,S(42)); pin.Appearance=Appearance.Button; pin.Anchor=AnchorStyles.Top|AnchorStyles.Right; tip.SetToolTip(pin,"항상 위에 표시");
        int sx=p+S(12)+TextRenderer.MeasureText(Title,titleFont).Width+TextRenderer.MeasureText("일본어 000개",Font).Width; // 제목·개수 자리 다음
        search.BorderStyle=BorderStyle.FixedSingle; search.BackColor=cSurface; search.ForeColor=Color.White; search.Font=new Font("Yu Gothic UI",9.5f);
        search.Bounds=new Rectangle(sx,p+(S(28)-search.Height)/2,pin.Left-S(6)-sx,search.Height); search.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        search.HandleCreated+=delegate { Gdi.SendMessage(search.Handle,0x1501,(IntPtr)1,"글꼴 검색"); }; Controls.Add(search); // EM_SETCUEBANNER
        box.Multiline=true; box.AcceptsReturn=true; box.BorderStyle=BorderStyle.FixedSingle; box.BackColor=cSurface; box.ForeColor=Color.White;
        box.Font=new Font("Yu Gothic UI",11f); box.Bounds=new Rectangle(p,p+S(34),w,S(58)); box.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        box.Text="「ねえ、聞こえる？」\r\nあの夏のメロディー。"; Controls.Add(box);
        int y=box.Bottom+S(8);
        Toggle(across,"가로",p,y,S(48)); Toggle(down,"세로",p+S(48),y,S(48)); Toggle(japanese,"일본어만",p+S(104),y,S(76));
        across.Appearance=down.Appearance=japanese.Appearance=Appearance.Button; across.Checked=true; japanese.Checked=true;
        var options=Flat("⚙",ClientSize.Width-p-S(30),y); options.Font=new Font("Segoe UI Symbol",11f); tip.SetToolTip(options,"설정");
        var startup=new ToolStripMenuItem("Windows 시작 시 자동 실행"); settingsMenu.Items.Add(startup);
        options.Click+=delegate {
            try { startup.Checked=Startup.Enabled; settingsMenu.Show(options,new Point(options.Width,options.Height),ToolStripDropDownDirection.BelowLeft); }
            catch(Exception ex) { MessageBox.Show(this,"시작프로그램 설정을 읽을 수 없습니다.\n"+ex.Message,"FontPeek",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        };
        startup.Click+=delegate {
            try { Startup.SetEnabled(!startup.Checked); startup.Checked=Startup.Enabled; }
            catch(Exception ex) { MessageBox.Show(this,"시작프로그램 설정을 바꿀 수 없습니다.\n"+ex.Message,"FontPeek",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        };
        sizeLabel.Bounds=new Rectangle(ClientSize.Width-p-S(72),y,S(36),S(28)); sizeLabel.TextAlign=ContentAlignment.MiddleRight; sizeLabel.ForeColor=cMuted;
        sizeLabel.Anchor=AnchorStyles.Top|AnchorStyles.Right; Controls.Add(sizeLabel);
        slider.Minimum=12; slider.Maximum=120; slider.Value=32; slider.TickStyle=TickStyle.None; slider.AutoSize=false; slider.TabStop=false; slider.BackColor=cBack;
        slider.Bounds=new Rectangle(p+S(188),y,sizeLabel.Left-S(4)-(p+S(188)),S(28)); slider.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; Controls.Add(slider);
        board.BackColor=cBack; board.Bounds=new Rectangle(p,y+S(36),w,ClientSize.Height-(y+S(36))-p);
        board.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; Controls.Add(board);
        LoadFonts(); LoadSettings(); KeepVisible();
        sizeLabel.Text=slider.Value.ToString(); Restyle(across); Restyle(down); Restyle(japanese); Restyle(pin); TopMost=pin.Checked;
        delay.Interval=150; delay.Tick+=delegate { delay.Stop(); Relayout(); };
        box.TextChanged+=delegate { Schedule(); };
        slider.ValueChanged+=delegate { sizeLabel.Text=slider.Value.ToString(); Schedule(); };
        across.CheckedChanged+=delegate { Restyle(across); Restyle(down); Relayout(); };
        japanese.CheckedChanged+=delegate { Restyle(japanese); board.AutoScrollPosition=Point.Empty; Relayout(); };
        board.Resize+=delegate { Schedule(); };
        search.TextChanged+=delegate { board.AutoScrollPosition=Point.Empty; Relayout(); };
        search.KeyPress+=delegate(object s,KeyPressEventArgs e) { if(e.KeyChar==(char)27) search.Text=""; if(e.KeyChar=='\r' || e.KeyChar==(char)27) e.Handled=true; }; // Esc=지우기, 띵 소리 막기
        pin.CheckedChanged+=delegate { Restyle(pin); TopMost=pin.Checked; };
        board.Paint+=PaintBoard; board.MouseMove+=Hover; board.MouseClick+=Copy;
        FormClosing+=delegate { SaveSettings(); };
        FormClosed+=delegate { Gdi.DeleteDC(dc); delay.Dispose(); tip.Dispose(); settingsMenu.Dispose(); };
        Shown+=delegate { Relayout(); box.Focus(); box.SelectionStart=box.TextLength; };
    }

    protected override CreateParams CreateParams { get { var cp=base.CreateParams; cp.Style|=0x20000; return cp; } } // WS_MINIMIZEBOX: 작업표시줄 클릭으로 최소화
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); int round=2; Gdi.DwmSetWindowAttribute(Handle,33,ref round,4); } // Win11 둥근 모서리
    protected override void WndProc(ref Message m) {
        if(m.Msg==0xA3) return; // 제목 영역 더블클릭 최대화 막기
        base.WndProc(ref m);
        if(m.Msg!=0x84 || (int)m.Result!=1) return; // WM_NCHITTEST: 가장자리=크기 조절, 위쪽=이동
        long lp=m.LParam.ToInt64(); var c=PointToClient(new Point((short)(lp&0xffff),(short)((lp>>16)&0xffff)));
        int g=S(6); bool l=c.X<g, r=c.X>=ClientSize.Width-g, t=c.Y<g, b=c.Y>=ClientSize.Height-g;
        m.Result=(IntPtr)(t&&l ? 13 : t&&r ? 14 : b&&l ? 16 : b&&r ? 17 : l ? 10 : r ? 11 : t ? 12 : b ? 15 : c.Y<box.Top ? 2 : 1);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); int p=S(8); var size=TextRenderer.MeasureText(Title,titleFont);
        TextRenderer.DrawText(e.Graphics,Title,titleFont,new Rectangle(p+S(2),p,size.Width,S(28)),Color.White,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);
        TextRenderer.DrawText(e.Graphics,countText,Font,new Rectangle(p+S(6)+size.Width,p,search.Left-(p+S(6)+size.Width),S(28)),cMuted,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);
    }

    Button Flat(string text,int x,int y) {
        var b=new Button(); b.Text=text; b.Bounds=new Rectangle(x,y,S(30),S(28)); b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderSize=0;
        b.FlatAppearance.MouseOverBackColor=cSurface; b.FlatAppearance.MouseDownBackColor=cLine; b.ForeColor=cMuted; b.TabStop=false; b.Cursor=Cursors.Hand;
        b.Anchor=AnchorStyles.Top|AnchorStyles.Right; Controls.Add(b); return b;
    }
    void Toggle(ButtonBase c,string text,int x,int y,int w) {
        c.Text=text; c.Bounds=new Rectangle(x,y,w,S(28)); c.FlatStyle=FlatStyle.Flat; c.TextAlign=ContentAlignment.MiddleCenter; c.BackColor=cSurface;
        c.TabStop=false; c.Cursor=Cursors.Hand; c.FlatAppearance.BorderColor=cLine; c.FlatAppearance.CheckedBackColor=cAccent; Controls.Add(c);
    }
    void Restyle(ButtonBase c) {
        bool on=c is CheckBox ? ((CheckBox)c).Checked : ((RadioButton)c).Checked;
        c.ForeColor=on ? cBack : cMuted; c.FlatAppearance.MouseOverBackColor=on ? cAccent : cLine;
    }
    void Schedule() { delay.Stop(); delay.Start(); }

    void LoadFonts() {
        var charsets=new Dictionary<string,HashSet<int>>(); var vertical=new HashSet<string>();
        var query=new Gdi.LOGFONT(); query.charSet=1; query.face="";
        Gdi.EnumProc proc=delegate(IntPtr lf,IntPtr tm,uint type,IntPtr param) {
            if((type&1)!=0) return 1; // 비트맵 글꼴 제외
            var l=(Gdi.LOGFONT)Marshal.PtrToStructure(lf,typeof(Gdi.LOGFONT));
            if(l.face.StartsWith("@")) { vertical.Add(l.face); return 1; }
            HashSet<int> set; if(!charsets.TryGetValue(l.face,out set)) charsets[l.face]=set=new HashSet<int>();
            set.Add(l.charSet); return 1;
        };
        Gdi.EnumFontFamiliesExW(dc,query,proc,IntPtr.Zero,0);
        foreach(var pair in charsets) {
            var cs=pair.Value; var glyphs=new ushort[3];
            IntPtr f=Gdi.Font(pair.Key,20,false,false), old=Gdi.SelectObject(dc,f);
            Gdi.GetGlyphIndicesW(dc,"あア気",3,glyphs,1);
            Gdi.SelectObject(dc,old); Gdi.DeleteObject(f);
            bool kana=glyphs[0]!=0xffff && glyphs[1]!=0xffff, kanji=glyphs[2]!=0xffff;
            var e=new Entry(); e.Name=pair.Key; e.HasVertical=vertical.Contains("@"+pair.Key);
            e.Japanese=cs.Contains(128) ? (!cs.Contains(129) || kanji) : kana && !cs.Contains(129) && !cs.Contains(134) && !cs.Contains(136);
            all.Add(e);
        }
        all.Sort(delegate(Entry a,Entry b) { return string.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase); });
    }

    // 세로판(@)이 제대로 서는지: ー가 세로 막대로, 。가 오른쪽 위로 가야 통과.
    bool NativeVertical(Entry e) {
        if(e.VerticalCheck==0) {
            Rectangle bar=Ink(e.Name,"ー"), dot=Ink(e.Name,"。");
            e.VerticalCheck=e.HasVertical && bar.Height>bar.Width*2 && !dot.IsEmpty && dot.Left+dot.Width/2>32 && dot.Top+dot.Height/2<32 ? 1 : 2;
        }
        return e.VerticalCheck==1;
    }
    static Rectangle Ink(string face,string ch) {
        using(var bmp=new Bitmap(64,64,PixelFormat.Format32bppRgb)) {
            using(var g=Graphics.FromImage(bmp)) {
                g.Clear(Color.White); IntPtr hdc=g.GetHdc();
                IntPtr f=Gdi.Font(face,32,true,false), old=Gdi.SelectObject(hdc,f);
                Gdi.SetTextAlign(hdc,24); Gdi.SetBkMode(hdc,1); Gdi.SetTextColor(hdc,0);
                Gdi.TextOutW(hdc,20,16,ch,ch.Length); // 글자 틀 ≈ (16,16)-(48,48), 가운데 (32,32)
                Gdi.SelectObject(hdc,old); Gdi.DeleteObject(f); g.ReleaseHdc(hdc);
            }
            var data=bmp.LockBits(new Rectangle(0,0,64,64),ImageLockMode.ReadOnly,PixelFormat.Format32bppRgb);
            var pixels=new int[64*64]; Marshal.Copy(data.Scan0,pixels,0,pixels.Length); bmp.UnlockBits(data);
            int l=64,t=64,r=-1,b=-1;
            for(int i=0;i<pixels.Length;i++) if((pixels[i]&0xff)<128) { int x=i%64, y=i/64; l=Math.Min(l,x); r=Math.Max(r,x); t=Math.Min(t,y); b=Math.Max(b,y); }
            return r<0 ? Rectangle.Empty : Rectangle.FromLTRB(l,t,r+1,b+1);
        }
    }

    void Relayout() {
        string q=search.Text.Trim();
        shown=all.FindAll(delegate(Entry e) { return (!japanese.Checked || e.Japanese) && (q.Length==0 || e.Name.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0); });
        bool tate=down.Checked; int px=S(slider.Value), pitch=px*3/2, pad=S(12), nameH=S(22), gap=S(8);
        int avail=board.Width-SystemInformation.VerticalScrollBarWidth-1;
        int limit=tate ? Math.Max(px*3,board.Height-nameH-pad*2-gap*2-px/2) : avail-pad*2-px/2; // 어림값이 모자랄 때를 위한 반 글자 여유
        int extent=Wrap((box.Text.Trim().Length==0 ? Fallback : box.Text.Replace("\r","")).Split('\n'),px,limit), n=lines.Count;
        int w=tate ? Math.Max(S(132),(n-1)*pitch+px+pad*2) : avail, h=tate ? S(12)+nameH+extent+px/2+pad : S(12)+nameH+(n-1)*pitch+px*5/4+pad/2;
        int perRow=Math.Max(1,(avail+gap)/(w+gap));
        for(int i=0;i<shown.Count;i++) shown[i].Box=new Rectangle(i%perRow*(w+gap),i/perRow*(h+gap),w,h);
        board.AutoScrollMinSize=new Size(0,(shown.Count+perRow-1)/perRow*(h+gap));
        countText=(japanese.Checked ? "일본어 " : "전체 ")+shown.Count+"개";
        Invalidate(new Rectangle(0,0,ClientSize.Width,box.Top)); board.Invalidate();
    }

    // 모든 글꼴에 같은 줄바꿈을 쓴다(전각=1em, 반각=0.6em으로 어림). 글꼴마다 재면 글꼴당 약 1.5ms씩 들어 입력이 버벅인다.
    // 가장 긴 줄의 길이(세로쓰기면 열 높이)를 돌려준다.
    int Wrap(string[] paras,int px,int limit) {
        lines.Clear(); int widest=px;
        foreach(var s in paras) {
            int start=0, run=0;
            for(int i=0;i<s.Length;i++) {
                char c=s[i]; int a=char.IsLowSurrogate(c) ? 0 : c<0x100 || (c>=0xFF61 && c<=0xFF9F) ? px*3/5 : px; // …―“” 등은 일본어 글꼴에서 전각
                // 줄머리 금칙 문자(、。」！？ 등)는 줄을 바꾸지 않고 앞줄 끝에 매단다
                if(run+a>limit && i>start && !char.IsLowSurrogate(c) && NoStart.IndexOf(c)<0) { lines.Add(s.Substring(start,i-start)); widest=Math.Max(widest,run); start=i; run=0; }
                run+=a;
            }
            lines.Add(s.Substring(start)); widest=Math.Max(widest,run);
        }
        return widest;
    }
    static bool Turned(char c) { return (c<0x2460 && c!='※' && c!='‼' && c!='⁉') || Turn.IndexOf(c)>=0; }

    void PaintBoard(object sender,PaintEventArgs e) {
        var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(cBack);
        int oy=board.AutoScrollPosition.Y, px=S(slider.Value), pitch=px*3/2, pad=S(12), nameH=S(22); bool tate=down.Checked;
        var visible=new List<Entry>();
        using(var paper=new SolidBrush(cPaper))
            foreach(var en in shown) {
                var r=en.Box; r.Offset(0,oy); if(!r.IntersectsWith(e.ClipRectangle)) continue; visible.Add(en);
                if(tate) NativeVertical(en); // 보이는 카드만 검사(결과는 저장됨)
                using(var path=Rounded(r,S(8))) g.FillPath(paper,path);
                TextRenderer.DrawText(g,en.Name,nameFont,new Rectangle(r.Left+pad,r.Top+S(6),r.Width-pad*2,nameH),cName,
                    TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            }
        IntPtr hdc=g.GetHdc();
        try {
            Gdi.SetBkMode(hdc,1); Gdi.SetTextColor(hdc,ColorTranslator.ToWin32(cInk)); Gdi.SetTextAlign(hdc,24); // TA_BASELINE
            foreach(var en in visible) {
                var r=en.Box; r.Offset(0,oy); int top=r.Top+S(12)+nameH, left=r.Left+pad, right=r.Right-pad;
                bool native=tate && en.VerticalCheck==1;
                int saved=Gdi.SaveDC(hdc); Gdi.IntersectClipRect(hdc,r.Left+S(3),r.Top+S(6)+nameH,r.Right-S(3),r.Bottom-S(3));
                IntPtr f=en.Font(px,native), old=Gdi.SelectObject(hdc,f);
                for(int i=0;i<lines.Count;i++) {
                    string t=lines[i]; if(t.Length==0) continue;
                    if(!tate) Gdi.TextOutW(hdc,left,top+i*pitch+px*95/100,t,t.Length);
                    else if(native) Gdi.TextOutW(hdc,right-i*pitch-px*88/100,top,t,t.Length);
                    else Upright(hdc,en.Name,t,right-i*pitch,top,px,f);
                }
                Gdi.SelectObject(hdc,old); Gdi.RestoreDC(hdc,saved);
            }
        } finally { g.ReleaseHdc(hdc); }
    }
    // 세로판을 못 쓰는 글꼴: 한 글자씩 세워 그린다. x는 이 줄(열)의 오른쪽 끝, y는 위쪽 끝.
    void Upright(IntPtr hdc,string face,string t,int x,int y,int px,IntPtr font) {
        IntPtr turned=Gdi.Font(face,px,false,true);
        for(int i=0;i<t.Length;i++) {
            char c=t[i]; if(char.IsLowSurrogate(c)) continue;
            string u=char.IsHighSurrogate(c) && i+1<t.Length ? t.Substring(i,2) : c.ToString();
            Gdi.SIZE z; Gdi.GetTextExtentPoint32W(hdc,u,u.Length,out z);
            if(Turned(c)) { Gdi.SelectObject(hdc,turned); Gdi.TextOutW(hdc,x-px*88/100,y,u,u.Length); Gdi.SelectObject(hdc,font); y+=z.cx; continue; }
            int dx=0,dy=0;
            if(Dots.IndexOf(c)>=0) { dx=px/2; dy=-px/2; } else if(Small.IndexOf(c)>=0) { dx=px/8; dy=-px/8; }
            Gdi.TextOutW(hdc,x-px/2-z.cx/2+dx,y+px*88/100+dy,u,u.Length); y+=px;
        }
        Gdi.DeleteObject(turned);
    }
    void Hover(object sender,MouseEventArgs e) {
        int oy=board.AutoScrollPosition.Y; Entry hit=null;
        foreach(var en in shown) if(en.Box.Contains(e.X,e.Y-oy)) { hit=en; break; }
        if(hit!=hover) { hover=hit; tip.SetToolTip(board,hit==null ? "" : hit.Name+"\n클릭하면 이름 복사"); board.Cursor=hit==null ? Cursors.Default : Cursors.Hand; }
    }
    void Copy(object sender,MouseEventArgs e) {
        if(e.Button!=MouseButtons.Left || hover==null) return;
        try { Clipboard.SetText(hover.Name); tip.Show("복사됨 · "+hover.Name,board,e.X+S(12),e.Y+S(16),1500); } catch {}
    }
    static GraphicsPath Rounded(Rectangle r,int radius) {
        var p=new GraphicsPath(); int d=radius*2;
        p.AddArc(r.Left,r.Top,d,d,180,90); p.AddArc(r.Right-d,r.Top,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.Left,r.Bottom-d,d,d,90,90);
        p.CloseFigure(); return p;
    }

    void LoadSettings() {
        try {
            var L=File.ReadAllLines(settings,Encoding.UTF8); var b=L[0].Split(',');
            Bounds=new Rectangle(int.Parse(b[0]),int.Parse(b[1]),int.Parse(b[2]),int.Parse(b[3])); pin.Checked=b.Length>4 && b[4]=="1";
            slider.Value=Math.Max(slider.Minimum,Math.Min(slider.Maximum,int.Parse(L[1])));
            down.Checked=L[2]=="1"; across.Checked=!down.Checked; japanese.Checked=L[3]!="0";
            box.Text=string.Join("\r\n",L,4,L.Length-4);
        } catch {}
    }
    void SaveSettings() {
        try {
            var r=WindowState==FormWindowState.Normal ? Bounds : RestoreBounds;
            Directory.CreateDirectory(Path.GetDirectoryName(settings));
            File.WriteAllText(settings,r.X+","+r.Y+","+r.Width+","+r.Height+","+(pin.Checked ? 1 : 0)+"\r\n"+slider.Value+"\r\n"+(down.Checked ? 1 : 0)+"\r\n"+(japanese.Checked ? 1 : 0)+"\r\n"+box.Text,Encoding.UTF8);
        } catch {}
    }
    void KeepVisible() {
        var a=Screen.FromRectangle(Bounds).WorkingArea;
        Location=new Point(Math.Max(a.Left,Math.Min(Left,a.Right-Width)),Math.Max(a.Top,Math.Min(Top,a.Bottom-Height)));
    }
}
