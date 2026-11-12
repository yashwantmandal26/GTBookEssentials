using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;

namespace GTBook {
    public static class OemHandoff {
        public const string ReviewedHash="82EA2D022281D5F69C4B2CB7BB4751A3C0404F46B95EB3B1E7BAFEF5D9D4DBC6";
        public static bool Matches(string path,string product,string originalFile,string hash){
            if(!string.Equals(Path.GetFileName(path),"ControlCenter.exe",StringComparison.OrdinalIgnoreCase))return false;
            if(string.Equals(hash,ReviewedHash,StringComparison.OrdinalIgnoreCase))return true;
            bool vendorPath=path.IndexOf("Infinix",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("Official_ControlCenter",StringComparison.OrdinalIgnoreCase)>=0;
            return vendorPath&&product=="ControlCenter"&&string.Equals(originalFile,"ControlCenter.dll",StringComparison.OrdinalIgnoreCase);
        }
        internal static bool GracefulThenStop(Action close,Func<int,bool> wait,Action terminate){close();if(wait(700))return true;terminate();return wait(1500);}
        public static string CloseOfficial(){
            if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))return "Automatic OEM hand-off requires administrator rights.";
            // On unreviewed hardware, keep the OEM utility available instead of disabling its controls.
            if(!Identity.Read().Supported)return "";
            int session;using(var self=Process.GetCurrentProcess())session=self.SessionId;
            foreach(var candidate in Process.GetProcessesByName("ControlCenter"))using(candidate){
                try{
                    if(candidate.HasExited||candidate.SessionId!=session)continue;
                    string path=candidate.MainModule.FileName;var version=FileVersionInfo.GetVersionInfo(path);string hash="";
                    if(!Matches(path,version.ProductName,version.OriginalFilename,hash))using(var sha=SHA256.Create())using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
                    if(!Matches(path,version.ProductName,version.OriginalFilename,hash))return "An unrecognized ControlCenter is running. It was left open; close it before using hardware controls.";
                    long started=candidate.StartTime.ToUniversalTime().Ticks;
                    if(!GracefulThenStop(()=>{candidate.CloseMainWindow();},candidate.WaitForExit,()=>{
                        if(candidate.StartTime.ToUniversalTime().Ticks!=started)throw new IOException("Official process identity changed during hand-off.");
                        candidate.Kill();
                    }))return "Official Control Center did not exit. Close it from the tray and retry.";
                    Store.Log("Official Control Center closed for exclusive device control.");
                }catch(InvalidOperationException){}catch(Exception e){return "Official Control Center could not be closed: "+e.Message;}
            }return "";
        }
    }
    public static class ReadingRefresh {
        public static int Interval(bool battery){return battery?6000:3000;}
        public static bool ShouldPoll(bool visible,bool minimized,bool overview,bool busy){return visible&&!minimized&&overview&&!busy;}
    }
}
