using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text;
using System.Windows.Forms;

namespace GTBook {
    public enum FeedbackKind { Message,Mode,Brightness,Keyboard,Error,KeyboardBrightness }
    public sealed class FeedbackNotice {
        public FeedbackKind Kind;
        public string Title="",Detail="";
        public int Percent=-1,Mode=-1,RequestedMode=-1;
        public Color[] Zones;
        public bool IsPending;
        private string customControlLabel;
        public string ControlLabel{
            get{
                if(!string.IsNullOrEmpty(customControlLabel))return customControlLabel;
                return Kind==FeedbackKind.Brightness?"Screen brightness":Kind==FeedbackKind.KeyboardBrightness?"Keyboard brightness":"";
            }
            set{customControlLabel=value;}
        }
        public static FeedbackNotice Pending(Request request){
            if(request.Action=="mode")return new FeedbackNotice{Kind=FeedbackKind.Mode,Title=PerformanceModes.Names[request.Mode]+" mode",Detail=request.Mode==3?"Ultimate Performance · Full fans ON":"",Mode=-1,RequestedMode=request.Mode,IsPending=true};
            if(request.Action=="screen-brightness")return new FeedbackNotice{Kind=FeedbackKind.Brightness,Title="Screen brightness",Detail="",IsPending=true};
            if(request.Action=="rear-off"||request.Action=="rear"||request.Action=="rear-music"){
                var rn=new FeedbackNotice{Kind=FeedbackKind.KeyboardBrightness,Title=request.Action=="rear-off"||request.Brightness==0?"Off":request.Brightness+"%",Percent=request.Brightness,IsPending=true};
                rn.ControlLabel="Rear light bar";
                return rn;
            }
            if(request.Action=="auto"||request.Action=="fan"||request.Action=="usb")return new FeedbackNotice{Title=request.Action=="usb"?"USB charging "+(request.Enabled?"ON":"OFF"):request.Action=="auto"?"Automatic cooling":"Maximum fans",Detail="",IsPending=true};
            var notice=Applied(request,new Reply{Ok=true});notice.IsPending=true;notice.Detail="";return notice;
        }
        public static FeedbackNotice Applied(Request r,Reply reply){
            if(!reply.Ok)return new FeedbackNotice{Kind=FeedbackKind.Error,Title="Couldn't apply that",Detail=reply.Message};
            if(r.Action=="mode"){
                string[] names={"Quiet","Balanced","Performance","Turbo"};
                if(r.Mode<0||r.Mode>3||!reply.Data.ContainsKey("mode")||reply.Data["mode"]!=names[r.Mode])return new FeedbackNotice{Kind=FeedbackKind.Error,Title="Mode not confirmed",Detail="Wait a moment for live sync, then try again."};
                return new FeedbackNotice{Kind=FeedbackKind.Mode,Mode=r.Mode,Title=names[r.Mode]+" mode",Detail=r.Mode==3?"Ultimate Performance · Full fans ON":""};
            }
            if(r.Action=="screen-brightness"){
                int percent;if(!reply.Data.ContainsKey("display-brightness")||!int.TryParse(reply.Data["display-brightness"],out percent)||percent<0||percent>100)return new FeedbackNotice{Kind=FeedbackKind.Error,Title="Brightness not confirmed",Detail=reply.Message};
                return new FeedbackNotice{Kind=FeedbackKind.Brightness,Title="Screen brightness",Detail="",Percent=percent};
            }
            if(r.Action=="keyboard"||r.Action=="keyboard-animation"){
                var zones=new Color[4];for(int i=0;i<4;i++)zones[i]=r.Brightness==0?Color.FromArgb(144,148,151):LivePreview.KeyColour(r,i,i/3.0,0);
                bool brightness=r.Feedback=="keyboard-brightness";
                return new FeedbackNotice{Kind=brightness?FeedbackKind.KeyboardBrightness:FeedbackKind.Keyboard,Title=brightness?(r.Brightness==0?"Off":r.Brightness+"%"):r.Effect,Detail="",Percent=r.Brightness,Zones=zones};
            }
            if(r.Action=="rear-off"){
                var rn=new FeedbackNotice{Kind=FeedbackKind.KeyboardBrightness,Title="Off",Percent=0};
                rn.ControlLabel="Rear light bar";
                return rn;
            }
            if(r.Action=="rear"||r.Action=="rear-music"){
                var rn=new FeedbackNotice{Kind=FeedbackKind.KeyboardBrightness,Title=(r.Brightness==0?"Off":r.Brightness+"%"),Percent=r.Brightness};
                rn.ControlLabel="Rear light bar";
                return rn;
            }
            return new FeedbackNotice{Title="All set"};
        }
    }
    public sealed class ModeChime : IDisposable {
        readonly SoundPlayer[] players=new SoundPlayer[4];readonly MemoryStream[] streams=new MemoryStream[4];
        public ModeChime(){for(int i=0;i<4;i++){streams[i]=new MemoryStream(Wave(i),false);players[i]=new SoundPlayer(streams[i]);}}
        public static byte[] Wave(int mode){
            if(mode<0||mode>3)throw new ArgumentException("Unknown performance mode.");
            const int rate=22050;int count=(int)(rate*.30);var data=new MemoryStream();
            using(var writer=new BinaryWriter(data,Encoding.ASCII,true)){
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                double[] notes={660*Math.Pow(2,mode/6.0),880*Math.Pow(2,mode/6.0)};
                for(int n=0;n<count;n++){
                    double t=n/(double)rate;double sample=0;
                    for(int note=0;note<2;note++){double at=t-note*.085;if(at>=0&&at<.20){double envelope=Math.Min(1,at/.008)*Math.Exp(-at*24)*Math.Min(1,(.20-at)/.025);sample+=.12*envelope*(Math.Sin(2*Math.PI*notes[note]*at)+.16*Math.Sin(4*Math.PI*notes[note]*at));}}
                    writer.Write((short)(Math.Max(-.35,Math.Min(.35,sample))*32767));
                }
            }return data.ToArray();
        }
        public void Play(int mode){try{if(mode>=0&&mode<players.Length){foreach(var player in players)player.Stop();players[mode].Play();}}catch(Exception e){Store.Log("Mode chime: "+e.Message);}}
        public void Dispose(){foreach(var player in players){player.Stop();player.Dispose();}foreach(var stream in streams)stream.Dispose();}
    }
    public sealed class ShortcutToast : Form {
        public const float VisualScale=.66424f;
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();readonly Stopwatch elapsed=Stopwatch.StartNew();
        readonly PopupMotion motion=new PopupMotion();readonly bool renderOnly;
        FeedbackNotice notice=new FeedbackNotice();
        float brightnessFontSize;int brightnessFontDpi;Size brightnessFontClient;
        internal int DisplayedPercent{get{return notice.Percent;}}
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
        public ShortcutToast(bool renderOnly=false){
            this.renderOnly=renderOnly;
            FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(319,109);BackColor=Theme.Ivory;
            UpdateShape();
            timer.Interval=16;timer.Tick+=(s,e)=>{double ms=elapsed.Elapsed.TotalMilliseconds;if(motion.Expired(ms)){Hide();timer.Stop();return;}double alpha=Math.Max(.01,motion.Opacity(ms));if(Math.Abs(Opacity-alpha)>.001)Opacity=alpha;Invalidate();timer.Interval=motion.NextTick(ms);};
        }
        void UpdateShape(){if(Width<=0||Height<=0)return;using(var path=Theme.Round(new RectangleF(0,0,Width,Height),23*DeviceDpi/96f*VisualScale)){var previous=Region;Region=new Region(path);if(previous!=null)previous.Dispose();}}
        void PlacePopup(){if(renderOnly)return;var screen=Screen.PrimaryScreen??Screen.FromHandle(Handle);Location=PopupLayout.BottomCenter(screen.WorkingArea,Size,(int)(36*DeviceDpi/96f));}
        protected override void OnShown(EventArgs e){base.OnShown(e);PlacePopup();}
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);UpdateShape();if(Visible)PlacePopup();}
        public void Notify(string message,int percent=-1){Notify(new FeedbackNotice{Title=message,Kind=percent>=0?FeedbackKind.Brightness:FeedbackKind.Message,Percent=percent});}
        public void Notify(FeedbackNotice value){
            double now=elapsed.Elapsed.TotalMilliseconds;bool visible=Visible;
            if(visible&&value.IsPending&&value.Kind==FeedbackKind.Brightness&&notice.Kind==FeedbackKind.Brightness&&notice.Percent>=0){motion.Hold(now);timer.Interval=motion.NextTick(now);timer.Start();return;}
            motion.Update(now,visible,value.Percent,value.IsPending?10000:2400);notice=value;AccessibleName=value.Title+(value.Kind==FeedbackKind.Brightness&&value.Percent>=0?" "+value.Percent+"%":"")+" "+value.Detail;
            var handle=Handle;PlacePopup();Opacity=Math.Max(.01,motion.Opacity(now));
            if(!visible){Show();PlacePopup();}Invalidate();timer.Interval=16;timer.Start();
        }
        void TextAt(Graphics g,string text,float size,FontStyle style,RectangleF rect,Color color,TextFormatFlags flags=TextFormatFlags.EndEllipsis){using(var font=new Font("Segoe UI",size*VisualScale,style))TextRenderer.DrawText(g,text,font,Rectangle.Round(rect),color,TextFormatFlags.NoPrefix|flags);}
        internal static float FittedTitleSize(Graphics g,string text,RectangleF rect){
            for(float size=25;size>=14;size-=.5f)using(var font=new Font("Segoe UI",size*VisualScale,FontStyle.Bold)){
                var measured=TextRenderer.MeasureText(g,text,font,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
                if(measured.Width<=rect.Width&&measured.Height<=rect.Height)return size;
            }return 14;
        }
        protected override void OnPaint(PaintEventArgs e){
            if(notice.Kind==FeedbackKind.Brightness||notice.Kind==FeedbackKind.KeyboardBrightness){PaintBrightness(e.Graphics);return;}
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);float s=DeviceDpi/96f*VisualScale;
            var outer=new RectangleF(.5f*s,.5f*s,Width-s,Height-s);
            using(var path=Theme.Round(outer,23*s))using(var border=new LinearGradientBrush(outer,Color.FromArgb(255,181,45),Color.FromArgb(255,83,18),35f))g.FillPath(border,path);
            var inner=outer;inner.Inflate(-3*s,-3*s);using(var path=Theme.Round(inner,20*s))using(var fill=new SolidBrush(Theme.White))g.FillPath(fill,path);
            var tile=new RectangleF(19*s,20*s,54*s,54*s);using(var path=Theme.Round(tile,16*s))using(var fill=new SolidBrush(Theme.Pearl))g.FillPath(fill,path);
            DrawIcon(g,new RectangleF(31*s,32*s,30*s,30*s),s);
            bool keyboard=notice.Kind==FeedbackKind.Keyboard||notice.Kind==FeedbackKind.KeyboardBrightness;
            bool screen=notice.Kind==FeedbackKind.Brightness;
            if(keyboard||screen){
                var title=PopupLayout.KeyboardTitle(ClientSize,s);
                if(notice.ControlLabel.Length>0){
                    TextAt(g,notice.ControlLabel,12,FontStyle.Bold,new RectangleF(91*s,14*s,Width-113*s,24*s),Theme.OrangeInk);
                    title=new RectangleF(91*s,40*s,Width-113*s,46*s);
                }
                string primary=screen?(notice.Percent<0?"…":notice.Percent+"%"):notice.Title;
                TextAt(g,primary,FittedTitleSize(g,primary,title),FontStyle.Bold,title,Theme.Ink,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter);
            }else{
                var title=PopupLayout.KeyboardTitle(ClientSize,s);
                TextAt(g,notice.Title,FittedTitleSize(g,notice.Title,title),FontStyle.Bold,title,Theme.Ink,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter);
                if(notice.Kind==FeedbackKind.Error)TextAt(g,notice.Detail,9,FontStyle.Regular,new RectangleF(90*s,82*s,Width-111*s,34*s),Theme.Muted);
            }
            float y=120*s;
            if(notice.Kind==FeedbackKind.Mode){
                string[] names={"Quiet","Balanced","Performance","Turbo"};float cell=(Width-40*s)/4;
                for(int i=0;i<4;i++){
                    var pill=new RectangleF(20*s+i*cell,y,cell-6*s,27*s);bool selected=notice.Mode==i||(notice.IsPending&&notice.RequestedMode==i);
                    Color color = selected ? (i == 3 ? Color.FromArgb(255, 60, 20) : Color.FromArgb(255, 119, 22)) : Theme.Pearl;
                    using(var path=Theme.Round(pill,10*s))using(var brush=new SolidBrush(color))g.FillPath(brush,path);
                }
            }else if(notice.Kind==FeedbackKind.Keyboard&&notice.Zones!=null){
                float cell=(Width-44*s)/4;for(int z=0;z<4;z++)for(int row=0;row<2;row++){
                    var key=new RectangleF(22*s+z*cell,y+row*13*s,cell-7*s,10*s);
                    using(var path=Theme.Round(key,3*s))using(var brush=new SolidBrush(notice.Zones[z]))g.FillPath(brush,path);
                }
            }else if(notice.Percent>=0||notice.Kind==FeedbackKind.Brightness){
                bool keyboardBrightness=notice.Kind==FeedbackKind.KeyboardBrightness;bool primaryBrightness=keyboardBrightness||notice.Kind==FeedbackKind.Brightness;
                var bar=new RectangleF(22*s,y+3*s,Width-(primaryBrightness?44:105)*s,keyboardBrightness?13*s:10*s);using(var path=Theme.Round(bar,5*s))using(var brush=new SolidBrush(Theme.Pearl))g.FillPath(brush,path);
                bar.Width*=(float)Math.Max(0,Math.Min(100,motion.Percent(elapsed.Elapsed.TotalMilliseconds)))/100f;if(bar.Width>0)using(var path=Theme.Round(bar,5*s))using(var brush=new LinearGradientBrush(bar,Color.FromArgb(255,175,36),Color.FromArgb(255,91,18),0f))g.FillPath(brush,path);
                if(!primaryBrightness)TextAt(g,notice.Percent<0?"…":notice.Percent+"%",12,FontStyle.Bold,new RectangleF(Width-76*s,y-5*s,55*s,28*s),Theme.Ink,TextFormatFlags.Right);
            }
        }
        internal static RectangleF[] BrightnessLayout(Size size,float d){
            return new[]{new RectangleF(14*d,18*d,38*d,38*d),new RectangleF(64*d,15*d,size.Width-169*d,44*d),new RectangleF(size.Width-105*d,12*d,90*d,50*d),new RectangleF(16*d,size.Height-26*d,size.Width-32*d,7*d)};
        }
        internal static float BrightnessValueSize(Graphics g,string value,RectangleF rect){
            for(float size=24;size>=16;size-=.5f)using(var font=new Font("Segoe UI",size,FontStyle.Bold)){
                var measured=TextRenderer.MeasureText(g,value,font,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
                if(measured.Width<=rect.Width&&measured.Height<=rect.Height)return size;
            }return 16;
        }
        void PaintBrightness(Graphics g){
            float d=DeviceDpi/96f;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);
            var outer=new RectangleF(.5f*d,.5f*d,Width-d,Height-d);
            using(var path=Theme.Round(outer,15*d))using(var border=new LinearGradientBrush(outer,Color.FromArgb(255,204,143),Color.FromArgb(244,119,42),30f))g.FillPath(border,path);
            var inner=outer;inner.Inflate(-1.5f*d,-1.5f*d);
            using(var path=Theme.Round(inner,13.5f*d))using(var fill=new LinearGradientBrush(inner,Theme.White,Theme.Ivory,80f))g.FillPath(fill,path);
            var layout=BrightnessLayout(ClientSize,d);
            using(var path=Theme.Round(layout[0],12*d))using(var fill=new LinearGradientBrush(layout[0],Theme.White,Theme.Pearl,60f))g.FillPath(fill,path);
            DrawIcon(g,new RectangleF(21*d,25*d,24*d,24*d),.8f*d);
            TextAt(g,notice.ControlLabel.Replace(" ","\n"),9.5f/VisualScale,FontStyle.Bold,layout[1],Theme.Muted,TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter);
            string value=notice.Percent<0?"…":notice.Kind==FeedbackKind.KeyboardBrightness&&notice.Percent==0?"Off":notice.Percent+"%";
            if(brightnessFontDpi!=DeviceDpi||brightnessFontClient!=ClientSize){brightnessFontSize=BrightnessValueSize(g,"100%",layout[2]);brightnessFontDpi=DeviceDpi;brightnessFontClient=ClientSize;}
            TextAt(g,value,brightnessFontSize/VisualScale,FontStyle.Bold,layout[2],Theme.Ink,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
            var track=layout[3];using(var path=Theme.Round(track,3.5f*d))using(var fill=new SolidBrush(Theme.Pearl))g.FillPath(fill,path);
            track.Width*=(float)Math.Max(0,Math.Min(100,motion.Percent(elapsed.Elapsed.TotalMilliseconds)))/100f;
            if(track.Width>0)using(var path=Theme.Round(track,3.5f*d))using(var fill=new LinearGradientBrush(track,Color.FromArgb(255,184,75),Color.FromArgb(246,113,32),0f))g.FillPath(fill,path);
        }
        void DrawIcon(Graphics g,RectangleF r,float scale){
            using(var pen=new Pen(Color.FromArgb(214,88,15),2.4f*scale)){
                pen.StartCap=pen.EndCap=LineCap.Round;
                if(notice.Kind==FeedbackKind.Brightness){g.DrawEllipse(pen,r.X+8*scale,r.Y+8*scale,14*scale,14*scale);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,r.X+15*scale+(float)Math.Cos(a)*12*scale,r.Y+15*scale+(float)Math.Sin(a)*12*scale,r.X+15*scale+(float)Math.Cos(a)*15*scale,r.Y+15*scale+(float)Math.Sin(a)*15*scale);}}
                else if(notice.ControlLabel=="Rear light bar"){var barRect=new RectangleF(r.X+2*scale,r.Y+10*scale,r.Width-4*scale,10*scale);using(var p=Theme.Round(barRect,4*scale)) g.DrawPath(pen,p);g.DrawLine(pen,r.X+7*scale,r.Y+15*scale,r.Right-7*scale,r.Y+15*scale);}
                else if(notice.Kind==FeedbackKind.Keyboard||notice.Kind==FeedbackKind.KeyboardBrightness){g.DrawRectangle(pen,r.X,r.Y+4*scale,r.Width,r.Height-8*scale);for(int i=0;i<4;i++)g.DrawLine(pen,r.X+(5+i*6)*scale,r.Y+10*scale,r.X+(5+i*6)*scale,r.Y+16*scale);g.DrawLine(pen,r.X+7*scale,r.Y+22*scale,r.Right-7*scale,r.Y+22*scale);}
                else if(notice.Kind==FeedbackKind.Mode&&notice.Mode==0){g.DrawArc(pen,r,55,285);g.DrawArc(pen,new RectangleF(r.X+9*scale,r.Y-4*scale,r.Width-3*scale,r.Height-3*scale),75,185);}
                else if(notice.Kind==FeedbackKind.Mode&&notice.Mode==1){for(int i=0;i<3;i++){float x=r.X+(4+i*11)*scale;g.DrawLine(pen,x,r.Top,x,r.Bottom);g.FillEllipse(Brushes.White,x-3*scale,r.Y+(i%2==0?7:18)*scale,6*scale,6*scale);g.DrawEllipse(pen,x-3*scale,r.Y+(i%2==0?7:18)*scale,6*scale,6*scale);}}
                else if(notice.Kind==FeedbackKind.Mode){g.DrawLines(pen,new[]{new PointF(r.X+18*scale,r.Top),new PointF(r.X+4*scale,r.Y+17*scale),new PointF(r.X+15*scale,r.Y+17*scale),new PointF(r.X+11*scale,r.Bottom),new PointF(r.Right,r.Y+11*scale),new PointF(r.X+18*scale,r.Y+11*scale),new PointF(r.X+18*scale,r.Top)});}
                else if(notice.Kind==FeedbackKind.Error){g.DrawEllipse(pen,r);g.DrawLine(pen,r.X+15*scale,r.Y+6*scale,r.X+15*scale,r.Y+17*scale);g.DrawEllipse(pen,r.X+14*scale,r.Y+23*scale,2*scale,2*scale);}
                else g.DrawLines(pen,new[]{new PointF(r.X+3*scale,r.Y+16*scale),new PointF(r.X+12*scale,r.Y+25*scale),new PointF(r.X+28*scale,r.Y+5*scale)});
            }
        }
        public static void RenderExamples(string directory){
            Directory.CreateDirectory(directory);using(var toast=new ShortcutToast(true)){
                toast.Location=new Point(-32000,-32000);toast.Show();toast.timer.Stop();toast.Opacity=1;
                for(int i=0;i<11;i++){
                    Request r=i<3?new Request{Action="mode",Mode=i}:new Request{Action=i==3?"screen-brightness":"keyboard",Effect=i==5?"Clock":"Rainbow",Brightness=i==5?50:i==6?0:100};var reply=new Reply{Ok=true};if(i<3)reply.Data["mode"]=new[]{"Quiet","Balanced","Performance"}[i];else reply.Data["display-brightness"]="70";
                    if(i==5||i==6)r.Feedback="keyboard-brightness";if(i==7)r.Effect="Magnetic Slingshot";if(i==8)r.Effect="Orbital Eclipse";
                    if(i==9){r.Action="mode";r.Mode=2;}if(i==10)r.Feedback="keyboard-brightness";
                    toast.notice=i>=9?FeedbackNotice.Pending(r):FeedbackNotice.Applied(r,reply);toast.motion.Update(toast.elapsed.Elapsed.TotalMilliseconds,false,toast.notice.Percent);using(var image=new Bitmap(toast.Width,toast.Height)){toast.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(directory,"Toast-"+i+".png"));}
                }
            }
        }
        protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
    }
}
