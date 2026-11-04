using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace GTBook {
    // Reuse one isolated helper for a key-repeat burst; drop it after five idle seconds.
    public sealed class BrightnessBroker : IDisposable {
        readonly object gate=new object();readonly Timer idle;Process process;DateTime lastUsed;bool disposed;
        public BrightnessBroker(){idle=new Timer(Cleanup,null,Timeout.Infinite,Timeout.Infinite);}
        public int ProcessId {get{lock(gate)return process==null?0:process.Id;}}
        public Reply Run(int step){lock(gate){
            if(disposed)return Reply.Fail("Brightness helper is closing.");
            try{
                Protocol.Validate(new Request{Action="screen-brightness",Step=step});
                if(process==null||process.HasExited){Stop();process=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--brightness-worker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true});}
                process.StandardInput.WriteLine(step.ToString(System.Globalization.CultureInfo.InvariantCulture));process.StandardInput.Flush();
                var read=process.StandardOutput.ReadLineAsync();if(!read.Wait(5000)){Stop();return Reply.Fail("Brightness helper timed out. Read back the display brightness before retrying.");}
                string json=read.Result;if(string.IsNullOrEmpty(json))throw new IOException("Brightness helper exited before replying.");
                return new JavaScriptSerializer().Deserialize<Reply>(json)??Reply.Fail("Brightness helper returned no result.");
            }catch(Exception e){Stop();return Reply.Fail("Brightness could not complete: "+e.Message);}
            finally{lastUsed=DateTime.UtcNow;if(!disposed)idle.Change(5000,Timeout.Infinite);}
        }}
        void Cleanup(object unused){lock(gate){if(disposed)return;double remaining=5000-(DateTime.UtcNow-lastUsed).TotalMilliseconds;if(remaining>0){idle.Change((int)Math.Ceiling(remaining),Timeout.Infinite);return;}Stop();}}
        void Stop(){if(process==null)return;try{if(!process.HasExited){process.StandardInput.Close();if(!process.WaitForExit(300)){process.Kill();process.WaitForExit(1000);}}}catch{}finally{process.Dispose();process=null;}}
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;idle.Dispose();Stop();}}
        public static int Serve(TextReader input,TextWriter output){
            string line;var json=new JavaScriptSerializer();
            while((line=input.ReadLine())!=null){Reply reply;try{int step;if(!int.TryParse(line,out step))throw new ArgumentException("Invalid brightness delta.");Protocol.Validate(new Request{Action="screen-brightness",Step=step});reply=DisplayBrightness.Change(step);}catch(Exception e){reply=Reply.Fail(e.Message);}output.WriteLine(json.Serialize(reply));output.Flush();}return 0;
        }
    }
}
