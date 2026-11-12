using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GTBook {
    public static class Theme {
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        public static void ApplyChrome(Form form){
            if(!form.IsHandleCreated)return;
            int dark=Depth>=.5f?1:0;
            try{DwmSetWindowAttribute(form.Handle,20,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        }
        public static float Depth {get;private set;}
        public static void SetDepth(float value){Depth=Math.Max(0,Math.Min(1,value));}
        public static Color Tone(Color light,Color dark){return Blend(light,dark,Depth);}
        public static Color White {get{return Tone(Color.White,Color.FromArgb(30,35,47));}}
        public static Color Ivory {get{return Tone(Color.FromArgb(255,250,246),Color.FromArgb(15,19,28));}}
        public static Color Pearl {get{return Tone(Color.FromArgb(255,240,226),Color.FromArgb(40,47,62));}}
        public static Color ContrastInk {get{return Color.FromArgb(35,42,49);}}
        public static Color Ink {get{return Tone(ContrastInk,Color.FromArgb(237,241,250));}}
        public static Color Muted {get{return Tone(Color.FromArgb(96,103,111),Color.FromArgb(167,179,199));}}
        public static readonly Color OrangeStart=Color.FromArgb(255,190,113);
        public static readonly Color OrangeEnd=Color.FromArgb(239,115,36);
        public static Color OrangeInk {get{return Tone(Color.FromArgb(164,72,20),Color.FromArgb(255,180,107));}}
        public static Color Shadow {get{return Tone(Color.FromArgb(228,210,195),Color.FromArgb(5,8,14));}}
        public static Color Edge {get{return Tone(Color.FromArgb(223,211,199),Color.FromArgb(68,80,101));}}
        public static Color[] Palette {get{return new[]{White,Ivory,Pearl,Ink,Muted,OrangeInk};}}
        public static void Apply(IEnumerable<Control> roots,Color[] previous){
            var visited=new HashSet<Control>();var current=Palette;
            foreach(var root in roots)ApplyControl(root,previous,current,visited);
        }
        static void ApplyControl(Control control,Color[] previous,Color[] current,HashSet<Control> visited){
            if(control==null||control.IsDisposed||!visited.Add(control))return;
            var button=control as RaisedButton;
            var form=control as Form;if(form!=null&&(Depth==0||Depth==1))ApplyChrome(form);
            if(button==null||!button.ColourSwatch){
                for(int i=0;i<previous.Length;i++)if(control.BackColor.A==255&&control.BackColor.ToArgb()==previous[i].ToArgb()){control.BackColor=current[i];break;}
                for(int i=0;i<previous.Length;i++)if(control.ForeColor.ToArgb()==previous[i].ToArgb()){control.ForeColor=current[i];break;}
            }
            foreach(Control child in control.Controls)ApplyControl(child,previous,current,visited);
            control.Invalidate();
        }
        public static GraphicsPath Round(RectangleF r,float radius) {
            var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
            if(d<=0){p.AddRectangle(r);return p;}
            p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        public static Color Blend(Color a,Color b,float weight){return Color.FromArgb((int)(a.R*(1-weight)+b.R*weight),(int)(a.G*(1-weight)+b.G*weight),(int)(a.B*(1-weight)+b.B*weight));}
        public static LinearGradientBrush OrangeBorder(RectangleF bounds,bool strong=false){return new LinearGradientBrush(bounds,strong?OrangeStart:Tone(Color.FromArgb(255,216,177),Color.FromArgb(91,105,130)),strong?OrangeEnd:Tone(Color.FromArgb(241,157,99),Color.FromArgb(49,59,79)),32f);}
        public static void PaintUnder(Control child,Graphics graphics){
            Point origin=child.Location;Control parent=child.Parent;
            while(parent!=null){
                var surface=parent as GradientSurface;
                if(surface!=null){var state=graphics.Save();graphics.TranslateTransform(-origin.X,-origin.Y);surface.PaintSurface(graphics);graphics.Restore(state);return;}
                if(parent.BackColor.A==255){graphics.Clear(parent.BackColor);return;}
                origin.Offset(parent.Location);parent=parent.Parent;
            }
            graphics.Clear(Ivory);
        }
    }
    public sealed class GradientSurface : Panel {
        public bool Sidebar;
        public GradientSurface(){DoubleBuffered=true;BackColor=Theme.Ivory;SetStyle(ControlStyles.ResizeRedraw,true);}
        public void PaintSurface(Graphics graphics){
            if(Width<=0||Height<=0)return;
            using(var fill=new LinearGradientBrush(ClientRectangle,Theme.Tone(Color.White,Color.FromArgb(23,29,42)),Sidebar?Theme.Tone(Color.FromArgb(255,232,210),Color.FromArgb(11,15,24)):Theme.Tone(Color.FromArgb(255,241,229),Color.FromArgb(12,16,25)),Sidebar?84f:32f))graphics.FillRectangle(fill,ClientRectangle);
        }
        protected override void OnPaintBackground(PaintEventArgs e){PaintSurface(e.Graphics);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Sidebar&&Width>1){using(var line=new LinearGradientBrush(ClientRectangle,Color.FromArgb(255,231,207),Color.FromArgb(239,144,78),90f))using(var pen=new Pen(line,1.2f*DeviceDpi/96f))e.Graphics.DrawLine(pen,Width-1,0,Width-1,Height);}}
    }
    public sealed class GradientFlowPanel : FlowLayoutPanel {
        public GradientFlowPanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);DoubleBuffered=true;BackColor=Color.Transparent;}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintUnder(this,e.Graphics);}
    }
    public class BufferedPanel : Panel {
        public BufferedPanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);DoubleBuffered=true;BackColor=Color.Transparent;}
    }
    public class BufferedTableLayoutPanel : TableLayoutPanel {
        public BufferedTableLayoutPanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);DoubleBuffered=true;BackColor=Color.Transparent;}
    }
    public class RaisedButton : Button {
        bool hover,down,selected,badgeDot;
        float holdProgress;
        public Color Accent {get;set;}
        public Color HoldAccent {get;set;}
        public Color BadgeColor {get;set;}
        public bool BadgeDot {get{return badgeDot;}set{if(badgeDot!=value){badgeDot=value;Invalidate();}}}
        public float HoldProgress {get{return holdProgress;}set{float v=Math.Max(0f,Math.Min(1f,value));if(Math.Abs(holdProgress-v)>.001f){holdProgress=v;Invalidate();}}}
        public bool SuppressNextClick {get;set;}
        Color feedback=Color.Empty;readonly Timer feedbackTimer=new Timer();
        public void Flash(bool success){feedback=success?Color.FromArgb(35,159,106):Color.FromArgb(214,57,58);feedbackTimer.Stop();feedbackTimer.Start();Invalidate();}
        public bool Selected {get{return selected;}set{if(selected!=value){selected=value;Invalidate();}}}
        public bool ColourSwatch {get;set;}
        public bool Primary {get;set;}
        public bool Navigation {get;set;}
        public bool Dropdown {get;set;}
        public RaisedButton() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;
            BackColor=Theme.White;ForeColor=Theme.Ink;Cursor=Cursors.Hand;
            feedbackTimer.Interval=1100;feedbackTimer.Tick+=(s,e)=>{feedbackTimer.Stop();feedback=Color.Empty;Invalidate();};
        }
        protected override void OnClick(EventArgs e){if(SuppressNextClick){SuppressNextClick=false;return;}base.OnClick(e);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;down=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)down=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space)down=true;Invalidate();base.OnKeyDown(e);}
        protected override void OnKeyUp(KeyEventArgs e){down=false;Invalidate();base.OnKeyUp(e);}
        protected override void OnLostFocus(EventArgs e){down=false;Invalidate();base.OnLostFocus(e);}
        protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintUnder(this,g);g.SmoothingMode=SmoothingMode.AntiAlias;
            float scale=DeviceDpi/96f;float offset=down?3*scale:0;
            var surface=new RectangleF(2*scale,1*scale+offset,Width-5*scale,Height-7*scale);
            if(surface.Width<=0||surface.Height<=0)return;
            if(Enabled&&!down)using(var shadow=Theme.Round(new RectangleF(surface.X,surface.Y+3*scale,surface.Width,surface.Height),10*scale))using(var fill=new SolidBrush(Theme.Shadow))g.FillPath(fill,shadow);
            Color top=ColourSwatch?BackColor:Theme.White;
            Color bottom=ColourSwatch?BackColor:Theme.Pearl;
            Color tint=!feedback.IsEmpty?feedback:!Accent.IsEmpty?Accent:selected?Theme.OrangeEnd:Color.Empty;
            if(!tint.IsEmpty&&!ColourSwatch){top=Theme.Blend(Theme.White,tint,.08f);bottom=Theme.Blend(Theme.White,tint,hover?.28f:.16f);}
            if(hover&&Enabled&&!ColourSwatch){top=Theme.Blend(top,Theme.Ink,.10f);bottom=Theme.Blend(bottom,Theme.Ink,.08f);}
            if((selected||Primary)&&!ColourSwatch&&feedback.IsEmpty){top=Accent.IsEmpty?Theme.OrangeStart:Theme.Blend(Theme.OrangeStart,Accent,.45f);bottom=Accent.IsEmpty?Theme.OrangeEnd:Accent;}
            if(down){top=bottom;bottom=ColourSwatch?BackColor:!tint.IsEmpty?Theme.Blend(Theme.White,tint,.32f):Theme.Pearl;}
            if(!Enabled&&feedback.IsEmpty&&!selected){top=Theme.Ivory;bottom=Theme.Pearl;}
            using(var p=Theme.Round(surface,10*scale)) {
                using(var fill=new LinearGradientBrush(surface,top,bottom,LinearGradientMode.Vertical))g.FillPath(fill,p);
                if(holdProgress>0f&&Enabled&&!ColourSwatch){
                    Color hc=HoldAccent.IsEmpty?Color.FromArgb(255,65,20):HoldAccent;
                    var fillRect=new RectangleF(surface.X,surface.Y,surface.Width*holdProgress,surface.Height);
                    if(fillRect.Width>1f){
                        var saved=g.Save();g.SetClip(p);
                        using(var hFill=new LinearGradientBrush(surface,Color.FromArgb(200,Theme.Blend(Theme.OrangeStart,hc,.35f)),Color.FromArgb(230,hc),0f))g.FillRectangle(hFill,fillRect);
                        g.Restore(saved);
                    }
                }
                if(holdProgress>0f&&Enabled){
                    Color hc=HoldAccent.IsEmpty?Color.FromArgb(255,65,20):HoldAccent;
                    using(var edge=new Pen(hc,1.8f*scale))g.DrawPath(edge,p);
                }else if(!tint.IsEmpty&&(Enabled||!feedback.IsEmpty)){
                    using(var gradient=new LinearGradientBrush(surface,Theme.Blend(Color.White,tint,.52f),tint,32f))using(var edge=new Pen(gradient,1.4f*scale))g.DrawPath(edge,p);
                }else using(var edge=new Pen(Theme.Edge,scale))g.DrawPath(edge,p);
                if(!ColourSwatch)using(var bevel=new Pen(Color.FromArgb(42,Color.White),scale))g.DrawLine(bevel,surface.Left+12*scale,surface.Top+2*scale,surface.Right-12*scale,surface.Top+2*scale);
            }
            if(selected){
                if(Navigation){using(var p=Theme.Round(new RectangleF(9*scale,surface.Top+10*scale,3*scale,surface.Height-20*scale),1.5f*scale))using(var marker=new SolidBrush(Theme.OrangeEnd))g.FillPath(marker,p);}
                else using(var marker=new SolidBrush(Theme.OrangeEnd))g.FillEllipse(marker,10*scale,surface.Top+surface.Height/2-2*scale,4*scale,4*scale);
            }
            var textBounds=Rectangle.Round(surface);textBounds.Inflate(-(int)(6*scale),0);
            if(Navigation)textBounds.X+=(int)(14*scale);
            if(Dropdown){
                textBounds.Width-=(int)(22*scale);float x=surface.Right-17*scale,y=surface.Top+surface.Height/2;
                using(var pen=new Pen(selected?Theme.ContrastInk:Theme.OrangeInk,2.2f*scale)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLines(pen,new[]{new PointF(x-5*scale,y-2*scale),new PointF(x,y+3*scale),new PointF(x+5*scale,y-2*scale)});}
            }else if(badgeDot){
                textBounds.Width-=(int)(18*scale);
            }
            if(textBounds.Width>4&&!string.IsNullOrEmpty(Text)){
                float fontSize=Font.Size;var fontStyle=selected?FontStyle.Bold:Font.Style;
                Font drawFont=new Font(Font.FontFamily,fontSize,fontStyle,Font.Unit);
                try{
                    while(fontSize>7.2f){
                        var measured=TextRenderer.MeasureText(g,Text,drawFont,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
                        if(measured.Width<=textBounds.Width)break;
                        fontSize=Math.Max(7.2f,fontSize-0.5f);drawFont.Dispose();drawFont=new Font(Font.FontFamily,fontSize,fontStyle,Font.Unit);
                    }
                    TextRenderer.DrawText(g,Text,drawFont,textBounds,(selected||Primary&&Enabled)&&!ColourSwatch?Theme.ContrastInk:Enabled?ForeColor:Theme.Muted,(Navigation?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
                }finally{drawFont.Dispose();}
            }
            if(badgeDot&&!Dropdown){
                float d=8.5f*scale,dx=surface.Right-18*scale,dy=surface.Top+(surface.Height-d)/2f;
                Color bc=BadgeColor.IsEmpty?Color.FromArgb(255,126,29):BadgeColor;
                Color ring=(selected||Primary)&&!ColourSwatch?Color.FromArgb(245,255,248,238):Theme.White;
                using(var fill=new SolidBrush(bc))using(var halo=new Pen(ring,1.8f*scale)){
                    g.FillEllipse(fill,dx,dy,d,d);
                    g.DrawEllipse(halo,dx,dy,d,d);
                }
            }
            if(Focused&&ShowFocusCues){var r=Rectangle.Round(surface);r.Inflate(-(int)(4*scale),-(int)(4*scale));ControlPaint.DrawFocusRectangle(g,r,Theme.Muted,top);}
        }
        protected override void Dispose(bool disposing){if(disposing)feedbackTimer.Dispose();base.Dispose(disposing);}
    }
    public sealed class EffectSection : Panel {
        bool selected;
        public bool Selected {get{return selected;}set{selected=value;Invalidate();}}
        public Color Accent=Color.FromArgb(111,0,255);
        public EffectSection(){DoubleBuffered=true;BackColor=Theme.White;Padding=new Padding(12);SetStyle(ControlStyles.ResizeRedraw,true);}
        protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Theme.White);}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96f;
            using(var path=Theme.Round(new RectangleF(s,s,Width-3*s,Height-3*s),12*s))
            using(var gradient=Theme.OrangeBorder(ClientRectangle,selected))using(var pen=new Pen(gradient,selected?1.8f*s:s))e.Graphics.DrawPath(pen,path);
        }
    }
}
