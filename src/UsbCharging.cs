using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GTBook {
    public static class UsbCharging {
        public static Reply Apply(OemProtocol oem,bool enabled,Action<int> delay){
            oem.SetUSBCharging(enabled);uint actual=uint.MaxValue;
            for(int attempt=0;attempt<8;attempt++){
                delay(100);actual=oem.GetUSBCharging();
                if(actual==(enabled?1u:0u)){
                    var reply=new Reply{Ok=true,Message="USB charging firmware setting confirmed "+(enabled?"ON.":"OFF.")};
                    reply.Data["usb"]=enabled?"On":"Off";return reply;
                }
            }
            throw new IOException("USB charging was not confirmed by firmware (read "+actual+"). Refresh and retry.");
        }
        public static Dictionary<string,object> VerifyRequests(){
            if(!Identity.Read().Supported)throw new IOException("No reviewed USB charging profile for this device.");
            string conflict=Hardware.Conflict();if(conflict.Length>0)throw new IOException(conflict);
            var report=new Dictionary<string,object>();var cases=new List<object>();report["cases"]=cases;
            report["shutdown_port_power_measured"]=false;
            using(var lease=new HardwareLease()){
                if(!lease.Acquire(3000))throw new IOException("Hardware is busy.");
                using(var oem=new OemProtocol()){
                    uint original=oem.GetUSBCharging();if(original>1)throw new IOException("Cannot safely restore unknown USB charging setting: "+original);
                    report["original_on"]=original==1;bool ok=true;
                    try{foreach(bool enabled in new[]{false,true}){
                        var reply=Apply(oem,enabled,Thread.Sleep);cases.Add(new{enabled=enabled,confirmed=reply.Ok});
                    }}catch(Exception error){ok=false;report["error"]=error.Message;}
                    finally{try{Apply(oem,original==1,Thread.Sleep);report["original_restored"]=oem.GetUSBCharging()==original;}catch(Exception error){ok=false;report["restore_error"]=error.Message;}}
                    report["ok"]=ok&&report.ContainsKey("original_restored")&&Convert.ToBoolean(report["original_restored"]);
                }
            }return report;
        }
    }
}
