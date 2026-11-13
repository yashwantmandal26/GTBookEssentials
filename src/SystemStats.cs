using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GTBook {
    public sealed class SystemStatsSnapshot {
        public int MemoryPercent = -1;
        public string MemoryValue = "Unavailable";
        public string MemoryDetail = "";
        public int DiskPercent = -1;
        public string DiskValue = "Unavailable";
        public string DiskDetail = "";
    }

    public static class SystemStats {
        [StructLayout(LayoutKind.Sequential)] struct Memory { public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended; }
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx(ref Memory memory);
        [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
        static string disk="Unavailable";
        static int diskPercent=-1;
        static string diskDetail="";
        static DateTime diskAt=DateTime.MinValue;

        public static TimeSpan GetUptime() {
            try {
                return TimeSpan.FromMilliseconds(GetTickCount64());
            } catch {
                return TimeSpan.FromMilliseconds((uint)Environment.TickCount);
            }
        }

        public static string FormatUptime(TimeSpan t) {
            if (t.TotalDays >= 1) {
                return string.Format("{0}d {1}h {2}m", (int)t.TotalDays, t.Hours, t.Minutes);
            }
            if (t.TotalHours >= 1) {
                return string.Format("{0}h {1}m", (int)t.TotalHours, t.Minutes);
            }
            return string.Format("{0}m", Math.Max(1, (int)t.TotalMinutes));
        }

        static string FormatGb(ulong bytes){
            double gb=bytes/(1024.0*1024.0*1024.0);
            return gb>=100?Math.Round(gb).ToString("0"):gb.ToString("0.0");
        }

        public static SystemStatsSnapshot Read() {
            var snap=new SystemStatsSnapshot();
            var m=new Memory{Length=(uint)Marshal.SizeOf(typeof(Memory))};
            if(GlobalMemoryStatusEx(ref m)&&m.TotalPhysical>0){
                snap.MemoryPercent=(int)Math.Max(0,Math.Min(100,m.Load));
                snap.MemoryValue=snap.MemoryPercent+"%";
                ulong used=m.TotalPhysical>m.AvailablePhysical?m.TotalPhysical-m.AvailablePhysical:0;
                snap.MemoryDetail=FormatGb(used)+" / "+FormatGb(m.TotalPhysical)+" GB used";
            }
            if((DateTime.UtcNow-diskAt).TotalSeconds>30){
                try{
                    var d=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                    if(d.IsReady&&d.TotalSize>0){
                        diskPercent=(int)Math.Max(0,Math.Min(100,Math.Round((1-d.AvailableFreeSpace/(double)d.TotalSize)*100)));
                        disk=diskPercent+"% used";
                        diskDetail=FormatGb((ulong)d.AvailableFreeSpace)+" GB free of "+FormatGb((ulong)d.TotalSize)+" GB";
                    }else{
                        disk="Unavailable";diskPercent=-1;diskDetail="";
                    }
                }catch{
                    disk="Unavailable";diskPercent=-1;diskDetail="";
                }
                diskAt=DateTime.UtcNow;
            }
            snap.DiskPercent=diskPercent;
            snap.DiskValue=diskPercent>=0?diskPercent+"%":disk;
            snap.DiskDetail=diskDetail;
            return snap;
        }

        public static string Summary() {
            var snap=Read();
            string ram=snap.MemoryPercent>=0?snap.MemoryPercent+"% used":"Unavailable";
            return "Memory: "+ram+"   ·   System disk: "+(snap.DiskPercent>=0?snap.DiskPercent+"% used":snap.DiskValue);
        }
    }
}
