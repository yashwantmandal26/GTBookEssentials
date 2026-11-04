using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace GTBook {
    public static class RuntimeMetrics {
        static readonly Stopwatch clock=Stopwatch.StartNew();static readonly Dictionary<string,object> values=new Dictionary<string,object>();static double offset;
        public static void Begin(){using(var process=Process.GetCurrentProcess()){values["process_started_utc"]=process.StartTime.ToUniversalTime().ToString("o");offset=Math.Max(0,(DateTime.UtcNow-process.StartTime.ToUniversalTime()).TotalMilliseconds-clock.Elapsed.TotalMilliseconds);}values["version"]=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();}
        public static void Record(string stage){try{values[stage+"_ms"]=Math.Round(offset+clock.Elapsed.TotalMilliseconds,1);using(var process=Process.GetCurrentProcess())values["private_mib"]=Math.Round(process.PrivateMemorySize64/1048576.0,2);Store.AtomicText(Path.Combine(Store.DirectoryPath,"runtime-startup.json"),new JavaScriptSerializer().Serialize(values));}catch{}}
    }
}
