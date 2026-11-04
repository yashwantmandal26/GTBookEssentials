using System;
using System.IO;
using System.Management;
using System.Linq;
using System.Web.Script.Serialization;

namespace GTBook {
    public static class WindowLifecycle {
        public static bool HideInsteadOfExit(bool preview,bool exitRequested,System.Windows.Forms.CloseReason reason){return !preview&&!exitRequested&&reason==System.Windows.Forms.CloseReason.UserClosing;}
    }
    public static class GpuPending {
        public sealed class State {public string Boot="";public int Before,Target;}
        static string boot="";static State state;
        public static bool Ready{get{return boot.Length>0;}}
        public static bool Pending {get{return state!=null&&state.Boot==boot&&boot.Length>0;}}
        public static int Before {get{return Pending?state.Before:0;}}
        public static int Target {get{return Pending?state.Target:0;}}
        public static bool SameBoot(State value,string current){return value!=null&&!string.IsNullOrEmpty(current)&&value.Boot==current&&value.Before>=1&&value.Before<=3&&value.Target>=1&&value.Target<=3;}
        public static void Initialize(){
            try{
                using(var search=new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem")){
                    search.Options.Timeout=TimeSpan.FromSeconds(2);
                    using(var rows=search.Get())foreach(ManagementObject row in rows)using(row){boot=Convert.ToString(row["LastBootUpTime"]);break;}
                }
                string path=Path.Combine(Store.DirectoryPath,"gpu-restart-state.json");
                if(File.Exists(path)){var saved=new JavaScriptSerializer().Deserialize<State>(File.ReadAllText(path));if(SameBoot(saved,boot))state=saved;}
            }catch(Exception error){Store.Log("GPU restart state: "+error.Message);}
        }
        public static void Save(int before,int target){
            var value=new State{Boot=boot,Before=before,Target=target};
            if(!SameBoot(value,boot))throw new IOException("Could not identify this Windows boot; restart state was not recorded.");
            Store.AtomicText(Path.Combine(Store.DirectoryPath,"gpu-restart-state.json"),new JavaScriptSerializer().Serialize(value));state=value;
        }
    }
}
