using System;
using System.Drawing;

namespace GTBook {
    // Deterministic timing: a visible popup is updated in place, never re-shown at 1% opacity.
    public sealed class PopupMotion {
        double appeared,deadline,fromOpacity,fromPercent,targetPercent=-1;
        static double Ease(double value){value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value);}
        public double Opacity(double milliseconds){return milliseconds>=deadline?1-Ease((milliseconds-deadline)/220):fromOpacity+(1-fromOpacity)*Ease((milliseconds-appeared)/140);}
        public double Percent(double milliseconds){return targetPercent<0?-1:fromPercent+(targetPercent-fromPercent)*Ease((milliseconds-appeared)/160);}
        public bool Expired(double milliseconds){return milliseconds>=deadline+220;}
        public int NextTick(double milliseconds){return milliseconds<appeared+160||milliseconds>=deadline?16:Math.Max(16,(int)Math.Ceiling(deadline-milliseconds));}
        public void Hold(double milliseconds,int lifetime=10000){
            if(milliseconds<deadline)deadline=milliseconds+lifetime;
            else Update(milliseconds,true,(int)targetPercent,lifetime);
        }
        public void Update(double milliseconds,bool visible,int percent,int lifetime=2400){
            double alpha=visible?Opacity(milliseconds):1;
            double before=visible?Percent(milliseconds):percent;
            appeared=milliseconds;deadline=milliseconds+lifetime;fromOpacity=alpha;
            fromPercent=before<0?percent:before;targetPercent=percent;
        }
    }
    public static class PopupLayout {
        public static RectangleF KeyboardTitle(Size size,float scale){return new RectangleF(91*scale,20*scale,Math.Max(1,size.Width-113*scale),54*scale);}
        public static RectangleF Title(Size size,float scale,bool badge){return new RectangleF(89*scale,20*scale,Math.Max(1,size.Width-(badge?194:110)*scale),31*scale);}
        public static RectangleF Badge(Size size,float scale){return new RectangleF(size.Width-84*scale,23*scale,64*scale,25*scale);}
        public static Point BottomCenter(Rectangle area,Size popup,int bottomGap){
            int x=area.Left+(area.Width-popup.Width)/2,y=area.Bottom-popup.Height-bottomGap;
            return new Point(Math.Max(area.Left,Math.Min(x,area.Right-popup.Width)),Math.Max(area.Top,Math.Min(y,area.Bottom-popup.Height)));
        }
    }
}
