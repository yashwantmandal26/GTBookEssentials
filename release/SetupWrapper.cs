using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

namespace GTBook.Setup {
    internal static class Program {
        [STAThread] static int Main(string[] args){
            bool checkOnly=args.Length==1&&args[0]=="--check-only";
            Application.EnableVisualStyles();
            string parent=Path.Combine(Path.GetTempPath(),"GTBOOK-Essentials-Setup");
            string work=Path.Combine(parent,Guid.NewGuid().ToString("N"));
            try{
                Directory.CreateDirectory(work);string bundle=Path.Combine(work,"package.zip");
                using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("GTBook.Package")){
                    if(input==null)throw new IOException("The installation package is missing.");
                    using(var output=File.Create(bundle))input.CopyTo(output);
                }
                ZipFile.ExtractToDirectory(bundle,work);
                string[] folders=Directory.GetDirectories(work);
                if(folders.Length!=1)throw new IOException("Unexpected installation package layout.");
                string script=Path.Combine(folders[0],"Install.ps1");
                if(!File.Exists(script))throw new IOException("The installer could not be found.");
                using(var process=Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\""+(checkOnly?" -CheckOnly":"")){
                    UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folders[0]
                })){process.WaitForExit();return process.ExitCode;}
            }catch(Exception error){MessageBox.Show(error.Message,"GTBOOK Essentials setup",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
            finally{
                // Only our unique extraction directory; never a path from the ZIP.
                try{string resolved=Path.GetFullPath(work),allowed=Path.GetFullPath(parent)+Path.DirectorySeparatorChar;
                    if(resolved.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(resolved))Directory.Delete(resolved,true);
                }catch{}
            }
        }
    }
}
