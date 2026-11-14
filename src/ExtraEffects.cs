using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace GTBook {
    public static class ExtraEffects {
        public static readonly string[] Names={"Aurora","Rainbow Prism","Blazing Fire","Comet Trail","Ocean Tide","Sunset Glow","Neon Pulse","Candy Chase","Magnetic Slingshot","Orbital Eclipse","Zoom Bloom","Meteor Shower","Starlight","Colour Cascade","Silk Flow"};
        const double Tau=2*Math.PI;
        static readonly Color[] Candy={Hue(.92),Hue(.50),Hue(.12),Hue(.75)};
        static readonly Color[] Sunset={Hue(.98),Hue(.045),Hue(.105),Hue(.91)};
        static readonly Color[] Ocean={Hue(.62),Hue(.50),Hue(.43),Hue(.55)};
        static readonly double[] StarOffsets={.08,.61,.34,.86};
        public static bool Contains(string name){return Names.Contains(name);}
        public static bool HasDirection(string name){return Contains(name)&&name!="Magnetic Slingshot"&&name!="Zoom Bloom"&&name!="Starlight";}
        public static bool UsesColours(string name){return Contains(name)&&name!="Rainbow Prism"&&name!="Blazing Fire"&&name!="Ocean Tide"&&name!="Sunset Glow"&&name!="Candy Chase";}
        public static string Description(string name){
            string[] descriptions={"Layered colour ribbons unfurl with a luminous crest.","A full-spectrum ribbon glides through four vivid zones.","Crimson embers ripple into amber and golden flame.","A bright colour crest pulls a long, velvety trail.","Cobalt depths swell into luminous turquoise tides.","Ruby, coral, gold and magenta melt into one another.","Alternating neon colours rise and fall in soft pulses.","Pink, cyan, gold and violet flow in silky ribbons.","Two glowing ribbons meet, exchange colours and rebound.","A velvet shadow glides between two bright colour rims.","A luminous bloom expands from the centre and folds back.","Twin shooting stars sweep past with overlapping trails.","Soft, scattered colour glints shimmer over a dim glow.","A curtain of colour fills each zone, then gently recedes.","Two colours weave through a slow, fluid light current."};
            int i=Array.IndexOf(Names,name);return i<0?"":descriptions[i];
        }
        static double Unit(double value){return Math.Max(0,Math.Min(1,value));}
        // Quintic envelopes settle with zero velocity and acceleration.
        static double Smooth(double value){value=Unit(value);return value*value*value*(value*(value*6-15)+10);}
        static double Pulse(double phase){return .5+.5*Math.Cos(Tau*phase);}
        static double Glow(double x,double centre,double width){return Math.Exp(-.5*Math.Pow((x-centre)/width,2));}
        static double RingGlow(double x,double centre,double width){double d=((x-centre+2)%4+4)%4-2;return Glow(d,0,width)+Glow(d,4,width)+Glow(d,-4,width);}
        static Color Hue(double h){return LightingColour.Hsv(h,1,1);}
        // The gentler cubic slope keeps large palette hue changes readable at 15 Hz.
        static Color Ribbon(Color[] palette,double position){position=(position%4+4)%4;int index=(int)position;double x=position-index;return LightingColour.MixHue(palette[index],palette[(index+1)%4],x*x*(3-2*x));}
        public static double SpeedScale(int speed){return Math.Pow(2,(Math.Max(1,Math.Min(9,speed))-4)/5.0);}
        public static Color[] Frame(Request r,double seconds){
            if(!Contains(r.Effect))throw new ArgumentException("Unknown Extra animation.");
            // All patterns close over six base seconds. Wrap phase before harmonics to
            // keep long sessions stable; the existing bounded 15 Hz HID stream is reused.
            double phase=(Math.Max(0,seconds)*SpeedScale(r.Speed)%6)/6,angle=phase*Tau;
            Color primary=LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[0])),secondary=LightingColour.Vivid(ColorTranslator.FromHtml(r.Colors[1]));
            // Respect identical colours instead of silently inventing a second colour.
            var zones=new Color[4];
            for(int z=0;z<4;z++){
                double local=angle-z*Math.PI/2,wave=(Math.Sin(local)+1)/2;
                switch(r.Effect){
                    case "Aurora":
                        double drape=Smooth(Unit(.5+.36*Math.Sin(local)+.18*Math.Cos(2*angle+z*Math.PI/3)));
                        zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,drape),.28+.72*Smooth(wave));break;
                    case "Rainbow Prism":
                        double prismShimmer=.78+.22*Pulse(phase*2+z/3.0);
                        zones[z]=LightingColour.Dim(Hue(z/4.0-phase+.02*Math.Sin(angle)),prismShimmer);break;
                    case "Blazing Fire":
                        double flame=Smooth(.5+.30*Math.Sin(local)+.15*Math.Sin(2*angle+z*Math.PI)+.08*Math.Cos(3*angle-z));
                        double ember=.85+.15*Pulse(phase*3+z*.6);
                        zones[z]=LightingColour.Dim(Hue(.005+.118*flame),Unit((.28+.72*flame)*ember));break;
                    case "Comet Trail":
                        double head=phase*4,crest=RingGlow(z,head,.55),tail=.60*RingGlow(z,head-.75,.72)+.25*RingGlow(z,head-1.5,.90);
                        zones[z]=LightingColour.Dim(LightingColour.MixHue(secondary,primary,crest),Unit(.03+.84*crest+.22*tail));break;
                    case "Ocean Tide":
                        double tide=Smooth(.5+.36*Math.Sin(local)+.16*Math.Sin(2*angle-z*Math.PI/2));
                        double seafoam=Smooth(.5+.36*Math.Sin(local+Math.PI/3));
                        Color oceanColor=LightingColour.MixHue(Hue(.66),Hue(.44),seafoam);
                        zones[z]=LightingColour.Dim(oceanColor,.22+.78*tide);break;
                    case "Sunset Glow":
                        zones[z]=LightingColour.Dim(Ribbon(Sunset,z*.55-phase*4),.76+.24*wave);break;
                    case "Neon Pulse":
                        double beat=Math.Pow(Pulse(phase+(z%2==0?0:.5)),3);
                        zones[z]=LightingColour.Dim(z%2==0?primary:secondary,.08+.92*Smooth(beat));break;
                    case "Candy Chase":
                        double pop=.75+.25*Pulse(phase*4-z*.8);
                        zones[z]=LightingColour.Dim(Ribbon(Candy,z-phase*4),pop);break;
                    case "Magnetic Slingshot":
                        double left=1.5-1.5*Math.Cos(angle),right=3-left,a=Glow(z,left,.68),b=Glow(z,right,.68);
                        double collide=Glow(Math.Abs(left-right),0,.6);
                        zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,b/Math.Max(.001,a+b)),Unit(.05+.78*Math.Max(a,b)+.17*collide));break;
                    case "Orbital Eclipse":
                        double orbit=phase*4,shadow=RingGlow(z,orbit,.56),rim=RingGlow(z,orbit-.95,.55);
                        zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,rim),Unit(.82-.78*shadow+.22*rim));break;
                    case "Zoom Bloom":
                        // A one-dimensional radial wave: the two centre zones open
                        // together, reach the outer pair, then fold back without a cut.
                        double radius=1-Math.Cos(angle),distance=Math.Abs(z-1.5),bloom=Glow(distance,radius,.62);
                        zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,Smooth(radius/2)),.065+.935*bloom);break;
                    case "Meteor Shower":
                        double first=RingGlow(z,phase*4,.54),second=RingGlow(z,phase*4-2,.62),wake=RingGlow(z,phase*4-.8,.78);
                        double spark=.88+.12*Pulse(phase*4+z*.5);
                        zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,second/Math.Max(.001,first+second)),Unit((.025+.86*first+.56*second+.15*wake)*spark));break;
                    case "Starlight":
                        // Fixed phase offsets give a scattered twinkle, without random
                        // frame noise, hard flashes, or pretending to address each key.
                        double glint=Math.Pow(Pulse(phase+StarOffsets[z]),3),halo=Pulse(2*phase+z*.31);
                        zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,Smooth(halo)),.09+.91*glint);break;
                    case "Colour Cascade":
                        double fill=Smooth(Unit(.5+.45*Math.Sin(local)+.20*Math.Sin(2*angle-z*.9)));
                        double crestCascade=Pulse(phase-z/4.0);
                        zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,fill),.32+.68*Smooth(crestCascade));break;
                    case "Silk Flow": // Broad, intertwined currents rather than a chase.
                        double silk=Unit(.5+.32*Math.Sin(angle-z*.75)+.18*Math.Sin(2*angle+z*.55));
                        zones[z]=LightingColour.Dim(LightingColour.MixHue(primary,secondary,Smooth(silk)),.62+.38*Pulse(phase+z/6.0));break;
                    default:throw new ArgumentException("Unknown Extra animation.");
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
            using(var keyboardLease=new HardwareLease(@"Global\GTBookStableKeyboard"))
            using(var channel=string.IsNullOrEmpty(r.KeyboardSession)?null:new KeyboardChannel(r.KeyboardSession)){
                if(!keyboardLease.Acquire(0))throw new IOException("Another keyboard controller is active.");
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
                    var clock=Stopwatch.StartNew();var pacer=new Pacer();byte[][] previous=null;
                    string conflictFailure="";int checkingConflict=0;
                    using(var monitor=new System.Threading.Timer(state=>{
                        if(Interlocked.Exchange(ref checkingConflict,1)!=0)return;
                        try{
                            string c=Hardware.Conflict();
                            if(!string.IsNullOrEmpty(c))Volatile.Write(ref conflictFailure,c);
                        }catch{}finally{Volatile.Write(ref checkingConflict,0);}
                    },null,2000,5000)){
                    int frames=LiveStream(r,()=>stop.WaitOne(0),()=>{
                        string conflict=Volatile.Read(ref conflictFailure);if(conflict.Length>0)throw new IOException(conflict);
                        return OwnerAlive(r.OwnerProcessId,r.OwnerStartedTicks);
                    },packets=>{
                        if(previous!=null&&packets.Select((p,i)=>p.SequenceEqual(previous[i])).All(equal=>equal))return true;
                        for(int i=0;i<4;i++)if(previous==null||!packets[i].SequenceEqual(previous[i])){if(i>0)Thread.Sleep(1);keyboard.Send(packets[i]);}previous=packets;return true;
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
                    }
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
