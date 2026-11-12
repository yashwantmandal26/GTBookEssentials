using System;
using System.Drawing;
using System.Reflection;

[assembly: AssemblyTitle("GTBOOK Essentials")]
[assembly: AssemblyProduct("GTBOOK Essentials")]
[assembly: AssemblyDescription("Keyboard lighting, cooling and laptop essentials")]
[assembly: AssemblyVersion("2.1.1.0")]
[assembly: AssemblyFileVersion("2.1.1.0")]
[assembly: AssemblyInformationalVersion("2.1.1")]

namespace GTBook {
    public static class AppIdentity {
        public const string Name="GTBOOK Essentials";
        public static Bitmap LoadLogo(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("GTBookEssentials.Logo")){if(stream==null)throw new InvalidOperationException("The generated app logo is missing.");using(var image=Image.FromStream(stream))return new Bitmap(image);}}
        public static Icon LoadIcon(int size){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("GTBookEssentials.Icon")){if(stream==null)return (Icon)SystemIcons.Application.Clone();using(var icon=new Icon(stream,new Size(size,size)))return (Icon)icon.Clone();}}
    }
}
