using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GTBook {
    public static class Theme {
        public static readonly Color White=Color.FromArgb(255,255,255);
        public static readonly Color Ivory=Color.FromArgb(255,250,246);
        public static readonly Color Pearl=Color.FromArgb(255,240,226);
        public static readonly Color Ink=Color.FromArgb(35,42,49);
        public static readonly Color Muted=Color.FromArgb(96,103,111);
        public static readonly Color OrangeStart=Color.FromArgb(255,190,113);
        public static readonly Color OrangeEnd=Color.FromArgb(239,115,36);
        public static readonly Color OrangeInk=Color.FromArgb(164,72,20);
        public static GraphicsPath Round(RectangleF r,float radius) {
            var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
            if(d<=0){p.AddRectangle(r);return p;}
            p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        public static Color Blend(Color a,Color b,float weight){return Color.FromArgb((int)(a.R*(1-weight)+b.R*weight),(int)(a.G*(1-weight)+b.G*weight),(int)(a.B*(1-weight)+b.B*weight));}
        public static LinearGradientBrush OrangeBorder(RectangleF bounds,bool strong=false){return new LinearGradientBrush(bounds,strong?OrangeStart:Color.FromArgb(255,216,177),strong?OrangeEnd:Color.FromArgb(241,157,99),32f);}
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
            using(var fill=new LinearGradientBrush(ClientRectangle,Theme.White,Sidebar?Color.FromArgb(255,232,210):Color.FromArgb(255,241,229),Sidebar?84f:32f))graphics.FillRectangle(fill,ClientRectangle);
        }
        protected override void OnPaintBackground(PaintEventArgs e){PaintSurface(e.Graphics);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Sidebar&&Width>1){using(var line=new LinearGradientBrush(ClientRectangle,Color.FromArgb(255,231,207),Color.FromArgb(239,144,78),90f))using(var pen=new Pen(line,1.2f*DeviceDpi/96f))e.Graphics.DrawLine(pen,Width-1,0,Width-1,Height);}}
    }
    public sealed class GradientFlowPanel : FlowLayoutPanel {
        public GradientFlowPanel(){DoubleBuffered=true;BackColor=Color.Transparent;SetStyle(ControlStyles.ResizeRedraw,true);}
    }
    public class RaisedButton : Button {
        bool hover,down,selected;
        public Color Accent {get;set;}
        Color feedback=Color.Empty;readonly Timer feedbackTimer=new Timer();
        public void Flash(bool success){feedback=success?Color.FromArgb(35,159,106):Color.FromArgb(214,57,58);feedbackTimer.Stop();feedbackTimer.Start();Invalidate();}
        public bool Selected {get{return selected;}set{if(selected!=value){selected=value;Invalidate();}}}
        public bool ColourSwatch {get;set;}
        public bool Navigation {get;set;}
        public bool Dropdown {get;set;}
        public RaisedButton() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;
            BackColor=Theme.White;ForeColor=Theme.Ink;Cursor=Cursors.Hand;
            feedbackTimer.Interval=1100;feedbackTimer.Tick+=(s,e)=>{feedbackTimer.Stop();feedback=Color.Empty;Invalidate();};
        }
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;down=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)down=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space)down=true;Invalidate();base.OnKeyDown(e);}
        protected override void OnKeyUp(KeyEventArgs e){down=false;Invalidate();base.OnKeyUp(e);}
        protected override void OnLostFocus(EventArgs e){down=false;Invalidate();base.OnLostFocus(e);}
        protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
        protected override void OnPaintBackground(PaintEventArgs e){
            Theme.PaintUnder(this,e.Graphics);
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintUnder(this,g);g.SmoothingMode=SmoothingMode.AntiAlias;
            float scale=DeviceDpi/96f;float offset=down?3*scale:0;
            var surface=new RectangleF(2*scale,1*scale+offset,Width-5*scale,Height-7*scale);
            if(surface.Width<=0||surface.Height<=0)return;
            if(Enabled&&!down)using(var shadow=Theme.Round(new RectangleF(surface.X,surface.Y+2*scale,surface.Width,surface.Height),10*scale))using(var fill=new SolidBrush(Color.FromArgb(228,210,195)))g.FillPath(fill,shadow);
            Color top=ColourSwatch?BackColor:Theme.White;
            Color bottom=ColourSwatch?BackColor:Theme.Pearl;
            Color tint=!feedback.IsEmpty?feedback:!Accent.IsEmpty?Accent:selected?Theme.OrangeEnd:Color.Empty;
            if(!tint.IsEmpty&&!ColourSwatch){top=Theme.Blend(Theme.White,tint,.08f);bottom=Theme.Blend(Theme.White,tint,hover?.28f:.16f);}
            if(hover&&Enabled&&!ColourSwatch){top=Theme.Blend(top,Color.White,.35f);bottom=Theme.Blend(bottom,Color.White,.4f);}
            if(selected&&!ColourSwatch&&feedback.IsEmpty){top=Theme.OrangeStart;bottom=Theme.OrangeEnd;}
            if(down){top=bottom;bottom=ColourSwatch?BackColor:!tint.IsEmpty?Theme.Blend(Theme.White,tint,.32f):Theme.Pearl;}
            if(!Enabled&&feedback.IsEmpty&&!selected){top=Theme.Ivory;bottom=Theme.Pearl;}
            using(var p=Theme.Round(surface,10*scale)) {
                using(var fill=new LinearGradientBrush(surface,top,bottom,LinearGradientMode.Vertical))g.FillPath(fill,p);
                if(!tint.IsEmpty&&(Enabled||!feedback.IsEmpty)){
                    using(var gradient=new LinearGradientBrush(surface,Theme.Blend(Color.White,tint,.52f),tint,32f))using(var edge=new Pen(gradient,1.4f*scale))g.DrawPath(edge,p);
                }else using(var edge=new Pen(Color.FromArgb(223,211,199),scale))g.DrawPath(edge,p);
            }
            if(selected){
                if(Navigation){using(var p=Theme.Round(new RectangleF(9*scale,surface.Top+10*scale,3*scale,surface.Height-20*scale),1.5f*scale))using(var marker=new SolidBrush(Theme.OrangeEnd))g.FillPath(marker,p);}
                else using(var marker=new SolidBrush(Theme.OrangeEnd))g.FillEllipse(marker,10*scale,surface.Top+surface.Height/2-2*scale,4*scale,4*scale);
            }
            var textBounds=Rectangle.Round(surface);textBounds.Inflate(-(int)(8*scale),0);
            if(Navigation)textBounds.X+=(int)(14*scale);
            if(Dropdown){
                textBounds.Width-=(int)(24*scale);float x=surface.Right-19*scale,y=surface.Top+surface.Height/2;
                using(var pen=new Pen(Theme.OrangeInk,2.2f*scale)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLines(pen,new[]{new PointF(x-5*scale,y-2*scale),new PointF(x,y+3*scale),new PointF(x+5*scale,y-2*scale)});}
            }
            using(var selectedFont=selected?new Font(Font,FontStyle.Bold):null)TextRenderer.DrawText(g,Text,selectedFont??Font,textBounds,Enabled?ForeColor:Theme.Muted,(Navigation?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
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
