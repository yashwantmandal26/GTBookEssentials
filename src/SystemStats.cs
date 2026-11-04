using System;
using System.IO;
using System.Runtime.InteropServices;
namespace GTBook {
    public static class SystemStats {
        [StructLayout(LayoutKind.Sequential)] struct Memory { public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended; }
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx(ref Memory memory);
        static string disk="Unavailable";static DateTime diskAt=DateTime.MinValue;
        public static string Summary() {
            var m=new Memory{Length=(uint)Marshal.SizeOf(typeof(Memory))};
            string ram=GlobalMemoryStatusEx(ref m)?m.Load+"% used":"Unavailable";
            if((DateTime.UtcNow-diskAt).TotalSeconds>30){try{var d=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));disk=Math.Round((1-d.AvailableFreeSpace/(double)d.TotalSize)*100)+"% used";}catch{disk="Unavailable";}diskAt=DateTime.UtcNow;}
            return "Memory: "+ram+"   ·   System disk: "+disk;
        }
    }
}
