using System;
using System.IO;
using System.Threading;

namespace GTBook {
    public static class PerformanceModes {
        public static readonly string[] Names={"Quiet","Balanced","Performance","Turbo"};
        public static int Index(string name){return Array.IndexOf(Names,name);}
        public static Request SwitchRequest(int mode,int confirmedMode,string snapshotMode){
            bool wasTurbo=confirmedMode==3||string.Equals(snapshotMode,"Turbo",StringComparison.Ordinal);
            return new Request{Action="mode",Mode=mode,RestoreAutoCooling=mode!=3&&wasTurbo};
        }
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
        internal static Reply ApplyModeWithCooling(OemProtocol oem,int mode,bool restoreAutoCooling,Action<int> delay,Action<string> log=null){
            var confirmed=Apply(oem,mode,delay);
            var answer=new Reply{Ok=true,Message=confirmed.Message};
            answer.Data["mode"]=confirmed.Data["mode"];
            if(mode==3){
                bool fullFan=false;
                for(int attempt=0;attempt<4;attempt++){
                    if(attempt>0)delay(50);
                    try{if(oem.GetFanFullMode()==1u){fullFan=true;break;}}catch(Exception fanReadEx){if(attempt==3&&log!=null)log("Turbo fan readback: "+fanReadEx.Message);}
                }
                if(fullFan){
                    answer.Ok=true;
                    answer.Data["cooling"]="Full fan override";
                }else{
                    answer.Ok=false;
                    answer.Data["mode"]="Performance";
                    answer.Data["cooling"]="Firmware auto";
                    answer.Data["partial"]="True";
                    answer.Data["requested-mode"]="Turbo";
                    answer.Message="Turbo fans could not be confirmed. Performance mode active with automatic cooling.";
                    if(log!=null)log("Turbo mode: firmware did not confirm Full fan override.");
                }
            }else if(restoreAutoCooling){
                bool fanRestoreWriteOk=false;string restoreError=null;
                try{
                    oem.SetFanFullMode(false);
                    fanRestoreWriteOk=true;
                }catch(Exception fanEx){
                    restoreError=fanEx.Message;
                    if(log!=null)log("Mode exit restore auto cooling: "+fanEx.Message);
                }
                bool autoRestored=false;
                if(fanRestoreWriteOk){
                    for(int attempt=0;attempt<4;attempt++){
                        if(attempt>0)delay(50);
                        try{
                            if(oem.GetFanFullMode()==0u){autoRestored=true;break;}
                        }catch(Exception fanReadEx){
                            if(attempt==3&&log!=null)log("Auto cooling readback: "+fanReadEx.Message);
                        }
                    }
                }
                answer.Data["cooling"]=autoRestored?"Firmware auto":"Full fan override";
                if(!autoRestored){
                    answer.Ok=false;
                    answer.Data["cooling-restore-failed"]="True";
                    answer.Message="Power mode confirmed · "+Names[mode]+", but automatic cooling could not be restored."+(restoreError!=null?" ("+restoreError+")":"");
                    if(log!=null)log("Mode exit: firmware did not confirm automatic cooling restore.");
                }
            }
            return answer;
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
        readonly Func<string> conflictChecker;readonly Func<OemProtocol> oemFactory;
        volatile bool disposed;
        public ModeObserver(int initial,Func<bool> idle,Action<int> changed,Func<string> conflictChecker=null,Func<OemProtocol> oemFactory=null){
            state=new ModeObservation(initial);this.idle=idle;this.changed=changed;
            this.conflictChecker=conflictChecker;this.oemFactory=oemFactory;
            var thread=new Thread(Watch){IsBackground=true,Name="GTBOOK performance key"};thread.Start();
        }
        public void Confirm(int mode){state.Confirm(mode);}
        public bool IsCurrent(int mode){return !disposed&&state.Current==mode;}
        void Watch(){
            OemProtocol oem=null;int failures=0,interval=60,conflictCounter=40;bool inConflict=false;
            try{using(var lease=new HardwareLease())while(!stop.WaitOne(interval)){
                bool checkNow=inConflict||(++conflictCounter>=40);
                if(checkNow){
                    conflictCounter=0;
                    bool conflictDetected=true;
                    try{
                        string c=conflictChecker!=null?conflictChecker():Hardware.Conflict();
                        conflictDetected=!string.IsNullOrEmpty(c);
                    }catch(Exception ex){
                        conflictDetected=true;
                        Store.Log("Performance key monitor conflict check failed: "+ex.Message);
                    }
                    if(conflictDetected){
                        inConflict=true;
                        if(oem!=null){oem.Dispose();oem=null;}
                        interval=250;
                        continue;
                    }else{
                        inConflict=false;
                        interval=60;
                    }
                }
                if(inConflict){interval=250;continue;}
                if(!idle()||!lease.Acquire(40))continue;
                int observed=-1;bool notify=false;
                try{
                    if(disposed)break;if(oem==null)oem=oemFactory!=null?oemFactory():new OemProtocol();
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
