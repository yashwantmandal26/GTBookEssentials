using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace GTBook {
    // Reuse one isolated helper for a key-repeat burst; drop it after fifteen idle seconds.
    public sealed class NativeKeyboardBroker : IDisposable {
        readonly object gate=new object();readonly Timer idle;readonly Func<Process> launch;Process process;DateTime lastUsed;bool disposed;
        public NativeKeyboardBroker(Func<Process> launch=null){this.launch=launch;idle=new Timer(Cleanup,null,Timeout.Infinite,Timeout.Infinite);}
        public static bool Supports(Request r){return r!=null&&(r.Action=="mode"||r.Action=="auto"||r.Action=="fan"||r.Action=="usb"||r.Action=="keyboard"&&!ExtraEffects.Contains(r.Effect));}
        public int ProcessId {get{lock(gate)return process==null?0:process.Id;}}
        public Reply Run(Request request){lock(gate){
            if(disposed)return Reply.Fail("Device helper is closing.");
            try{
                Protocol.Validate(request);if(!Supports(request))throw new ArgumentException("Unsupported device command.");
                if(process==null||process.HasExited){Stop();process=launch!=null?launch():Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--keyboard-worker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true});}
                process.StandardInput.WriteLine(new JavaScriptSerializer().Serialize(request));process.StandardInput.Flush();
                var read=process.StandardOutput.ReadLineAsync();if(!read.Wait(5000)){Stop();return Reply.Fail("Device helper timed out. Refresh device readings before retrying.");}
                string json=read.Result;if(string.IsNullOrEmpty(json))throw new IOException("Device helper exited before replying.");
                return new JavaScriptSerializer().Deserialize<Reply>(json)??Reply.Fail("Device helper returned no result.");
            }catch(Exception e){Stop();return Reply.Fail("Device command could not complete: "+e.Message);}
            finally{lastUsed=DateTime.UtcNow;if(!disposed)idle.Change(15000,Timeout.Infinite);}
        }}
        void Cleanup(object unused){lock(gate){if(disposed)return;double remaining=15000-(DateTime.UtcNow-lastUsed).TotalMilliseconds;if(remaining>0){idle.Change((int)Math.Ceiling(remaining),Timeout.Infinite);return;}Stop();}}
        void Stop(){if(process==null)return;try{if(!process.HasExited){process.StandardInput.Close();if(!process.WaitForExit(300)){process.Kill();process.WaitForExit(1000);}}}catch{}finally{process.Dispose();process=null;}}
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;idle.Dispose();Stop();}}
        public static int Serve(TextReader input,TextWriter output,Func<Request,Reply> execute=null){
            string line;var json=new JavaScriptSerializer();
            using(var devices=new NativeDevices()){
            while((line=input.ReadLine())!=null){Reply reply;
                try{
                    var request=json.Deserialize<Request>(line);Protocol.Validate(request);
                    if(!Supports(request))throw new ArgumentException("Unsupported device command.");
                    if(execute!=null)reply=execute(request);
                    else using(var lease=new HardwareLease()){
                        if(!lease.Acquire(3000))throw new IOException("Hardware is busy. Retry after the command finishes.");
                        reply=Hardware.Execute(request,devices);
                    }
                }catch(Exception error){devices.Dispose();reply=Reply.Fail(error.Message);}
                output.WriteLine(json.Serialize(reply));output.Flush();
            }return 0;}
        }
    }
}
