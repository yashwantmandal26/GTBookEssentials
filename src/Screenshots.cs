using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace GTBook {
    public sealed class ScreenshotKeys {
        public struct Decision {public bool Capture,Consume;}
        public bool InterceptionEnabled { get; set; }
        public bool OemInsertRemapEnabled { get; set; }
        public ScreenshotKeys(bool interceptionEnabled = true, bool oemInsertRemapEnabled = false) {
            InterceptionEnabled = interceptionEnabled;
            OemInsertRemapEnabled = oemInsertRemapEnabled;
        }
        readonly HashSet<int> held=new HashSet<int>(),modified=new HashSet<int>();long last=-1000;
        public Decision Feed(int key,bool extended,bool down,bool modifiers,bool injected,long milliseconds){
            var result=new Decision();
            if(!InterceptionEnabled||injected)return result;
            bool isPrtScn=(key==0x2c);
            bool isOemInsert=(key==0x2d&&extended&&OemInsertRemapEnabled);
            if(!isPrtScn&&!isOemInsert)return result;
            if(down){
                if(modifiers){modified.Add(key);return result;}
                result.Consume=true;bool first=held.Add(key);if(first&&milliseconds-last>=250){last=milliseconds;result.Capture=true;}return result;
            }
            if(modified.Remove(key))return result;
            bool wasHeld=held.Remove(key);if(modifiers&&!wasHeld)return result;
            result.Consume=wasHeld||!modifiers;
            // Also accept devices that report Print Screen only on release.
            if(!wasHeld&&!modifiers&&milliseconds-last>=250){last=milliseconds;result.Capture=true;}
            return result;
        }
    }
    public sealed class ScreenshotResult : IDisposable {
        public string Path;
        public Bitmap Image;
        public void Dispose(){if(Image!=null){Image.Dispose();Image=null;}}
    }
    public static class Screenshots {
        public static string Folder {get {string pictures=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);if(string.IsNullOrWhiteSpace(pictures))pictures=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Pictures");return System.IO.Path.Combine(pictures,"GTBOOK Essentials Screenshots");}}
        public static void CopyDesktop(Graphics graphics,Rectangle bounds){graphics.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);}
        public static ScreenshotResult Capture(){return Capture(SystemInformation.VirtualScreen,Folder,CopyDesktop);}
        public static ScreenshotResult Capture(Rectangle bounds,string folder,Action<Graphics,Rectangle> copy){
            if(bounds.Width<=0||bounds.Height<=0)throw new ArgumentException("The desktop has no drawable screen area.");
            Directory.CreateDirectory(folder);Bitmap bitmap=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb);
            string path=System.IO.Path.Combine(folder,"Screenshot "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".png");
            try {using(var g=Graphics.FromImage(bitmap))copy(g,bounds);using(var file=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None))bitmap.Save(file,ImageFormat.Png);File.Move(path+".tmp",path);return new ScreenshotResult{Image=bitmap,Path=path};}
            catch{bitmap.Dispose();throw;}
        }
        public static bool CopyToClipboard(Bitmap bitmap){try{var data=new DataObject();data.SetData(DataFormats.Bitmap,true,bitmap);Clipboard.SetDataObject(data,true,2,20);return true;}catch(System.Runtime.InteropServices.ExternalException){return false;}}
    }
}
