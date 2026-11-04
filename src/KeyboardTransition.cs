using System;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace GTBook {
    // One UI writer and one animation reader. Page-backed memory avoids files, polling
    // threads and restarting the HID controller when a key changes the running effect.
    public sealed class KeyboardChannel : IDisposable {
        const int Capacity=8192,PayloadOffset=32;
        readonly MemoryMappedFile memory;readonly MemoryMappedViewAccessor view;
        readonly EventWaitHandle changed,acknowledged;readonly object gate=new object();
        int sequence,seen;bool disposed;
        public readonly string Id;
        public WaitHandle Changed{get{return changed;}}
        public KeyboardChannel(string id=null){
            Id=id??Guid.NewGuid().ToString("N");Guid parsed;if(!Guid.TryParseExact(Id,"N",out parsed))throw new ArgumentException("Invalid keyboard session.");
            string name=@"Local\GTBookKeyboard-"+Id;
            try{
                memory=id==null?MemoryMappedFile.CreateNew(name,Capacity):MemoryMappedFile.OpenExisting(name);
                view=memory.CreateViewAccessor(0,Capacity);
                changed=new EventWaitHandle(false,EventResetMode.AutoReset,name+"-change");
                acknowledged=new EventWaitHandle(false,EventResetMode.AutoReset,name+"-ack");
            }catch{Dispose();throw;}
        }
        public Reply Update(Request request,int timeout=3000){lock(gate){
            if(disposed)return Reply.Fail("Keyboard session is closing.");
            Protocol.Validate(request);if(request.Action!="keyboard-animation"||!ExtraEffects.Contains(request.Effect))throw new ArgumentException("Choose an Extra effect.");
            byte[] data=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(request));if(data.Length>Capacity-PayloadOffset)throw new ArgumentException("Keyboard update is too large.");
            int current=++sequence;acknowledged.Reset();
            view.WriteArray(PayloadOffset,data,0,data.Length);view.Write(4,data.Length);Thread.MemoryBarrier();view.Write(0,current);changed.Set();
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(watch.ElapsedMilliseconds<timeout){
                if(!acknowledged.WaitOne(Math.Max(1,timeout-(int)watch.ElapsedMilliseconds)))break;
                if(view.ReadInt32(8)!=current)continue;
                return view.ReadInt32(12)==1?new Reply{Ok=true,Message="Keyboard transition applied."}:Reply.Fail("The keyboard transition could not complete. Retry after checking the device.");
            }
            return Reply.Fail("Keyboard update timed out. The new setting is not confirmed.");
        }}
        public bool TryRead(out Request request,out int revision){
            request=null;revision=view.ReadInt32(0);if(revision==seen||revision==0)return false;
            Thread.MemoryBarrier();int length=view.ReadInt32(4);if(length<1||length>Capacity-PayloadOffset)throw new IOException("Invalid keyboard update length.");
            var data=new byte[length];view.ReadArray(PayloadOffset,data,0,length);
            request=new JavaScriptSerializer().Deserialize<Request>(Encoding.UTF8.GetString(data));Protocol.Validate(request);
            if(request.Action!="keyboard-animation"||!ExtraEffects.Contains(request.Effect))throw new IOException("Invalid live keyboard update.");
            seen=revision;return true;
        }
        public void Acknowledge(int revision,bool success){view.Write(12,success?1:-1);Thread.MemoryBarrier();view.Write(8,revision);acknowledged.Set();}
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;if(acknowledged!=null)acknowledged.Dispose();if(changed!=null)changed.Dispose();if(view!=null)view.Dispose();if(memory!=null)memory.Dispose();}}
    }

    // Interpolate actual emitted colours and intensity; never insert a black/reset frame.
    public sealed class KeyboardTransition {
        public const double Duration=.20;
        Request target;double phaseOrigin,phaseOffset,started;
        Color[] from,last;double fromBrightness,lastBrightness;
        public KeyboardTransition(Request initial){target=initial;lastBrightness=initial.Brightness;}
        public Request Target{get{return target;}}
        public double Brightness{get{return lastBrightness;}}
        public bool Settled(double seconds){return from==null||seconds-started>=Duration;}
        public void Change(Request next,double seconds){
            Color[] current=last??Frame(seconds);from=(Color[])current.Clone();fromBrightness=lastBrightness;
            double phase=(seconds-phaseOrigin)*ExtraEffects.SpeedScale(target.Speed)+phaseOffset;
            // Brightness and speed changes retain the exact animation phase.
            phaseOffset=next.Effect==target.Effect?phase:0;phaseOrigin=seconds;started=seconds;target=next;
        }
        public Color[] Frame(double seconds){
            double phase=(Math.Max(0,seconds-phaseOrigin)*ExtraEffects.SpeedScale(target.Speed)+phaseOffset)/ExtraEffects.SpeedScale(target.Speed);
            var desired=ExtraEffects.Frame(target,phase);
            double x=from==null?1:Math.Max(0,Math.Min(1,(seconds-started)/Duration));double blend=x*x*(3-2*x);
            lastBrightness=from==null?target.Brightness:fromBrightness+(target.Brightness-fromBrightness)*blend;
            if(from!=null&&blend<1)for(int z=0;z<4;z++)desired[z]=LightingColour.MixHue(from[z],desired[z],blend);
            if(blend>=1)from=null;last=desired;return desired;
        }
        public byte[][] Packets(double seconds){
            var colours=Frame(seconds);var packets=new byte[4][];int[] commands={0x60,0x64,0x70,0x74};
            for(int z=0;z<4;z++)packets[z]=Protocol.Keyboard(commands[z],LightingColour.Hex(colours[z]),(int)Math.Round(lastBrightness));return packets;
        }
    }
}
