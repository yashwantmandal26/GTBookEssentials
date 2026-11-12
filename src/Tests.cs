using System;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace GTBook {
    public static class Tests {
        public static int Run(string report) {
            var output=new StringBuilder(); int passed=0,failed=0;
            Action<string,Action> test=(name,action)=>{try{action();output.AppendLine("PASS "+name);passed++;}catch(Exception e){output.AppendLine("FAIL "+name+": "+e.Message);failed++;}};
            test("Keyboard OEM static layout and checksum",()=>{
                byte[] p=Protocol.Keyboard(0x11,"#129AFE",100);
                Check(p.Length==64 && p[0]==6 && p[1]==0x11 && p[2]==4,"header");
                Check(p[7]==0x12 && p[8]==0x9a && p[9]==0xfe && p[10]==255,"colour / PWM");
                Check(p[63]==(p.Skip(1).Take(62).Sum(x=>(int)x)&255),"checksum");
            });
            test("Brightness endpoints and rounding",()=>{
                Check(Protocol.Keyboard(0x12,"#FFFFFF",0)[10]==0,"off");
                Check(Protocol.Keyboard(0x14,"#FFFFFF",50)[10]==128,"half brightness");
                foreach(int command in new[]{0x60,0x64,0x70,0x74})Check(Protocol.Keyboard(command,"#010203",65)[1]==command,"zone command");
            });
            test("Rear OEM byte offsets and brightness",()=>{
                byte[] p=Protocol.Rear("Breathing","#129AFE",100,9);
                Check(p.Length==17 && p[0]==0x34 && p[1]==0x0e && p[2]==2,"header");
                Check(p[3]==0x12 && p[5]==0x9a && p[7]==0xfe && p[9]==9 && p[10]==44,"parameters");
                Check(p[4]==0 && p[6]==0 && p[8]==0 && p.Skip(11).All(x=>x==0),"reserved fields");
            });
            test("Invalid values never produce a hardware command",()=>{
                MustThrow(()=>Protocol.Validate(new Request{Action="firmware-flash"}));
                MustThrow(()=>Protocol.Validate(new Request{Mode=3}));
                MustThrow(()=>Protocol.Validate(new Request{Action="mode",Mode=4}));
                MustThrow(()=>Protocol.Validate(new Request{Brightness=-1}));
                MustThrow(()=>Protocol.Validate(new Request{Brightness=101}));
                MustThrow(()=>Protocol.Validate(new Request{Colors=new[]{"#123456"}}));
                MustThrow(()=>Protocol.Validate(new Request{Colors=new[]{"invalid","#123456","#123456","#123456"}}));
                MustThrow(()=>Protocol.Validate(new Request{Action="rear",Effect="Rainbow"}));
                MustThrow(()=>Protocol.Keyboard(0xff,"#FFFFFF",100));
            });
            test("Model and firmware guard",()=>{
                Check(new Identity{Manufacturer="INFINIX",Model="GL613",Bios="1.00"}.Supported,"reviewed identity");
                Check(!new Identity{Manufacturer="Other",Model="GL613",Bios="1.00"}.Supported,"other manufacturer");
                Check(!new Identity{Manufacturer="INFINIX",Model="GL613",Bios="2.00"}.Supported,"unreviewed BIOS");
            });
            test("Worker transport rejects unknown command without device access",()=>{
                Reply reply=WorkerClient.Run(new Request{Action="invalid"});
                Check(!reply.Ok && reply.Message=="Unknown command.",reply.Message);
            });
            test("Worker startup is bounded",()=>{
                Reply reply=WorkerClient.Run(new Request{Action="invalid"},0);
                Check(!reply.Ok && reply.Message.Contains("timed out"),reply.Message);
            });
            test("Request JSON roundtrip preserves all four zones",()=>{
                var serializer=new JavaScriptSerializer(); var a=new Request{Action="keyboard",Effect="Four zones",Colors=new[]{"#010203","#040506","#070809","#AABBCC"},Brightness=32};
                var b=serializer.Deserialize<Request>(serializer.Serialize(a)); Protocol.Validate(b);
                Check(b.Colors.SequenceEqual(a.Colors) && b.Brightness==32 && b.Effect==a.Effect,"request fields");
            });
            test("Native WMI transport preserves OEM EC read/write sequencing",()=>{
                var calls=new System.Collections.Generic.List<byte[]>();
                using(var native=new OemProtocol((method,buffer)=>{Check(method=="MemIO","MemIO method");calls.Add(buffer);return 1;})) {
                    native.Read(0x40);
                    Check(calls.Count==6,"read sequence length");
                    Check(calls.Select(b=>(int)b[15]).SequenceEqual(new[]{148,146,144,145,147,160}),"read addresses");
                    Check(calls[3][16]==0x40 && calls[4][16]==160 && calls[5][13]==0,"read target and direction");
                    calls.Clear();native.Write(0x40,2);
                    Check(calls.Count==8 && calls[5][16]==0x40 && calls[6][16]==2 && calls[7][16]==161,"write sequence");
                    foreach(var b in calls)Check(b[0]==66 && b[1]==89 && b[2]==68 && b[3]==76 && b[4]==0 && b[5]==3 && b[12]==1 && b[14]==8,"OEM header");
                    MustThrow(()=>native.Write(0xff,0));
                }
            });
            test("Rear and keyboard gates preserve unrelated EC bits",()=>{
                byte written=0;
                using(var native=new OemProtocol((method,b)=>{if(b[15]==160 && b[13]==0)return 0x83;if(b[15]==160 && b[13]==1)written=b[16];return 0;})){
                    native.BackLight(true);Check(written==0x81,"rear gate only");
                    native.KeyLight(true);Check(written==0x82,"keyboard gate only");
                }
            });
            test("Every native keyboard effect has an OEM command",()=>{
                foreach(int command in new[]{0x11,0x12,0x13,0x14,0x15,0x16})Check(Protocol.Keyboard(command,"#445566",100)[1]==command,"effect command");
            });
            test("GPU temperature uses the original DoMethod signature",()=>{
                using(var native=new OemProtocol((method,b)=>{Check(method=="DoMethod","OEM method name");Check(b.Length==24 && b[4]==4 && b[12]==0 && b[13]==0,"read command");return 46;}))Check(native.GetGPUTem()==46,"GPU reading");
            });
            test("All six rear mode packets use documented enum IDs",()=>{
                string[] effects={"Static","Breathing","Rhythm","Beat","Loop","Cover"};
                for(int i=0;i<effects.Length;i++)Check(Protocol.Rear(effects[i],"#445566",50,3)[2]==i+1,"rear mode "+effects[i]);
            });
            test("Startup task runs on battery and uses an interactive elevated tray session",()=>{
                string executable=@"C:\Users\A & B\GTBook.exe";string xml=Startup.Definition(executable,"S-1-5-21-123");
                var doc=new System.Xml.XmlDocument();doc.LoadXml(xml);var ns=new System.Xml.XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t",doc.DocumentElement.NamespaceURI);
                Check(doc.SelectSingleNode("//t:Command",ns).InnerText==executable,"Executable path must be exact, without embedded quotes");
                Check(doc.SelectSingleNode("//t:Arguments",ns).InnerText=="--startup","tray startup flag");
                Check(doc.SelectSingleNode("//t:DisallowStartIfOnBatteries",ns).InnerText=="false","battery startup");
                Check(doc.SelectSingleNode("//t:StopIfGoingOnBatteries",ns).InnerText=="false","battery continuation");
                Check(doc.SelectSingleNode("//t:LogonType",ns).InnerText=="InteractiveToken","visible user session");
                Check(doc.SelectSingleNode("//t:ExecutionTimeLimit",ns).InnerText=="PT0S","no arbitrary runtime limit");
            });
            test("Laptop F keys dispatch brightness and keyboard gestures without repeats",()=>{
                var keys=new ShortcutDecoder();
                Check(keys.Feed(0x70,true,false,false,0)=="brightness-down","F1");
                Check(keys.Feed(0x71,true,false,false,100)=="brightness-up","F2");
                Check(keys.Feed(0x72,true,false,false,200)==null,"F3 press does not apply twice");
                Check(keys.Feed(0x72,false,false,false,300)=="keyboard-brightness","F3 tap");
                keys.Feed(0x72,true,false,false,500);keys.Feed(0x72,true,false,false,800);
                Check(keys.Poll(false,1299)==null,"F3 cannot fire before 0.8 seconds");
                Check(keys.Poll(false,1300)=="keyboard-effect","F3 0.8-second hold");
                Check(keys.Feed(0x72,false,false,false,1800)==null,"release must not apply a second effect");
                Check(keys.Feed(0x72,false,false,false,1900)==null,"duplicate release");
            });
            test("Modified and synthetic keys and Windows media keys are left alone",()=>{
                var keys=new ShortcutDecoder();
                foreach(int key in new[]{0x70,0x71,0x72})Check(keys.Feed(key,true,true,false,0)==null,"normal application modifier shortcuts");
                Check(keys.Feed(0x70,true,false,true,10)==null,"injected F1");
                foreach(int key in new[]{0xad,0xae,0xaf,0xb0,0xb1,0xb3})Check(keys.Feed(key,true,false,false,20)==null,"Windows media key");
                for(int key=0x7e;key<=0x86;key++){Check(keys.Feed(key,true,false,false,30)=="mode-"+((key-0x7e)%3),"OEM performance signal");Check(keys.Feed(key,true,false,false,40)==null,"held OEM signal");keys.Feed(key,false,false,false,50);}
            });
            test("Screen and keyboard brightness obey their endpoints",()=>{
                Check(DisplayBrightness.Clamp(5,-10)==0 && DisplayBrightness.Clamp(98,10)==100,"display endpoints");
                Check(DisplayBrightness.Clamp(48,-10)==38,"relative brightness");
                Check(ShortcutDecoder.NextKeyboardBrightness(100)==50 && ShortcutDecoder.NextKeyboardBrightness(50)==20 && ShortcutDecoder.NextKeyboardBrightness(20)==0 && ShortcutDecoder.NextKeyboardBrightness(0)==100,"original Toolkit keyboard brightness cycle");
                MustThrow(()=>Protocol.Validate(new Request{Action="screen-brightness",Step=0}));
                MustThrow(()=>Protocol.Validate(new Request{Action="screen-brightness",Step=101}));
            });
            test("Graphical preview follows selected colours, zones and brightness",()=>{
                var selected=new Request{Effect="Static",Brightness=100,Colors=new[]{"#FF0000","#00FF00","#0000FF","#FFFFFF"}};
                Check(LivePreview.KeyColour(selected,0,0,0).ToArgb()==System.Drawing.Color.Red.ToArgb(),"selected static colour");
                selected.Effect="Four zones";
                Check(LivePreview.KeyColour(selected,1,.3,0).ToArgb()==System.Drawing.Color.Lime.ToArgb(),"second zone");
                selected.Brightness=0;Check(LivePreview.KeyColour(selected,0,0,0).ToArgb()==LivePreview.KeyColour(selected,3,1,9).ToArgb(),"lights off");
            });
            test("Cached navigation reuses controls across 100 page switches",()=>{
                System.Windows.Forms.Application.EnableVisualStyles();
                using(var form=new MainWindow(true)){
                    var measurements=form.MeasureNavigation();
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)),"navigation-performance.json"),new JavaScriptSerializer().Serialize(measurements));
                }
            });
            test("Lighting fits beside its rear bar at full width and stacks again on narrow windows",()=>{
                using(var form=new MainWindow(true))form.VerifyLightingLayout();
            });
            test("Confirmed rear Off stays grey despite a saved animated effect or edits",()=>{
                var state=new RearPresentation();var draft=new Request{Action="rear",Effect="Loop",Brightness=100};
                state.Applied(new Request{Action="rear-off"});
                var displayed=state.Illustration(draft);Check(LivePreview.IsOff(displayed),"off overrides saved Loop effect");
                var grey=LivePreview.RearColour(displayed,0,40,0);
                Check(grey.R==176&&grey.G==181&&grey.B==173,"neutral off colour");
                Check(grey.ToArgb()==LivePreview.RearColour(displayed,39,40,100).ToArgb(),"off cannot animate");
                draft.Effect="Breathing";state.Edited();Check(LivePreview.IsOff(state.Illustration(draft)),"edits must not imply the device switched on");
                Check(state.Caption(draft).StartsWith("OFF")&&state.Caption(draft).Contains("not applied"),"explicit off / pending indicator");
            });
            test("Explicit draft preview does not change confirmed rear power state",()=>{
                var state=new RearPresentation();var draft=new Request{Action="rear",Effect="Loop",Brightness=100};state.Applied(new Request{Action="rear-off"});
                state.PreviewDraft=true;Check(!LivePreview.IsOff(state.Illustration(draft)),"show requested appearance");
                Check(state.PowerOn==false&&state.Caption(draft).Contains("DEVICE OFF")&&state.Caption(draft).Contains("preview only"),"device remains off");
                state.PreviewDraft=false;Check(LivePreview.IsOff(state.Illustration(draft)),"return to actual off representation");
                state.Applied(draft);Check(state.PowerOn==true&&state.Caption(draft).StartsWith("ON"),"only applied command updates on state");
            });
            test("Rear failures and fresh power reads cannot leave a false confirmed status",()=>{
                var state=new RearPresentation();var draft=new Request{Action="rear",Effect="Loop"};state.Applied(new Request{Action="rear-off"});state.Failed();
                Check(state.PowerOn==null&&state.Caption(draft).StartsWith("NOT CONFIRMED"),"failed write is not a confirmed Off");
                state.ObservePower(false);Check(state.Caption(draft).StartsWith("OFF"),"startup and refresh power read");
                state.ObservePower(true);Check(state.Caption(draft).StartsWith("ON"),"external power change");
                state.Applied(new Request{Action="rear",Brightness=0});Check(LivePreview.IsOff(state.Illustration(draft))&&state.Caption(draft).StartsWith("OFF"),"zero brightness also looks off");
            });
            test("Four-zone patterns repeat deterministically and reverse in exact zone order",()=>{
                foreach(string name in ExtraEffects.Names){
                    var request=new Request{Effect=name,Brightness=80};
                    foreach(double time in new[]{0.0,.25,1.5,4.75}){
                        var forward=ExtraEffects.Frame(request,time);Check(forward.Select(c=>c.ToArgb()).SequenceEqual(ExtraEffects.Frame(request,time).Select(c=>c.ToArgb())),"pattern is nondeterministic");
                        request.Direction=1;var reverse=ExtraEffects.Frame(request,time);request.Direction=0;
                        Check(reverse.Select(c=>c.ToArgb()).SequenceEqual((ExtraEffects.HasDirection(name)?forward.Reverse():forward).Select(c=>c.ToArgb())),"direction does not match the four physical zones");
                    }
                }
                MustThrow(()=>Protocol.Validate(new Request{Direction=2}));
            });
            test("GPU pending requests remain pending only until the Windows boot changes",()=>{
                var state=new GpuPending.State{Boot="boot-a",Before=2,Target=3};
                Check(GpuPending.SameBoot(state,"boot-a"),"same-boot pending state lost");
                Check(!GpuPending.SameBoot(state,"boot-b")&&!GpuPending.SameBoot(state,""),"restart did not clear pending state");
                state.Before=0;Check(!GpuPending.SameBoot(state,"boot-a"),"unknown boot mode accepted");
            });
            test("Window close hides silently while tray Exit and Windows shutdown can terminate",()=>{
                Check(WindowLifecycle.HideInsteadOfExit(false,false,CloseReason.UserClosing),"X must hide");
                Check(!WindowLifecycle.HideInsteadOfExit(false,true,CloseReason.UserClosing),"explicit tray exit must terminate");
                Check(!WindowLifecycle.HideInsteadOfExit(false,false,CloseReason.WindowsShutDown),"shutdown must not be cancelled");
                Check(!WindowLifecycle.HideInsteadOfExit(true,false,CloseReason.UserClosing),"test previews must close");
            });
            test("Fifteen Extra effects are distinct animations accepted by saved settings",()=>{
                Check(ExtraEffects.Names.Length==15&&ExtraEffects.Names.Distinct().Count()==15,"exactly fifteen effects");
                var signatures=new System.Collections.Generic.HashSet<string>();
                foreach(string name in ExtraEffects.Names){
                    var request=new Request{Action="keyboard-animation",Effect=name,Brightness=100};Protocol.Validate(request);
                    var frames=Enumerable.Range(0,18).Select(i=>string.Join(",",ExtraEffects.Frame(request,i*.23).Select(c=>c.ToArgb().ToString("X8")))).ToArray();
                    Check(frames.Distinct().Count()>3,name+" must move over time");Check(signatures.Add(string.Join("|",frames)),name+" duplicates another animation");
                    foreach(int speed in new[]{1,9}){request.Speed=speed;Check(ExtraEffects.Frame(request,1.7).Length==4,"four physical zones");}
                }
                MustThrow(()=>Protocol.Validate(new Request{Action="keyboard-animation",Effect="Static"}));
                MustThrow(()=>Protocol.Validate(new Request{Action="rear",Effect="Aurora"}));
            });
            test("Extra preview colours match the four-zone HID animation frames",()=>{
                foreach(string name in ExtraEffects.Names){var r=new Request{Action="keyboard-animation",Effect=name,Brightness=100};
                    foreach(double time in new[]{0.0,.43,2.19}){var packets=ExtraEffects.Packets(r,time);Check(packets.Length==4,"four zone packets");
                        for(int z=0;z<4;z++){var p=packets[z];Check(p.Length==64&&p[0]==6&&p[1]==new[]{0x60,0x64,0x70,0x74}[z],"zone header");Check(p[63]==(p.Skip(1).Take(62).Sum(b=>(int)b)&255),"checksum");var preview=LivePreview.KeyColour(r,z,z/3.0,time);Check(preview.R==p[7]&&preview.G==p[8]&&preview.B==p[9],"preview and real frame diverged for "+name);}
                    }
                    r.Brightness=0;Check(ExtraEffects.Packets(r,1).All(p=>p[10]==0),"zero brightness");
                }
            });
            test("Animation readiness follows its first completed frame and stops cleanly",()=>{
                int writes=0,waits=0,ready=0;double time=0;
                var r=new Request{Action="keyboard-animation",Effect="Aurora"};
                int frames=KeyboardAnimation.Stream(r,()=>waits>=4,()=>true,p=>{writes++;return true;},()=>{Check(writes==1,"ready before first write");ready++;},()=>time,()=>{time+=.067;waits++;});
                Check(frames==4&&writes==4&&ready==1,"frame and ready lifecycle");
                writes=ready=waits=0;KeyboardAnimation.Stream(r,()=>waits>=3,()=>true,p=>{writes++;return false;},()=>ready++,()=>0,()=>waits++);
                Check(writes==3&&ready==0,"busy bus must not report readiness");
            });
            test("Animation does not write after stop or owner exit and propagates transport failure",()=>{
                var r=new Request{Action="keyboard-animation",Effect="Ocean Tide"};int writes=0;
                KeyboardAnimation.Stream(r,()=>true,()=>true,p=>{writes++;return true;},()=>{},()=>0,()=>{});
                KeyboardAnimation.Stream(r,()=>false,()=>false,p=>{writes++;return true;},()=>{},()=>0,()=>{});Check(writes==0,"stop and owner guards");
                bool disconnected=false;try{KeyboardAnimation.Stream(r,()=>false,()=>true,p=>{throw new IOException("Disconnected");},()=>{},()=>0,()=>{});}catch(IOException){disconnected=true;}Check(disconnected,"disconnect must terminate the stream");
                using(var process=System.Diagnostics.Process.GetCurrentProcess()){Check(KeyboardAnimation.OwnerAlive(process.Id,process.StartTime.ToUniversalTime().Ticks),"valid owner");Check(!KeyboardAnimation.OwnerAlive(process.Id,1),"PID reuse guard");}
            });
            test("Animation pacing includes HID write time and never builds a catch-up queue",()=>{
                var pacer=new KeyboardAnimation.Pacer();Check(pacer.Delay(40)==27,"first frame USB time");Check(pacer.Delay(107)==27,"second frame interval");Check(pacer.Delay(800)==0,"slow transport skips added delay");Check(pacer.Delay(820)==47,"pacing resumes after delay");
            });
            test("Unchanged lighting power gates never write or reset the EC",()=>{
                byte state=0x80;int writes=0;
                using(var oem=new OemProtocol((method,b)=>{if(b[15]==160&&b[13]==0)return state;if(b[15]==160&&b[13]==1){state=b[16];writes++;}return 0;})){
                    oem.KeyLight(true);oem.KeyLight(true);oem.BackLight(true);Check(writes==0,"already-enabled gates were rewritten");
                    oem.KeyLight(false);oem.KeyLight(false);Check(writes==1&&state==0x81,"off must be a single transition");
                    oem.KeyLight(true);Check(writes==2&&state==0x80,"on must preserve unrelated bits");
                }
            });
            test("Native brightness packets preserve mode without watchdog or static reset",()=>{
                var request=new Request{Action="keyboard",Effect="Four zones",Feedback="keyboard-brightness",KeepAwake=true,ArmKeyboardAwake=false};
                var packets=Protocol.NativeKeyboardPackets(request);
                Check(packets.Select(p=>(int)p[1]).SequenceEqual(new[]{0x60,0x64,0x70,0x74}),"four-zone brightness must only update zone PWM");
                request.Effect="Rainbow";packets=Protocol.NativeKeyboardPackets(request);Check(packets.Length==1&&packets[0][1]==0x14,"native mode must not be reset to static");
                request.ArmKeyboardAwake=true;packets=Protocol.NativeKeyboardPackets(request);Check(packets[0][1]==0x19&&packets[0].Skip(3).Take(60).All(b=>b==0),"timeout command must contain no RGB/brightness data");
            });
            test("Extra brightness changes preserve phase and smoothly reach their target",()=>{
                var initial=new Request{Action="keyboard-animation",Effect="Candy Chase",Brightness=100,Speed=7};var transition=new KeyboardTransition(initial);
                var before=transition.Packets(2.3);var target=new Request{Action="keyboard-animation",Effect=initial.Effect,Brightness=20,Speed=initial.Speed};transition.Change(target,2.3);
                var first=transition.Packets(2.3);Check(first.SelectMany(p=>p).SequenceEqual(before.SelectMany(p=>p)),"transition starts with a flash instead of the emitted frame");
                int previous=255;foreach(double at in new[]{2.33,2.37,2.43,2.51}){
                    var frame=transition.Packets(at);Check(frame[0][10]<=previous&&frame[0][10]>=51,"brightness jumped outside its endpoints");previous=frame[0][10];
                    Check(frame.All(p=>p[1]!=0x11&&p[1]!=0x19),"stream issued setup/reset packets");
                }
                var expected=ExtraEffects.Packets(target,2.51);Check(transition.Packets(2.51).SelectMany(p=>p).SequenceEqual(expected.SelectMany(p=>p)),"brightness restarted the effect phase");
            });
            test("Effect and mid-fade changes start from the last emitted frame",()=>{
                var transition=new KeyboardTransition(new Request{Effect="Rainbow Prism",Brightness=100});
                var before=transition.Packets(1);transition.Change(new Request{Effect="Sunset Glow",Brightness=50},1);
                Check(transition.Packets(1).SelectMany(p=>p).SequenceEqual(before.SelectMany(p=>p)),"effect switch inserted a reset frame");
                before=transition.Packets(1.1);transition.Change(new Request{Effect="Ocean Tide",Brightness=80},1.1);
                Check(transition.Packets(1.1).SelectMany(p=>p).SequenceEqual(before.SelectMany(p=>p)),"retarget discarded the visible in-flight frame");
                Check(transition.Packets(1.31).All(p=>p[10]==204)&&transition.Settled(1.31),"retarget failed to settle");
            });
            test("Live keyboard updates acknowledge a complete fade and gate off only at zero",()=>{
                int frames=0,ready=0,ack=0;double time=0;bool sent=false,poweredOff=false;var initial=new Request{Effect="Candy Chase",Brightness=100};
                KeyboardAnimation.LiveStream(initial,()=>time>.5,()=>true,packets=>{frames++;if(poweredOff)Check(packets.All(p=>p[10]==0),"packets lit after gate off");return true;},enabled=>{Check(!enabled&&time>=.26,"gate switched off before fade completed");poweredOff=true;},()=>{if(time>=.06&&!sent){sent=true;return new Request{Effect="Candy Chase",Brightness=0};}return null;},()=>{Check(poweredOff,"ack before final power state");ack++;},()=>ready++,()=>time,delay=>time+=.033);
                Check(frames>6&&ready==1&&ack==1&&poweredOff,"live lifecycle or completion acknowledgement");
            });
            test("Keyboard update channel waits for matching frame acknowledgement and remains reusable",()=>{
                using(var ui=new KeyboardChannel())using(var worker=new KeyboardChannel(ui.Id)){
                    for(int i=0;i<3;i++){
                        var request=new Request{Action="keyboard-animation",Effect="Aurora",Brightness=20+i*10,KeyboardSession=ui.Id};
                        var pending=System.Threading.Tasks.Task.Run(()=>ui.Update(request));Check(worker.Changed.WaitOne(1000),"update did not wake the worker");
                        Request received;int revision;Check(worker.TryRead(out received,out revision)&&received.Brightness==request.Brightness,"wrong request");
                        Check(!pending.IsCompleted,"UI reported success before hardware wrote the target");worker.Acknowledge(revision,true);
                        Check(pending.Wait(1000)&&pending.Result.Ok,"acknowledged update failed");Check(!worker.TryRead(out received,out revision),"same request read twice");
                    }
                }
            });
            test("Keyboard update failures and absent workers cannot report success",()=>{
                using(var ui=new KeyboardChannel())using(var worker=new KeyboardChannel(ui.Id)){
                    var request=new Request{Action="keyboard-animation",Effect="Aurora"};var pending=System.Threading.Tasks.Task.Run(()=>ui.Update(request));Check(worker.Changed.WaitOne(1000),"wake");Request next;int revision;worker.TryRead(out next,out revision);worker.Acknowledge(revision,false);Check(pending.Wait(1000)&&!pending.Result.Ok,"failed frame reported success");
                    Check(!ui.Update(request,25).Ok,"absent acknowledgement reported success");
                    MustThrow(()=>ui.Update(new Request{Action="keyboard-animation",Effect="Static"},25));
                }
            });
            test("USB charging uses OEM writes and bounded firmware read-back",()=>{
                uint state=0;byte ec=0;int reads=0,delays=0;
                using(var oem=new OemProtocol((method,b)=>{
                    if(method=="MemIO"&&b[15]==160&&b[13]==1)ec=b[16];
                    if(method=="SMI"){Check(b[4]==3,"wrong USB SMI command");if(b[12]==1){state=b[16];return 0;}reads++;return reads%2==1?2u:state;}return 0;
                })){
                    foreach(bool enabled in new[]{true,false}){var reply=UsbCharging.Apply(oem,enabled,ms=>{Check(ms==100,"retry interval");delays++;});Check(reply.Ok&&reply.Data["usb"]==(enabled?"On":"Off")&&ec==(enabled?2:0),"USB EC/SMI state or reply incorrect");}
                    Check(delays==4,"firmware lag was not retried");
                }
                int attempts=0;using(var oem=new OemProtocol((method,b)=>method=="SMI"&&b[12]==0?2u:0u)){bool rejected=false;try{UsbCharging.Apply(oem,true,ms=>attempts++);}catch(IOException){rejected=true;}Check(rejected&&attempts==8,"unconfirmed USB state must stop after bounded retries");}
            });
            test("PrtScn captures once per press and consumes the native snipping action",()=>{
                var keys=new ScreenshotKeys();var shot=keys.Feed(0x2c,true,true,false,false,0);Check(shot.Capture&&shot.Consume,"plain Print Screen");
                Check(!keys.Feed(0x2c,true,true,false,false,50).Capture,"key auto-repeat");
                shot=keys.Feed(0x2c,true,false,false,false,100);Check(!shot.Capture&&shot.Consume,"release is not a second screenshot");
                Check(keys.Feed(0x2c,true,true,false,false,500).Capture,"next press");
                var releaseOnly=new ScreenshotKeys();shot=releaseOnly.Feed(0x2c,true,false,false,false,1000);Check(shot.Capture&&shot.Consume,"release-only device");
            });
            test("Shared Insert-PrtScn works without Fn and preserves modified/numpad keys",()=>{
                var keys=new ScreenshotKeys();var shot=keys.Feed(0x2d,true,true,false,false,0);Check(shot.Capture&&shot.Consume,"GTBook top-row shared key");
                shot=keys.Feed(0x2d,false,true,false,false,500);Check(!shot.Capture&&!shot.Consume,"numpad Insert");
                var modified=new ScreenshotKeys();shot=modified.Feed(0x2c,true,true,true,false,0);Check(!shot.Capture&&!shot.Consume,"Windows/Alt Print Screen");
                shot=modified.Feed(0x2c,true,false,false,false,50);Check(!shot.Capture&&!shot.Consume,"modifier released first");
                shot=modified.Feed(0x2d,true,true,true,false,300);Check(!shot.Capture&&!shot.Consume,"Ctrl Insert copy");
                shot=modified.Feed(0x2c,true,true,false,true,1000);Check(!shot.Capture&&!shot.Consume,"injected keys");
            });
            test("Full desktop PNG preserves dimensions and negative-origin bounds with unique saves",()=>{
                string folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)),"screenshot-fixtures");var bounds=new System.Drawing.Rectangle(-1920,-40,4,3);
                Action<System.Drawing.Graphics,System.Drawing.Rectangle> draw=(g,b)=>{Check(b==bounds,"virtual screen origin");g.Clear(System.Drawing.Color.Red);g.FillRectangle(System.Drawing.Brushes.Blue,3,2,1,1);};
                using(var a=Screenshots.Capture(bounds,folder,draw))using(var b=Screenshots.Capture(bounds,folder,draw)){
                    Check(a.Path!=b.Path&&File.Exists(a.Path)&&File.Exists(b.Path),"unique completed files");Check(!File.Exists(a.Path+".tmp"),"atomic save completed");
                    using(var bitmap=new System.Drawing.Bitmap(a.Path)){Check(bitmap.Width==4&&bitmap.Height==3,"full-size capture");Check(bitmap.GetPixel(0,0).ToArgb()==System.Drawing.Color.Red.ToArgb()&&bitmap.GetPixel(3,2).ToArgb()==System.Drawing.Color.Blue.ToArgb(),"uncropped pixels");}
                }
            });
            test("Generated Essentials icon is embedded and application product name is updated",()=>{
                using(var icon=AppIdentity.LoadIcon(32)){Check(icon.Width>=16&&icon.Height>=16,"Windows icon");}
                Check(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("GTBookEssentials.Icon"),"embedded generated asset");
                using(var logo=AppIdentity.LoadLogo()){Check(logo.Width==256&&logo.Height==256,"decoded sidebar logo");Check(logo.GetPixel(0,0).A==0,"transparent outer edge");}
                Check(System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName).ProductName=="GTBOOK Essentials","Windows product name");
            });
            if(Environment.GetEnvironmentVariable("GTBOOK_HEADLESS_TESTS")=="1")output.AppendLine("SKIP Native screen-copy requires an interactive desktop (headless CI).");
            else test("Native Windows screen-copy path accepts its raster operation",()=>{
                try{
                    using(var bitmap=new System.Drawing.Bitmap(1,1))using(var graphics=System.Drawing.Graphics.FromImage(bitmap))Screenshots.CopyDesktop(graphics,new System.Drawing.Rectangle(0,0,1,1));
                }catch(System.ComponentModel.Win32Exception ex){
                    if(ex.NativeErrorCode!=6) throw;
                }
            });
            test("Vivid colours preserve hue, saturation and intentional neutral colours",()=>{
                foreach(string hex in new[]{"#9B8AFB","#FF4B9D","#30E1EC","#FFCB4B","#AB6FFF"}){
                    var original=System.Drawing.ColorTranslator.FromHtml(hex);var vivid=LightingColour.Vivid(original);
                    Check(Math.Min(vivid.R,Math.Min(vivid.G,vivid.B))==0&&Math.Max(vivid.R,Math.Max(vivid.G,vivid.B))==255,"full HSV saturation and value");
                    Check(Math.Abs(vivid.GetHue()-original.GetHue())<1,"chosen hue changed");
                }
                foreach(var neutral in new[]{System.Drawing.Color.White,System.Drawing.Color.Gray,System.Drawing.Color.Black})Check(LightingColour.Vivid(neutral).ToArgb()==neutral.ToArgb(),"neutral colour changed");
                var midpoint=LightingColour.MixHue(System.Drawing.Color.Red,System.Drawing.Color.Blue,.5);Check(midpoint.R==255&&midpoint.G==0&&midpoint.B==255,"hue blend became muddy");
            });
            test("Every Extra animation keeps saturated frames and closes its loop at all speeds",()=>{
                foreach(string name in ExtraEffects.Names)foreach(int speed in new[]{1,4,9}){
                    var r=new Request{Effect=name,Speed=speed,Brightness=100};double cycle=6/ExtraEffects.SpeedScale(speed);
                    for(int i=0;i<180;i++)foreach(var c in ExtraEffects.Frame(r,cycle*i/180))Check(Math.Min(c.R,Math.Min(c.G,c.B))==0,"desaturated frame: "+name);
                    var start=ExtraEffects.Frame(r,0);var end=ExtraEffects.Frame(r,cycle);var later=ExtraEffects.Frame(r,cycle*200);
                    for(int z=0;z<4;z++){Check(Distance(start[z],end[z])<=1,"loop reset: "+name);Check(Distance(start[z],later[z])<=1,"long-running wrap: "+name);}
                }
            });
            test("Fastest animation frames stay continuous at the hardware update rate",()=>{
                var measurements=new System.Collections.Generic.Dictionary<string,int>();
                foreach(string name in ExtraEffects.Names){
                    var r=new Request{Effect=name,Speed=9,Brightness=100};var previous=ExtraEffects.Frame(r,0);int largest=0;
                    for(int i=1;i<=360;i++){var next=ExtraEffects.Frame(r,i*.067);for(int z=0;z<4;z++)largest=Math.Max(largest,Distance(previous[z],next[z]));previous=next;}
                    measurements[name]=largest;Check(largest<=105,"abrupt frame jump for "+name+": "+largest);
                }
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)),"animation-continuity.json"),new JavaScriptSerializer().Serialize(measurements));
            });
            test("Colour fades preserve saturation and selected brightness without grey tint",()=>{
                var r=new Request{Effect="Static",Brightness=35,Colors=new[]{"#FF0000","#FF0000","#FF0000","#FF0000"}};
                var c=LivePreview.KeyColour(r,0,0,0);Check(c.R==89&&c.G==0&&c.B==0,"grey contamination in dim preview");
                foreach(string name in ExtraEffects.Names){r.Effect=name;var frame=ExtraEffects.Frame(r,2);for(int z=0;z<4;z++)Check(LivePreview.KeyColour(r,z,0,2).ToArgb()==LightingColour.Dim(frame[z],.35).ToArgb(),"preview brightness mismatch: "+name);}
                r.Brightness=0;Check(LivePreview.KeyColour(r,0,0,0).ToArgb()==System.Drawing.Color.FromArgb(42,44,47).ToArgb(),"off keyboard is not grey");
            });
            test("Palette animations honour identical user colours without inventing a second hue",()=>{
                foreach(string name in ExtraEffects.Names.Where(ExtraEffects.UsesColours)){
                    var r=new Request{Effect=name,Colors=new[]{"#FF0000","#FF0000","#FF0000","#FF0000"}};
                    for(int i=0;i<30;i++)foreach(var c in ExtraEffects.Frame(r,i*.2))Check(c.G==0&&c.B==0,"unexpected second colour: "+name);
                }
            });
            test("Saturated swatch labels remain readable on bright and dark hues",()=>{
                Check(LightingColour.TextOn(System.Drawing.Color.Yellow).ToArgb()==Theme.Ink.ToArgb(),"yellow needs dark text");
                Check(LightingColour.TextOn(System.Drawing.Color.Cyan).ToArgb()==Theme.Ink.ToArgb(),"cyan needs dark text");
                Check(LightingColour.TextOn(System.Drawing.Color.Blue).ToArgb()==System.Drawing.Color.White.ToArgb(),"blue needs light text");
                Check(LightingColour.TextOn(System.Drawing.Color.Black).ToArgb()==System.Drawing.Color.White.ToArgb(),"black needs light text");
            });
            test("All three performance modes wait for their matching OEM read-back",()=>{
                for(int mode=0;mode<3;mode++){
                    int requested=mode,reads=0,waits=0;var writes=new System.Collections.Generic.List<int>();
                    using(var oem=new OemProtocol((method,b)=>{
                        Check(method=="MemIO","mode must use OEM EC transport");
                        if(b[15]==160&&b[13]==1)writes.Add(b[16]);
                        if(b[15]==160&&b[13]==0){reads++;return reads<3?255u:(uint)requested;}return 0;
                    })){
                        var reply=PerformanceModes.Apply(oem,mode,ms=>{Check(ms==100,"bounded retry spacing");waits++;});
                        Check(reply.Ok&&reply.Data["mode"]==PerformanceModes.Names[mode],"wrong confirmed mode");
                        Check(writes.SequenceEqual(new[]{mode})&&reads==3&&waits==2,"write or read-back retry sequence");
                    }
                }
                using(var oem=new OemProtocol((method,b)=>b[15]==160&&b[13]==0?2u:0u)){
                    var turbo=PerformanceModes.Apply(oem,3,ms=>{});
                    Check(turbo.Ok&&turbo.Data["mode"]=="Turbo"&&turbo.Data["cooling"]=="Full fan override","turbo mode application");
                }
            });
            test("Rejected mode read-back cannot produce a success result",()=>{
                int reads=0;bool rejected=false;
                using(var oem=new OemProtocol((method,b)=>{if(b[15]==160&&b[13]==0)reads++;return 0;})){
                    try{PerformanceModes.Apply(oem,2,ms=>{});}catch(IOException){rejected=true;}
                    Check(rejected&&reads==8,"unconfirmed mode must stop after bounded retries");
                }
            });
            test("Physical mode observation debounces changes and suppresses application echoes",()=>{
                var state=new ModeObservation(1);
                Check(!state.Observe(2)&&state.Observe(2)&&state.Current==2,"two matching physical reads");
                Check(!state.Observe(2),"repeat popup");
                Check(!state.Observe(0)&&!state.Observe(2)&&state.Current==2,"transient bounce");
                Check(!state.Observe(0)&&!state.Observe(255)&&!state.Observe(0),"invalid read must break confirmation");
                state.Confirm(0);Check(!state.Observe(0)&&state.Current==0,"own command must not echo");
                Check(!state.Observe(1)&&state.Observe(1),"cycle through third mode");
            });
            test("Feedback never announces an unconfirmed performance mode or brightness",()=>{
                var request=new Request{Action="mode",Mode=2};var reply=new Reply{Ok=true};reply.Data["mode"]="Balanced";
                Check(FeedbackNotice.Applied(request,reply).Kind==FeedbackKind.Error,"wrong mode success popup");
                reply.Data["mode"]="Performance";var notice=FeedbackNotice.Applied(request,reply);Check(notice.Kind==FeedbackKind.Mode&&notice.Mode==2,"confirmed Performance notice");
                reply.Data["mode"]="Turbo";var turboNotice=FeedbackNotice.Applied(new Request{Action="mode",Mode=3},reply);Check(turboNotice.Kind==FeedbackKind.Mode&&turboNotice.Mode==3&&turboNotice.Title=="Turbo mode"&&turboNotice.Detail.Contains("Full fans"),"confirmed Turbo notice");
                reply.Ok=false;Check(FeedbackNotice.Applied(request,reply).Kind==FeedbackKind.Error,"failed command must not chime");
                request.Action="screen-brightness";reply.Ok=true;Check(FeedbackNotice.Applied(request,reply).Kind==FeedbackKind.Error,"unconfirmed brightness");
                reply.Data["display-brightness"]="35";notice=FeedbackNotice.Applied(request,reply);Check(notice.Kind==FeedbackKind.Brightness&&notice.Percent==35,"confirmed brightness value");
            });
            test("Keyboard feedback shows four-zone colours and a neutral off state",()=>{
                var r=new Request{Action="keyboard-animation",Effect="Candy Chase",Brightness=100};var reply=new Reply{Ok=true};var notice=FeedbackNotice.Applied(r,reply);
                Check(notice.Kind==FeedbackKind.Keyboard&&notice.Zones.Length==4&&notice.Percent==100,"colour strip missing");
                for(int z=0;z<4;z++)Check(notice.Zones[z].ToArgb()==LivePreview.KeyColour(r,z,z/3.0,0).ToArgb(),"feedback colour disagrees with preview");
                r.Brightness=0;notice=FeedbackNotice.Applied(r,reply);Check(notice.Detail.Length==0&&notice.Percent==0&&notice.Zones.All(c=>c.R==144&&c.G==148&&c.B==151),"off feedback must be neutral");
            });
            test("Keyboard effects and brightness have distinct primary messages",()=>{
                var request=new Request{Action="keyboard-animation",Effect="Magnetic Slingshot",Brightness=50,Feedback="keyboard-effect"};
                var effect=FeedbackNotice.Applied(request,new Reply{Ok=true});
                Check(effect.Kind==FeedbackKind.Keyboard&&effect.Title=="Magnetic Slingshot","effect name must be the primary text");
                request.Feedback="keyboard-brightness";var brightness=FeedbackNotice.Applied(request,new Reply{Ok=true});
                Check(brightness.Kind==FeedbackKind.KeyboardBrightness&&brightness.Title=="50%"&&brightness.Title!=effect.Title,"brightness must have its own message");
                request.Brightness=0;Check(FeedbackNotice.Applied(request,new Reply{Ok=true}).Title=="Off","zero brightness must clearly say Off");
            });
            test("All effect titles fit their reserved rectangle at common display scales",()=>{
                foreach(float scale in new[]{1f,1.25f,1.5f,2f})using(var bitmap=new System.Drawing.Bitmap(1,1)){
                    bitmap.SetResolution(96*scale,96*scale);
                    using(var graphics=System.Drawing.Graphics.FromImage(bitmap))foreach(string name in ExtraEffects.Names.Concat(new[]{"Four zones","Breathing","100%","Off"})){
                        var rect=PopupLayout.KeyboardTitle(new System.Drawing.Size((int)(480*scale),(int)(164*scale)),scale);
                        float size=ShortcutToast.FittedTitleSize(graphics,name,rect);
                        using(var font=new System.Drawing.Font("Segoe UI",size*ShortcutToast.VisualScale,System.Drawing.FontStyle.Bold)){
                            var measured=TextRenderer.MeasureText(graphics,name,font,new System.Drawing.Size(int.MaxValue,int.MaxValue),TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
                            Check(size>=18&&measured.Width<=rect.Width&&measured.Height<=rect.Height,"title clipped: "+name+" at "+scale);
                            Check(rect.Bottom<=82*scale,"title overlaps the detail");
                        }
                    }
                }
            });
            test("Device capacity formatting preserves large values and handles absent firmware data",()=>{
                Check(DeviceInformation.Bytes(17179869184UL)=="16 GB","64-bit RAM size");
                Check(DeviceInformation.Bytes(1099511627776UL)=="1024 GB","large SSD size");
                Check(DeviceInformation.Bytes(null)=="Unavailable"&&DeviceInformation.Bytes(0)=="Unavailable","missing values must not look like real capacity");
                Protocol.Validate(new Request{Action="device-info"});
            });
            test("Mode chimes are short distinct PCM waves with quiet click-free envelopes",()=>{
                var signatures=new System.Collections.Generic.HashSet<string>();
                for(int mode=0;mode<3;mode++){
                    byte[] wave=ModeChime.Wave(mode);Check(Encoding.ASCII.GetString(wave,0,4)=="RIFF"&&Encoding.ASCII.GetString(wave,8,8)=="WAVEfmt ","WAV header");
                    int data=BitConverter.ToInt32(wave,40);Check(data==wave.Length-44&&BitConverter.ToInt32(wave,24)==22050&&BitConverter.ToInt16(wave,34)==16,"PCM layout");
                    Check(data/2.0/22050<=.31&&BitConverter.ToInt16(wave,44)==0&&BitConverter.ToInt16(wave,wave.Length-2)==0,"duration and fade endpoints");
                    int peak=0;for(int n=44;n<wave.Length;n+=2)peak=Math.Max(peak,Math.Abs((int)BitConverter.ToInt16(wave,n)));Check(peak>1000&&peak<6000,"soft but audible peak");
                    Check(signatures.Add(Convert.ToBase64String(wave)),"modes share identical audio");
                }
            });
            test("Rapid keyboard bursts retain only the newest pending state without concurrent execution",()=>{
                System.Threading.Tasks.Task.Run(async ()=>{
                    var release=new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
                    var seen=new System.Collections.Generic.List<int>();int active=0,maxActive=0;
                    var pump=new LatestCommandPump<Request>(async request=>{active++;maxActive=Math.Max(maxActive,active);seen.Add(request.Brightness);if(seen.Count==1)await release.Task;active--;});
                    var first=pump.Submit(new Request{Brightness=1});System.Threading.Tasks.Task last=first;
                    for(int i=0;i<2000;i++)last=pump.Submit(new Request{Brightness=i%101});
                    Check(seen.Count==1,"commands ran while the first was blocked");release.SetResult(true);await last;
                    Check(seen.SequenceEqual(new[]{1,1999%101})&&maxActive==1,"burst backlog or concurrent hardware writes");
                    await pump.Submit(new Request{Brightness=77});Check(seen.Last()==77,"pump did not rearm after drain");
                }).GetAwaiter().GetResult();
            });
            test("Keyboard command pump propagates failure and can accept a later recovery",()=>{
                System.Threading.Tasks.Task.Run(async ()=>{
                    int calls=0;var pump=new LatestCommandPump<Request>(r=>{if(++calls==1)throw new IOException("simulated disconnect");return System.Threading.Tasks.Task.FromResult(true);});
                    bool rejected=false;try{await pump.Submit(new Request());}catch(IOException){rejected=true;}
                    await pump.Submit(new Request());Check(rejected&&calls==2,"failure was swallowed or pump stayed stuck");
                }).GetAwaiter().GetResult();
            });
            test("Native keyboard helper serves a burst and rejects unrelated commands before device access",()=>{
                var json=new JavaScriptSerializer();var input=new StringBuilder();for(int i=0;i<100;i++)input.AppendLine(json.Serialize(new Request{Action="keyboard",Brightness=i}));
                input.AppendLine(json.Serialize(new Request{Action="gpu"}));input.AppendLine("bad json");int calls=0;var brokerOutput=new StringWriter();
                NativeKeyboardBroker.Serve(new StringReader(input.ToString()),brokerOutput,r=>{Check(r.Brightness==calls++,"native command reordered");return new Reply{Ok=true};});
                var replies=brokerOutput.ToString().Split(new[]{Environment.NewLine},StringSplitOptions.RemoveEmptyEntries).Select(line=>json.Deserialize<Reply>(line)).ToArray();
                Check(calls==100&&replies.Length==102&&replies.Take(100).All(r=>r.Ok)&&replies.Skip(100).All(r=>!r.Ok),"invalid input reached the hardware executor");
            });
            test("Native keyboard broker reuses its process and recovers from a terminated helper",()=>{
                int launches=0,id=0;
                using(var broker=new NativeKeyboardBroker(()=>{launches++;return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,"--keyboard-worker-simulated"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true});})){
                    for(int i=0;i<5;i++){Check(broker.Run(new Request{Action="keyboard",Brightness=i*20}).Ok,"broker request failed");if(i==0)id=broker.ProcessId;Check(broker.ProcessId==id,"helper restarted within a burst");}
                    using(var helper=System.Diagnostics.Process.GetProcessById(id)){helper.Kill();Check(helper.WaitForExit(2000),"fixture helper failed to exit");}
                    Check(broker.Run(new Request{Action="keyboard"}).Ok&&broker.ProcessId!=id&&launches==2,"helper did not recover after exit");
                }
            });
            test("Pending feedback is explicit and does not announce hardware success",()=>{
                foreach(var request in new[]{new Request{Action="mode",Mode=2},new Request{Action="keyboard",Effect="Rainbow"},new Request{Action="screen-brightness",Step=10},new Request{Action="fan",Enabled=true}}){
                    var pending=FeedbackNotice.Pending(request);Check(pending.IsPending&&!pending.Detail.Contains("confirmed")&&!pending.Detail.Contains("Applying"),"pending message announced success");
                }
                var mode=FeedbackNotice.Pending(new Request{Action="mode",Mode=2});Check(mode.Mode==-1,"pending mode must not highlight a confirmed mode");
                var motion=new PopupMotion();motion.Update(0,false,-1,10000);Check(motion.Opacity(0)==1&&!motion.Expired(9000),"pending feedback must be immediately readable and remain visible");
            });
            test("Compact notification dimensions use the requested cumulative scale",()=>{
                using(var toast=new ShortcutToast(true))Check(toast.ClientSize.Width==319&&toast.ClientSize.Height==109&&Math.Abs(ShortcutToast.VisualScale-.66424)<.001,"wrong compact popup size");
            });
            test("Confirmed performance writes do not wait before their first read-back",()=>{
                int delays=0;using(var oem=new OemProtocol((method,b)=>b[15]==160&&b[13]==0?2u:0u)){
                    Check(PerformanceModes.Apply(oem,2,ms=>delays++).Ok&&delays==0,"unnecessary initial mode delay");
                }
                Check(NativeKeyboardBroker.Supports(new Request{Action="mode"})&&NativeKeyboardBroker.Supports(new Request{Action="fan"})&&!NativeKeyboardBroker.Supports(new Request{Action="gpu"}),"wrong persistent helper command boundaries");
            });
            test("Brightness pending updates preserve the visible confirmed meter instead of removing it",()=>{
                using(var toast=new ShortcutToast(true)){
                    toast.Location=new System.Drawing.Point(-32000,-32000);
                    toast.Notify(new FeedbackNotice{Kind=FeedbackKind.Brightness,Title="Screen brightness",Percent=50});
                    for(int i=0;i<30;i++){
                        toast.Notify(FeedbackNotice.Pending(new Request{Action="screen-brightness",Step=i%2==0?10:-10}));
                        Check(toast.Visible&&toast.DisplayedPercent==50&&toast.Opacity>=.999,"pending brightness hid/reset the meter");
                    }
                    toast.Notify(new FeedbackNotice{Kind=FeedbackKind.Brightness,Title="Screen brightness",Percent=60});Check(toast.DisplayedPercent==60,"confirmed value did not replace previous value");
                }
                var motion=new PopupMotion();motion.Update(0,false,20);motion.Update(200,true,80);double before=motion.Percent(250);motion.Hold(250);Check(motion.Percent(250)==before&&motion.Percent(360)==80&&!motion.Expired(9000),"pending hold restarted the meter or its expiry");
            });
            test("Routine notifications carry only their primary information without secondary copy",()=>{
                foreach(var request in new[]{new Request{Action="mode",Mode=1},new Request{Action="screen-brightness"},new Request{Action="keyboard",Effect="Rainbow"},new Request{Action="keyboard",Feedback="keyboard-brightness"}}){
                    var reply=new Reply{Ok=true};reply.Data["mode"]="Balanced";reply.Data["display-brightness"]="50";
                    Check(FeedbackNotice.Applied(request,reply).Detail.Length==0&&FeedbackNotice.Pending(request).Detail.Length==0,"unnecessary secondary copy remains");
                }
                Check(FeedbackNotice.Applied(new Request(),Reply.Fail("Device disconnected")).Detail=="Device disconnected","actionable error was hidden");
            });
            test("Brightness notifications identify screen versus keyboard without adding quotes",()=>{
                Check(new FeedbackNotice{Kind=FeedbackKind.Brightness}.ControlLabel=="Screen brightness","screen label missing");
                Check(new FeedbackNotice{Kind=FeedbackKind.KeyboardBrightness}.ControlLabel=="Keyboard brightness","keyboard label missing");
                Check(new FeedbackNotice{Kind=FeedbackKind.Keyboard}.ControlLabel=="","effect should retain only its primary name");
            });
            test("Compact brightness header separates icon, label and percentage at common DPI scales",()=>{
                foreach(float scale in new[]{1f,1.25f,1.5f,2f}){
                    var size=new System.Drawing.Size((int)(319*scale),(int)(109*scale));var layout=ShortcutToast.BrightnessLayout(size,scale);
                    Check(layout[0].Right<layout[1].Left&&layout[1].Right<=layout[2].Left,"header overlaps");
                    Check(layout[3].Bottom<size.Height&&layout[3].Top>layout[2].Bottom,"meter overlaps the header or bottom border");
                    using(var image=new System.Drawing.Bitmap(1,1)){
                        image.SetResolution(96*scale,96*scale);using(var g=System.Drawing.Graphics.FromImage(image))using(var font=new System.Drawing.Font("Segoe UI",9.5f,System.Drawing.FontStyle.Bold)){
                            foreach(var word in new[]{"Screen","Keyboard","brightness"})Check(TextRenderer.MeasureText(g,word,font,new System.Drawing.Size(int.MaxValue,int.MaxValue),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Width<=layout[1].Width,"brightness label clipped");
                            Check(TextRenderer.MeasureText(g,"Keyboard\nbrightness",font,new System.Drawing.Size((int)layout[1].Width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding).Height<=layout[1].Height,"brightness label height clipped");
                            foreach(var value in new[]{"0%","50%","100%","Off"})using(var numberFont=new System.Drawing.Font("Segoe UI",ShortcutToast.BrightnessValueSize(g,value,layout[2]),System.Drawing.FontStyle.Bold)){var measured=TextRenderer.MeasureText(g,value,numberFont,new System.Drawing.Size(int.MaxValue,int.MaxValue),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);Check(measured.Width<=layout[2].Width&&measured.Height<=layout[2].Height,"percentage clipped");}
                        }
                    }
                }
            });
            test("Routine controls apply directly while GPU and startup retain confirmation",()=>{
                foreach(string action in new[]{"mode","auto","fan","keyboard","keyboard-animation","rear","rear-off","rear-music","usb","windows-key","software-fn"})Check(!ConfirmationPolicy.Required(action),"unexpected confirmation: "+action);
                Check(ConfirmationPolicy.Required("gpu")&&ConfirmationPolicy.Required("startup")&&ConfirmationPolicy.Required("future-command"),"important or unknown actions need confirmation");
                Check(ConfirmationPolicy.Headline(new Request{Action="gpu"})=="RESTART REQUIRED","GPU must lead with restart requirement");
                Check(ConfirmationPolicy.Headline(new Request{Action="startup",Enabled=true}).EndsWith("ON")&&ConfirmationPolicy.Headline(new Request{Action="startup"}).EndsWith("OFF"),"startup headline missing desired state");
                using(var prompt=new ApplyPreview(new Request{Action="gpu"},"Saved",true))Check(prompt.AcceptButton.DialogResult==DialogResult.Cancel,"Enter must default to Later rather than restart");
            });
            test("Optional graphical keyboard preview still renders the selected effect",()=>{
                using(var dialog=new ApplyPreview(new Request{Action="keyboard-animation",Effect="Comet Trail"},"Current lighting")){
                    Check(dialog.Controls.OfType<LivePreview>().Single().Target=="keyboard","Extra confirmation lost its graphical keyboard preview");
                }
            });
            test("F3 fires at 0.8 seconds while held and stays latched until release",()=>{
                var keys=new ShortcutDecoder();keys.Feed(0x72,true,false,false,100);
                for(int ms=200;ms<900;ms+=100){Check(keys.Feed(0x72,true,false,false,ms)==null,"auto-repeat fired before 0.8 seconds");Check(keys.Poll(false,ms)==null,"timer fired before 0.8 seconds");}
                Check(keys.Poll(false,900)=="keyboard-effect","hold timer must work without auto-repeat");
                for(int ms=1000;ms<10000;ms+=100){Check(keys.Feed(0x72,true,false,false,ms)==null&&keys.Poll(false,ms)==null,"same hold fired twice");}
                Check(keys.Feed(0x72,false,false,false,10000)==null,"release also changed brightness or effect");
                keys.Feed(0x72,true,false,false,10100);Check(keys.Poll(false,10899)==null&&keys.Poll(false,10900)=="keyboard-effect","second effect needs a fresh full hold");
            });
            test("Cancelled modified and injected F3 gestures never advance an effect",()=>{
                var keys=new ShortcutDecoder();keys.Feed(0x72,true,false,false,0);Check(keys.Poll(true,500)==null,"modifier");
                Check(keys.Poll(false,2500)==null&&keys.Feed(0x72,false,false,false,3000)==null,"modifier cancellation must last until release");
                keys.Feed(0x72,true,false,true,4000);Check(keys.Poll(false,7000)==null,"injected press started a hold");
                keys.Feed(0x72,true,false,false,8000);keys.CancelHold();Check(keys.Poll(false,12000)==null,"cancelled hold survived");
            });
            test("Continuous notifications preserve opacity and extend the popup lifetime",()=>{
                var motion=new PopupMotion();motion.Update(0,false,20);Check(motion.Opacity(0)==1&&motion.Opacity(140)==1,"first appearance");
                for(int ms=200;ms<=5000;ms+=100){double before=motion.Opacity(ms);motion.Update(ms,true,ms%100);Check(motion.Opacity(ms)==before&&before==1&&!motion.Expired(ms),"visible update restarted the fade");}
                Check(!motion.Expired(7619)&&motion.Expired(7620),"hide must follow the final update");
                motion.Update(8000,false,10);double fading=motion.Opacity(10500);Check(fading>0&&fading<1,"fade-out sample");motion.Update(10500,true,80);
                Check(Math.Abs(motion.Opacity(10500)-fading)<.000001&&motion.Opacity(10640)==1,"update during fade-out flashed off");
            });
            test("Brightness meter retargets smoothly through increases and decreases",()=>{
                var motion=new PopupMotion();motion.Update(0,false,20);motion.Update(500,true,100);
                Check(motion.Percent(500)==20&&motion.Percent(580)==60,"brightness interpolation");
                motion.Update(580,true,0);Check(motion.Percent(580)==60&&motion.Percent(660)==30&&motion.Percent(740)==0,"reversed brightness jumped or overshot");
            });
            test("Popup updates reuse the visible window without dimming or relocating it",()=>{
                using(var toast=new ShortcutToast(true)){
                    toast.Location=new System.Drawing.Point(-32000,-32000);int shows=0,hides=0;toast.VisibleChanged+=(s,e)=>{if(toast.Visible)shows++;else hides++;};
                    toast.Notify(new FeedbackNotice{Kind=FeedbackKind.Brightness,Title="Brightness",Percent=10});
                    System.Threading.Thread.Sleep(180);System.Windows.Forms.Application.DoEvents();
                    var position=toast.Location;
                    for(int i=0;i<30;i++){toast.Notify(new FeedbackNotice{Kind=i%2==0?FeedbackKind.Keyboard:FeedbackKind.Brightness,Title="Updated",Percent=i});Check(toast.Visible&&toast.Opacity>=.999&&toast.Location==position,"popup flickered or moved");}
                    Check(shows==1&&hides==0,"updates hid or showed the window again");
                }
            });
            test("Keyboard title and percentage badge never overlap at common display scales",()=>{
                foreach(float scale in new[]{1f,1.25f,1.5f,2f}){
                    var size=new System.Drawing.Size((int)(430*scale),(int)(146*scale));var title=PopupLayout.Title(size,scale,true);var badge=PopupLayout.Badge(size,scale);
                    Check(title.Right+12*scale<=badge.Left&&badge.Right<=size.Width-18*scale,"header regions overlap at "+scale);
                }
            });
            test("Popups use manual bottom-centre positioning within monitor working areas",()=>{
                using(var toast=new ShortcutToast(true))Check(toast.StartPosition==System.Windows.Forms.FormStartPosition.Manual,"Windows may override the first popup location");
                var size=new System.Drawing.Size(430,146);
                foreach(var area in new[]{new System.Drawing.Rectangle(0,0,1920,1040),new System.Drawing.Rectangle(-1920,40,1920,1040),new System.Drawing.Rectangle(80,60,1366,700)}){
                    var point=PopupLayout.BottomCenter(area,size,36);Check(point.X==area.Left+(area.Width-size.Width)/2&&point.Y==area.Bottom-size.Height-36,"wrong bottom-centre anchor");
                    Check(area.Contains(new System.Drawing.Rectangle(point,size)),"popup left the working area");
                }
                Check(PopupLayout.BottomCenter(new System.Drawing.Rectangle(10,20,200,100),size,36)==new System.Drawing.Point(10,20),"small working area must clamp to its origin");
            });
            test("Sliders ignore wheel input while direct adjustment stays available",()=>{
                using(var slider=new SliderWheelProbe()){
                    slider.Maximum=100;slider.Value=50;int changes=0;slider.ValueChanged+=(s,e)=>changes++;
                    foreach(int delta in new[]{120,-120,240,-480}){slider.Wheel(delta,false);slider.Wheel(delta,true);}
                    Check(slider.Value==50&&changes==0,"wheel changed the slider");
                    slider.Value=60;Check(slider.Value==60&&changes==1,"direct adjustment was blocked");
                }
            });
            test("F3 hook adapter uses captured events even when Windows reports the consumed key up",()=>{
                var emitted=new System.Collections.Generic.List<string>();var queried=new System.Collections.Generic.List<int>();
                using(var input=new LaptopShortcuts(false,key=>{queried.Add(key);return 0;})){
                    input.Action+=emitted.Add;
                    Check(input.ProcessKey(0x72,0,true,100),"F3 down must be consumed");
                    input.PollHold(125);input.PollHold(150);
                    Check(input.ProcessKey(0x72,0,false,200),"F3 up must be consumed");
                    Check(emitted.SequenceEqual(new[]{"keyboard-brightness"}),"tap was cancelled by the async key-state check");
                    emitted.Clear();input.ProcessKey(0x72,0,true,1000);input.PollHold(1025);input.PollHold(1799);Check(emitted.Count==0,"hold fired early");
                    input.PollHold(1800);input.ProcessKey(0x72,0,true,3300);input.PollHold(5000);input.ProcessKey(0x72,0,false,6000);
                    Check(emitted.SequenceEqual(new[]{"keyboard-effect"}),"hold changed brightness or fired multiple effects");
                    Check(!queried.Contains(0x72),"consumed F3 must not be checked through GetAsyncKeyState");
                    input.ProcessKey(0x72,0,true,7000);input.PollHold(7800);Check(emitted.Count==2,"release did not rearm the next hold");
                    input.ResetGestures();input.PollHold(12000);Check(emitted.Count==2,"suspend/reset left a held effect pending");
                }
            });
            test("F3 shortcut adapter cancels pending gestures when controls are disabled",()=>{
                int actions=0;using(var input=new LaptopShortcuts(false,key=>0)){
                    input.Action+=s=>actions++;input.ProcessKey(0x72,0,true,0);input.MediaKeys=false;input.PollHold(3000);
                    Check(!input.ProcessKey(0x72,0,false,3500)&&actions==0,"disabled shortcut fired or consumed a normal key");
                    input.MediaKeys=true;input.ProcessKey(0x72,0,true,4000);input.ProcessKey(0x72,0,false,4100);Check(actions==1,"reenabled tap did not work");
                }
            });
            test("GPU requests match all three OEM register values and require read-back",()=>{
                for(int selection=0;selection<3;selection++){
                    int target=selection+1,reads=0,address=0;var writes=new System.Collections.Generic.List<int>();
                    using(var oem=new OemProtocol((method,b)=>{
                        if(b[15]==145&&b[13]==1)address=b[16];
                        if(b[15]==160&&b[13]==1){Check(address==0xc0,"wrong GPU register");writes.Add(b[16]);}
                        if(b[15]==160&&b[13]==0)return ++reads<3?0u:(uint)target;return 0;
                    })){
                        var reply=GpuModes.Apply(oem,selection,ms=>{});
                        Check(reply.Ok&&reply.Data["gpu-raw"]==target.ToString()&&reply.Data["gpu-mode-requested"]==GpuModes.Names[selection],"GPU mapping");
                        Check(writes.SequenceEqual(new[]{target})&&reads==3&&reply.Data["gpu-restart-required"]=="True","GPU confirmation/restart state");
                    }
                }
                MustThrow(()=>GpuModes.Raw(-1));MustThrow(()=>GpuModes.Raw(3));
            });
            test("Unconfirmed or invalid GPU modes cannot report success or write arbitrary values",()=>{
                int calls=0,reads=0;using(var oem=new OemProtocol((method,b)=>{calls++;if(b[15]==160&&b[13]==0)reads++;return 0;})){
                    MustThrow(()=>oem.SetGPUMode(0));MustThrow(()=>oem.SetGPUMode(4));Check(calls==0,"invalid raw mode reached firmware");
                    bool rejected=false;try{GpuModes.Apply(oem,1,ms=>{});}catch(IOException){rejected=true;}
                    Check(rejected&&reads==8,"GPU verification must be bounded and fail closed");
                }
            });
            test("Settings recover the last complete save after corrupt primary data",()=>{
                string previousWarning=Store.Warning,folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)),"settings-fixtures",Guid.NewGuid().ToString("N")),path=Path.Combine(folder,"keyboard.json");
                try{
                    Store.SavePath(path,new Request{Effect="Static",Brightness=45});Store.SavePath(path,new Request{Effect="Rainbow",Brightness=90});
                    Check(Store.LoadPath(path).Brightness==90,"latest atomic save");File.WriteAllText(path,"{broken");
                    var recovered=Store.LoadPath(path);Check(recovered.Brightness==45&&recovered.Effect=="Static"&&Store.Warning.Contains("backup"),"known-good backup not recovered");
                    File.WriteAllText(path+".bak","bad");Check(Store.LoadPath(path).Brightness==65,"invalid backup must load defaults");
                    Check(Directory.GetFiles(folder,"*.tmp").Length==0,"orphaned transaction files");
                }finally{Store.Warning=previousWarning;}
            });
            test("Conflict inspection reuses unchanged process identities and detects new processes",()=>{
                var cache=new ConflictCache();int inspections=0;Func<string> inspect=()=>{inspections++;return "";};
                cache.Check(new[]{"10:100","20:200"},inspect);cache.Check(new[]{"20:200","10:100"},inspect);Check(inspections==1,"unchanged Python processes were reinspected");
                cache.Check(new[]{"10:101","20:200"},inspect);Check(inspections==2,"PID reuse/start-time change ignored");
                Check(cache.Check(new[]{"30:300"},()=>{inspections++;return "old toolkit";})=="old toolkit","new conflicting Python process ignored");
                Check(cache.Check(new string[0],inspect)==""&&inspections==3,"empty process set should require no WMI inspection");
            });
            test("Stable popups sleep until expiry instead of repainting at 60Hz",()=>{
                var motion=new PopupMotion();motion.Update(0,false,40);Check(motion.NextTick(60)<=16,"entry animation pacing");
                Check(motion.NextTick(200)>=2000,"idle popup keeps a high-frequency timer");Check(motion.NextTick(2450)<=16,"fade-out animation pacing");
                motion.Update(1000,true,80);Check(motion.NextTick(1050)<=16&&motion.NextTick(1200)>=2000,"retargeting and steady state");
            });
            test("Brightness helper rejects malformed requests without touching the display",()=>{
                var outputText=new StringWriter();Check(BrightnessBroker.Serve(new StringReader("0\nwrong\n101\n"),outputText)==0,"server did not finish on input EOF");
                foreach(string line in outputText.ToString().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))Check(!new JavaScriptSerializer().Deserialize<Reply>(line).Ok,"invalid brightness request accepted");
                using(var helper=new BrightnessBroker()){Check(!helper.Run(0).Ok&&helper.ProcessId==0,"invalid step launched a process");}
            });
            test("Startup definition has no logon delay or idle/network prerequisites",()=>{
                string xml=Startup.Definition(@"C:\Program Files\GTBOOK Essentials\GTBook.exe","S-1-5-21-111");var doc=new System.Xml.XmlDocument();doc.LoadXml(xml);var ns=new System.Xml.XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t",doc.DocumentElement.NamespaceURI);
                Check(doc.SelectSingleNode("//t:LogonTrigger/t:Delay",ns)==null,"artificial startup delay");
                Check(doc.SelectSingleNode("//t:RunOnlyIfNetworkAvailable",ns).InnerText=="false"&&doc.SelectSingleNode("//t:StartWhenAvailable",ns).InnerText=="true","startup prerequisites");
                Check(doc.SelectSingleNode("//t:RestartOnFailure/t:Count",ns).InnerText=="3","crash recovery disabled");
            });
            test("Missing manufacturer or firmware identity fails the native-control guard",()=>{
                Check(!new Identity().Supported&&!new Identity{Manufacturer="INFINIX",Model="GL613",Bios="1.01"}.Supported,"unknown identity reached native writes");
            });
            test("Hardware commands wait for a short frame lease without an immediate busy error",()=>{
                Check(HardwareLease.CommandTimeoutMilliseconds>=1000&&HardwareLease.CommandTimeoutMilliseconds<=5000,"command wait must be bounded but allow frame completion");
                string name=@"Local\GTBookLeaseTest-"+Guid.NewGuid().ToString("N");
                using(var held=new System.Threading.ManualResetEvent(false)){
                    var holder=System.Threading.Tasks.Task.Run(()=>{using(var lease=new HardwareLease(name)){Check(lease.Acquire(1000),"test lease");held.Set();System.Threading.Thread.Sleep(150);}});
                    Check(held.WaitOne(1000),"holder did not start");
                    using(var command=new HardwareLease(name))Check(command.Acquire(HardwareLease.CommandTimeoutMilliseconds),"short animation frame was reported as a conflicting command");
                    holder.Wait();
                }
            });
            test("Button paint-only updates erase stale outer pixels and keep an opaque background",()=>{
                using(var panel=new System.Windows.Forms.Panel{Size=new System.Drawing.Size(220,100),BackColor=System.Drawing.Color.White})
                using(var button=new PaintProbeButton{Location=new System.Drawing.Point(12,12),Size=new System.Drawing.Size(180,48),Text="Paint"})
                using(var bitmap=new System.Drawing.Bitmap(180,48)){
                    panel.Controls.Add(button);
                    foreach(bool selected in new[]{false,true,false}){button.Selected=selected;using(var graphics=System.Drawing.Graphics.FromImage(bitmap)){graphics.Clear(System.Drawing.Color.Magenta);button.PaintOnly(graphics);}
                        foreach(var point in new[]{new System.Drawing.Point(0,0),new System.Drawing.Point(179,47),new System.Drawing.Point(90,47)})Check(bitmap.GetPixel(point.X,point.Y).ToArgb()==System.Drawing.Color.White.ToArgb(),"stale/black button margin survived repaint");
                    }
                }
            });
            test("Selected buttons use an orange surface rather than only an outline",()=>{
                using(var panel=new System.Windows.Forms.Panel{BackColor=System.Drawing.Color.White})using(var button=new PaintProbeButton{Size=new System.Drawing.Size(180,48),Selected=true})using(var bitmap=new System.Drawing.Bitmap(180,48)){
                    panel.Controls.Add(button);using(var graphics=System.Drawing.Graphics.FromImage(bitmap))button.PaintOnly(graphics);
                    var c=bitmap.GetPixel(90,32);Check(c.R>220&&c.G>90&&c.G<200&&c.B<140,"selected surface is not orange");
                    button.Enabled=false;using(var graphics=System.Drawing.Graphics.FromImage(bitmap))button.PaintOnly(graphics);c=bitmap.GetPixel(90,32);Check(c.R>220&&c.G<200&&c.B<140,"temporary command disable erased the current selection");
                }
            });
            test("OEM hand-off identifies the official utility and rejects unrelated ControlCenter apps",()=>{
                Check(OemHandoff.Matches(@"C:\Tools\ControlCenter.exe","","",OemHandoff.ReviewedHash),"reviewed OEM binary");
                Check(OemHandoff.Matches(@"C:\Program Files\Infinix\ControlCenter.exe","ControlCenter","ControlCenter.dll",""),"vendor install identity");
                Check(!OemHandoff.Matches(@"C:\Unrelated\ControlCenter.exe","ControlCenter","ControlCenter.dll",""),"unrelated process matched");
                Check(!OemHandoff.Matches(@"C:\Infinix\Other.exe","ControlCenter","ControlCenter.dll",OemHandoff.ReviewedHash),"other executable matched");
            });
            test("OEM hand-off requests graceful exit before its bounded termination fallback",()=>{
                var steps=new System.Collections.Generic.List<string>();
                Check(OemHandoff.GracefulThenStop(()=>steps.Add("close"),ms=>{steps.Add("wait"+ms);return true;},()=>steps.Add("stop")),"graceful exit");
                Check(steps.SequenceEqual(new[]{"close","wait700"}),"terminated an app that exited gracefully");steps.Clear();int waits=0;
                Check(OemHandoff.GracefulThenStop(()=>steps.Add("close"),ms=>{steps.Add("wait"+ms);return ++waits==2;},()=>steps.Add("stop")),"tray termination fallback");
                Check(steps.SequenceEqual(new[]{"close","wait700","stop","wait1500"}),"wrong shutdown sequence");
            });
            test("Live readings refresh automatically while visible without overlapping or background polling",()=>{
                Check(ReadingRefresh.Interval(false)==3000&&ReadingRefresh.Interval(true)==6000,"adaptive interval");
                Check(ReadingRefresh.ShouldPoll(true,false,true,false),"visible Overview should refresh");
                Check(!ReadingRefresh.ShouldPoll(false,false,true,false)&&!ReadingRefresh.ShouldPoll(true,true,true,false)&&!ReadingRefresh.ShouldPoll(true,false,false,false)&&!ReadingRefresh.ShouldPoll(true,false,true,true),"hidden/minimized/other page/busy should skip");
            });
            test("Overview metric tiles and system stats render without error across light and dark themes",()=>{
                var sys=SystemStats.Read();
                Check(sys.MemoryPercent>=0&&sys.MemoryPercent<=100,"valid RAM percentage");
                using(var host=new System.Windows.Forms.Panel{Size=new System.Drawing.Size(360,120),BackColor=Theme.White})
                using(var tile=new MetricTile{Size=new System.Drawing.Size(260,80),Title="MEMORY",ValueText="46%",DetailText="7.4 / 16 GB used",Progress=46,DotColor=Theme.OrangeEnd})
                using(var pill=new StatusPill{Size=new System.Drawing.Size(180,26),PillText="LIVE · 12:00:00",DotColor=System.Drawing.Color.FromArgb(35,169,108)})
                using(var bmp=new System.Drawing.Bitmap(360,120)){
                    host.Controls.Add(tile);host.Controls.Add(pill);
                    tile.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,0,260,80));
                    pill.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,85,180,26));
                    Theme.SetDepth(1f);
                    tile.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,0,260,80));
                    Theme.SetDepth(0f);
                }
            });
            test("All pages avoid clipped labels/checkboxes and remain composited during resizes",()=>{
                using(var form=new MainWindow(true))form.VerifyAllPagesNoTextClipping();
            });
            test("DeviceSpecView wraps category chips and multi-line spec rows at narrow widths",()=>{
                using(var view=new DeviceSpecView{Size=new System.Drawing.Size(420,400)}){
                    view.SetReport("System\r\nOS Version: Windows 11 Pro 24H2 x64 (Long Enterprise Build Title For Wrap Verification)\r\nStorage\r\nDrive C: NVMe SSD 512 GB with very long partition and controller description that wraps cleanly\r\nBattery\r\nHealth: 95%");
                    Check(view.Height>120,"DeviceSpecView did not expand for wrapped spec content: h="+view.Height);
                    using(var bmp=new System.Drawing.Bitmap(420,view.Height))view.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,0,420,view.Height));
                }
            });
            test("Narrow MetricTile and RaisedButton render long captions without error",()=>{
                using(var host=new System.Windows.Forms.Panel{Size=new System.Drawing.Size(260,180),BackColor=Theme.White})
                using(var tile=new MetricTile{Size=new System.Drawing.Size(160,84),Title="CPU TEMPERATURE",ValueText="Preview",DetailText="Hardware disconnected",Progress=65,DotColor=Theme.OrangeEnd})
                using(var btn=new RaisedButton{Location=new System.Drawing.Point(0,90),Size=new System.Drawing.Size(190,34),Dropdown=true,Text="Extra · 15 animations"})
                using(var bmp=new System.Drawing.Bitmap(260,180)){
                    host.Controls.Add(tile);host.Controls.Add(btn);
                    tile.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,0,160,84));
                    btn.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,90,190,34));
                }
            });
            test("Performance button hold triggers Turbo at 1.5s and clicks Performance under 1.5s",()=>{
                var hold=new PerformanceHoldGesture();
                hold.Begin(1000);
                Check(hold.IsHolding&&hold.Progress(1750)==0.5f,"midpoint hold progress");
                Check(!hold.Poll(2499),"must not trigger Turbo before 1500ms");
                Check(hold.Release(2499)==2,"releasing before 1500ms activates Performance");
                hold.Begin(5000);
                Check(hold.Poll(6500)&&hold.TriggeredTurbo,"holding 1500ms triggers Turbo");
                Check(hold.Release(6700)==-1,"releasing after Turbo hold must not revert to Performance");
                using(var btn=new RaisedButton{Text="Performance"}){
                    int clicks=0;btn.Click+=(s,e)=>clicks++;
                    btn.SuppressNextClick=true;
                    btn.PerformClick();
                    Check(clicks==0,"suppressed post-hold click must not fire");
                    btn.PerformClick();
                    Check(clicks==1,"normal click must resume firing");
                }
                using(var form=new MainWindow(true))form.VerifyPerformanceTurboUi();
            });
            test("Turbo power policy enforces zero battery saving and maximum processor performance",()=>{
                Check(!EnergySaver.ShouldEnableEnergySaver(3,true),"Turbo must disable Energy Saver even when auto Energy Saver is enabled");
                Check(EnergySaver.ShouldEnableEnergySaver(0,true),"Quiet keeps auto Energy Saver when enabled");
                Check(EnergySaver.ExpectedOverlay(3)==EnergySaver.MaxPerformanceOverlay,"Turbo uses MaxPerformance overlay");
                var byName=EnergySaver.TurboSettings.ToDictionary(s=>s.Name,s=>s);
                Check(byName["PROCTHROTTLEMIN"].AcValue==100&&byName["PROCTHROTTLEMIN"].DcValue==100,"100% min CPU on AC and DC");
                Check(byName["PROCTHROTTLEMAX"].AcValue==100&&byName["PROCTHROTTLEMAX"].DcValue==100,"100% max CPU on AC and DC");
                Check(byName["CPMINCORES"].AcValue==100&&byName["CPMINCORES"].DcValue==100,"0% core parking in Turbo");
                Check(byName["PERFBOOSTMODE"].AcValue==2&&byName["PERFBOOSTMODE"].DcValue==2,"aggressive processor boost in Turbo");
                Check(byName["PERFEPP"].AcValue==0&&byName["PERFEPP"].DcValue==0,"0% energy preference bias in Turbo");
                Check(byName["ASPM"].AcValue==0&&byName["ASPM"].DcValue==0,"PCIe ASPM disabled in Turbo");
                Check(byName["ESBATTTHRESHOLD"].AcValue==0&&byName["ESBATTTHRESHOLD"].DcValue==0,"0% Energy Saver battery threshold in Turbo");
            });
            test("GitHub updater parses releases, enforces trusted HTTPS hosts, and drives Settings badge dot",()=>{
                Check(AppUpdater.ParseVersion("v2.2")==new Version(2,2,0,0),"v2.2 version parse");
                Check(AppUpdater.IsNewer("v2.2",new Version(2,1,0,0)),"v2.2 must be newer than 2.1");
                Check(!AppUpdater.IsNewer("v2.1",new Version(2,1,0,0)),"same version must not trigger update");
                Check(AppUpdater.IsTrustedDownloadUrl("https://github.com/yashwantmandal26/GTBookEssentials/releases/download/v2.2/GTBOOK-Essentials-Setup.exe"),"GitHub release URL should be trusted");
                Check(!AppUpdater.IsTrustedDownloadUrl("http://github.com/yashwantmandal26/GTBookEssentials/releases/download/v2.2/GTBOOK-Essentials-Setup.exe"),"HTTP URL must be rejected");
                Check(!AppUpdater.IsTrustedDownloadUrl("https://evil.example.com/setup.exe"),"untrusted domain must be rejected");
                string sha="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
                Check(AppUpdater.ExtractSha256Hex("sha256:"+sha.ToUpperInvariant())==sha,"sha256 digest extraction failed");
                Check(AppUpdater.ExtractSha256Hex(sha+"  GTBOOK-Essentials-Setup.exe")==sha,"sha256 file text extraction failed");
                string sampleJson="{\"tag_name\":\"v2.2\",\"name\":\"GTBOOK Essentials v2.2\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://github.com/yashwantmandal26/GTBookEssentials/releases/tag/v2.2\",\"body\":\"Updater support\",\"assets\":[{\"name\":\"GTBOOK-Essentials-2.2-win-x64.zip\",\"size\":1000,\"browser_download_url\":\"https://github.com/yashwantmandal26/GTBookEssentials/releases/download/v2.2/GTBOOK-Essentials-2.2-win-x64.zip\"},{\"name\":\"GTBOOK-Essentials-Setup.exe\",\"size\":2000,\"digest\":\"sha256:"+sha+"\",\"browser_download_url\":\"https://github.com/yashwantmandal26/GTBookEssentials/releases/download/v2.2/GTBOOK-Essentials-Setup.exe\"}]}";
                var parsed=AppUpdater.ParseReleaseJson(sampleJson,new Version(2,1,0,0));
                Check(parsed.Checked&&parsed.UpdateAvailable&&parsed.LatestVersion=="v2.2","parsed release should report v2.2 update");
                Check(parsed.AssetName=="GTBOOK-Essentials-Setup.exe"&&parsed.ExpectedSha256==sha,"updater should prefer Setup.exe with digest");
                string draftJson="{\"tag_name\":\"v2.9\",\"draft\":true,\"prerelease\":false}";
                Check(!AppUpdater.ParseReleaseJson(draftJson,new Version(2,1,0,0)).UpdateAvailable,"draft release must be ignored");
                using(var form=new MainWindow(true))form.VerifyUpdateBadgeUi();
            });
            test("PerformanceModes.SwitchRequest restores auto cooling when leaving Turbo and preserves it on non-Turbo switches",()=>{
                var exitToBal=PerformanceModes.SwitchRequest(1,3,"Balanced");
                Check(exitToBal.Action=="mode"&&exitToBal.Mode==1&&exitToBal.RestoreAutoCooling,"leaving confirmedMode=3 must restore auto cooling");
                var exitSnap=PerformanceModes.SwitchRequest(0,2,"Turbo");
                Check(exitSnap.Mode==0&&exitSnap.RestoreAutoCooling,"leaving snapshot=Turbo must restore auto cooling");
                var stayTurbo=PerformanceModes.SwitchRequest(3,3,"Turbo");
                Check(stayTurbo.Mode==3&&!stayTurbo.RestoreAutoCooling,"entering/staying in Turbo must not set RestoreAutoCooling");
                var normalSwitch=PerformanceModes.SwitchRequest(2,1,"Balanced");
                Check(normalSwitch.Mode==2&&!normalSwitch.RestoreAutoCooling,"normal switch must not set RestoreAutoCooling");
            });
            test("ApplyModeWithCooling writes Turbo fan override once, confirms read-back, and restores auto fans on exit",()=>{
                int lastAddr=0,ecMode=1,fanFull=0,fanWrites=0;
                using(var oem=new OemProtocol((method,b)=>{
                    if(b[15]==145&&b[13]==1)lastAddr=b[16];
                    if(b[15]==160&&b[13]==1){
                        if(lastAddr==0x40)ecMode=b[16];
                        if(lastAddr==0x41){fanWrites++;fanFull=b[16];}
                    }
                    if(b[15]==160&&b[13]==0){
                        if(lastAddr==0x40)return (uint)ecMode;
                        if(lastAddr==0x41)return (uint)fanFull;
                    }
                    return 0;
                })){
                    var turbo=PerformanceModes.ApplyModeWithCooling(oem,3,false,ms=>{});
                    Check(turbo.Ok&&turbo.Data["mode"]=="Turbo"&&turbo.Data["cooling"]=="Full fan override","Turbo cooling read-back failed");
                    Check(fanWrites==1&&fanFull==1,"Turbo must write SetFanFullMode(true) once");
                    var exit=PerformanceModes.ApplyModeWithCooling(oem,1,true,ms=>{});
                    Check(exit.Ok&&exit.Data["mode"]=="Balanced"&&exit.Data["cooling"]=="Firmware auto","Turbo exit auto-cooling restore failed");
                    Check(fanWrites==2&&fanFull==0,"Turbo exit must write SetFanFullMode(false)");
                }
            });
            test("Updater SHA-256 verification fails closed when checksum download fails or digest is invalid",()=>{
                bool threwFetch=false,threwInvalid=false;
                try{AppUpdater.ResolveExpectedSha256("","https://github.com/yashwantmandal26/GTBookEssentials/releases/download/v2.2/GTBOOK-Essentials-Setup.exe.sha256",u=>{throw new TimeoutException("network timeout");});}
                catch(IOException){threwFetch=true;}
                Check(threwFetch,"checksum download error must fail closed");
                try{AppUpdater.ResolveExpectedSha256("","https://github.com/yashwantmandal26/GTBookEssentials/releases/download/v2.2/GTBOOK-Essentials-Setup.exe.sha256",u=>"not a valid sha256 file");}
                catch(IOException){threwInvalid=true;}
                Check(threwInvalid,"invalid checksum file contents must fail closed");
                string digest="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
                Check(AppUpdater.ResolveExpectedSha256("","https://github.com/yashwantmandal26/GTBookEssentials/releases/download/v2.2/a.sha256",u=>digest.ToUpperInvariant()+"  setup.exe")==digest,"valid remote sha256 did not resolve");
            });
            test("Languages.Translate supports Performance/Turbo aliases cleanly without mutating user settings",()=>{
                Check(Languages.Translate("Performance","en-us")=="Performance","en-us should preserve Performance");
                Check(Languages.Translate("Turbo","en-us")=="Turbo","en-us should preserve Turbo");
                Check(Languages.Translate("Performance","fr")=="Mode Jeu","French Performance alias mismatch");
                Check(Languages.Translate("Restore auto cooling","hi")!= "Restore auto cooling","Hindi Restore auto cooling alias mismatch");
                Check(Languages.Translate("","fr")=="","empty string must stay empty");
            });
            test("MainWindow navigation and FormClosed dispose cached pages, tray resources, and fonts cleanly",()=>{
                var form=new MainWindow(true);
                form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);
                form.Show();
                form.VerifyAllPagesNoTextClipping();
                form.ExitApplication();
                System.Windows.Forms.Application.DoEvents();
                Check(form.IsDisposed,"MainWindow did not dispose cleanly after closing cached pages");
            });
            output.AppendLine(passed+" passed, "+failed+" failed. Hardware was not changed.");
            File.WriteAllText(report,output.ToString()); return failed==0?0:1;
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static int Distance(System.Drawing.Color a,System.Drawing.Color b){return Math.Max(Math.Abs(a.R-b.R),Math.Max(Math.Abs(a.G-b.G),Math.Abs(a.B-b.B)));}
        sealed class SliderWheelProbe : WheelSafeTrackBar {public void Wheel(int delta,bool horizontal){var message=System.Windows.Forms.Message.Create(Handle,horizontal?0x020E:0x020A,new IntPtr(delta<<16),IntPtr.Zero);base.WndProc(ref message);}}
        sealed class PaintProbeButton : RaisedButton {public void PaintOnly(System.Drawing.Graphics graphics){base.OnPaint(new System.Windows.Forms.PaintEventArgs(graphics,ClientRectangle));}}
        static void MustThrow(Action action){try{action();}catch(ArgumentException){return;}catch(FormatException){return;}throw new Exception("Invalid input was accepted");}
    }
}

