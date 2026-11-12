using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GTBook {
    // Retarget from the current palette so rapid clicks never restart at an endpoint.
    public sealed class ThemeMotion : IDisposable {
        readonly Timer timer=new Timer{Interval=16};
        readonly Stopwatch clock=new Stopwatch();
        readonly Func<IEnumerable<Control>> roots;
        float origin;public bool DarkTarget {get;private set;}
        public bool Running {get{return timer.Enabled;}}
        public ThemeMotion(Func<IEnumerable<Control>> controls){roots=controls;DarkTarget=Theme.Depth>=.5f;timer.Tick+=(s,e)=>Advance(clock.Elapsed.TotalMilliseconds);}
        public static float Position(float start,bool dark,double milliseconds){float x=(float)Math.Max(0,Math.Min(1,milliseconds/320));x=x*x*(3-2*x);return start+((dark?1:0)-start)*x;}
        public void Toggle(){origin=Theme.Depth;DarkTarget=!DarkTarget;clock.Restart();timer.Start();}
        public void Advance(double milliseconds){var previous=Theme.Palette;Theme.SetDepth(Position(origin,DarkTarget,milliseconds));Theme.Apply(roots(),previous);if(milliseconds>=320){timer.Stop();clock.Stop();}}
        public void Dispose(){timer.Stop();timer.Dispose();clock.Stop();}
    }
    public sealed class ThemeSwitch : Control {
        public ThemeSwitch(){DoubleBuffered=true;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);TabStop=true;Cursor=Cursors.Hand;BackColor=Color.Transparent;AccessibleName="Dark theme";AccessibleDescription="Switch light and dark appearance with one click.";AccessibleRole=AccessibleRole.CheckButton;}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){OnClick(EventArgs.Empty);e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
        protected override void OnPaint(PaintEventArgs e){
            Theme.PaintUnder(this,e.Graphics);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96f;
            var face=new RectangleF(s,s,Width-3*s,Height-5*s);
            using(var path=Theme.Round(new RectangleF(face.X,face.Y+2*s,face.Width,face.Height),10*s))using(var brush=new SolidBrush(Theme.Shadow))g.FillPath(brush,path);
            using(var path=Theme.Round(face,10*s)){
                using(var fill=new LinearGradientBrush(face,Theme.White,Theme.Pearl,90f))g.FillPath(fill,path);
                using(var pen=new Pen(Theme.Edge,s))g.DrawPath(pen,path);
            }
            var track=new RectangleF(Width-57*s,(Height-26*s)/2,46*s,23*s);
            TextRenderer.DrawText(g,"Dark mode",Font,new Rectangle((int)(10*s),0,Math.Max(20,(int)(track.Left-14*s)),Height-(int)(3*s)),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
            using(var path=Theme.Round(track,12*s))using(var fill=new SolidBrush(Theme.Blend(Theme.Edge,Theme.OrangeEnd,Theme.Depth)))g.FillPath(fill,path);
            var knob=new RectangleF(track.Left+3*s+23*s*Theme.Depth,track.Top+3*s,17*s,17*s);
            using(var path=Theme.Round(new RectangleF(knob.X,knob.Y+s,knob.Width,knob.Height),9*s))using(var shadow=new SolidBrush(Color.FromArgb(100,Color.Black)))g.FillPath(shadow,path);
            using(var path=Theme.Round(knob,9*s))using(var fill=new LinearGradientBrush(knob,Color.White,Color.FromArgb(210,220,236),90f))g.FillPath(fill,path);
            if(Focused&&ShowFocusCues){var focus=Rectangle.Round(face);focus.Inflate(-4,-4);ControlPaint.DrawFocusRectangle(g,focus,Theme.Ink,Theme.White);}
        }
    }
}
