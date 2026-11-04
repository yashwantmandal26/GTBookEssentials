using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace GTBook {
    public sealed class Request {
        public string Action = "probe";
        public string Feedback = "";
        public string KeyboardSession = "";
        public bool ArmKeyboardAwake = true;
        public int Mode = 1;
        public string Effect = "Static";
        public string[] Colors = new[] { "#7600FF", "#00D9FF", "#FF006A", "#FFBF00" };
        public int Brightness = 65;
        public int Speed = 4;
        public int Direction;
        public bool Enabled;
        public bool KeepAwake;
        public string ZoneEffect="Static";
        public int Step;
        public int OwnerProcessId;
        public long OwnerStartedTicks;
    }
    public sealed class Reply {
        public bool Ok;
        public string Message = "";
        public Dictionary<string, string> Data = new Dictionary<string, string>();
        public static Reply Fail(string message) { return new Reply { Message = message }; }
    }
    public static class Protocol {
        public static byte[] Color(string hex) {
            if (hex == null || hex.Length != 7 || hex[0] != '#') throw new ArgumentException("Choose a valid #RRGGBB colour.");
            return new[] { byte.Parse(hex.Substring(1,2), NumberStyles.HexNumber), byte.Parse(hex.Substring(3,2), NumberStyles.HexNumber), byte.Parse(hex.Substring(5,2), NumberStyles.HexNumber) };
        }
        public static void Validate(Request r) {
            if (r == null) throw new ArgumentException("Missing request.");
            if(r.Direction<0||r.Direction>1)throw new ArgumentException("Invalid animation direction.");
            Guid session;if(!string.IsNullOrEmpty(r.KeyboardSession)&&!Guid.TryParseExact(r.KeyboardSession,"N",out session))throw new ArgumentException("Invalid keyboard session.");
            if (!new[] { "probe", "device-info", "refresh", "mode", "auto", "fan", "usb", "gpu", "keyboard", "keyboard-animation", "rear", "rear-off", "rear-music", "screen-brightness" }.Contains(r.Action)) throw new ArgumentException("Unknown command.");
            if(r.Step < -100 || r.Step >100 || r.Action=="screen-brightness"&&r.Step==0)throw new ArgumentException("Invalid brightness step.");
            if (r.Mode < 0 || r.Mode > 2) throw new ArgumentException("Unsupported power mode.");
            if (r.Brightness < 0 || r.Brightness > 100 || r.Speed < 1 || r.Speed > 9) throw new ArgumentException("Setting outside its valid range.");
            if (r.Colors == null || r.Colors.Length != 4) throw new ArgumentException("Four zone colours are required.");
            foreach (string c in r.Colors) Color(c);
            if(!new[]{"Static","Breathing","Clock","Rainbow"}.Contains(r.ZoneEffect))throw new ArgumentException("Invalid custom-zone effect.");
            if (!new[] { "Static", "Breathing", "Rainbow", "Clock", "Flow", "Wave", "Four zones", "Loop", "Cover", "Rhythm", "Beat" }.Contains(r.Effect)&&!ExtraEffects.Contains(r.Effect)) throw new ArgumentException("Unknown lighting effect.");
            if(r.Action=="keyboard-animation"&&!ExtraEffects.Contains(r.Effect))throw new ArgumentException("Choose an Extra animation.");
            if(r.Action=="keyboard"&&!new[]{"Static","Breathing","Rainbow","Clock","Flow","Wave","Four zones"}.Contains(r.Effect)&&!ExtraEffects.Contains(r.Effect))throw new ArgumentException("Invalid keyboard effect.");
            if (r.Action == "rear" && !new[]{"Static","Breathing","Loop","Cover"}.Contains(r.Effect)) throw new ArgumentException("Choose a native rear effect.");
            if(r.Action=="rear-music" && r.Effect!="Rhythm" && r.Effect!="Beat")throw new ArgumentException("Choose a music effect.");
        }
        public static byte[] Keyboard(int command, string color, int brightness) {
            if (!(command>=0x11&&command<=0x16 || command==0x19 || command>=0x60&&command<=0x67 || command>=0x70&&command<=0x77) || brightness < 0 || brightness > 100) throw new ArgumentException("Invalid keyboard packet.");
            byte[] rgb = Color(color), packet = new byte[64];
            packet[0] = 6; packet[1] = (byte)command; packet[2] = 4;
            Array.Copy(rgb, 0, packet, 7, 3); packet[10] = (byte)Math.Round(brightness * 255.0 / 100);
            packet[63] = (byte)(packet.Skip(1).Take(62).Sum(x => (int)x) & 255);
            return packet;
        }
        public static byte[][] NativeKeyboardPackets(Request r){
            Validate(r);if(r.Action!="keyboard"||ExtraEffects.Contains(r.Effect))throw new ArgumentException("Choose a native keyboard effect.");
            var packets=new List<byte[]>();
            if(r.KeepAwake&&r.ArmKeyboardAwake)packets.Add(Keyboard(0x19,"#000000",0));
            if(r.Effect=="Four zones"){
                if(r.Feedback!="keyboard-brightness")packets.Add(Keyboard(0x11,r.Colors[0],r.Brightness));
                int offset=Array.IndexOf(new[]{"Static","Breathing","Clock","Rainbow"},r.ZoneEffect);
                int[] zones={0x60,0x64,0x70,0x74};for(int i=0;i<4;i++)packets.Add(Keyboard(zones[i]+offset,r.Colors[i],r.Brightness));
            }else packets.Add(Keyboard(r.Effect=="Breathing"?0x12:r.Effect=="Clock"?0x13:r.Effect=="Rainbow"?0x14:r.Effect=="Flow"?0x15:r.Effect=="Wave"?0x16:0x11,r.Colors[0],r.Brightness));
            return packets.ToArray();
        }
        public static byte[] Rear(string effect, string color, int brightness, int speed) {
            if (!new[]{"Static","Breathing","Loop","Cover","Rhythm","Beat"}.Contains(effect) || brightness < 0 || brightness > 100 || speed < 1 || speed > 9) throw new ArgumentException("Invalid rear packet.");
            byte[] rgb = Color(color), packet = new byte[17];
            packet[0] = 0x34; packet[1] = 0x0e; packet[2] = (byte)(Array.IndexOf(new[]{"Static","Breathing","Rhythm","Beat","Loop","Cover"},effect)+1);
            packet[3] = rgb[0]; packet[5] = rgb[1]; packet[7] = rgb[2];
            packet[9] = (byte)speed; packet[10] = (byte)(brightness * 44 / 100);
            return packet; // OEM PARAMS uses zero for the final byte, not a guessed checksum.
        }
    }
    public sealed class Identity {
        public string Manufacturer, Model, Bios;
        public bool Supported { get { return (Manufacturer??"").IndexOf("INFINIX", StringComparison.OrdinalIgnoreCase) >= 0 && string.Equals(Model,"GL613",StringComparison.OrdinalIgnoreCase) && Bios == "1.00"; } }
        public static Identity Read() {
            using (var systems = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
            using (var result = systems.Get())
            using (var bios = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS"))
            using (var versions = bios.Get()) {
                var s = result.Cast<ManagementObject>().First(); var b = versions.Cast<ManagementObject>().First();
                return new Identity { Manufacturer = Convert.ToString(s["Manufacturer"]).Trim(), Model = Convert.ToString(s["Model"]).Trim(), Bios = Convert.ToString(b["SMBIOSBIOSVersion"]).Trim() };
            }
        }
    }
    public static class Hardware {
        static readonly object conflictLock=new object();static readonly ConflictCache pythonCache=new ConflictCache();
        public static string Base { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static string OemDirectory { get { return Path.GetFullPath(Path.Combine(Base, "..", "Official_ControlCenter")); } }
        public static string Conflict() {
            lock(conflictLock)return ConflictCore();
        }
        static string ConflictCore(){
            var python=new List<string>();
            foreach (var p in Process.GetProcesses()) {
                using (p) {
                    string name;
                    try { name = p.ProcessName.ToLowerInvariant(); } catch (InvalidOperationException) { continue; }
                    if (new[] { "controlcenter", "infinixgttoolkit", "infinix toolkit", "infinixtoolkit", "infinixtoolkitfast" }.Contains(name)) return p.ProcessName + " is running. Close it from its tray before using hardware controls, then Refresh.";
                    if(name=="python"||name=="pythonw"){try{python.Add(p.Id+":"+p.StartTime.ToUniversalTime().Ticks);}catch{return "A Python process cannot be inspected. Close other hardware utilities before retrying.";}}
                }
            }
            return pythonCache.Check(python,()=>{
            using (var q = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name = 'python.exe' OR Name = 'pythonw.exe'"))
            using (var rows = q.Get()) {
                foreach (ManagementObject row in rows) {
                    string command = Convert.ToString(row["CommandLine"]).ToLowerInvariant();
                    if (command.Length == 0) return "A Python process cannot be inspected. Close other hardware utilities before retrying.";
                    if (command.Contains("infinix_toolkit.py") || command.Contains("infinix_backlight.py")) return "The old Python toolkit is running. Close it before using hardware controls.";
                }
            }
            return "";
            });
        }
        static object Oem() {
            return new OemProtocol();
        }
        static uint Call(object oem, string method, params object[] args) {
            try { return Convert.ToUInt32(oem.GetType().GetMethod(method).Invoke(oem, args)); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }
        static string Temperature(uint raw) { return raw >= 1 && raw <= 125 ? raw + " °C" : "Unavailable"; }
        public static Reply Execute(Request r) {
            Protocol.Validate(r);
            if(r.Action=="device-info")return DeviceInformation.Read();
            if(r.Action=="screen-brightness")return DisplayBrightness.Change(r.Step);
            Identity id = Identity.Read();
            var answer = new Reply { Ok = true, Message = "Device checked. Settings change only when you press Apply." };
            answer.Data["device"] = id.Manufacturer + " " + id.Model;
            answer.Data["bios"] = id.Bios;
            answer.Data["supported"] = id.Supported.ToString();
            string conflict = Conflict(); answer.Data["conflict"] = conflict;
            answer.Data["transport"] = "Native OEM ACPI/WMI provider; no ControlCenter DLL required";
            if (r.Action == "probe") return answer;
            if (!id.Supported) throw new InvalidOperationException("Native controls currently support INFINIX GL613 BIOS 1.00. Detected: " + id.Model + ", " + id.Bios + ". Use the official app for this firmware.");
            if (conflict.Length > 0) throw new InvalidOperationException(conflict);
            if(r.Action=="keyboard-animation")return KeyboardAnimation.Run(r);
            // One worker owns all transports. The original app does not honour our mutex,
            // so ownership is also checked immediately before each action.
            if (r.Action == "keyboard") {
                if(ExtraEffects.Contains(r.Effect))throw new ArgumentException("Extra effects require the keyboard animation worker.");
                using(var gate=(OemProtocol)Oem()) {gate.KeyLight(r.Brightness>0); }
                using (var keyboard = new KeyboardDevice()) {
                    foreach(var packet in Protocol.NativeKeyboardPackets(r))keyboard.Send(packet);
                }
                answer.Message = "Keyboard command sent • " + r.Effect + ", " + r.Brightness + "% (visual confirmation needed).";
                return answer;
            }
            using(var native = (OemProtocol)Oem()) {
            object oem=native;
            if (r.Action == "mode") {
                var confirmed=PerformanceModes.Apply(native,r.Mode,Thread.Sleep);
                answer.Message=confirmed.Message;answer.Data["mode"]=confirmed.Data["mode"];
            } else if (r.Action == "auto" || r.Action=="fan") {
                bool enabled=r.Action=="fan" && r.Enabled;
                Call(oem, "SetFanFullMode", enabled); Thread.Sleep(100);
                if (Call(oem, "GetFanFullMode") != (enabled?1u:0u)) throw new IOException("Firmware did not confirm cooling mode.");
                answer.Message = enabled?"Fan Max confirmed.":"Automatic firmware cooling confirmed.";
                answer.Data["cooling"]=enabled?"Full fan override":"Firmware auto";
            } else if(r.Action=="usb") {
                var usb=UsbCharging.Apply(native,r.Enabled,Thread.Sleep);answer.Message=usb.Message;answer.Data["usb"]=usb.Data["usb"];
            } else if(r.Action=="gpu") {
                var gpu=GpuModes.Apply(native,r.Mode,Thread.Sleep);answer.Message=gpu.Message;foreach(var item in gpu.Data)answer.Data[item.Key]=item.Value;
            } else if (r.Action == "rear" || r.Action == "rear-off") {
                // Find the OEM UART before changing its power gate.
                string port = RearDevice.FindPort();
                using (var rear = new RearDevice(port)) {
                    if (r.Action == "rear-off") {
                        rear.Send(Protocol.Rear("Static", "#000000", 0, 1));
                        Call(oem, "BackLight", false);
                        if (Call(oem, "getLight2") == 0) throw new IOException("Rear power-off read-back did not match.");
                        answer.Message = "Rear power gate confirmed off.";
                    } else {
                        Call(oem, "BackLight", true);
                        if (Call(oem, "getLight2") != 0) throw new IOException("Rear power-on read-back did not match.");
                        byte[] frame=Protocol.Rear(r.Effect, r.Colors[0], r.Brightness, r.Speed);
                        if(r.Effect=="Loop" || r.Effect=="Cover") {byte[] second=Protocol.Color(r.Colors[1]);Array.Copy(second,0,frame,12,3);frame[15]=(byte)(r.Brightness*44/100);}
                        rear.Send(frame);
                        answer.Message = "Rear command sent • " + r.Effect + " (visual confirmation needed).";
                    }
                }
                return answer;
            } else if(r.Action=="rear-music") {
                string port=RearDevice.FindPort();
                using(var rear=new RearDevice(port))
                using(var meter=new AudioMeter())
                using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,@"Local\GTBookMusicStop")) {
                    using(var lease=new HardwareLease()){if(!lease.Acquire(3000))throw new IOException("Another hardware command is busy.");Call(oem,"BackLight",true);}
                    try {
                        int count=0;
                        while(!stop.WaitOne(50)) {
                            if(++count%20==0 && Conflict().Length>0)throw new IOException("Another control utility started; music stopped.");
                            byte[] frame=Protocol.Rear(r.Effect,r.Colors[0],r.Brightness,r.Speed);
                            byte peak=(byte)Math.Min(255,Math.Round(meter.Peak()*255)); frame[12]=peak;frame[15]=peak;rear.Send(frame);
                        }
                    } finally {rear.Send(Protocol.Rear("Static",r.Colors[0],r.Brightness,1));}
                }
                answer.Message="Music sync stopped; static rear colour restored.";return answer;
            }
            if(r.Action!="refresh")return answer;
            uint mode = Call(oem, "GetPerformanceMode");
            answer.Data["mode"] = mode <= 3 ? new[] { "Quiet", "Balanced", "Gaming", "OEM mode 3" }[mode] : "Unknown (" + mode + ")";
            answer.Data["cpu"] = Temperature(Call(oem, "GetCPUTem"));
            answer.Data["cooling"] = Call(oem, "GetFanFullMode") == 0 ? "Firmware auto" : "Full fan override";
            foreach(var field in new[]{"gpu","cpu-fan","gpu-fan","usb","gpu-mode"}) {
                try { uint v=Call(oem,field=="gpu"?"GetGPUTem":field=="cpu-fan"?"GetCPUFan":field=="gpu-fan"?"GetGPUFan":field=="usb"?"GetUSBCharging":"GetGPUMode");
                    answer.Data[field]=field=="gpu"?Temperature(v):field.EndsWith("fan")?(v*100)+" RPM":field=="usb"?(v==1?"On":"Off"):GpuModes.Name(v);
                    if(field=="gpu-mode")answer.Data["gpu-raw"]=v.ToString();
                } catch(Exception e){answer.Data[field]="Unavailable: "+e.Message;}
            }
            try{answer.Data["rear-power"]=Call(oem,"getLight2")==0?"On":"Off";}catch(Exception e){answer.Data["rear-power-read-error"]=e.Message;}
            if (r.Action == "refresh") answer.Message = "Hardware refreshed • " + DateTime.Now.ToString("HH:mm:ss");
            return answer;
            }
        }
    }
    public static class WorkerClient {
        static readonly Lazy<BrightnessBroker> brightness=new Lazy<BrightnessBroker>(()=>new BrightnessBroker());
        public static void DisposeShared(){if(brightness.IsValueCreated)brightness.Value.Dispose();}
        public static Reply Run(Request request, int timeout = 12000, WaitHandle stop = null) {
            try {if(request.Action=="screen-brightness"){Protocol.Validate(request);return brightness.Value.Run(request.Step);}return RunCore(request,timeout,stop);}catch(Exception e){return Reply.Fail("Hardware helper could not complete: "+e.Message);}
        }
        static Reply RunCore(Request request, int timeout, WaitHandle stop) {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(request)));
            var start = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--worker " + encoded) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(start)) {
                var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                bool exited;
                if(stop==null)exited=process.WaitForExit(timeout);
                else {while(!process.WaitForExit(100) && !stop.WaitOne(0)){} exited=process.HasExited || process.WaitForExit(4000);}
                if (!exited) {
                    process.Kill(); process.WaitForExit();
                    return Reply.Fail("Hardware timed out. A command may have partially applied; Refresh before retrying.");
                }
                string json = output.Result;
                try { return new JavaScriptSerializer().Deserialize<Reply>(json) ?? Reply.Fail("Worker returned no result."); }
                catch { return Reply.Fail("Hardware worker failed: " + errors.Result + " " + json); }
            }
        }
    }
}


