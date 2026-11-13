using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Windows.Forms;

namespace GTBook {
    public sealed class LivePreview : Control {
        readonly Timer animation=new Timer();
        public Func<Request> Selection;
        public Func<string> Caption;
        public Func<string> Badge;
        public string Target="keyboard";
        readonly Stopwatch clock=new Stopwatch();
        public bool Animate {set{if(value){animation.Interval=Target=="keyboard"?67:33;clock.Start();animation.Start();}else{clock.Stop();animation.Stop();}}}
        double phase {get{return clock.Elapsed.TotalSeconds;}}
        public LivePreview(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);DoubleBuffered=true;BackColor=Theme.White;Height=170;AccessibleName="Graphical preview, not applied";animation.Interval=33;animation.Tick+=(s,e)=>{var form=FindForm();if(form!=null&&form.Visible&&form.WindowState!=FormWindowState.Minimized){if(Target=="rear"&&Selection!=null&&IsOff(Selection()))return;Invalidate();}};}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);Animate=Visible&&FindForm()!=null&&FindForm().Visible;}
        protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
        static AudioMeter liveAudioMeter;
        static DateTime nextAudioMeterAttempt=DateTime.MinValue;
        static double cachedLivePeak=0;
        static DateTime cachedLivePeakAt=DateTime.MinValue;

        public static double GetLiveAudioPeak(){
            if((DateTime.UtcNow-cachedLivePeakAt).TotalMilliseconds<30)return cachedLivePeak;
            try{
                if(liveAudioMeter==null&&DateTime.UtcNow>=nextAudioMeterAttempt){
                    try{liveAudioMeter=new AudioMeter();}
                    catch{nextAudioMeterAttempt=DateTime.UtcNow.AddSeconds(5);}
                }
                if(liveAudioMeter!=null){
                    cachedLivePeak=liveAudioMeter.Peak();
                    cachedLivePeakAt=DateTime.UtcNow;
                    return cachedLivePeak;
                }
            }catch{
                try{if(liveAudioMeter!=null)liveAudioMeter.Dispose();}catch{}
                liveAudioMeter=null;
                nextAudioMeterAttempt=DateTime.UtcNow.AddSeconds(5);
            }
            cachedLivePeak=0;
            cachedLivePeakAt=DateTime.UtcNow;
            return 0;
        }

        public static Color Hue(double h){h=(h%1+1)%1;double x=h*6;int i=(int)x;int f=(int)((x-i)*255);switch(i){case 0:return Color.FromArgb(255,f,0);case 1:return Color.FromArgb(255-f,255,0);case 2:return Color.FromArgb(0,255,f);case 3:return Color.FromArgb(0,255-f,255);case 4:return Color.FromArgb(f,0,255);default:return Color.FromArgb(255,0,255-f);}}
        public static Color KeyColour(Request r,int zone,double position,double time) {
            if(r.Brightness==0)return Color.FromArgb(42,44,47);
            if(ExtraEffects.Contains(r.Effect))return LightingColour.Dim(ExtraEffects.Frame(r,time)[zone],r.Brightness/100.0);
            string effect=r.Effect=="Four zones"?r.ZoneEffect:r.Effect;
            Color chosen=LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[r.Effect=="Four zones"?zone:0]));
            if(effect=="Rainbow")chosen=Hue(position+time/7);
            if(effect=="Clock")chosen=Hue(time/6);
            if(effect=="Flow")chosen=Hue(position-time/5);
            double intensity=r.Brightness/100.0;
            if(effect=="Breathing")intensity*=.15+.85*(Math.Sin(time*2)+1)/2;
            if(effect=="Wave")intensity*=.12+.88*(Math.Sin(position*9-time*4)+1)/2;
            return LightingColour.Dim(chosen,intensity);
        }
        public static bool IsOff(Request r){return r.Action=="rear-off"||r.Brightness==0;}
        public static Color RearColour(Request r,int i,int count,double phase){
            if(IsOff(r))return Color.FromArgb(176,181,173);
            Color c1=LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[0]));
            Color c2=(r.Colors.Length>1&&!string.IsNullOrEmpty(r.Colors[1]))?LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[1])):c1;
            double alpha=Math.Max(0.06,r.Brightness/100.0);
            double pos=count>1?(double)i/(count-1):0.5;

            if(r.Effect=="Static"){
                return LightingColour.Dim(c1,alpha);
            }
            if(r.Effect=="Breathing"){
                double freq=1.0+(r.Speed/9.0)*2.2;
                double wave=(Math.Sin(phase*freq*Math.PI)+1.0)/2.0;
                double pulse=0.12+0.88*Math.Pow(wave,1.8);
                return LightingColour.Dim(c1,alpha*pulse);
            }
            if(r.Effect=="Loop"){
                double wave=(Math.Sin((pos*2.5-phase*1.5)*Math.PI*2.0)+1.0)/2.0;
                int rc=(int)Math.Round(c1.R+(c2.R-c1.R)*wave);
                int gc=(int)Math.Round(c1.G+(c2.G-c1.G)*wave);
                int bc=(int)Math.Round(c1.B+(c2.B-c1.B)*wave);
                Color blended=Color.FromArgb(Math.Max(0,Math.Min(255,rc)),Math.Max(0,Math.Min(255,gc)),Math.Max(0,Math.Min(255,bc)));
                return LightingColour.Dim(blended,alpha);
            }
            if(r.Effect=="Cover"){
                double sweep=(Math.Sin(phase*1.6)+1.0)/2.0;
                double diff=(pos-sweep)*10.0;
                double factor=1.0/(1.0+Math.Exp(-diff));
                int rc=(int)Math.Round(c1.R+(c2.R-c1.R)*factor);
                int gc=(int)Math.Round(c1.G+(c2.G-c1.G)*factor);
                int bc=(int)Math.Round(c1.B+(c2.B-c1.B)*factor);
                Color blended=Color.FromArgb(Math.Max(0,Math.Min(255,rc)),Math.Max(0,Math.Min(255,gc)),Math.Max(0,Math.Min(255,bc)));
                return LightingColour.Dim(blended,alpha);
            }
            if(r.Effect=="Rhythm"){
                double dist=Math.Abs(pos-0.5)*2.0;
                double simulatedPeak=0.2+0.75*Math.Max(0,Math.Sin(phase*5.0)*0.5+Math.Sin(phase*11.0+i*0.05)*0.3+0.2);
                double audioPeak=GetLiveAudioPeak();
                double level=audioPeak>0.03?Math.Min(1.0,audioPeak*1.4):simulatedPeak;
                level=Math.Max(0.12,Math.Min(1.0,level));
                if(dist<=level){
                    double intensity=0.4+0.6*(1.0-(dist/level)*0.45);
                    double blendT=Math.Min(1.0,dist/Math.Max(0.01,level));
                    int rc=(int)Math.Round(c1.R+(c2.R-c1.R)*blendT);
                    int gc=(int)Math.Round(c1.G+(c2.G-c1.G)*blendT);
                    int bc=(int)Math.Round(c1.B+(c2.B-c1.B)*blendT);
                    Color blended=Color.FromArgb(Math.Max(0,Math.Min(255,rc)),Math.Max(0,Math.Min(255,gc)),Math.Max(0,Math.Min(255,bc)));
                    return LightingColour.Dim(blended,alpha*intensity);
                }else{
                    return LightingColour.Dim(c1,alpha*0.07);
                }
            }
            if(r.Effect=="Beat"){
                double beatTime=phase*2.2;
                double cycle=beatTime%1.0;
                double kick=Math.Exp(-cycle*4.8);
                double dist=Math.Abs(pos-0.5)*2.0;
                double ripple=Math.Max(0,1.0-Math.Abs(dist-cycle*1.25)*4.0);
                double pulse=kick*0.85+ripple*0.35+0.1;
                double audioPeak=GetLiveAudioPeak();
                if(audioPeak>0.03)pulse=Math.Max(pulse,audioPeak*1.3);
                pulse=Math.Max(0.08,Math.Min(1.0,pulse));
                return LightingColour.Dim(c1,alpha*pulse);
            }
            return LightingColour.Dim(c1,alpha);
        }
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Theme.White);
            if(Width>2&&Height>2){var bounds=new RectangleF(.5f,.5f,Width-1,Height-1);using(var path=Theme.Round(bounds,13*DeviceDpi/96f)){
                using(var fill=new LinearGradientBrush(bounds,Theme.White,Theme.Pearl,22f))g.FillPath(fill,path);
                using(var edge=Theme.OrangeBorder(bounds))using(var pen=new Pen(edge,DeviceDpi/96f))g.DrawPath(pen,path);
            }}
            Request r=Selection==null?new Request():Selection();if(r==null)return;
            float s=DeviceDpi/96f;float w=Width,h=Height;
            if(Target=="keyboard"){
                var shell=new RectangleF(12*s,15*s,w-24*s,h-49*s);
                using(var p=Theme.Round(shell,12*s)){
                    using(var brush=new LinearGradientBrush(shell,Color.FromArgb(42,45,56),Color.FromArgb(23,25,33),90f))g.FillPath(brush,p);
                    using(var edge=new Pen(Color.FromArgb(83,86,96),s))g.DrawPath(edge,p);
                }
                int columns=20;float gap=3*s,keyW=(shell.Width-20*s-gap*(columns-1))/columns,keyH=(shell.Height-20*s-gap*4)/5;
                Color[] extra=ExtraEffects.Contains(r.Effect)?ExtraEffects.Frame(r,phase):null;
                if(extra!=null&&r.Brightness>0)for(int zone=0;zone<4;zone++){
                    var wash=new RectangleF(shell.X+6*s+zone*(shell.Width-12*s)/4,shell.Y+5*s,(shell.Width-12*s)/4,shell.Height-10*s);
                    Color glow=LightingColour.Dim(extra[zone],r.Brightness/100.0);
                    using(var brush=new LinearGradientBrush(wash,Color.FromArgb(68,glow),Color.FromArgb(12,glow),90f))g.FillRectangle(brush,wash);
                }
                for(int y=0;y<5;y++)for(int x=0;x<columns;x++){
                    var key=new RectangleF(shell.X+10*s+x*(keyW+gap),shell.Y+10*s+y*(keyH+gap),keyW,keyH);
                    Color c=extra==null?KeyColour(r,Math.Min(3,x/5),x/19.0,phase):r.Brightness==0?Color.FromArgb(42,44,47):LightingColour.Dim(extra[Math.Min(3,x/5)],r.Brightness/100.0);
                    using(var p=Theme.Round(key,3*s))using(var b=new SolidBrush(c))g.FillPath(b,p);
                }
                Draw(g,r.Effect+" · "+r.Brightness+"% · preview only",new RectangleF(10*s,h-29*s,w-20*s,24*s),9);
            } else if(Target=="rear"){
                bool off=IsOff(r);
                var shell=new RectangleF(14*s, 38*s, w-28*s, 44*s);
                if (!off) {
                    Color primeColor = LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[0]));
                    float glowAlpha = Math.Max(12f, Math.Min(70f, (r.Brightness / 100f) * 60f));
                    var topAura = new RectangleF(shell.X + 8*s, shell.Y - 14*s, shell.Width - 16*s, 15*s);
                    using(var topBrush = new LinearGradientBrush(topAura, Color.FromArgb(0, primeColor), Color.FromArgb((int)glowAlpha, primeColor), 90f)) {
                        g.FillRectangle(topBrush, topAura);
                    }
                    var btmAura = new RectangleF(shell.X + 8*s, shell.Bottom - 1*s, shell.Width - 16*s, 15*s);
                    using(var btmBrush = new LinearGradientBrush(btmAura, Color.FromArgb((int)glowAlpha, primeColor), Color.FromArgb(0, primeColor), 90f)) {
                        g.FillRectangle(btmBrush, btmAura);
                    }
                }
                using(var p=Theme.Round(shell, 10*s)){
                    Color shellTop = Theme.Tone(Color.FromArgb(28, 32, 42), Color.FromArgb(14, 17, 24));
                    Color shellBtm = Theme.Tone(Color.FromArgb(16, 18, 24), Color.FromArgb(8, 10, 14));
                    using(var b=new LinearGradientBrush(shell, shellTop, shellBtm, 90f)) g.FillPath(b, p);
                    Color rimColor = Theme.Tone(Color.FromArgb(64, 72, 88), Color.FromArgb(40, 46, 60));
                    using(var pen=new Pen(rimColor, 1.2f*s)) g.DrawPath(pen, p);
                }
                int count=40;
                float padInside = 10*s;
                float cell = (shell.Width - padInside * 2) / count;
                float segW = Math.Max(2f, cell - 1.5f*s);
                float segH = shell.Height - 16*s;
                float segY = shell.Y + 8*s;
                for(int i=0; i<count; i++){
                    Color c = off ? Color.FromArgb(70, 78, 92) : RearColour(r, i, count, phase);
                    float segX = shell.X + padInside + i * cell;
                    var segRect = new RectangleF(segX, segY, segW, segH);
                    if (!off) {
                        using(var haloBrush = new SolidBrush(Color.FromArgb(55, c))) {
                            g.FillRectangle(haloBrush, segX - 0.5f*s, segY - 2*s, segW + 1f*s, segH + 4*s);
                        }
                    }
                    using(var segPath = Theme.Round(segRect, 2.5f*s)) {
                        using(var segBrush = new SolidBrush(c)) g.FillPath(segBrush, segPath);
                    }
                    if (!off) {
                        Color coreC = Color.FromArgb(220, Math.Min(255, c.R + 65), Math.Min(255, c.G + 65), Math.Min(255, c.B + 65));
                        float coreH = Math.Max(2f, segH * 0.26f);
                        float coreY = segY + (segH - coreH) / 2f;
                        using(var coreBrush = new SolidBrush(coreC)) {
                            g.FillRectangle(coreBrush, segX + 0.5f*s, coreY, Math.Max(1f, segW - 1f*s), coreH);
                        }
                    }
                }
                string caption = Caption!=null ? Caption() : off ? "OFF · selected preview" : r.Effect+" · "+r.Brightness+"% · simulated preview";
                string badge = Badge!=null ? Badge() : caption.StartsWith("NOT CONFIRMED") ? "UNKNOWN" : off ? "OFF" : caption.Contains("preview only") || caption.StartsWith("PREVIEW") ? "PREVIEW" : "ON";
                float pillW = 100*s, pillH = 24*s;
                var pillRect = new RectangleF(w - pillW - 14*s, 9*s, pillW, pillH);
                Color pillFill = Theme.Tone(Color.FromArgb(248, 249, 252), Color.FromArgb(24, 28, 38));
                Color pillBorder = Theme.Tone(Color.FromArgb(226, 230, 238), Color.FromArgb(48, 56, 74));
                using(var pp = Theme.Round(pillRect, pillH / 2f)) {
                    using(var pb = new SolidBrush(pillFill)) g.FillPath(pb, pp);
                    using(var ppen = new Pen(pillBorder, 1f*s)) g.DrawPath(ppen, pp);
                }
                Color badgeDot = badge == "ON" ? Color.FromArgb(35, 169, 108) :
                                 badge == "PREVIEW" ? Theme.OrangeEnd :
                                 badge == "MUSIC" ? Color.FromArgb(0, 195, 255) :
                                 badge == "STARTING" ? Theme.OrangeEnd :
                                 Color.FromArgb(145, 150, 160);
                float dotD = 6.5f*s;
                float dotX = pillRect.X + 10*s;
                float dotY = pillRect.Y + (pillH - dotD) / 2f;
                using(var dotBrush = new SolidBrush(badgeDot)) g.FillEllipse(dotBrush, dotX, dotY, dotD, dotD);
                using(var bf = new Font("Segoe UI", 8.2f, FontStyle.Bold)) {
                    var textRect = new Rectangle((int)(dotX + dotD + 6*s), (int)pillRect.Y, (int)(pillW - dotD - 18*s), (int)pillH);
                    TextRenderer.DrawText(g, badge, bf, textRect, Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                string bottomText = badge=="PREVIEW" ? "Previewing look · not sent to device until Apply" :
                                    badge=="MUSIC" ? "Music sync active · reacting to audio playback" :
                                    off ? (badge=="OFF" ? "Rear light off · customize a look and apply" : "Device state not confirmed") :
                                    r.Effect + " · " + r.Brightness + "% brightness · " + (RearLighting.UsesSecondColour(r.Effect) ? "2 colours" : "live effect");
                Draw(g, bottomText, new RectangleF(14*s, h - 28*s, w - 28*s, 22*s), 9);
            } else DrawBehavior(g,r,s);
        }
        void DrawBehavior(Graphics g,Request r,float s){
            if(r.Action=="mode"||r.Action=="fan"||r.Action=="auto"){
                bool full=r.Action=="fan"&&r.Enabled;string name=r.Action=="mode"?new[]{"Quiet","Balanced","Performance","Turbo"}[r.Mode]:full?"Maximum fans":"Automatic cooling";
                Draw(g,name,new RectangleF(16*s,10*s,Width-32*s,30*s),13);
                string[] labels={"Performance","Fan noise","Battery saving"};float[][] levels={new[]{.35f,.25f,.9f},new[]{.65f,.55f,.6f},new[]{.95f,.85f,.25f},new[]{1f,1f,0f}};
                float[] values=r.Action=="mode"?levels[r.Mode]:full?new[]{.8f,1f,.25f}:new[]{.6f,.5f,.65f};
                for(int i=0;i<3;i++){
                    float y=(52+i*31)*s;Draw(g,labels[i],new RectangleF(16*s,y,125*s,24*s),9);
                    var bar=new RectangleF(145*s,y+5*s,Math.Max(20,Width-170*s),10*s);
                    using(var b=new SolidBrush(Theme.Pearl))g.FillRectangle(b,bar);
                    bar.Width*=values[i];using(var b=Theme.OrangeBorder(bar,true))g.FillRectangle(b,bar);
                }
                Draw(g,"Relative tendencies, not measured RPM or power limits",new RectangleF(16*s,Height-28*s,Width-32*s,24*s),8);
            }else if(r.Action=="gpu"){
                string target=GpuModes.Names[r.Mode];
                float cardW=250*s,cardH=74*s,gap=18*s;
                float startX=(Width-(2*cardW+gap))/2f;
                for(int i=0;i<2;i++){
                    var chip=new RectangleF(startX+i*(cardW+gap),24*s,cardW,cardH);
                    bool active=r.Mode==1||r.Mode==0&&i==0||r.Mode==2&&i==1;
                    Color cardBg=active?Theme.Tone(Color.FromArgb(255,248,238),Color.FromArgb(28,34,48)):Theme.Tone(Color.FromArgb(246,246,250),Color.FromArgb(19,23,32));
                    Color cardBorder=active?Theme.OrangeEnd:Theme.Edge;
                    using(var p=Theme.Round(chip,12*s)){
                        using(var b=new SolidBrush(cardBg))g.FillPath(b,p);
                        using(var pen=new Pen(cardBorder,active?1.8f*s:1f*s))g.DrawPath(pen,p);
                    }
                    string title=i==0?"NVIDIA dGPU":"Intel iGPU";
                    string status=active?"ACTIVE":"OFF";
                    string desc=i==0?(active?"Dedicated GPU · High Performance":"Powered off · Battery saving"):(active?"Display Engine & Media":"Bypassed for direct dGPU");
                    using(var font=new Font("Segoe UI",11f,FontStyle.Bold))TextRenderer.DrawText(g,title,font,new Rectangle((int)(chip.X+16*s),(int)(chip.Y+12*s),(int)(chip.Width-86*s),(int)(24*s)),active?Theme.Ink:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
                    var badgeRect=new RectangleF(chip.Right-68*s,chip.Y+12*s,54*s,22*s);
                    using(var bp=Theme.Round(badgeRect,6*s)){
                        using(var bb=new SolidBrush(active?Color.FromArgb(239,115,36):Theme.Tone(Color.FromArgb(218,220,226),Color.FromArgb(36,42,54))))g.FillPath(bb,bp);
                    }
                    using(var bf=new Font("Segoe UI",8.5f,FontStyle.Bold))TextRenderer.DrawText(g,status,bf,Rectangle.Round(badgeRect),active?Color.White:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                    using(var font=new Font("Segoe UI",9f))TextRenderer.DrawText(g,desc,font,new Rectangle((int)(chip.X+16*s),(int)(chip.Y+40*s),(int)(chip.Width-32*s),(int)(22*s)),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
                }
                string summary=r.Mode==0?"Direct dGPU mode: Display connects directly to NVIDIA GPU for maximum FPS. Restart required.":r.Mode==1?"Hybrid mode: Dynamic switching between Intel and NVIDIA GPUs. Restart required.":"iGPU mode: NVIDIA GPU stays completely off for maximum battery life. Restart required.";
                Draw(g,summary,new RectangleF(startX,114*s,Width-2*startX,48*s),9.5f);
            }else {
                string text=r.Action=="usb"?"USB power while laptop is off: "+(r.Enabled?"ON":"OFF"):r.Action=="startup"?"Start in the system tray at sign-in: "+(r.Enabled?"ON":"OFF"):r.Action=="windows-key"?"Windows key during gaming: "+(r.Enabled?"LOCKED":"NORMAL"):r.Action=="software-fn"?"F1 / F2 brightness + F3 keyboard lighting: "+(r.Enabled?"ON":"OFF"):"Ready to apply";
                var icon=new RectangleF(24*s,25*s,Width-48*s,70*s);
                using(var p=Theme.Round(icon,14*s))using(var b=new SolidBrush(r.Enabled?Color.FromArgb(219,230,215):Theme.Pearl))g.FillPath(b,p);
                Draw(g,text,icon,12);
                Draw(g,r.Action=="usb"?"This controls USB power, not the laptop battery charge limit.":"",new RectangleF(16*s,115*s,Width-32*s,46*s),9);
            }
        }
        void Draw(Graphics g,string text,RectangleF bounds,float size){using(var font=new Font("Segoe UI",size))TextRenderer.DrawText(g,text,font,Rectangle.Round(bounds),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);}
    }
    public static class ConfirmationPolicy {
        public static bool Required(string action){
            switch(action){
                case "mode":case "auto":case "fan":case "keyboard":case "keyboard-animation":
                case "rear":case "rear-off":case "rear-music":case "usb":case "windows-key":
                case "software-fn":case "screen-brightness":case "refresh":case "probe":return false;
                default:return true;
            }
        }
        public static string Headline(Request request){
            if(request.Action=="gpu")return "RESTART REQUIRED";
            if(request.Action=="startup")return "START WITH WINDOWS: "+(request.Enabled?"ON":"OFF");
            return request.Action=="keyboard"||request.Action=="keyboard-animation"?request.Effect:"Preview your change";
        }
    }
    public sealed class ApplyPreview : Form {
        public ApplyPreview(Request request,string current,bool restart=false){
            float s=1f;using(var g=Graphics.FromHwnd(IntPtr.Zero)){if(g.DpiX>0)s=g.DpiX/96f;}
            Func<int,int> S=v=>(int)Math.Round(v*s);
            Text=restart?"Restart to activate GPU mode":"Confirm your change";Font=new Font("Segoe UI",10);BackColor=Theme.Ivory;ForeColor=Theme.Ink;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
            HandleCreated+=(sender,e)=>Theme.ApplyChrome(this);
            string headline=ConfirmationPolicy.Headline(request);
            string message=request.Action=="gpu"?GpuModes.Names[request.Mode]+(restart?" request saved. Save your work before restarting.":" selected. The new mode activates after Windows restarts."):request.Action=="startup"?(request.Enabled?"Essentials will start in the tray when you sign in.":"Essentials will no longer start automatically when you sign in."):current;
            var title=new Label{Text=headline,Font=new Font("Segoe UI",request.Action=="startup"?20:24,FontStyle.Bold),ForeColor=Theme.OrangeInk,BackColor=Color.Transparent,UseMnemonic=false,Location=new Point(S(14),S(8)),Size=new Size(S(538),S(40))};
            var desc=new Label{Text=message,Location=new Point(S(16),S(50)),Size=new Size(S(534),S(58)),ForeColor=Theme.Ink,BackColor=Color.Transparent,Font=new Font("Segoe UI",10f),UseMnemonic=false};
            int titleH=Math.Max(S(40),TextRenderer.MeasureText(title.Text,title.Font,new Size(title.Width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix).Height+S(4));
            title.Height=titleH;desc.Top=title.Bottom+S(4);
            int descH=Math.Max(S(54),TextRenderer.MeasureText(desc.Text,desc.Font,new Size(desc.Width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix).Height+S(6));
            desc.Height=descH;
            var banner=new GradientSurface{Location=new Point(S(22),S(16)),Size=new Size(S(566),Math.Max(S(116),desc.Bottom+S(10)))};Controls.Add(banner);
            banner.Controls.Add(title);banner.Controls.Add(desc);
            var view=new LivePreview{Selection=()=>request,Target=request.Action=="keyboard"||request.Action=="keyboard-animation"?"keyboard":request.Action.StartsWith("rear")?"rear":"behavior",Location=new Point(S(22),banner.Bottom+S(8)),Size=new Size(S(566),S(186))};Controls.Add(view);
            int buttonY=view.Bottom+S(20);
            var apply=new RaisedButton{Text=restart?"Restart now":request.Action=="gpu"?"Save GPU request":"Apply change",Location=new Point(S(22),buttonY),Size=new Size(S(210),S(44)),DialogResult=DialogResult.OK,Accent=Theme.OrangeEnd,Primary=true};
            apply.Font=new Font("Segoe UI",10.5f,FontStyle.Bold);
            var cancel=new RaisedButton{Text=restart?"Later":"Keep current",Location=new Point(S(250),buttonY),Size=new Size(S(170),S(44)),DialogResult=DialogResult.Cancel};
            cancel.Font=new Font("Segoe UI",10.5f);
            Controls.Add(apply);Controls.Add(cancel);AcceptButton=restart?(IButtonControl)cancel:apply;CancelButton=cancel;
            ClientSize=new Size(S(610),Math.Max(S(410),apply.Bottom+S(20)));
            if(restart)Shown+=(sender,e)=>cancel.Focus();
        }
    }
}
