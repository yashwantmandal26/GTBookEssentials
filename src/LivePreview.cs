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
        public string Target="keyboard";
        readonly Stopwatch clock=new Stopwatch();
        public bool Animate {set{if(value){clock.Start();animation.Start();}else{clock.Stop();animation.Stop();}}}
        double phase {get{return clock.Elapsed.TotalSeconds;}}
        public LivePreview(){DoubleBuffered=true;BackColor=Theme.Ivory;Height=170;AccessibleName="Graphical preview, not applied";animation.Interval=100;animation.Tick+=(s,e)=>{var form=FindForm();if(form!=null&&form.Visible&&form.WindowState!=FormWindowState.Minimized){if(Target=="rear"&&Selection!=null&&IsOff(Selection()))return;Invalidate();}};}
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
            if(r.Effect=="Rhythm"||r.Effect=="Beat")alpha*=Math.Abs(i-count/2)<(Math.Sin(phase*4)+1)*count/4?1:.06;
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
                using(var p=Theme.Round(shell,12*s))using(var brush=new SolidBrush(Color.FromArgb(222,222,216)))g.FillPath(brush,p);
                int columns=20;float gap=3*s,keyW=(shell.Width-20*s-gap*(columns-1))/columns,keyH=(shell.Height-20*s-gap*4)/5;
                Color[] extra=ExtraEffects.Contains(r.Effect)?ExtraEffects.Frame(r,phase):null;
                for(int y=0;y<5;y++)for(int x=0;x<columns;x++){
                    var key=new RectangleF(shell.X+10*s+x*(keyW+gap),shell.Y+10*s+y*(keyH+gap),keyW,keyH);
                    Color c=extra==null?KeyColour(r,Math.Min(3,x/5),x/19.0,phase):r.Brightness==0?Color.FromArgb(42,44,47):LightingColour.Dim(extra[Math.Min(3,x/5)],r.Brightness/100.0);
                    using(var p=Theme.Round(key,3*s))using(var b=new SolidBrush(c))g.FillPath(b,p);
                }
                Draw(g,r.Effect+" · "+r.Brightness+"% · preview only",new RectangleF(10*s,h-29*s,w-20*s,24*s),9);
            } else if(Target=="rear"){
                bool off=IsOff(r);
                var shell=new RectangleF(12*s,23*s,w-24*s,h-60*s);
                using(var p=Theme.Round(shell,14*s))using(var b=new SolidBrush(off?Color.FromArgb(221,224,217):Color.FromArgb(56,58,59)))g.FillPath(b,p);
                int count=40;float cell=(shell.Width-22*s)/count;
                for(int i=0;i<count;i++){
                    Color c=RearColour(r,i,count,phase);
                    using(var b=new SolidBrush(c))g.FillRectangle(b,shell.X+11*s+i*cell,shell.Y+shell.Height*.35f,Math.Max(1,cell-2*s),shell.Height*.3f);
                }
                string caption=Caption!=null?Caption():off?"OFF · selected preview":r.Effect+" · "+r.Brightness+"% · simulated preview";
                string badge=caption.StartsWith("NOT CONFIRMED")?"UNKNOWN":off?"OFF":caption.Contains("preview only")||caption.StartsWith("PREVIEW")?"PREVIEW":"ON";
                var pill=new RectangleF(w-100*s,0,86*s,20*s);
                using(var path=Theme.Round(pill,8*s))using(var fill=new SolidBrush(off?Color.FromArgb(215,220,210):Theme.Pearl))g.FillPath(fill,path);
                Draw(g,badge,new RectangleF(pill.X+10*s,pill.Y,pill.Width-10*s,pill.Height),9);
                Draw(g,caption,new RectangleF(10*s,h-29*s,w-20*s,24*s),9);
            } else DrawBehavior(g,r,s);
        }
        void DrawBehavior(Graphics g,Request r,float s){
            if(r.Action=="mode"||r.Action=="fan"||r.Action=="auto"){
                bool full=r.Action=="fan"&&r.Enabled;string name=r.Action=="mode"?new[]{"Quiet","Balanced","Gaming"}[r.Mode]:full?"Maximum fans":"Automatic cooling";
                Draw(g,name,new RectangleF(16*s,10*s,Width-32*s,30*s),13);
                string[] labels={"Performance","Fan noise","Battery saving"};float[][] levels={new[]{.35f,.25f,.9f},new[]{.65f,.55f,.6f},new[]{.95f,.85f,.25f}};
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
                for(int i=0;i<2;i++){
                    var chip=new RectangleF((28+i*165)*s,30*s,140*s,70*s);
                    bool active=r.Mode==1||r.Mode==0&&i==0||r.Mode==2&&i==1;
                    using(var p=Theme.Round(chip,10*s))using(var b=new SolidBrush(active?Color.FromArgb(219,230,215):Theme.Pearl))g.FillPath(b,p);
                    Draw(g,i==0?"NVIDIA GPU":"Intel GPU",chip,11);
                }
                Draw(g,target+" · saved after Apply; restart required",new RectangleF(16*s,118*s,Width-32*s,42*s),10);
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
            Text=restart?"Restart to activate GPU mode":"Confirm your change";Font=new Font("Segoe UI",10);BackColor=Theme.Ivory;ForeColor=Theme.Ink;ClientSize=new Size(610,410);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;
            var banner=new GradientSurface{Location=new Point(22,16),Size=new Size(566,112)};Controls.Add(banner);
            var title=new Label{Text=ConfirmationPolicy.Headline(request),Font=new Font("Segoe UI",request.Action=="startup"?20:24,FontStyle.Bold),ForeColor=Theme.OrangeInk,BackColor=Color.Transparent,Location=new Point(14,8),Size=new Size(538,44)};banner.Controls.Add(title);
            string message=request.Action=="gpu"?GpuModes.Names[request.Mode]+(restart?" request saved. Save your work before restarting.":" selected. The new mode activates after Windows restarts."):request.Action=="startup"?(request.Enabled?"Essentials will start in the tray when you sign in.":"Essentials will no longer start automatically when you sign in."):current;
            banner.Controls.Add(new Label{Text=message,Location=new Point(16,59),Size=new Size(534,45),ForeColor=Theme.Ink,BackColor=Color.Transparent});
            var view=new LivePreview{Selection=()=>request,Target=request.Action=="keyboard"||request.Action=="keyboard-animation"?"keyboard":request.Action.StartsWith("rear")?"rear":"behavior",Location=new Point(22,144),Size=new Size(566,180)};Controls.Add(view);
            var apply=new RaisedButton{Text=restart?"Restart now":request.Action=="gpu"?"Save GPU request":"Apply change",Location=new Point(22,350),Size=new Size(210,42),DialogResult=DialogResult.OK,Accent=Theme.OrangeEnd};
            var cancel=new RaisedButton{Text=restart?"Later":"Keep current",Location=new Point(250,350),Size=new Size(170,42),DialogResult=DialogResult.Cancel};Controls.Add(apply);Controls.Add(cancel);AcceptButton=restart?(IButtonControl)cancel:apply;CancelButton=cancel;
            if(restart)Shown+=(s,e)=>cancel.Focus();
        }
    }
}