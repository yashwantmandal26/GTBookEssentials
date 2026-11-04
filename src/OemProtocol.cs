using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;

namespace GTBook {
    // Byte layouts and EC sequences decoded from the shipped BYDWmi2.dll.
    // No assembly from the .NET 6 OEM app is loaded into the Framework UI.
    public sealed class OemProtocol : IDisposable {
        readonly ManagementObject provider;
        readonly Func<string,byte[],uint> transport;
        public OemProtocol(Func<string,byte[],uint> transport){this.transport=transport;}
        public OemProtocol() {
            var scope = new ManagementScope(@"\\.\root\wmi",new ConnectionOptions { EnablePrivileges=true, Timeout=TimeSpan.FromSeconds(3) });
            scope.Connect();
            using(var q=new ManagementObjectSearcher(scope,new ObjectQuery("SELECT * FROM ACPIMethod")))
            using(var rows=q.Get()) { provider=rows.Cast<ManagementObject>().FirstOrDefault(); }
            if(provider==null)throw new IOException("OEM ACPIMethod provider not found on this device.");
        }
        public static byte[] Buffer(uint command, byte operation, byte value) {
            byte[] b=new byte[24]; b[0]=66;b[1]=89;b[2]=68;b[3]=76;
            Array.Copy(BitConverter.GetBytes(command),0,b,4,4); b[12]=operation;b[16]=value;return b;
        }
        uint Invoke(string method,byte[] buffer) {
            if(transport!=null)return transport(method,buffer);
            using(var input=provider.GetMethodParameters(method)) {
                input["IData"]=buffer;
                using(var output=provider.InvokeMethod(method,input,new InvokeMethodOptions{Timeout=TimeSpan.FromSeconds(3)})) {
                    if(output==null || output["OData"]==null)throw new IOException(method+" returned no OData.");
                    uint value=Convert.ToUInt32(output["OData"]);
                    if(value>=0x80000000)throw new IOException("OEM provider rejected "+method+" (0x"+value.ToString("X8")+"). Try Launch_Admin.bat.");
                    return value;
                }
            }
        }
        uint IO(byte address,byte value,bool write) {
            byte[] b=Buffer(0x300,1,value);b[13]=(byte)(write?1:0);b[14]=8;b[15]=address;
            return Invoke("MemIO",b);
        }
        public uint Read(byte address) {
            IO(148,0,true);IO(146,1,true);IO(144,0,true);IO(145,address,true);IO(147,160,true);
            return IO(160,0,false);
        }
        public void Write(byte address,byte value) {
            if(!new byte[]{0x40,0x41,0x4e,0x5e,0xc0}.Contains(address))throw new ArgumentException("Unreviewed EC write.");
            IO(148,0,true);IO(145,0,true);IO(146,0,true);IO(146,1,true);IO(144,0,true);IO(145,address,true);IO(160,value,true);IO(147,161,true);
            Thread.Sleep(30);
        }
        public uint GetPerformanceMode(){return Read(0x40);}
        public uint SetPerformanceMode(int value){Write(0x40,(byte)value);return 0;}
        public uint GetCPUTem(){return Read(10);}
        public uint GetGPUTem(){return Invoke("DoMethod",Buffer(4,0,0));}
        public uint GetCPUFan(){return Read(8);}
        public uint GetGPUFan(){return Read(9);}
        public uint GetFanFullMode(){return Read(0x41);}
        public uint SetFanFullMode(bool enabled){Write(0x41,(byte)(enabled?1:0));return 0;}
        public uint GetUSBCharging(){return Invoke("SMI",Buffer(3,0,0));}
        public uint SetUSBCharging(bool enabled){Write(0x4e,(byte)(enabled?2:0));return Invoke("SMI",Buffer(3,1,(byte)(enabled?1:0)));}
        public uint GetGPUMode(){return Read(0xc0);}
        public uint SetGPUMode(int mode){if(mode<1||mode>3)throw new ArgumentException("Unsupported GPU mode.");Write(0xc0,(byte)mode);return 0;}
        uint Light(int mask,bool enabled){uint old=Read(0x5e);byte next=(byte)(enabled?old&~(uint)mask:old|(uint)mask);if(next!=(byte)old)Write(0x5e,next);return 0;}
        public uint KeyLight(bool enabled){return Light(1,enabled);}
        public uint BackLight(bool enabled){return Light(2,enabled);}
        public uint getLight2(){return Read(0x5e)&2;}
        public void Dispose(){if(provider!=null)provider.Dispose();}
    }
}
