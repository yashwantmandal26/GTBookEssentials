using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace GTBook {
    public sealed class AppSignals : IDisposable {
        public const string ShowName=@"Local\GTBookEssentialsShow",ExitName=@"Local\GTBookEssentialsExit";
        readonly EventWaitHandle show,exit;readonly RegisteredWaitHandle showWait,exitWait;
        static EventWaitHandle Create(string name){var security=new EventWaitHandleSecurity();security.AddAccessRule(new EventWaitHandleAccessRule(WindowsIdentity.GetCurrent().User,EventWaitHandleRights.FullControl,AccessControlType.Allow));bool created;return new EventWaitHandle(false,EventResetMode.AutoReset,name,out created,security);}
        public static bool Send(string name){try{using(var signal=EventWaitHandle.OpenExisting(name,EventWaitHandleRights.Modify)){signal.Set();return true;}}catch(WaitHandleCannotBeOpenedException){return false;}catch(UnauthorizedAccessException){return false;}}
        public AppSignals(MainWindow form){
            var handle=form.Handle;show=Create(ShowName);exit=Create(ExitName);
            Action<Action> post=action=>{try{if(!form.IsDisposed)form.BeginInvoke(action);}catch(InvalidOperationException){}};
            showWait=ThreadPool.RegisterWaitForSingleObject(show,(s,t)=>post(()=>{form.ShowWithDeviceControl();}),null,Timeout.Infinite,false);
            exitWait=ThreadPool.RegisterWaitForSingleObject(exit,(s,t)=>post(()=>form.ExitApplication()),null,Timeout.Infinite,false);
        }
        public void Dispose(){showWait.Unregister(null);exitWait.Unregister(null);show.Dispose();exit.Dispose();}
    }
}
