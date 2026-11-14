using System;
using System.IO;
using System.Collections.Generic;
using System.Management;

namespace GTBook {
    public static class GpuModes {
        // OEM BYDWmi2.SetGPUMode writes these exact values to EC register C0.
        public static readonly string[] Names={"dGPU only","Hybrid","iGPU only"};
        public static int Raw(int selection){if(selection<0||selection>2)throw new ArgumentException("Choose a supported GPU mode.");return selection+1;}
        public static string Name(uint raw){return raw>=1&&raw<=3?Names[raw-1]:"Unknown ("+raw+")";}
        public static int DetectPnpMode(){
            try{
                using(var searcher=new ManagementObjectSearcher("SELECT Name, Status FROM Win32_VideoController"))
                using(var rows=searcher.Get()){
                    bool hasIntel=false,hasNvidia=false;
                    foreach(ManagementObject row in rows)using(row){
                        string name=Convert.ToString(row["Name"]).ToLowerInvariant();
                        string status=Convert.ToString(row["Status"]);
                        if(status!="OK")continue;
                        if(name.Contains("intel")||name.Contains("amd")||name.Contains("radeon"))hasIntel=true;
                        if(name.Contains("nvidia")||name.Contains("geforce")||name.Contains("rtx")||name.Contains("gtx"))hasNvidia=true;
                    }
                    if(hasIntel&&hasNvidia)return 2;
                    if(hasNvidia)return 1;
                    if(hasIntel)return 3;
                }
            }catch{}
            return 0;
        }
        public static Reply Apply(OemProtocol oem,int selection,Action<int> delay){
            int target=Raw(selection);oem.SetGPUMode(target);uint actual=0;
            for(int i=0;i<8;i++){delay(100);actual=oem.GetGPUMode();if(actual==target){
                var reply=new Reply{Ok=true,Message=Names[selection]+" request saved and verified. Restart Windows to activate it."};
                reply.Data["gpu-mode-requested"]=Names[selection];reply.Data["gpu-restart-required"]="True";reply.Data["gpu-raw"]=target.ToString();return reply;
            }}throw new IOException("GPU firmware request was not confirmed (read "+actual+"). Refresh before retrying; active GPU mode was not verified.");
        }
        public static Dictionary<string,object> VerifyRequests(){
            var id=Identity.Read();if(!id.Supported)throw new IOException("No reviewed GPU profile for this device.");
            string conflict=Hardware.Conflict();if(conflict.Length>0)throw new IOException(conflict);
            var report=new Dictionary<string,object>();var cases=new List<object>();report["cases"]=cases;
            using(var lease=new HardwareLease()){
                if(!lease.Acquire(3000))throw new IOException("Another hardware command is busy.");
                using(var oem=new OemProtocol()){
                    uint original=oem.GetGPUMode();if(original<1||original>3)throw new IOException("Cannot safely restore unknown GPU firmware value: "+original);
                    report["original_raw"]=original;bool ok=true;
                    try{for(int i=0;i<3;i++){var reply=Apply(oem,i,System.Threading.Thread.Sleep);cases.Add(new {mode=Names[i],raw=i+1,confirmed=reply.Ok});}}
                    catch(Exception e){ok=false;report["error"]=e.Message;}
                    finally{try{Apply(oem,(int)original-1,System.Threading.Thread.Sleep);report["restored_raw"]=oem.GetGPUMode();report["original_restored"]=Convert.ToUInt32(report["restored_raw"])==original;}catch(Exception e){ok=false;report["restore_error"]=e.Message;}}
                    report["ok"]=ok&&report.ContainsKey("original_restored")&&Convert.ToBoolean(report["original_restored"]);report["active_mux_after_reboot_verified"]=false;
                }
            }return report;
        }
    }
}
