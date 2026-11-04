using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GTBook {
    public sealed class AudioMeter : IDisposable {
        [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class Enumerator {}
        [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IEnum {
            void EnumAudioEndpoints(int flow,uint mask,out IntPtr devices);
            void GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
        }
        [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice {
            void Activate(ref Guid id,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)] out object result);
        }
        [ComImport,Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMeter {
            void GetPeakValue(out float peak);
            void GetMeteringChannelCount(out uint channels);
            void GetChannelsPeakValues(uint channels,IntPtr peaks);
            void QueryHardwareSupport(out uint mask);
        }
        object enumerator,device,active; IMeter meter;
        public AudioMeter() {
            try { enumerator=new Enumerator(); IDevice endpoint; ((IEnum)enumerator).GetDefaultAudioEndpoint(0,0,out endpoint);device=endpoint;
                Guid id=typeof(IMeter).GUID;endpoint.Activate(ref id,23,IntPtr.Zero,out active);meter=(IMeter)active;
            } catch {Dispose();throw;}
        }
        public float Peak(){float p;meter.GetPeakValue(out p);return Math.Max(0,Math.Min(1,p));}
        public void Dispose(){meter=null;foreach(object obj in new[]{active,device,enumerator})if(obj!=null && Marshal.IsComObject(obj))Marshal.ReleaseComObject(obj);active=device=enumerator=null;}
    }
    public sealed class KeyLock : IDisposable {
        delegate IntPtr Hook(int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,Hook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        IntPtr handle; Hook callback;
        public bool Enabled {get{return handle!=IntPtr.Zero;}}
        public void Set(bool enabled) {
            if(enabled && !Enabled) {
                callback=(code,msg,data)=>{int key=code>=0?Marshal.ReadInt32(data):0;return code>=0&&(key==0x5b||key==0x5c)?new IntPtr(1):CallNextHookEx(handle,code,msg,data);};
                handle=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);if(!Enabled)throw new Win32Exception();
            } else if(!enabled && Enabled){UnhookWindowsHookEx(handle);handle=IntPtr.Zero;}
        }
        public void Dispose(){Set(false);}
    }
    public static class Startup {
        public const string TaskName="GTBookStable";
        public static string Definition(string executable,string userSid) {
            var document=new System.Xml.XmlDocument();
            document.LoadXml("<Task version='1.2' xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'><RegistrationInfo><Description>GTBOOK Essentials at sign-in</Description></RegistrationInfo><Triggers><LogonTrigger><Enabled>true</Enabled><UserId /></LogonTrigger></Triggers><Principals><Principal id='User'><UserId /><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals><Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><Enabled>true</Enabled><Hidden>false</Hidden><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority><RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure></Settings><Actions Context='User'><Exec><Command /><Arguments>--startup</Arguments><WorkingDirectory /></Exec></Actions></Task>");
            var ns=new System.Xml.XmlNamespaceManager(document.NameTable);ns.AddNamespace("t",document.DocumentElement.NamespaceURI);
            foreach(System.Xml.XmlNode node in document.SelectNodes("//t:UserId",ns))node.InnerText=userSid;
            document.SelectSingleNode("//t:Command",ns).InnerText=executable;
            document.SelectSingleNode("//t:WorkingDirectory",ns).InnerText=System.IO.Path.GetDirectoryName(executable);
            return document.OuterXml;
        }
        static dynamic Connect(){dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));service.Connect();return service;}
        public static bool Enabled {get {object service=null,folder=null,task=null;try{service=Connect();folder=((dynamic)service).GetFolder("\\");task=((dynamic)folder).GetTask(TaskName);return ((dynamic)task).Enabled;}catch{return false;}finally{Release(task);Release(folder);Release(service);}}}
        public static void Set(bool enabled) {
            object service=null,folder=null,task=null;
            try {service=Connect();folder=((dynamic)service).GetFolder("\\");
                if(enabled){string exe=Process.GetCurrentProcess().MainModule.FileName;string sid=System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;task=((dynamic)folder).RegisterTask(TaskName,Definition(exe,sid),6,sid,null,3,null);}
                else {task=((dynamic)folder).GetTask(TaskName);((dynamic)task).Enabled=false;}
            }finally{Release(task);Release(folder);Release(service);}
        }
        static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
        public static void ConfigureReplacement(string executable,bool startNow=true) {
            object service=null,folder=null,original=null,toolkit=null,running=null;
            string previous=null;bool oldEnabled=false,registered=false,originalChanged=false;
            string backup=System.IO.Path.Combine(Store.DirectoryPath,"startup-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            try {
                service=Connect();folder=((dynamic)service).GetFolder("\\");
                try {original=((dynamic)folder).GetTask("ControlCenter");}catch(System.Runtime.InteropServices.COMException){}
                if(original!=null&&!Identity.Read().Supported){Release(original);original=null;}
                try {toolkit=((dynamic)folder).GetTask(TaskName);previous=((dynamic)toolkit).Xml;}catch(System.Runtime.InteropServices.COMException){}
                System.IO.Directory.CreateDirectory(backup);
                if(original!=null){
                    string path=Convert.ToString(((dynamic)original).Definition.Actions.Item(1).Path).Trim('\"');
                    string expected=System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(executable),"..","Official_ControlCenter","ControlCenter.exe"));
                    bool known=string.Equals(System.IO.Path.GetFullPath(path),expected,StringComparison.OrdinalIgnoreCase)||string.Equals(System.IO.Path.GetFileName(path),"ControlCenter.exe",StringComparison.OrdinalIgnoreCase)&&(path.IndexOf("Official_ControlCenter",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("Infinix",StringComparison.OrdinalIgnoreCase)>=0);
                    if(!known)throw new Exception("The ControlCenter startup task points to an unrelated application; no startup changes were made.");
                    oldEnabled=((dynamic)original).Enabled;System.IO.File.WriteAllText(System.IO.Path.Combine(backup,"ControlCenter.xml"),((dynamic)original).Xml);
                }
                if(previous!=null)System.IO.File.WriteAllText(System.IO.Path.Combine(backup,TaskName+".xml"),previous);
                Release(toolkit);toolkit=null;
                string sid=System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
                toolkit=((dynamic)folder).RegisterTask(TaskName,Definition(executable,sid),6,sid,null,3,null);registered=true;
                if(original!=null){((dynamic)original).Enabled=false;originalChanged=true;}
                dynamic def=((dynamic)toolkit).Definition;
                if(!((dynamic)toolkit).Enabled || def.Settings.DisallowStartIfOnBatteries || def.Settings.StopIfGoingOnBatteries || (original!=null&&((dynamic)original).Enabled))throw new Exception("Startup verification failed.");
                if(startNow)running=((dynamic)toolkit).Run(null);
                System.IO.File.WriteAllText(System.IO.Path.Combine(Store.DirectoryPath,"startup-setup.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {ok=true,toolkit_enabled=true,original_disabled=original!=null,battery_allowed=true,arguments="--startup",backup=backup,executable=executable,at=DateTime.Now.ToString("s")}));
            }catch {
                if(originalChanged)((dynamic)original).Enabled=oldEnabled;
                if(registered){if(previous!=null){object restored=((dynamic)folder).RegisterTask(TaskName,previous,6,null,null,3,null);Release(restored);}else ((dynamic)folder).DeleteTask(TaskName,0);}
                throw;
            }finally{Release(running);Release(toolkit);Release(original);Release(folder);Release(service);}
        }
    }
}
