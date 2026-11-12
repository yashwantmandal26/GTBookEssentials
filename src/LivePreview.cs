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
        public bool Animate {set{if(value){animation.Interval=Target=="keyboard"?67:100;clock.Start();animation.Start();}else{clock.Stop();animation.Stop();}}}
        double phase {get{return clock.Elapsed.TotalSeconds;}}
        public LivePreview(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);DoubleBuffered=true;BackColor=Theme.White;Height=170;AccessibleName="Graphical preview, not applied";animation.Interval=100;animation.Tick+=(s,e)=>{var form=FindForm();if(form!=null&&form.Visible&&form.WindowState!=FormWindowState.Minimized){if(Target=="rear"&&Selection!=null&&IsOff(Selection()))return;Invalidate();}};}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);Animate=Visible&&FindForm()!=null&&FindForm().Visible;}
        protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
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
            Color c=LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[0]));double alpha=r.Brightness/100.0;
            if(r.Effect=="Breathing")alpha*=.15+.85*(Math.Sin(phase*(1+r.Speed/3.0))+1)/2;
            if(r.Effect=="Loop")c=LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[((i+(int)(phase*8))/8)%2]));
            if(r.Effect=="Cover")c=LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[i<count*(Math.Sin(phase)+1)/2?0:1]));
            if(r.Effect=="Rhythm")alpha*=Math.Abs(i-count/2)<(Math.Sin(phase*4)+1)*count/4?1:.06;
            if(r.Effect=="Beat")alpha*=.06+.94*Math.Pow((Math.Sin(phase*3)+1)/2,2);
            return LightingColour.Dim(c,alpha);
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
                var shell=new RectangleF(14*s,42*s,w-28*s,h-82*s);
                using(var p=Theme.Round(shell,12*s)){
                    using(var b=new LinearGradientBrush(shell,Color.FromArgb(47,53,65),Color.FromArgb(16,20,28),90f))g.FillPath(b,p);
                    using(var pen=new Pen(Theme.Edge,s))g.DrawPath(pen,p);
                }
                int count=40;float cell=(shell.Width-22*s)/count;
                for(int i=0;i<count;i++){
                    Color c=off?Color.FromArgb(74,82,94):RearColour(r,i,count,phase);
                    float x=shell.X+11*s+i*cell;
                    if(!off)using(var b=new SolidBrush(Color.FromArgb(42,c)))g.FillRectangle(b,x,shell.Y+shell.Height*.2f,Math.Max(1,cell),shell.Height*.6f);
                    using(var b=new SolidBrush(c))g.FillRectangle(b,x,shell.Y+shell.Height*.38f,Math.Max(1,cell-s),shell.Height*.24f);
                }
                string caption=Caption!=null?Caption():off?"OFF · selected preview":r.Effect+" · "+r.Brightness+"% · simulated preview";
                string badge=Badge!=null?Badge():caption.StartsWith("NOT CONFIRMED")?"UNKNOWN":off?"OFF":caption.Contains("preview only")||caption.StartsWith("PREVIEW")?"PREVIEW":"ON";
                var pill=new RectangleF(w-112*s,10*s,96*s,24*s);
                using(var path=Theme.Round(pill,8*s))using(var fill=new SolidBrush(Theme.Pearl))g.FillPath(fill,path);
                Draw(g,badge,new RectangleF(pill.X+10*s,pill.Y,pill.Width-10*s,pill.Height),9);
                Draw(g,badge=="PREVIEW"?"Selected look · not sent to the device":badge=="MUSIC"?"Music sync active · simulated movement":off?badge=="OFF"?"Bar off · preview a look before applying":"Device state not confirmed":r.Effect+" · "+r.Brightness+"% · simulated appearance",new RectangleF(12*s,h-29*s,w-24*s,24*s),9);
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
