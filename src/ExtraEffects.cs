using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace GTBook {
    public static class ExtraEffects {
        public static readonly string[] Names={"Aurora","Rainbow Prism","Blazing Fire","Comet Trail","Ocean Tide","Sunset Glow","Neon Pulse","Candy Chase","Magnetic Slingshot","Orbital Eclipse"};
        public static bool Contains(string name){return Names.Contains(name);}
        public static bool HasDirection(string name){return Contains(name)&&name!="Magnetic Slingshot";}
        public static bool UsesColours(string name){return new[]{"Aurora","Comet Trail","Neon Pulse","Magnetic Slingshot","Orbital Eclipse"}.Contains(name);}
        public static string Description(string name){
            string[] descriptions={"Saturated ribbons drift smoothly across four zones.","A rotating rainbow, one colour per zone.","Warm, gently shifting ember and flame colours.","A bright comet wraps seamlessly with a fading colour trail.","Deep blue and turquoise waves roll across the keyboard.","Orange, coral and pink drift like a sunset.","Two neon colours breathe in staggered waves.","Vivid pink, cyan, gold and violet glide between zones.","Two ribbons cross, trade colours and spring back.","A moving shadow orbits between two coloured rims."};
            int i=Array.IndexOf(Names,name);return i<0?"":descriptions[i];
        }
        static double Unit(double value){return Math.Max(0,Math.Min(1,value));}
        static double Smooth(double value){value=Unit(value);return value*value*(3-2*value);}
        static double Glow(double x,double centre,double width){return Math.Exp(-.5*Math.Pow((x-centre)/width,2));}
        static double RingGlow(double x,double centre,double width){double d=((x-centre+2)%4+4)%4-2;return Glow(d,0,width)+Glow(d,4,width)+Glow(d,-4,width);}
        static Color Hue(double h){return LightingColour.Hsv(h,1,1);}
        public static double SpeedScale(int speed){return Math.Pow(2,(Math.Max(1,Math.Min(9,speed))-4)/5.0);}
        public static Color[] Frame(Request r,double seconds){
            if(!Contains(r.Effect))throw new ArgumentException("Unknown Extra animation.");
            // Six seconds per base revolution; bounded speed keeps all four zones readable.
            double t=Math.Max(0,seconds)*SpeedScale(r.Speed),angle=t*Math.PI/3;
            Color primary=LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[0])),secondary=LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[1]));
            // Respect identical colours instead of silently inventing a second colour.
            var zones=new Color[4];Color[] candy={Hue(.92),Hue(.50),Hue(.12),Hue(.75)};
            for(int z=0;z<4;z++){
                double wave=(Math.Sin(angle-z*Math.PI/2)+1)/2;
                switch(r.Effect){
                    case "Aurora":zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,wave),.62+.38*(Math.Sin(angle+z*Math.PI/2)+1)/2);break;
                    case "Rainbow Prism":zones[z]=Hue(z/4.0-t/6);break;
                    case "Blazing Fire":double heat=wave;zones[z]=LightingColour.Dim(Hue(.008+.12*heat),.58+.42*heat);break;
                    case "Comet Trail":double head=t*4/6,crest=RingGlow(z,head,.48),tail=.55*RingGlow(z,head-.65,.65)+.25*RingGlow(z,head-1.3,.8);zones[z]=LightingColour.Dim(LightingColour.MixHue(secondary,primary,crest),Unit(.035+.73*crest+.30*tail));break;
                    case "Ocean Tide":zones[z]=LightingColour.Dim(LightingColour.MixHue(Hue(.65),Hue(.48),wave),.5+.5*wave);break;
                    case "Sunset Glow":zones[z]=LightingColour.MixHue(Hue(.09),Hue(.94),wave);break;
                    case "Neon Pulse":zones[z]=LightingColour.Dim(z%2==0?primary:secondary,.06+.94*Math.Pow(wave,1.5));break;
                    case "Candy Chase":double position=((z-t*4/6)%4+4)%4;int index=(int)position;zones[z]=LightingColour.MixHue(candy[index],candy[(index+1)%4],Smooth(position-index));break;
                    case "Magnetic Slingshot":double left=1.5-1.5*Math.Cos(angle),right=3-left,a=Glow(z,left,.75),b=Glow(z,right,.75);zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,b/Math.Max(.001,a+b)),.09+.91*Math.Max(a,b));break;
                    default:double orbit=t*4/6,shadow=RingGlow(z,orbit,.48),rim=Math.Max(RingGlow(z,orbit-.85,.42),RingGlow(z,orbit+.85,.42));zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,rim),Unit(.78-.70*shadow+.22*rim));break;
                }
            }
            if(r.Direction==1&&HasDirection(r.Effect))Array.Reverse(zones);
            return zones;
        }
        public static byte[][] Packets(Request r,double seconds){
            Color[] zones=Frame(r,seconds);int[] commands={0x60,0x64,0x70,0x74};var packets=new byte[4][];
            for(int i=0;i<4;i++)packets[i]=Protocol.Keyboard(commands[i],"#"+zones[i].R.ToString("X2")+zones[i].G.ToString("X2")+zones[i].B.ToString("X2"),r.Brightness);
            return packets;
        }
    }
    public sealed class HardwareLease : IDisposable {
        public const int CommandTimeoutMilliseconds=3000;
        readonly Mutex mutex;bool owned;
        public HardwareLease(string name=@"Global\GTBookStableHardware"){mutex=new Mutex(false,name);}
        public bool Acquire(int timeout){try{owned=mutex.WaitOne(timeout);}catch(AbandonedMutexException){owned=true;}return owned;}
        public void Release(){if(owned){mutex.ReleaseMutex();owned=false;}}
        public void Dispose(){Release();mutex.Dispose();}
    }
    public static class KeyboardAnimation {
        public sealed class Pacer {
            double next;
            public void Reset(double milliseconds){next=milliseconds;}
            public int Delay(double elapsedMilliseconds,int interval=67){next+=interval;if(next<elapsedMilliseconds-interval)next=elapsedMilliseconds;return Math.Max(0,(int)Math.Ceiling(next-elapsedMilliseconds));}
        }
        public const string StopName=@"Local\GTBookKeyboardStop",ReadyName=@"Local\GTBookKeyboardReady";
        public static bool OwnerAlive(int id,long started){try{using(var process=Process.GetProcessById(id))return !process.HasExited&&process.StartTime.ToUniversalTime().Ticks==started;}catch{return false;}}
        // Shared by the real transport and deterministic stop/failure tests.
        public static int Stream(Request r,Func<bool> stopping,Func<bool> ownerAlive,Func<byte[][],bool> write,Action ready,Func<double> seconds,Action wait){
            int frames=0;double nextCheck=0;
            while(!stopping()){
                double time=seconds();if(time>=nextCheck){if(!ownerAlive())break;nextCheck=time+1;}
                if(write(ExtraEffects.Packets(r,time))){if(frames++==0)ready();}wait();
            }
            return frames;
        }
        public static int LiveStream(Request initial,Func<bool> stopping,Func<bool> ownerAlive,Func<byte[][],bool> write,Action<bool> power,Func<Request> update,Action applied,Action ready,Func<double> seconds,Action<int> wait){
            var transition=new KeyboardTransition(initial);bool powered=initial.Brightness>0,pending=false;int frames=0;double nextCheck=0;
            while(!stopping()){
                double time=seconds();if(time>=nextCheck){if(!ownerAlive())break;nextCheck=time+1;}
                var next=update();
                if(next!=null){
                    if(next.Brightness>0&&!powered){power(true);powered=true;}
                    transition.Change(next,time);pending=true;
                }
                if(write(transition.Packets(time))){
                    if(frames++==0)ready();
                    if(transition.Settled(time)){
                        if(transition.Target.Brightness==0&&powered){power(false);powered=false;}
                        if(pending){applied();pending=false;}
                    }
                }
                wait(pending?33:transition.Target.Brightness==0?1000:67);
            }
            return frames;
        }
        public static Reply Run(Request r){
            if(!OwnerAlive(r.OwnerProcessId,r.OwnerStartedTicks))throw new IOException("The animation owner is no longer running.");
            using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,StopName))
            using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,ReadyName))
            using(var lease=new HardwareLease())
            using(var channel=string.IsNullOrEmpty(r.KeyboardSession)?null:new KeyboardChannel(r.KeyboardSession)){
                if(!lease.Acquire(3000))throw new IOException("Another hardware command is busy. Retry after it finishes.");
                if(stop.WaitOne(0)||!OwnerAlive(r.OwnerProcessId,r.OwnerStartedTicks))return new Reply{Ok=true,Message="Animation cancelled before starting."};
                KeyboardDevice keyboard=null;int revision=0;
                try {
                    using(var oem=new OemProtocol())oem.KeyLight(r.Brightness>0);
                    keyboard=new KeyboardDevice();
                    if(r.KeepAwake&&r.ArmKeyboardAwake)keyboard.Send(Protocol.Keyboard(0x19,"#000000",0));
                    // Enter the reviewed static-zone mode once per connection. Live changes
                    // reuse this connection and never repeat its initial mode/watchdog setup.
                    var first=ExtraEffects.Frame(r,0);
                    keyboard.Send(Protocol.Keyboard(0x11,LightingColour.Hex(first[0]),r.Brightness));
                    bool awake=r.KeepAwake&&r.ArmKeyboardAwake;
                    lease.Release();
                    if(r.Brightness==0&&channel==null)return new Reply{Ok=true,Message="Keyboard off · brightness is 0%."};
                    var clock=Stopwatch.StartNew();var pacer=new Pacer();double nextConflict=1;byte[][] previous=null;
                    int frames=LiveStream(r,()=>stop.WaitOne(0),()=>{
                        if(clock.Elapsed.TotalSeconds>=nextConflict){nextConflict=clock.Elapsed.TotalSeconds+1;string conflict=Hardware.Conflict();if(conflict.Length>0)throw new IOException(conflict);}
                        return OwnerAlive(r.OwnerProcessId,r.OwnerStartedTicks);
                    },packets=>{
                        if(previous!=null&&packets.Select((p,i)=>p.SequenceEqual(previous[i])).All(equal=>equal))return true;
                        if(!lease.Acquire(100))return false;
                        try{for(int i=0;i<4;i++)if(previous==null||!packets[i].SequenceEqual(previous[i]))keyboard.Send(packets[i]);previous=packets;return true;}finally{lease.Release();}
                    },enabled=>{if(!lease.Acquire(3000))throw new IOException("Keyboard power control is busy.");try{using(var oem=new OemProtocol())oem.KeyLight(enabled);}finally{lease.Release();}},()=>{
                        Request next;int read;
                        if(channel==null||!channel.TryRead(out next,out read))return null;
                        if(next.OwnerProcessId!=r.OwnerProcessId||next.OwnerStartedTicks!=r.OwnerStartedTicks||next.KeyboardSession!=r.KeyboardSession)throw new IOException("Keyboard update owner does not match.");
                        if(next.KeepAwake&&next.ArmKeyboardAwake&&!awake){
                            if(!lease.Acquire(3000))throw new IOException("Keyboard configuration is busy.");
                            try{keyboard.Send(Protocol.Keyboard(0x19,"#000000",0));awake=true;}finally{lease.Release();}
                        }
                        revision=read;return next;
                    },()=>{if(channel!=null){channel.Acknowledge(revision,true);revision=0;}},()=>ready.Set(),()=>clock.Elapsed.TotalSeconds,interval=>{
                        int delay=pacer.Delay(clock.Elapsed.TotalMilliseconds,interval);
                        if(channel==null)stop.WaitOne(delay);else if(WaitHandle.WaitAny(new[]{stop,channel.Changed},delay)==1)pacer.Reset(clock.Elapsed.TotalMilliseconds);
                    });
                    var result=new Reply{Ok=true,Message="Extra animation stopped · last frame held."};result.Data["frames"]=frames.ToString();return result;
                }finally{lease.Release();if(revision!=0&&channel!=null)channel.Acknowledge(revision,false);if(keyboard!=null)keyboard.Dispose();}
            }
        }
        public static System.Collections.Generic.Dictionary<string,object> VerifyHardware(){
            var report=new System.Collections.Generic.Dictionary<string,object>();
            var cases=new System.Collections.Generic.List<object>();report["effects"]=cases;
            bool created;using(var instance=new Mutex(true,@"Local\GTBookStableUI",out created)){
                if(!created)throw new IOException("Close Toolkit before running the bounded animation check.");
                var identity=Identity.Read();if(!identity.Supported)throw new IOException("No reviewed hardware profile for this device.");
                string conflict=Hardware.Conflict();if(conflict.Length>0)throw new IOException(conflict);
                var saved=Store.Load("keyboard");if(ExtraEffects.Contains(saved.Effect))throw new IOException("Select an Official effect before the hardware check, so it can be restored exactly.");
                var restore=new Request{Action="keyboard",Effect=saved.Effect,Colors=(string[])saved.Colors.Clone(),Brightness=saved.Brightness,KeepAwake=saved.KeepAwake,ZoneEffect=saved.ZoneEffect};Protocol.Validate(restore);
                bool wasOff;using(var lease=new HardwareLease()){if(!lease.Acquire(3000))throw new IOException("Hardware is busy.");using(var oem=new OemProtocol())wasOff=(oem.Read(0x5e)&1)!=0;}
                try {
                    using(var owner=Process.GetCurrentProcess())foreach(string name in ExtraEffects.Names){
                        using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,StopName))
                        using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,ReadyName))
                        using(var cancel=new ManualResetEvent(false)){
                            stop.Reset();ready.Reset();var end=new Thread(()=>{if(!cancel.WaitOne(1500))stop.Set();});end.IsBackground=true;end.Start();
                            try{
                                var request=new Request{Action="keyboard-animation",Effect=name,Colors=(string[])saved.Colors.Clone(),Brightness=Math.Max(20,Math.Min(65,saved.Brightness)),KeepAwake=saved.KeepAwake,Speed=4,OwnerProcessId=owner.Id,OwnerStartedTicks=owner.StartTime.ToUniversalTime().Ticks};
                                Reply result=Hardware.Execute(request);bool ok=result.Ok&&ready.WaitOne(0)&&result.Data.ContainsKey("frames");
                                cases.Add(new {effect=name,transport_ok=ok,frames=result.Data.ContainsKey("frames")?result.Data["frames"]:"0"});
                                if(!ok)throw new IOException(name+" did not send an initial frame: "+result.Message);
                            }finally{cancel.Set();end.Join();}
                        }
                    }
                    report["ok"]=true;
                }catch(Exception e){report["ok"]=false;report["error"]=e.Message;}
                finally {
                    try{Reply restored=Hardware.Execute(restore);report["saved_profile_restored"]=restored.Ok;report["saved_effect"]=restore.Effect;
                        using(var lease=new HardwareLease()){if(!lease.Acquire(3000))throw new IOException("Could not restore keyboard power gate.");using(var oem=new OemProtocol()){oem.KeyLight(!wasOff);report["power_gate_restored"]=((oem.Read(0x5e)&1)!=0)==wasOff;}}
                    }catch(Exception e){report["ok"]=false;report["restoration_error"]=e.Message;}
                }
                report["physical_appearance_verified"]=false;return report;
            }
        }
    }
}
