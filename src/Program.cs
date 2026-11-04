using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace GTBook
{
	public static class Program
	{
		[DllImport("kernel32.dll")]
		private static extern IntPtr GetStdHandle(int kind);
#if TESTING
        [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
#endif

		[STAThread]
		public static int Main(string[] args)
		{
#if TESTING
            if(args.Length>0&&(args[0]=="--self-test"||args[0]=="--render"))SetProcessDPIAware();
#endif
            if(args.Length==1&&args[0]=="--show")return AppSignals.Send(AppSignals.ShowName)?0:2;
            if(args.Length==1&&args[0]=="--exit")return AppSignals.Send(AppSignals.ExitName)?0:2;
            if(args.Length==1&&args[0]=="--verify-usb"){
                Dictionary<string,object> report;
                try{report=UsbCharging.VerifyRequests();}catch(Exception error){report=new Dictionary<string,object>{{"ok",false},{"error",error.Message}};}
                Directory.CreateDirectory(Store.DirectoryPath);File.WriteAllText(Path.Combine(Store.DirectoryPath,"usb-verification.json"),new JavaScriptSerializer().Serialize(report));
                return report.ContainsKey("ok")&&Convert.ToBoolean(report["ok"])?0:1;
            }
            if(args.Contains("--verify-brightness-burst")){
                var report=new Dictionary<string,object>();int before=DisplayBrightness.Read();int step=before==100?-1:1;
                using(var broker=new BrightnessBroker())try{
                    var watch=Stopwatch.StartNew();var first=broker.Run(step);report["first_ms"]=watch.Elapsed.TotalMilliseconds;int id=broker.ProcessId;report["first_ok"]=first.Ok;
                    watch.Restart();var second=broker.Run(-step);report["second_ms"]=watch.Elapsed.TotalMilliseconds;report["reused_process"]=id!=0&&id==broker.ProcessId;report["second_ok"]=second.Ok;
                    report["before"]=before;report["restored"]=DisplayBrightness.Read();System.Threading.Thread.Sleep(5500);report["idle_helper_exited"]=broker.ProcessId==0;
                }finally{int after=DisplayBrightness.Read();if(after!=before)DisplayBrightness.Change(before-after);}
                Directory.CreateDirectory(Store.DirectoryPath);File.WriteAllText(Path.Combine(Store.DirectoryPath,"brightness-burst-verification.json"),new JavaScriptSerializer().Serialize(report));return 0;
            }
            if(args.Length==1&&args[0]=="--brightness-worker"){
                using(var input=new StreamReader(new FileStream(new SafeFileHandle(GetStdHandle(-10),false),FileAccess.Read)))
                using(var output=new StreamWriter(new FileStream(new SafeFileHandle(GetStdHandle(-11),false),FileAccess.Write),new UTF8Encoding(false)))return BrightnessBroker.Serve(input,output);
            }
            if(args.Contains("--verify-gpu")||args.Contains("--verify-gpu-and-start")){
                Dictionary<string,object> report;bool created;
                using(var single=new Mutex(true,@"Local\GTBookStableUI",out created)){
                    try{if(!created)throw new IOException("Exit Essentials before running the GPU request check.");report=GpuModes.VerifyRequests();}
                    catch(Exception e){report=new Dictionary<string,object>{{"ok",false},{"error",e.Message}};}
                    Directory.CreateDirectory(Store.DirectoryPath);File.WriteAllText(Path.Combine(Store.DirectoryPath,"gpu-verification.json"),new JavaScriptSerializer().Serialize(report));
                }
                if(args.Contains("--verify-gpu"))return report.ContainsKey("ok")&&Convert.ToBoolean(report["ok"])?0:1;
            }
            if(args.Contains("--verify-extra-and-start")) {
                Dictionary<string,object> report;
                try{report=KeyboardAnimation.VerifyHardware();}catch(Exception e){report=new Dictionary<string,object>{{"ok",false},{"error",e.Message}};}
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tests","extra-hardware.json"),new JavaScriptSerializer().Serialize(report));
                if(!report.ContainsKey("ok")||!Convert.ToBoolean(report["ok"]))return 1;
            }
			if (args.Contains("--verify-brightness") || args.Contains("--verify-brightness-and-start"))
			{
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				Stopwatch stopwatch = Stopwatch.StartNew();
				try
				{
					int num = DisplayBrightness.Read();
					dictionary["before"] = num;
					try
					{
						Reply reply = DisplayBrightness.Change((num <= 0) ? 1 : (-1));
						if (!reply.Ok)
						{
							throw new Exception(reply.Message);
						}
						dictionary["test_readback"] = DisplayBrightness.Read();
					}
					finally
					{
						int num2 = DisplayBrightness.Read();
						if (num2 != num)
						{
							DisplayBrightness.Change(num - num2);
						}
						dictionary["restored"] = DisplayBrightness.Read();
					}
					dictionary["ok"] = Convert.ToInt32(dictionary["restored"]) == num;
					dictionary["elapsed_ms"] = stopwatch.Elapsed.TotalMilliseconds;
				}
				catch (Exception ex)
				{
					dictionary["ok"] = false;
					dictionary["error"] = ex.Message;
				}
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tests", "brightness-hardware.json"), new JavaScriptSerializer().Serialize(dictionary));
				if (!Convert.ToBoolean(dictionary["ok"]))
				{
					return 1;
				}
				if (!args.Contains("--verify-brightness-and-start"))
				{
					return 0;
				}
			}
			if (args.Contains("--configure-startup"))
			{
				try
				{
					Startup.ConfigureReplacement(Process.GetCurrentProcess().MainModule.FileName,!args.Contains("--no-run"));
					return 0;
				}
				catch (Exception ex)
				{
					File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-setup.json"), new JavaScriptSerializer().Serialize(new
					{
						ok = false,
						error = ex.ToString()
					}));
					return 1;
				}
			}
#if TESTING
			if (args.Length > 0 && args[0] == "--self-test")
			{
				return Tests.Run((args.Length > 1) ? args[1] : "test-results.txt");
			}
#endif
			if (args.Length > 0 && args[0] == "--worker")
			{
				Reply reply2;
				try
				{
					if (args.Length != 2)
					{
						throw new ArgumentException("Missing worker command.");
					}
					Request request = new JavaScriptSerializer().Deserialize<Request>(Encoding.UTF8.GetString(Convert.FromBase64String(args[1])));
					Protocol.Validate(request);
					if (request.Action == "device-info" || request.Action == "screen-brightness" || request.Action == "keyboard-animation" || request.Action == "rear-music")
					{
						reply2 = Hardware.Execute(request);
					}
					else
					{
						using (Mutex mutex = new Mutex(false, "Global\\GTBookStableHardware"))
						{
							bool flag;
							try
							{
								flag = mutex.WaitOne(HardwareLease.CommandTimeoutMilliseconds);
							}
							catch (AbandonedMutexException)
							{
								flag = true;
							}
							if (!flag)
							{
								throw new IOException("Another GTBook command is still running.");
							}
							try
							{
								reply2 = Hardware.Execute(request);
							}
							finally
							{
								mutex.ReleaseMutex();
							}
						}
					}
				}
				catch (Exception ex3)
				{
					reply2 = Reply.Fail(ex3.Message);
				}
				using (FileStream stream = new FileStream(new SafeFileHandle(GetStdHandle(-11), false), FileAccess.Write))
				{
					using (StreamWriter streamWriter = new StreamWriter(stream, new UTF8Encoding(false)))
					{
						streamWriter.Write(new JavaScriptSerializer().Serialize(reply2));
					}
				}
				return (!reply2.Ok) ? 1 : 0;
			}
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			if (args.Length == 2 && args[0] == "--render" && Process.GetCurrentProcess().MainModule.FileName.EndsWith("GTBook.Tests.exe", StringComparison.OrdinalIgnoreCase))
			{
				using (MainWindow mainWindow = new MainWindow(true))
				{
					mainWindow.RenderPages(args[1]);
				}
				return 0;
			}
			if (Process.GetCurrentProcess().MainModule.FileName.EndsWith("GTBook.Tests.exe", StringComparison.OrdinalIgnoreCase))
			{
				Application.Run(new MainWindow(true));
				return 0;
			}
            RuntimeMetrics.Begin();
			bool createdNew;
			using (new Mutex(true, "Local\\GTBookStableUI", out createdNew))
			{
				if (!createdNew)
				{
					if (!args.Contains("--startup"))
					{
						if(!AppSignals.Send(AppSignals.ShowName))MessageBox.Show("GTBOOK Essentials is already open. Open it from the system tray.", "GTBOOK Essentials");
					}
					return 0;
				}
				Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
				{
					Store.Log(e.Exception.ToString());
                    Environment.ExitCode=1;Application.Exit();
				};
				AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
				{
					Store.Log(Convert.ToString(e.ExceptionObject));
				};
                using(var main=new MainWindow(false,args.Contains("--startup")))using(var signals=new AppSignals(main))Application.Run(main);
			}
			return Environment.ExitCode;
		}
	}
}
