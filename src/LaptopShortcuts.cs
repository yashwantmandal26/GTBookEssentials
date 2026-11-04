using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GTBook {
    public static class DisplayBrightness {
        public static int Clamp(int current,int delta){return Math.Max(0,Math.Min(100,current+delta));}
        public static int Read(){using(var q=new ManagementObjectSearcher(@"root\wmi","SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True"))using(var rows=q.Get()){var monitor=rows.Cast<ManagementObject>().FirstOrDefault();if(monitor==null)throw new IOException("Internal-display brightness is unavailable.");return Convert.ToInt32(monitor["CurrentBrightness"]);}}
        public static Reply Change(int delta){
            var scope=new ManagementScope(@"\\.\root\wmi");scope.Connect();
            string instance;int before;
            using(var q=new ManagementObjectSearcher(scope,new ObjectQuery("SELECT InstanceName,CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True")))
            using(var rows=q.Get()) {var monitor=rows.Cast<ManagementObject>().FirstOrDefault();if(monitor==null)throw new IOException("Windows does not expose brightness control for the internal display.");instance=Convert.ToString(monitor["InstanceName"]);before=Convert.ToInt32(monitor["CurrentBrightness"]);}
            int target=Clamp(before,delta);bool found=false;
            using(var q=new ManagementObjectSearcher(scope,new ObjectQuery("SELECT * FROM WmiMonitorBrightnessMethods")))
            using(var rows=q.Get()) foreach(ManagementObject method in rows){
                if(Convert.ToString(method["InstanceName"])!=instance)continue;found=true;
                using(var args=method.GetMethodParameters("WmiSetBrightness")){args["Timeout"]=0u;args["Brightness"]=(byte)target;using(var result=method.InvokeMethod("WmiSetBrightness",args,new InvokeMethodOptions{Timeout=TimeSpan.FromSeconds(2)})){if(result!=null && result["ReturnValue"]!=null && Convert.ToUInt32(result["ReturnValue"])!=0)throw new IOException("Windows rejected the brightness request.");}}
                break;
            }
            if(!found)throw new IOException("The active display has no matching brightness method.");
            using(var q=new ManagementObjectSearcher(scope,new ObjectQuery("SELECT InstanceName,CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True")))
            for(int attempt=0;attempt<4;attempt++){
                using(var rows=q.Get())foreach(ManagementObject monitor in rows)if(Convert.ToString(monitor["InstanceName"])==instance&&Convert.ToInt32(monitor["CurrentBrightness"])==target){var reply=new Reply{Ok=true,Message="Screen brightness · "+target+"%"};reply.Data["display-brightness"]=target.ToString();return reply;}
                Thread.Sleep(50);
            }
            throw new IOException("Screen brightness was requested but not confirmed by Windows.");
        }
    }
    public sealed class ShortcutDecoder {
        public static int NextKeyboardBrightness(int current){return current>50?50:current>20?20:current>0?0:100;}
        public const int EffectHoldMilliseconds=800;
        readonly HashSet<int> down=new HashSet<int>();long f3At;bool f3Fired,f3Cancelled;
        public bool HoldingEffectKey {get{return down.Contains(0x72);}}
        public string Poll(bool modifiers,long milliseconds){
            if(!HoldingEffectKey)return null;
            if(modifiers)f3Cancelled=true;
            if(!f3Cancelled&&!f3Fired&&milliseconds-f3At>=EffectHoldMilliseconds){f3Fired=true;return "keyboard-effect";}
            return null;
        }
        public void CancelHold(){down.Remove(0x72);f3Fired=false;f3Cancelled=false;}
        public string Feed(int key,bool pressed,bool modifiers,bool injected,long milliseconds){
            if(injected)return null;
            if(!pressed){bool existed=down.Remove(key);if(key==0x72&&existed){bool ignored=modifiers||f3Fired||f3Cancelled;f3Fired=f3Cancelled=false;return ignored?null:milliseconds-f3At>=EffectHoldMilliseconds?"keyboard-effect":"keyboard-brightness";}return null;}
            if(modifiers){if(key==0x72&&HoldingEffectKey)f3Cancelled=true;return null;}
            bool first=down.Add(key);
            if(key==0x70)return "brightness-down";
            if(key==0x71)return "brightness-up";
            if(key==0x72){if(first){f3At=milliseconds;f3Fired=f3Cancelled=false;}return Poll(false,milliseconds);}
            if(first&&key>=0x7e&&key<=0x86)return "mode-"+((key-0x7e)%3);
            return null;
        }
        public void Reset(){down.Clear();f3Fired=f3Cancelled=false;}
    }
    public sealed class LaptopShortcuts : IDisposable {
        delegate IntPtr Hook(int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,Hook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        IntPtr handle;Hook callback;readonly ShortcutDecoder decoder=new ShortcutDecoder();readonly Stopwatch clock=Stopwatch.StartNew();long repeatedAt=-100;
        readonly HashSet<int> consumed=new HashSet<int>();
        readonly ScreenshotKeys screenshots=new ScreenshotKeys();
        readonly System.Windows.Forms.Timer holdTimer=new System.Windows.Forms.Timer();
        readonly Func<int,short> keyState;
        bool ModifiersDown(){return (keyState(0x10)&0x8000)!=0||(keyState(0x11)&0x8000)!=0||(keyState(0x12)&0x8000)!=0||(keyState(0x5b)&0x8000)!=0||(keyState(0x5c)&0x8000)!=0;}
        void Emit(string action){if(action==null)return;bool brightness=action.StartsWith("brightness-");if(!brightness||clock.ElapsedMilliseconds-repeatedAt>=100){if(brightness)repeatedAt=clock.ElapsedMilliseconds;try{if(Action!=null)Action(action);}catch{}}}
        bool mediaKeys=true;
        public bool MediaKeys {get{return mediaKeys;}set{mediaKeys=value;if(!value)ResetGestures();}}
        public event Action<string> Action;
        public void ResetGestures(){holdTimer.Stop();decoder.Reset();consumed.Clear();}
        internal void PollHold(long milliseconds){
            if(!MediaKeys||!decoder.HoldingEffectKey){decoder.CancelHold();holdTimer.Stop();return;}
            // F3 is consumed by our hook. Its recorded down/up events are authoritative;
            // the Windows async key-state bit is not a second source of hold state.
            Emit(decoder.Poll(ModifiersDown(),milliseconds));
        }
        internal bool ProcessKey(int key,int flags,bool pressed,long milliseconds){
            bool mods=ModifiersDown(),injected=(flags&0x10)!=0;
            var shot=screenshots.Feed(key,(flags&1)!=0,pressed,mods,injected,milliseconds);
            if(shot.Capture)Emit("screenshot");
            if(shot.Consume)return true;
            if(!MediaKeys&&key>=0x70&&key<=0x72)return false;
            bool suppress=pressed?MediaKeys&&!mods&&!injected&&key>=0x70&&key<=0x72:consumed.Remove(key);
            if(pressed&&suppress)consumed.Add(key);
            string action=decoder.Feed(key,pressed,mods,injected,milliseconds);
            if(key==0x72&&!injected){if(decoder.HoldingEffectKey)holdTimer.Start();else holdTimer.Stop();}
            Emit(action);return suppress;
        }
        public LaptopShortcuts():this(true,GetAsyncKeyState){}
        internal LaptopShortcuts(bool installHook,Func<int,short> keyState){
            this.keyState=keyState;
            holdTimer.Interval=25;holdTimer.Tick+=(s,e)=>PollHold(clock.ElapsedMilliseconds);
            callback=(code,msg,data)=>{
                if(code<0)return CallNextHookEx(handle,code,msg,data);
                int kind=msg.ToInt32();if(kind!=0x100&&kind!=0x104&&kind!=0x101&&kind!=0x105)return CallNextHookEx(handle,code,msg,data);
                int key=Marshal.ReadInt32(data),flags=Marshal.ReadInt32(data,8);bool pressed=kind==0x100||kind==0x104;
                if(ProcessKey(key,flags,pressed,clock.ElapsedMilliseconds))return new IntPtr(1);
                return CallNextHookEx(handle,code,msg,data);
            };
            if(installHook){handle=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);if(handle==IntPtr.Zero){holdTimer.Dispose();throw new System.ComponentModel.Win32Exception();}}
        }
        public void Dispose(){holdTimer.Dispose();if(handle!=IntPtr.Zero){UnhookWindowsHookEx(handle);handle=IntPtr.Zero;}decoder.Reset();}
    }
}
