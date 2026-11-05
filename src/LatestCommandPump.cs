using System;
using System.Threading.Tasks;

namespace GTBook {
    // One in flight, one replaceable desired state. No queue of obsolete RGB levels.
    public sealed class LatestCommandPump<T> where T:class {
        readonly object gate=new object();readonly Func<T,Task> execute;
        T pending;TaskCompletionSource<bool> completion;
        public LatestCommandPump(Func<T,Task> execute){this.execute=execute;}
        public Task Submit(T command){
            TaskCompletionSource<bool> start=null;Task task;
            lock(gate){pending=command;if(completion==null){completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);start=completion;}task=completion.Task;}
            if(start!=null)Drain(start);return task;
        }
        async void Drain(TaskCompletionSource<bool> batch){
            try{while(true){T next;lock(gate){next=pending;pending=null;if(next==null){completion=null;break;}}await execute(next);}batch.TrySetResult(true);}
            catch(Exception error){lock(gate){pending=null;completion=null;}batch.TrySetException(error);}
        }
    }
}
