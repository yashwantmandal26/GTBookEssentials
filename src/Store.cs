using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace GTBook {
    public static class Store {
        static readonly object gate=new object();public static string Warning="";
        public static string DirectoryPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GTBookStable");}}
        public static void Log(string line){lock(gate)try{Directory.CreateDirectory(DirectoryPath);string path=Path.Combine(DirectoryPath,"activity.log");if(File.Exists(path)&&new FileInfo(path).Length>512000){File.Copy(path,path+".previous",true);File.WriteAllText(path,"");}File.AppendAllText(path,DateTime.Now.ToString("s")+"  "+line+Environment.NewLine);}catch{}}
        static Request Read(string path){var value=new JavaScriptSerializer().Deserialize<Request>(File.ReadAllText(path));Protocol.Validate(value);value.Feedback="";value.KeyboardSession="";return value;}
        public static Request Load(string name){return LoadPath(Path.Combine(DirectoryPath,name+".json"));}
        internal static Request LoadPath(string path){lock(gate){
            if(!File.Exists(path)&&!File.Exists(path+".bak"))return new Request();
            try{return Read(path);}catch{try{var restored=Read(path+".bak");Warning="Recovered the last saved preferences from backup. No hardware settings were changed.";return restored;}catch{Warning="Saved preferences could not be read; defaults loaded. No hardware settings were changed.";return new Request();}}
        }}
        internal static void AtomicText(string path,string text){
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] data=new UTF8Encoding(false).GetBytes(text);stream.Write(data,0,data.Length);stream.Flush(true);}if(File.Exists(path))File.Replace(temp,path,path+".bak",true);else File.Move(temp,path);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public static void Save(string name,Request request){SavePath(Path.Combine(DirectoryPath,name+".json"),request);}
        internal static void SavePath(string path,Request request){Protocol.Validate(request);lock(gate)AtomicText(path,new JavaScriptSerializer().Serialize(request));}
        public static bool Flag(string name,bool fallback=false){lock(gate){try{bool value;return bool.TryParse(File.ReadAllText(Path.Combine(DirectoryPath,name+".txt")),out value)?value:fallback;}catch{return fallback;}}}
        public static void SaveFlag(string name,bool value){lock(gate)AtomicText(Path.Combine(DirectoryPath,name+".txt"),value.ToString());}
    }
}
