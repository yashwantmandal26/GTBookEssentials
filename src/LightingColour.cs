using System;
using System.Drawing;

namespace GTBook {
    // A single colour model for editor swatches, previews and device frames.
    public static class LightingColour {
        static double Unit(double value){return Math.Max(0,Math.Min(1,value));}
        public static Color Hsv(double hue,double saturation,double value){
            hue=(hue%1+1)%1;saturation=Unit(saturation);value=Unit(value);
            double sector=hue*6,f=sector-Math.Floor(sector),p=value*(1-saturation),q=value*(1-saturation*f),t=value*(1-saturation*(1-f));
            double r,g,b;switch((int)sector){case 0:r=value;g=t;b=p;break;case 1:r=q;g=value;b=p;break;case 2:r=p;g=value;b=t;break;case 3:r=p;g=q;b=value;break;case 4:r=t;g=p;b=value;break;default:r=value;g=p;b=q;break;}
            return Color.FromArgb((int)Math.Round(r*255),(int)Math.Round(g*255),(int)Math.Round(b*255));
        }
        public static Color Vivid(Color color){
            // Keep deliberately neutral white, grey and black available.
            if(color.R==color.G&&color.G==color.B)return color;
            return Hsv(color.GetHue()/360.0,1,1);
        }
        public static string Hex(Color color){return "#"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2");}
        public static string VividHex(string hex){return Hex(Vivid(ColorTranslator.FromHtml(hex)));}
        static double Linear(byte channel){double c=channel/255.0;return c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);}
        public static Color TextOn(Color color){
            double luminance=.2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);
            double ink=.2126*Linear(Theme.Ink.R)+.7152*Linear(Theme.Ink.G)+.0722*Linear(Theme.Ink.B);
            return (luminance+.05)/(ink+.05)>1.05/(luminance+.05)?Theme.Ink:Color.White;
        }
        public static Color Dim(Color color,double intensity){
            intensity=Unit(intensity);return Color.FromArgb((int)Math.Round(color.R*intensity),(int)Math.Round(color.G*intensity),(int)Math.Round(color.B*intensity));
        }
        public static Color MixHue(Color a,Color b,double amount){
            amount=Unit(amount);double ah=a.GetHue()/360.0,bh=b.GetHue()/360.0;
            double av=Math.Max(a.R,Math.Max(a.G,a.B))/255.0,bv=Math.Max(b.R,Math.Max(b.G,b.B))/255.0;
            // Saturation is HSV saturation, rather than Color.GetSaturation's HSL value.
            double sa=av==0?0:1-Math.Min(a.R,Math.Min(a.G,a.B))/(255*av),sb=bv==0?0:1-Math.Min(b.R,Math.Min(b.G,b.B))/(255*bv);
            if(sa==0)ah=bh;if(sb==0)bh=ah;double delta=(bh-ah+1.5)%1-.5;
            return Hsv(ah+delta*amount,sa+(sb-sa)*amount,av+(bv-av)*amount);
        }
    }
}
