using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace GTBook {
    // Runs only in the bounded read-only helper, never on the UI or sensor polling thread.
    public static class DeviceInformation {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct DisplayMode {
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string DeviceName;
            public ushort SpecVersion,DriverVersion,Size,DriverExtra;
            public uint Fields;public int PositionX,PositionY;public uint Orientation,FixedOutput;
            public short Color,Duplex,YResolution,TTOption,Collate;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string FormName;
            public ushort LogPixels;public uint BitsPerPel,Width,Height,Flags,Frequency,ICMMethod,ICMIntent,MediaType,DitherType,Reserved1,Reserved2,PanningWidth,PanningHeight;
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        static extern bool EnumDisplaySettings(string device,int mode,ref DisplayMode value);
        static string Display(Screen screen,int index){
            var mode=new DisplayMode{Size=(ushort)Marshal.SizeOf(typeof(DisplayMode))};
            string prefix="Display "+(index+1)+(screen.Primary?" · Primary":"")+": ";
            if(EnumDisplaySettings(screen.DeviceName,-1,ref mode))return prefix+mode.Width+" × "+mode.Height+" · "+mode.Frequency+" Hz · "+mode.BitsPerPel+"-bit colour";
            return prefix+"Current display mode unavailable";
        }
        public static string Bytes(object value, bool allowZero = false){double size;if(value==null||!double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out size)||size<0||(size==0&&!allowZero))return "Unavailable";return (size/1073741824d).ToString("0.##",CultureInfo.InvariantCulture)+" GB";}
        public static string FreeBytes(object value){return Bytes(value,allowZero:true);}
        static string Value(Dictionary<string,object> row,string key){object v;return row.TryGetValue(key,out v)&&v!=null&&!string.IsNullOrWhiteSpace(Convert.ToString(v))?Convert.ToString(v).Trim():"Unavailable";}
        static string Date(string value){try{return ManagementDateTimeConverter.ToDateTime(value).ToString("dd MMM yyyy");}catch{return "Unavailable";}}
        static List<Dictionary<string,object>> Rows(string query,string scope="root\\cimv2"){
            var rows=new List<Dictionary<string,object>>();
            using(var search=new ManagementObjectSearcher(scope,query)){
                search.Options.Timeout=TimeSpan.FromSeconds(1.5);search.Options.ReturnImmediately=false;
                using(var result=search.Get())foreach(ManagementObject item in result)using(item){
                    var row=new Dictionary<string,object>();foreach(PropertyData property in item.Properties)row[property.Name]=property.Value;rows.Add(row);
                }
            }return rows;
        }
        static void Section(StringBuilder text,string name,Func<string> read){
            text.AppendLine(name.ToUpperInvariant());
            try{string value=read();text.AppendLine(string.IsNullOrWhiteSpace(value)?"Not reported by this device.":value);}catch(Exception){text.AppendLine("Unavailable — Windows did not report this information.");}
            text.AppendLine();
        }
        public static Reply Read(){
            var text=new StringBuilder();
            Section(text,"Device",()=>string.Join("\r\n",Rows("SELECT Manufacturer,Model,SystemType,TotalPhysicalMemory FROM Win32_ComputerSystem").Select(r=>Value(r,"Manufacturer")+" "+Value(r,"Model")+" · "+Value(r,"SystemType")+"\r\nUsable physical memory: "+Bytes(r["TotalPhysicalMemory"]))));
            Section(text,"BIOS & motherboard",()=>string.Join("\r\n",Rows("SELECT Manufacturer,SMBIOSBIOSVersion,ReleaseDate FROM Win32_BIOS").Select(r=>Value(r,"Manufacturer")+" · BIOS "+Value(r,"SMBIOSBIOSVersion")+" · "+Date(Value(r,"ReleaseDate"))))+"\r\n"+string.Join("\r\n",Rows("SELECT Manufacturer,Product,Version FROM Win32_BaseBoard").Select(r=>Value(r,"Manufacturer")+" "+Value(r,"Product")+" · "+Value(r,"Version"))));
            Section(text,"Windows",()=>string.Join("\r\n",Rows("SELECT Caption,Version,BuildNumber,OSArchitecture,LastBootUpTime FROM Win32_OperatingSystem").Select(r=>Value(r,"Caption")+" · "+Value(r,"OSArchitecture")+"\r\nVersion "+Value(r,"Version")+" · Build "+Value(r,"BuildNumber")+"\r\nLast boot: "+Date(Value(r,"LastBootUpTime")))));
            Section(text,"Processor",()=>string.Join("\r\n",Rows("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor").Select(r=>Value(r,"Name")+"\r\n"+Value(r,"NumberOfCores")+" cores · "+Value(r,"NumberOfLogicalProcessors")+" threads · Reported clock: "+Value(r,"MaxClockSpeed")+" MHz")));
            Section(text,"Memory modules",()=>string.Join("\r\n",Rows("SELECT DeviceLocator,Capacity,ConfiguredClockSpeed,Manufacturer,PartNumber FROM Win32_PhysicalMemory").Select(r=>Value(r,"DeviceLocator")+": "+Bytes(r["Capacity"])+" · "+Value(r,"ConfiguredClockSpeed")+" MT/s\r\n"+Value(r,"Manufacturer")+" "+Value(r,"PartNumber"))));
            Section(text,"Graphics",()=>string.Join("\r\n\r\n",Rows("SELECT Name,DriverVersion,DriverDate,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate FROM Win32_VideoController").Select(r=>Value(r,"Name")+"\r\nDriver "+Value(r,"DriverVersion")+" · "+Date(Value(r,"DriverDate"))+"\r\nOutput: "+Value(r,"CurrentHorizontalResolution")+" × "+Value(r,"CurrentVerticalResolution")+" · "+Value(r,"CurrentRefreshRate")+" Hz")));
            Section(text,"Displays",()=>string.Join("\r\n",Screen.AllScreens.Select(Display)));
            Section(text,"Storage devices",()=>string.Join("\r\n",Rows("SELECT Model,Size,InterfaceType FROM Win32_DiskDrive").Select(r=>Value(r,"Model")+" · "+Bytes(r["Size"])+" · "+Value(r,"InterfaceType"))));
            Section(text,"Storage space",()=>string.Join("\r\n",Rows("SELECT DeviceID,FileSystem,Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3").Select(r=>Value(r,"DeviceID")+" · "+Value(r,"FileSystem")+" · "+FreeBytes(r["FreeSpace"])+" free of "+Bytes(r["Size"]))));
            Section(text,"Battery",()=>string.Join("\r\n",Rows("SELECT Name,EstimatedChargeRemaining FROM Win32_Battery").Select(r=>Value(r,"Name")+" · "+Value(r,"EstimatedChargeRemaining")+"%"))+"\r\nPower source: "+SystemInformation.PowerStatus.PowerLineStatus);
            Section(text,"Battery capacity",()=>{
                var design=Rows("SELECT DesignedCapacity FROM BatteryStaticData","root\\wmi");var full=Rows("SELECT FullChargedCapacity FROM BatteryFullChargedCapacity","root\\wmi");
                if(design.Count==0||full.Count==0)return "Not reported by the battery firmware.";
                double d=Convert.ToDouble(design[0]["DesignedCapacity"]),f=Convert.ToDouble(full[0]["FullChargedCapacity"]);
                return d<=0||f<=0?"Not reported by the battery firmware.":"Design: "+d.ToString("0")+" mWh · Full charge: "+f.ToString("0")+" mWh\r\nEstimated health: "+Math.Min(100,100*f/d).ToString("0")+"%";
            });
            text.Append("Updated ").Append(DateTime.Now.ToString("dd MMM yyyy · HH:mm:ss"));
            var reply=new Reply{Ok=true,Message="Device information updated."};reply.Data["device-report"]=text.ToString();return reply;
        }
        public static string Example(){return "DEVICE\r\nINFINIX GTBook · x64-based PC\r\n\r\nPROCESSOR\r\nProcessor model · cores · threads\r\n\r\nMEMORY\r\nInstalled capacity · module speed\r\n\r\nGRAPHICS & DISPLAYS\r\nGraphics models · drivers · resolution · refresh rate\r\n\r\nWINDOWS, BIOS & STORAGE\r\nVersion · firmware · disks · free space\r\n\r\nBATTERY\r\nCharge · power source · design capacity · battery health\r\n\r\nLive specifications load in the installed app.";}
    }
}
