using System;
using System.IO;
using System.Threading;

namespace GTBook {
    public static class PerformanceModes {
        public static readonly string[] Names={"Quiet","Balanced","Gaming"};
        public static int Index(string name){return Array.IndexOf(Names,name);}
        public static Reply Apply(OemProtocol oem,int mode,Action<int> delay){
            if(mode<0||mode>2)throw new ArgumentException("Unknown performance mode.");
            oem.SetPerformanceMode(mode);uint actual=255;
            for(int i=0;i<8;i++){delay(100);actual=oem.GetPerformanceMode();if(actual==mode){var result=new Reply{Ok=true,Message="Power mode confirmed · "+Names[mode]};result.Data["mode"]=Names[mode];return result;}}
            throw new IOException("Power mode read-back did not match. Current raw value: "+actual+". Refresh before retrying.");
        }
    }
    public sealed class ModeObservation {
        readonly object gate=new object();int current,candidate=-1;
        public ModeObservation(int initial){current=initial;}
        public int Current {get{lock(gate)return current;}}
        public void Confirm(int mode){lock(gate){current=mode;candidate=-1;}}
        public bool Observe(int mode){lock(gate){
            if(mode<0||mode>2){candidate=-1;return false;}
            if(mode==current){candidate=-1;return false;}
            if(candidate!=mode){candidate=mode;return false;}
            current=mode;candidate=-1;return true;
        }}
    }
    // The physical mode key can change EC state without sending a Windows key.
    // Watch only that register in the tray, rather than waking all sensor workers.
    public sealed class ModeObserver : IDisposable {
        readonly ModeObservation state;readonly ManualResetEvent stop=new ManualResetEvent(false);readonly Func<bool> idle;readonly Action<int> changed;
        volatile bool disposed;
        public ModeObserver(int initial,Func<bool> idle,Action<int> changed){state=new ModeObservation(initial);this.idle=idle;this.changed=changed;var thread=new Thread(Watch){IsBackground=true,Name="GTBOOK performance key"};thread.Start();}
        public void Confirm(int mode){state.Confirm(mode);}
        public bool IsCurrent(int mode){return !disposed&&state.Current==mode;}
        void Watch(){
            OemProtocol oem=null;int failures=0,interval=1500;
            try{using(var lease=new HardwareLease())while(!stop.WaitOne(interval)){
                try{if(Hardware.Conflict().Length>0){interval=3000;continue;}}catch{interval=5000;continue;}
                if(!idle()||!lease.Acquire(100))continue;
                int observed=-1;bool notify=false;
                try{
                    if(disposed)break;if(oem==null)oem=new OemProtocol();
                    observed=(int)oem.GetPerformanceMode();notify=state.Observe(observed);
                    if(!notify&&observed>=0&&observed<3&&observed!=state.Current&&!stop.WaitOne(60))notify=state.Observe((int)oem.GetPerformanceMode());
                    failures=0;interval=1500;
                }catch{failures=Math.Min(5,failures+1);interval=Math.Min(30000,1500*(1<<failures));if(oem!=null){oem.Dispose();oem=null;}}
                finally{lease.Release();}
                if(notify&&!disposed)changed(state.Current);
            }}catch(Exception e){Store.Log("Performance key monitor: "+e.Message);}finally{disposed=true;if(oem!=null)oem.Dispose();stop.Dispose();}
        }
        public void Dispose(){if(disposed)return;disposed=true;try{stop.Set();}catch(ObjectDisposedException){}}
    }
}
