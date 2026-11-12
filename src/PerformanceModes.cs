using System;
using System.IO;
using System.Threading;

namespace GTBook {
    public static class PerformanceModes {
        public static readonly string[] Names={"Quiet","Balanced","Performance","Turbo"};
        public static int Index(string name){return Array.IndexOf(Names,name);}
        public static Reply Apply(OemProtocol oem,int mode,Action<int> delay){
            if(mode<0||mode>3)throw new ArgumentException("Unknown performance mode.");
            int ecMode=mode==3?2:mode;
            oem.SetPerformanceMode(ecMode);uint actual=255;
            for(int i=0;i<8;i++){if(i>0)delay(100);actual=oem.GetPerformanceMode();if(actual==ecMode){
                if(mode==3){try{oem.SetFanFullMode(true);}catch{}}
                var result=new Reply{Ok=true,Message="Power mode confirmed · "+Names[mode]};result.Data["mode"]=Names[mode];
                if(mode==3)result.Data["cooling"]="Full fan override";
                return result;
            }}
            throw new IOException("Power mode read-back did not match. Current raw value: "+actual+". Refresh before retrying.");
        }
    }
    public sealed class PerformanceHoldGesture {
        public const int TurboHoldMilliseconds = 1500;
        long downAt;
        bool holding, triggered;
        public bool IsHolding { get { return holding; } }
        public bool TriggeredTurbo { get { return triggered; } }
        public void Begin(long nowMs) { holding = true; triggered = false; downAt = nowMs; }
        public float Progress(long nowMs) {
            if (!holding || triggered) return 0f;
            return Math.Max(0f, Math.Min(1f, (nowMs - downAt) / (float)TurboHoldMilliseconds));
        }
        public bool Poll(long nowMs) {
            if (holding && !triggered && nowMs - downAt >= TurboHoldMilliseconds) {
                triggered = true;
                holding = false;
                return true;
            }
            return false;
        }
        public int Release(long nowMs) {
            if (!holding) return -1;
            holding = false;
            if (!triggered && nowMs - downAt >= TurboHoldMilliseconds) { triggered = true; return 3; }
            return triggered ? -1 : 2;
        }
        public void Cancel() { holding = false; triggered = false; }
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
            OemProtocol oem=null;int failures=0,interval=60,conflictCounter=0;
            try{using(var lease=new HardwareLease())while(!stop.WaitOne(interval)){
                if(++conflictCounter>=40){conflictCounter=0;try{if(Hardware.Conflict().Length>0){interval=3000;continue;}}catch{interval=5000;continue;}}
                if(!idle()||!lease.Acquire(40))continue;
                int observed=-1;bool notify=false;
                try{
                    if(disposed)break;if(oem==null)oem=new OemProtocol();
                    observed=(int)oem.GetPerformanceMode();notify=state.Observe(observed);
                    if(!notify&&observed>=0&&observed<3&&observed!=state.Current&&!stop.WaitOne(25))notify=state.Observe((int)oem.GetPerformanceMode());
                    failures=0;interval=60;
                }catch{failures=Math.Min(5,failures+1);interval=Math.Min(30000,60*(1<<failures));if(oem!=null){oem.Dispose();oem=null;}}
                finally{lease.Release();}
                if(notify&&!disposed)changed(state.Current);
            }}catch(Exception e){Store.Log("Performance key monitor: "+e.Message);}finally{disposed=true;if(oem!=null)oem.Dispose();stop.Dispose();}
        }
        public void Dispose(){if(disposed)return;disposed=true;try{stop.Set();}catch(ObjectDisposedException){}}
    }
}
