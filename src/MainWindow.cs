using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GTBook
{
	public sealed class MainWindow : Form
	{
		private readonly Color ink = Theme.Ink;

		private readonly Color muted = Theme.Muted;

		private readonly Panel body = new GradientSurface();

		private readonly Panel sidebar = new GradientSurface{Sidebar=true};
        private readonly Label pageEyebrow=new Label();
        private readonly Label littleQuote=new Label();

		private readonly Label status = new Label();

		private readonly Label heading = new Label();

		private readonly Label subtitle = new Label();

		private readonly Label modeValue = new Label();

		private readonly Label cpuValue = new Label();

		private readonly Label batteryValue = new Label();

		private readonly Label coolingValue = new Label();

		private readonly TextBox details = new TextBox();
        private string deviceReport="";
        private bool loadingDeviceInfo;
        private DateTime deviceInfoUpdated=DateTime.MinValue;

		private readonly Dictionary<string, string> snapshot = new Dictionary<string, string>();

		private readonly List<Button> navigation = new List<Button>();

		private readonly Request keyboard = Store.Load("keyboard");

		private readonly Request rear = Store.Load("rear");

		private readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();

		private FlowLayoutPanel page;

		private volatile bool busy;
        private volatile bool suspended;
        private ModeObserver modeObserver;
        private ModeChime modeChime;
        private bool modeSounds=true;
        private int confirmedMode=-1;
        private RaisedButton applyKeyboardButton;
        private bool lastKeyboardStopOk=true;
        private bool hardwareReady,initializingHardware;
        private readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        private ComboBox gpuSelection;
        private Label gpuState;
        private Button gpuApply;
        private bool gpuTouched,syncingGpu;
        private bool takingControl,readingsFailed;
        private Label readingsState;
        private DateTime readingsUpdatedUtc=DateTime.MinValue;

		private bool closeWhenDone;
        private bool exitRequested;
        private int bootGpuRaw;
        private Button gpuRestart;

		private readonly SemaphoreSlim operations = new SemaphoreSlim(1, 1);

		private ComboBox kbEffect;

		private ComboBox kbExtraEffect;

		private ComboBox rearEffect;

		private Action keyboardChoiceChanged;

		private bool syncingKeyboardChoice;

		private Button stopKeyboardButton;

		private TrackBar kbBright;

		private TrackBar rearBright;

		private TrackBar rearSpeed;

		private Button[] zoneButtons;

		private Button rearColor;

		private Label kbOutcome;

		private Label rearOutcome;

		private string selectedPage;

		private readonly Dictionary<string, Control[]> cachedPages = new Dictionary<string, Control[]>();

		private readonly Dictionary<string, Point> scrollPositions = new Dictionary<string, Point>();

		private string keyboardResult = "Not applied in this session";

		private string rearResult = "Not applied in this session";

		private readonly KeyLock keyLock = new KeyLock();

		private readonly NotifyIcon tray = new NotifyIcon();

		private readonly Label extra = new Label();

		private Task<Reply> musicTask;

		private Task musicCleanup;

		private readonly bool preview;
        private bool takingScreenshot;

		private EventWaitHandle musicStop;

		private LaptopShortcuts laptopKeys;

		private readonly ShortcutToast shortcutToast = new ShortcutToast();

		private int brightnessDelta;

		private int previewDepth;

		private bool changingScreen;

		private bool fnKeys = true;

		private int keyboardPending;
        private readonly LatestCommandPump<Request> keyboardCommands;
        private bool refreshingReadings;
        private int settingsRevision;
        private long keyboardFeedbackSequence;

		private readonly RearPresentation rearState = new RearPresentation();

		private LivePreview rearIllustration;

		private CheckBox rearDraftToggle;

		private Request musicSelection;

		private Task<Reply> keyboardTask;

		private Task keyboardCleanup;

		private EventWaitHandle keyboardStop;
        private KeyboardChannel keyboardChannel;
        private bool keyboardAwakeConfigured;
        private bool fittingCards;
        private Label usbState;
        private RaisedButton usbOn,usbOff;

		public MainWindow(bool preview = false, bool startup = false)
		{
			MainWindow mainWindow = this;
			this.preview = preview;
            keyboardCommands=new LatestCommandPump<Request>(r=>Run(r,"keyboard",false,!string.IsNullOrEmpty(r.Feedback)));
            try{string preference=Path.Combine(Store.DirectoryPath,"mode-sounds.txt");bool enabled;if(File.Exists(preference)&&bool.TryParse(File.ReadAllText(preference),out enabled))modeSounds=enabled;}catch{}
            for(int i=0;i<4;i++){keyboard.Colors[i]=LightingColour.VividHex(keyboard.Colors[i]);rear.Colors[i]=LightingColour.VividHex(rear.Colors[i]);}
			Text = AppIdentity.Name + (preview ? " (layout preview)" : "");
            Icon=AppIdentity.LoadIcon(64);
			base.Name = "GTBookStable";
			base.ClientSize = new Size(1080, 800);
			MinimumSize = new Size(860, 650);
			base.StartPosition = FormStartPosition.CenterScreen;
			base.AutoScaleMode = AutoScaleMode.Dpi;
			if (startup)
			{
				base.Opacity = 0.0;
				base.ShowInTaskbar = false;
			}
			Font = new Font("Segoe UI", 10f);
			BackColor = Theme.Ivory;
			ForeColor = ink;
			sidebar.Dock = DockStyle.Left;
			sidebar.Width = 210;
			sidebar.BackColor = Theme.Pearl;
			base.Controls.Add(body);
			base.Controls.Add(sidebar);
			body.Dock = DockStyle.Fill;
            var brandBitmap=AppIdentity.LoadLogo();
            var logo=new PictureBox{Image=brandBitmap,SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(22,26),Size=new Size(48,48),BackColor=Color.Transparent};sidebar.Controls.Add(logo);
			Label label = Label("GTBOOK", 15f, true);
			label.SetBounds(80, 26, 126, 32);
			sidebar.Controls.Add(label);
			Label label2 = Label("Essentials", 11f);
			label2.SetBounds(82, 60, 112, 24);
			sidebar.Controls.Add(label2);
			int num = 147;
			string[] array = new string[4] { "Overview", "Lighting", "Settings", "Device & help" };
			foreach (string text in array)
			{
				string target = text;
				Button button = Button(text, delegate
				{
					mainWindow.ShowPage(target);
				});
				button.SetBounds(16, num, 178, 48);
                ((RaisedButton)button).Navigation=true;
                button.Font=new Font("Segoe UI",10,FontStyle.Regular);
				sidebar.Controls.Add(button);
				navigation.Add(button);
				num += 57;
			}
            var quoteRail=new Panel{Dock=DockStyle.Bottom,Height=158,BackColor=Color.Transparent};
            var reminder=Label("A LITTLE REMINDER",7.5f,true);reminder.ForeColor=Theme.OrangeInk;reminder.SetBounds(24,8,164,20);quoteRail.Controls.Add(reminder);
            littleQuote.Font=new Font("Segoe UI",12,FontStyle.Regular);littleQuote.ForeColor=Theme.Ink;littleQuote.BackColor=Color.Transparent;littleQuote.SetBounds(24,36,164,64);quoteRail.Controls.Add(littleQuote);
            Label label3=Label("v"+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(2)+" · local & offline",8.5f);label3.SetBounds(24,116,164,24);quoteRail.Controls.Add(label3);sidebar.Controls.Add(quoteRail);
			Panel panel = new Panel
			{
				Dock = DockStyle.Top,
				Height = 110,
                BackColor=Color.Transparent
			};
            pageEyebrow.SetBounds(30,12,620,18);pageEyebrow.UseMnemonic=false;pageEyebrow.Font=new Font("Segoe UI",8,FontStyle.Bold);pageEyebrow.ForeColor=Theme.OrangeInk;pageEyebrow.BackColor=Color.Transparent;panel.Controls.Add(pageEyebrow);
			heading.SetBounds(28, 31, 620, 46);
			heading.Font = new Font("Segoe UI", 22f, FontStyle.Bold);
			heading.ForeColor = ink;
            heading.BackColor=Color.Transparent;
			subtitle.SetBounds(30, 78, 690, 26);
			subtitle.ForeColor = muted;
            subtitle.BackColor=Color.Transparent;
			panel.Controls.Add(heading);
			panel.Controls.Add(subtitle);
            panel.Resize+=(s,e)=>{int width=Math.Max(120,panel.ClientSize.Width-(int)(56*DeviceDpi/96f));heading.Width=width;subtitle.Width=width;pageEyebrow.Width=width;};
			Panel panel2 = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 68,
                BackColor=Color.Transparent,
				Padding = new Padding(28, 12, 24, 12)
			};
			status.Dock = DockStyle.Fill;
			status.Font = new Font("Segoe UI", 10f);
			status.ForeColor = muted;
            status.BackColor=Color.Transparent;
			status.Text = "Getting to know your GTBook…";
			panel2.Controls.Add(status);
			Panel panel3 = new Panel
			{
				Dock = DockStyle.Fill,
                BackColor=Color.Transparent,
				Padding = new Padding(26, 0, 22, 0)
			};
			page = new GradientFlowPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				AutoScroll = true,
				Padding = new Padding(0, 0, 8, 12)
			};
			page.SizeChanged += delegate
			{
				FitCards();
			};
			panel3.Controls.Add(page);
			body.Controls.Add(panel3);
			body.Controls.Add(panel2);
			body.Controls.Add(panel);
			base.FormClosing += async delegate(object s, FormClosingEventArgs e)
			{
                if(e.CloseReason==CloseReason.WindowsShutDown){exitRequested=true;if(keyboardStop!=null)keyboardStop.Set();if(musicStop!=null)musicStop.Set();return;}
                if(WindowLifecycle.HideInsteadOfExit(preview,exitRequested,e.CloseReason)){e.Cancel=true;Hide();return;}
                if(takingScreenshot){e.Cancel=true;closeWhenDone=true;status.Text="Saving your screenshot before closing…";return;}
				if (busy || musicTask != null || keyboardTask != null)
				{
					e.Cancel = true;
					closeWhenDone = true;
					status.Text = "Finishing the current command before closing…";
					if (!busy)
					{
						await StopKeyboardAnimation();
						await StopMusic();
						Close();
					}
				}
			};
			SystemEvents.PowerModeChanged += PowerChanged;
			base.FormClosed += delegate
			{
                lifetime.Cancel();
                lifetime.Dispose();if(!preview)WorkerClient.DisposeShared();
				SystemEvents.PowerModeChanged -= PowerChanged;
				if (laptopKeys != null)
				{
					laptopKeys.Dispose();
				}
				shortcutToast.Dispose();
                if(modeObserver!=null)modeObserver.Dispose();
                if(modeChime!=null)modeChime.Dispose();
				ClearCachedPages();
				clock.Stop();
				clock.Dispose();
				keyLock.Dispose();
				tray.Dispose();
                brandBitmap.Dispose();Icon.Dispose();
			};
			base.Shown += async delegate
			{
				if (startup)
				{
					mainWindow.Hide();
					mainWindow.Opacity = 1.0;
					mainWindow.ShowInTaskbar = true;
					Store.Log("Started with Windows · tray mode");
				}
				if (preview)
				{
					mainWindow.status.Text = "Layout preview · hardware is disconnected in this test build.";
				}
				else
				{
					mainWindow.InitializeLaptopKeys();
                    RuntimeMetrics.Record("shortcuts_ready");
                    await mainWindow.CloseOfficialOnOpen();
					await mainWindow.InitializeHardware();
					if (Store.Warning.Length > 0)
					{
						Label label4 = mainWindow.status;
						label4.Text = label4.Text + "\n" + Store.Warning;
					}
				}
			};
			tray.Icon = AppIdentity.LoadIcon(32);
			tray.Text = AppIdentity.Name;
			tray.Visible = true;
			ContextMenuStrip contextMenuStrip = new ContextMenuStrip
			{
				Items = 
				{
					{
						"Show GTBOOK Essentials",
						(Image)null,
						(EventHandler)delegate
						{
							ShowWithDeviceControl();
						}
					},
					{
						"Hide to tray",
						(Image)null,
						(EventHandler)delegate
						{
							Hide();
						}
					},
					{
						"Exit",
						(Image)null,
						(EventHandler)delegate
						{
							ExitApplication();
						}
					}
				}
			};
			tray.ContextMenuStrip = contextMenuStrip;
			tray.DoubleClick += delegate
			{
				ShowWithDeviceControl();
			};
			clock.Interval = ReadingRefresh.Interval(SystemInformation.PowerStatus.PowerLineStatus!=PowerLineStatus.Online);
			clock.Tick += async delegate
			{
				mainWindow.UpdateBattery();
				if (!preview && ReadingRefresh.ShouldPoll(mainWindow.Visible,mainWindow.WindowState==FormWindowState.Minimized,mainWindow.selectedPage=="Overview",mainWindow.busy||mainWindow.takingControl))
				{
					await mainWindow.Run(new Request
					{
						Action = "refresh"
					}, null, true);
				}
			};
			clock.Start();
			base.VisibleChanged += delegate
			{
				SetPreviewAnimation(page, base.Visible && base.WindowState != FormWindowState.Minimized);
                UpdateUiPolling();
			};
			base.Resize += delegate
			{
				SetPreviewAnimation(page, base.Visible && base.WindowState != FormWindowState.Minimized);
                UpdateUiPolling();
			};
			ShowPage("Overview");
		}

        private void UpdateUiPolling(){if(preview)return;clock.Interval=ReadingRefresh.Interval(SystemInformation.PowerStatus.PowerLineStatus!=PowerLineStatus.Online);if(Visible&&WindowState!=FormWindowState.Minimized&&selectedPage=="Overview")clock.Start();else clock.Stop();}
        private async Task CloseOfficialOnOpen(){
            if(preview||takingControl||IsDisposed)return;takingControl=true;
            try{string message=await Task.Run(()=>OemHandoff.CloseOfficial());if(!IsDisposed&&message.Length>0){status.Text=message;Store.Log(message);}}
            catch(Exception e){if(!IsDisposed)status.Text="Official app hand-off: "+e.Message;Store.Log(e.Message);}
            finally{takingControl=false;}
        }
        public async void ShowWithDeviceControl(){
            if(IsDisposed)return;Show();WindowState=FormWindowState.Normal;Activate();if(takingControl)return;
            await CloseOfficialOnOpen();if(!IsDisposed&&!busy)await Run(new Request{Action="refresh"},null,true);
        }
        public void ExitApplication(){exitRequested=true;Close();}
        private void RestartWindows(){if(preview)return;Process.Start(new ProcessStartInfo("shutdown.exe","/r /t 0"){UseShellExecute=false,CreateNoWindow=true});}
        private void UpdateReadingsStamp(){if(readingsState==null||readingsState.IsDisposed)return;readingsState.Text=preview?"PREVIEW":readingsFailed?"AUTO · retrying…":readingsUpdatedUtc==DateTime.MinValue?"AUTO · starting…":"LIVE · "+readingsUpdatedUtc.ToLocalTime().ToString("HH:mm:ss");}
        private async Task InitializeHardware(){
            if(initializingHardware||preview||IsDisposed)return;initializingHardware=true;hardwareReady=false;
            try{
                await Task.Run(()=>GpuPending.Initialize());
                foreach(int delay in new[]{0,1500,3500,8000,15000,30000}){
                    if(delay>0)await Task.Delay(delay,lifetime.Token);if(IsDisposed||suspended||closeWhenDone)return;
                    await Run(new Request{Action="refresh"},null,true);
                    if(snapshot.ContainsKey("supported")&&snapshot["supported"]=="False")return;
                    if(hardwareReady){
                        RuntimeMetrics.Record("hardware_ready");
                        if(keyboardTask==null&&ExtraEffects.Contains(keyboard.Effect)&&keyboard.Brightness>0&&Store.Flag("resume-extra",keyboard.Action=="keyboard-animation"))await StartKeyboardAnimation(Copy(keyboard,"keyboard-animation"),true,true);
                        return;
                    }
                }
                if(!IsDisposed)status.Text="Hardware is not ready yet. Shortcuts are running; use Refresh after the OEM driver is ready.";
            }catch(OperationCanceledException){}finally{initializingHardware=false;}
        }
        private void UpdateGpuUi(){
            if(gpuSelection==null||gpuSelection.IsDisposed||gpuState==null||gpuState.IsDisposed)return;
            int raw;bool known=snapshot.ContainsKey("gpu-raw")&&int.TryParse(snapshot["gpu-raw"],out raw)&&raw>=1&&raw<=3;
            if(known&&!gpuTouched){syncingGpu=true;gpuSelection.SelectedIndex=int.Parse(snapshot["gpu-raw"])-1;syncingGpu=false;}
            bool supported=snapshot.ContainsKey("supported")&&snapshot["supported"]=="True";
            if(gpuApply!=null&&!gpuApply.IsDisposed)gpuApply.Enabled=known&&supported&&GpuPending.Ready&&gpuSelection.SelectedIndex>=0;
            if(known&&bootGpuRaw==0)bootGpuRaw=GpuPending.Pending?GpuPending.Before:int.Parse(snapshot["gpu-raw"]);
            if(gpuRestart!=null)gpuRestart.Visible=GpuPending.Pending;
            gpuState.Text=known?"Boot firmware setting: "+GpuModes.Name((uint)bootGpuRaw)+".\n"+(GpuPending.Pending?"Pending: "+GpuModes.Name((uint)GpuPending.Target)+" · restart Windows to activate.":"No restart pending. Active display routing is not independently measured."):"Saved firmware setting not read. Refresh before applying.";
        }
		private Label Label(string text, float size = 10f, bool bold = false)
		{
			Label label = new Label();
			label.Text = text;
			label.UseMnemonic = false;
			label.AutoSize = false;
			label.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
			label.ForeColor = (bold ? ink : muted);
			label.BackColor = Color.Transparent;
			return label;
		}

		private Button Button(string text, Action action)
		{
			RaisedButton raisedButton = new RaisedButton();
			raisedButton.Text = text;
			raisedButton.UseMnemonic = false;
			raisedButton.AutoSize = false;
			raisedButton.Height = 38;
			raisedButton.Width = 140;
			raisedButton.BackColor = Theme.White;
			raisedButton.ForeColor = ink;
			raisedButton.FlatStyle = FlatStyle.Flat;
			raisedButton.Cursor = Cursors.Hand;
			raisedButton.UseVisualStyleBackColor = false;
			RaisedButton raisedButton2 = raisedButton;
			raisedButton2.FlatAppearance.BorderSize = 0;
			raisedButton2.FlatAppearance.MouseOverBackColor = Theme.White;
			raisedButton2.Click += delegate
			{
				action();
			};
			return raisedButton2;
		}

		private void FitCards()
		{
            if(fittingCards)return;fittingCards=true;page.SuspendLayout();
            try{
                float s=DeviceDpi/96f;int available=page.ClientSize.Width-page.Padding.Horizontal-(int)(20*s),gap=(int)(16*s);
                bool wide=selectedPage=="Lighting"&&available>=(int)(1160*s);
                page.FlowDirection=wide?FlowDirection.LeftToRight:FlowDirection.TopDown;page.WrapContents=wide;
                int index=0;
                foreach(Control control in page.Controls){
                    control.Width=wide?(index<2?(available-gap)/2:available):Math.Max(350,Math.Min(available,(int)(1100*s)));
                    control.Margin=new Padding(0,0,wide&&index==0?gap:0,(int)(14*s));
                    page.SetFlowBreak(control,wide&&index>0);index++;
                }
            }finally{page.ResumeLayout();fittingCards=false;}
		}

		public void RenderPages(string directory)
		{
			Directory.CreateDirectory(directory);
            ShortcutToast.RenderExamples(directory);
			base.ShowInTaskbar = false;
			base.StartPosition = FormStartPosition.Manual;
			base.Location = new Point(-32000, -32000);
			Show();
			Application.DoEvents();
			int[] array = new int[] { 1920,1080,860 };
			foreach (int num in array)
			{
				base.ClientSize = new Size(num, num==1920?1080:800);
				string[] array2 = new string[4] { "Overview", "Lighting", "Settings", "Device & help" };
				foreach (string text in array2)
				{
					ShowPage(text);
					PerformLayout();
					page.PerformLayout();
					Application.DoEvents();
					using (Bitmap bitmap = new Bitmap(base.Width, base.Height))
					{
						DrawToBitmap(bitmap, new Rectangle(0, 0, base.Width, base.Height));
						bitmap.Save(Path.Combine(directory, num + "-" + text.Replace(" & ", "-") + ".png"));
					}
				}
			}
			Request[] array3 = new Request[6]
			{
				Copy(keyboard, "keyboard"),
				Copy(rear, "rear"),
				new Request
				{
					Action = "mode",
					Mode = 0
				},
				new Request
				{
					Action = "fan",
					Enabled = true
				},
				new Request
				{
					Action = "gpu",
					Mode = 2
				},
				new Request
				{
					Action = "usb",
					Enabled = true
				}
			};
			foreach (Request request in array3)
			{
				using (ApplyPreview applyPreview = new ApplyPreview(request, "No hardware changes during preview"))
				{
					applyPreview.ShowInTaskbar = false;
					applyPreview.StartPosition = FormStartPosition.Manual;
					applyPreview.Location = new Point(-32000, -32000);
					applyPreview.Show();
					Application.DoEvents();
					using (Bitmap bitmap2 = new Bitmap(applyPreview.Width, applyPreview.Height))
					{
						applyPreview.DrawToBitmap(bitmap2, new Rectangle(0, 0, applyPreview.Width, applyPreview.Height));
						bitmap2.Save(Path.Combine(directory, "Confirm-" + request.Action + ".png"));
					}
				}
			}
			ShowPage("Lighting");
			rearState.Applied(new Request
			{
				Action = "rear-off"
			});
			UpdateRearIndicator();
			Control control = rearIllustration.Parent;
			using (Bitmap bitmap2 = new Bitmap(control.Width, control.Height))
			{
				control.DrawToBitmap(bitmap2, new Rectangle(0, 0, control.Width, control.Height));
				bitmap2.Save(Path.Combine(directory, "Rear-confirmed-off.png"));
			}
			rearState.PreviewDraft = true;
			UpdateRearIndicator();
			using (Bitmap bitmap2 = new Bitmap(control.Width, control.Height))
			{
				control.DrawToBitmap(bitmap2, new Rectangle(0, 0, control.Width, control.Height));
				bitmap2.Save(Path.Combine(directory, "Rear-off-draft-preview.png"));
			}
			base.ClientSize = new Size(1080, 800);
			page.AutoScrollPosition = Point.Empty;
			keyboard.Effect = "Aurora";
			SyncKeyboardEditor();
			Application.DoEvents();
			using (Bitmap bitmap2 = new Bitmap(base.Width, base.Height))
			{
				DrawToBitmap(bitmap2, new Rectangle(0, 0, base.Width, base.Height));
				bitmap2.Save(Path.Combine(directory, "Extra-keyboard.png"));
			}
            var fullKeyboard=applyKeyboardButton.Parent.Parent;
            using(var image=new Bitmap(fullKeyboard.Width,fullKeyboard.Height)){fullKeyboard.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(directory,"Keyboard-actions.png"));}
		}

		public Dictionary<string, object> MeasureNavigation()
		{
			base.ShowInTaskbar = false;
			base.StartPosition = FormStartPosition.Manual;
			base.Location = new Point(-32000, -32000);
			Show();
			Application.DoEvents();
			Stopwatch stopwatch = Stopwatch.StartNew();
			string[] array = new string[4] { "Overview", "Lighting", "Settings", "Device & help" };
			foreach (string title in array)
			{
				ShowPage(title);
			}
			stopwatch.Stop();
			ShowPage("Overview");
			Control objA = page.Controls[0];
			ShowPage("Lighting");
			ShowPage("Overview");
			if (!object.ReferenceEquals(objA, page.Controls[0]))
			{
				throw new Exception("Navigation rebuilt the overview instead of reusing it.");
			}
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			string[] array2 = new string[4] { "Overview", "Lighting", "Settings", "Device & help" };
			for (int j = 0; j < 100; j++)
			{
				ShowPage(array2[j % 4]);
			}
			stopwatch2.Stop();
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			dictionary.Add("switches", 100);
			dictionary.Add("warm_average_ms", stopwatch2.Elapsed.TotalMilliseconds / 100.0);
			dictionary.Add("first_four_pages_ms", stopwatch.Elapsed.TotalMilliseconds);
			dictionary.Add("reused_controls", true);
			dictionary.Add("hardware_access", false);
			return dictionary;
		}

#if TESTING
        internal void VerifyLightingLayout(){
            ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-32000,-32000);Show();
            foreach(int width in new[]{1920,860,1920}){
                ClientSize=new Size(width,1080);ShowPage("Lighting");PerformLayout();page.PerformLayout();Application.DoEvents();
                var left=page.Controls[0];var right=page.Controls[1];bool wide=width==1920;
                if(wide){
                    if(left.Top!=right.Top||right.Left<left.Right||right.Right>page.ClientSize.Width)throw new Exception("Lighting cards are not beside each other inside the viewport.");
                    if(right.Bottom>page.ClientSize.Height)throw new Exception("Rear-light controls require scrolling in the wide layout.");
                }else if(right.Top<=left.Top||left.Left!=right.Left)throw new Exception("Compact lighting did not return to a stacked layout.");
            }
        }
#endif

		private SoftPanel Card(string title, string description, int height)
		{
			SoftPanel softPanel = new SoftPanel();
			softPanel.Height = height;
			softPanel.Width = Math.Max(350, page.ClientSize.Width - 28);
			softPanel.Margin = new Padding(0, 0, 0, 14);
			SoftPanel softPanel2 = softPanel;
			Label label = Label(title, 14f, true);
			label.SetBounds(22, 17, softPanel2.Width - 44, 30);
			label.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			softPanel2.Controls.Add(label);
			if (description.Length > 0)
			{
				Label label2 = Label(description, 9.5f);
				label2.SetBounds(23, 51, softPanel2.Width - 46, 38);
				label2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
				softPanel2.Controls.Add(label2);
			}
			page.Controls.Add(softPanel2);
			return softPanel2;
		}

		private void Value(Label label, string text, Panel card, int x, int width)
		{
			label.Text = text;
			label.Font = new Font("Segoe UI", 17f, FontStyle.Bold);
			label.ForeColor = ink;
			label.SetBounds(x, 45, width, 40);
			card.Controls.Add(label);
		}

		private void ShowPage(string title)
		{
			page.SuspendLayout();
			SetPreviewAnimation(page, false);
			if (selectedPage != null)
			{
				cachedPages[selectedPage] = page.Controls.Cast<Control>().ToArray();
				scrollPositions[selectedPage] = new Point(-page.AutoScrollPosition.X, -page.AutoScrollPosition.Y);
			}
			page.Controls.Clear();
			page.AutoScrollPosition = Point.Empty;
			foreach (Button item in navigation)
			{
				((RaisedButton)item).Selected = Convert.ToString(item.Tag ?? item.Text) == title;
			}
			Label label = heading;
			object obj;
			switch (title)
			{
			default:
				obj = "Your little machine";
				break;
			case "Settings":
				obj = "Just how you like it";
				break;
			case "Lighting":
				obj = "Make it yours  ✦";
				break;
			case "Overview":
                obj = "Hello, GTBook";
				break;
			}
			label.Text = (string)obj;
			Label label2 = subtitle;
			object obj2;
			switch (title)
			{
			default:
				obj2 = "Connection details, a helping hand, and the original controls.";
				break;
			case "Settings":
				obj2 = "The useful little switches for your everyday setup.";
				break;
			case "Lighting":
				obj2 = "Pick a colour. Set the mood. Apply when you’re ready.";
				break;
			case "Overview":
				obj2 = "The everyday essentials, in one quiet corner.";
				break;
			}
			label2.Text = (string)obj2;
            pageEyebrow.Text=title=="Overview"?"":title=="Device & help"?"DEVICE & SUPPORT":title.ToUpperInvariant();
            littleQuote.Text=title=="Overview"?"Small steps.\nSteady progress.":title=="Lighting"?"A little colour.\nA little joy.":title=="Settings"?"Less fuss.\nMore flow.":"Good tools.\nQuiet confidence.";
            littleQuote.Tag=littleQuote.Text;
			Control[] value;
			if (cachedPages.TryGetValue(title, out value))
			{
				page.Controls.AddRange(value);
			}
			else
			{
				switch (title)
				{
				case "Overview":
					Overview();
					break;
				case "Lighting":
					Lighting();
					break;
				case "Settings":
					SettingsPage();
					break;
				default:
					Diagnostics();
					break;
				}
				if (base.IsHandleCreated && base.DeviceDpi != 96)
				{
					foreach (Control control in page.Controls)
					{
						control.Scale(new SizeF((float)base.DeviceDpi / 96f, (float)base.DeviceDpi / 96f));
					}
				}
				Languages.Apply(page);
			}
			Languages.Apply(sidebar);
			selectedPage = title;
			FitCards();
			page.ResumeLayout();
			if (scrollPositions.ContainsKey(title))
			{
				page.AutoScrollPosition = scrollPositions[title];
			}
			UpdateValues();
			UpdateBattery();
			SetPreviewAnimation(page, base.Visible);
            UpdateUiPolling();
            if(title=="Device & help"&&!preview)BeginInvoke((Action)async delegate{await LoadDeviceInfo();});
            if(!preview&&(title=="Overview"||title=="Settings")&&Visible&&!busy&&!takingControl&&IsHandleCreated)BeginInvoke((Action)async delegate{if(!IsDisposed&&!busy)await Run(new Request{Action="refresh"},null,true);});
		}

		private void SetPreviewAnimation(Control root, bool enabled)
		{
			foreach (Control control in root.Controls)
			{
				LivePreview livePreview = control as LivePreview;
				if (livePreview != null)
				{
					livePreview.Animate = enabled;
				}
				SetPreviewAnimation(control, enabled);
			}
		}

		private LivePreview AttachLightingPreview(SoftPanel card, Func<Request> selected, string target)
		{
			foreach (Control control in card.Controls)
			{
				if (control.Top >= 90)
				{
					control.Top += 170;
				}
			}
			card.Height += 170;
			LivePreview livePreview = new LivePreview();
			livePreview.Target = target;
			livePreview.Selection = selected;
			livePreview.Location = new Point(22, 91);
			livePreview.Size = new Size(card.Width - 44, 150);
			livePreview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			LivePreview livePreview2 = livePreview;
			card.Controls.Add(livePreview2);
			return livePreview2;
		}

		private void RearEdited()
		{
			rearState.Edited();
			UpdateRearIndicator();
		}

		private void UpdateRearIndicator()
		{
			if (rearDraftToggle != null && !rearDraftToggle.IsDisposed && rearDraftToggle.Checked != rearState.PreviewDraft)
			{
				rearDraftToggle.Checked = rearState.PreviewDraft;
			}
			if (rearIllustration != null && !rearIllustration.IsDisposed)
			{
				rearIllustration.AccessibleName = rearState.Caption(rear);
				rearIllustration.Invalidate();
			}
			if (rearOutcome != null && !rearOutcome.IsDisposed)
			{
				rearOutcome.Text = rearState.Caption(rear);
				rearOutcome.ForeColor = ((rearState.Failure != null) ? Color.FromArgb(153, 63, 85) : Theme.Ink);
			}
		}

		private bool ConfirmPreview(Request request)
		{
            if(!ConfirmationPolicy.Required(request.Action))return true;
			string current = ((request.Action == "keyboard"||request.Action=="keyboard-animation") ? keyboardResult : (request.Action.StartsWith("rear") ? rearState.Caption(rear) : ((request.Action == "mode") ? ("Current mode: " + (snapshot.ContainsKey("mode") ? snapshot["mode"] : "not read")) : "Nothing changes until you choose Apply change.")));
			previewDepth++;
			try
			{
				using (ApplyPreview applyPreview = new ApplyPreview(request, current))
				{
					return applyPreview.ShowDialog(base.Visible ? this : null) == DialogResult.OK;
				}
			}
			finally
			{
				previewDepth--;
			}
		}

		private void InitializeLaptopKeys()
		{
			try
			{
				string path = Path.Combine(Store.DirectoryPath, "fn-keys.txt");
				bool result;
				if (File.Exists(path) && bool.TryParse(File.ReadAllText(path), out result))
				{
					fnKeys = result;
				}
				laptopKeys = new LaptopShortcuts
				{
					MediaKeys = fnKeys
				};
				laptopKeys.Action += delegate(string action)
				{
					try
					{
						if (base.IsHandleCreated && !base.IsDisposed)
						{
							BeginInvoke((Action)async delegate
							{
								await HandleLaptopKey(action);
							});
						}
					}
					catch (InvalidOperationException)
					{
					}
				};
			}
			catch (Exception ex)
			{
				status.Text = "Laptop shortcut keys could not start: " + ex.Message;
				Store.Log(ex.Message);
			}
		}

		private async Task HandleLaptopKey(string action)
		{
			if (preview || base.IsDisposed)
			{
				return;
			}
            if(action=="screenshot"){await TakeScreenshot();return;}
			if (action == "brightness-down" || action == "brightness-up")
			{
				brightnessDelta = Math.Max(-100, Math.Min(100, brightnessDelta + ((action == "brightness-down") ? (-10) : 10)));
				if (changingScreen)
				{
					return;
				}
				changingScreen = true;
				try
				{
					while (brightnessDelta != 0 && !base.IsDisposed)
					{
						int delta = brightnessDelta;
						brightnessDelta = 0;
						await Run(new Request
						{
							Action = "screen-brightness",
							Step = delta
						}, null, false, true);
					}
					return;
				}
				finally
				{
					changingScreen = false;
				}
			}
			if (previewDepth > 0)
			{
				shortcutToast.Notify("Close the preview before using this shortcut");
				return;
			}
			if (action.StartsWith("mode-"))
			{
				int mode = int.Parse(action.Substring(5));
				await Run(new Request
				{
					Action = "mode",
					Mode = mode
				}, null, false, true);
				return;
			}
			if (action == "keyboard-brightness")
			{
				keyboard.Brightness = ShortcutDecoder.NextKeyboardBrightness(keyboard.Brightness);
			}
			else
			{
				if (!(action == "keyboard-effect"))
				{
					return;
				}
				string[] array = (ExtraEffects.Contains(keyboard.Effect) ? ExtraEffects.Names : new string[6] { "Static", "Breathing", "Wave", "Clock", "Rainbow", "Flow" });
				keyboard.Effect = array[(Array.IndexOf(array, keyboard.Effect) + 1) % array.Length];
			}
			keyboardPending++;
			try
			{
                var request=Copy(keyboard,"keyboard");request.Feedback=action;
				await QueueKeyboard(request);
			}
			finally
			{
				keyboardPending--;
				if (keyboardPending == 0)
				{
					SyncKeyboardEditor();
				}
			}
		}

		private Task QueueKeyboard(Request request){request.FeedbackSequence=++keyboardFeedbackSequence;shortcutToast.Notify(FeedbackNotice.Pending(request));return keyboardCommands.Submit(request);}
        private void SyncKeyboardEditor()
		{
			syncingKeyboardChoice = true;
			try
			{
				if (kbBright != null && !kbBright.IsDisposed)
				{
					kbBright.Value = keyboard.Brightness;
				}
				ComboBox comboBox = (ExtraEffects.Contains(keyboard.Effect) ? kbExtraEffect : kbEffect);
				if (comboBox != null && !comboBox.IsDisposed)
				{
					comboBox.SelectedItem = keyboard.Effect;
				}
			}
			finally
			{
				syncingKeyboardChoice = false;
			}
			if (keyboardChoiceChanged != null && kbEffect != null && !kbEffect.IsDisposed)
			{
				keyboardChoiceChanged();
			}
		}

		private void ClearCachedPage(string title)
		{
			if (selectedPage == title)
			{
				cachedPages[title] = page.Controls.Cast<Control>().ToArray();
				page.Controls.Clear();
				selectedPage = null;
			}
			Control[] value;
			if (cachedPages.TryGetValue(title, out value))
			{
				Control[] array = value;
				foreach (Control control in array)
				{
					control.Dispose();
				}
				cachedPages.Remove(title);
			}
			scrollPositions.Remove(title);
		}

		private void ClearCachedPages()
		{
			Control[] array = new Control[6] { modeValue, cpuValue, batteryValue, coolingValue, details, extra };
			foreach (Control control in array)
			{
				if (control.Parent != null)
				{
					control.Parent.Controls.Remove(control);
				}
			}
			if (selectedPage != null)
			{
				cachedPages[selectedPage] = page.Controls.Cast<Control>().ToArray();
			}
			page.Controls.Clear();
			selectedPage = null;
			foreach (Control[] value in cachedPages.Values)
			{
				array = value;
				foreach (Control control in array)
				{
					control.Dispose();
				}
			}
			cachedPages.Clear();
			scrollPositions.Clear();
		}

		private void Overview()
		{
			SoftPanel softPanel = Card("Right now", "", 118);
            readingsState=Label("",8.5f);readingsState.ForeColor=Theme.OrangeInk;readingsState.TextAlign=ContentAlignment.MiddleRight;readingsState.SetBounds(softPanel.Width-230,20,206,22);readingsState.Anchor=AnchorStyles.Top|AnchorStyles.Right;softPanel.Controls.Add(readingsState);readingsState.BringToFront();UpdateReadingsStamp();
			TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
			tableLayoutPanel.Location = new Point(22, 50);
			tableLayoutPanel.Height = 66;
			tableLayoutPanel.Width = softPanel.Width - 44;
			tableLayoutPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			tableLayoutPanel.ColumnCount = 3;
			TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
			for (int i = 0; i < 3; i++)
			{
				tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
			}
			Label[] array = new Label[3] { modeValue, cpuValue, batteryValue };
			string[] array2 = new string[3] { "POWER MODE", "CPU TEMPERATURE", "BATTERY" };
			for (int i = 0; i < 3; i++)
			{
				Panel panel = new Panel();
				panel.Dock = DockStyle.Fill;
				Panel panel2 = panel;
				Label label = Label(array2[i], 8f, true);
				label.Dock = DockStyle.Top;
				label.Height = 20;
				array[i].Dock = DockStyle.Fill;
				array[i].Font = new Font("Segoe UI", 16f, FontStyle.Bold);
				array[i].ForeColor = ink;
				panel2.Controls.Add(array[i]);
				panel2.Controls.Add(label);
				tableLayoutPanel2.Controls.Add(panel2, i, 0);
			}
			softPanel.Controls.Add(tableLayoutPanel2);
			SoftPanel softPanel2 = Card("Performance & cooling", "Choose your pace. Your laptop keeps looking after cooling.", 208);
			FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
			flowLayoutPanel.Location = new Point(22, 100);
			flowLayoutPanel.Height = 48;
			flowLayoutPanel.Width = softPanel2.Width - 44;
			flowLayoutPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			flowLayoutPanel.WrapContents = false;
			FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
			string[] array3 = new string[3] { "Quiet", "Balanced", "Gaming" };
			for (int i = 0; i < 3; i++)
			{
				int mode = i;
				Button button = Button(array3[i], async delegate
				{
					await Run(new Request
					{
						Action = "mode",
						Mode = mode
					}, null);
				});
				button.Width = 145;
				button.Margin = new Padding(0, 0, 10, 0);
				flowLayoutPanel2.Controls.Add(button);
			}
			softPanel2.Controls.Add(flowLayoutPanel2);
			Button button2 = Button("Restore auto cooling", async delegate
			{
				await Run(new Request
				{
					Action = "auto"
				}, null);
			});
			button2.SetBounds(22, 155, 192, 36);
			softPanel2.Controls.Add(button2);
			coolingValue.SetBounds(230, 160, 340, 26);
			coolingValue.ForeColor = muted;
			softPanel2.Controls.Add(coolingValue);
			SoftPanel softPanel3 = Card("System health", "Temperatures, fans, memory and storage", 146);
			extra.SetBounds(22, 91, softPanel3.Width - 44, 48);
			extra.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			extra.Font = new Font("Segoe UI", 10f);
			extra.ForeColor = ink;
			softPanel3.Controls.Add(extra);
			Button value = Button("Refresh readings", async delegate
			{
				await Run(new Request
				{
					Action = "refresh"
				}, null);
			});
			page.Controls.Add(value);
		}

		private ComboBox Effect(Panel parent, string[] items, string selected, int y)
		{
			ComboBox comboBox = new ComboBox();
			comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
			comboBox.Location = new Point(22, y);
			comboBox.Width = 174;
            comboBox.FlatStyle=FlatStyle.Standard;
            comboBox.Font=new Font("Segoe UI",10f);
            comboBox.DrawMode=DrawMode.OwnerDrawFixed;comboBox.ItemHeight=30;
            comboBox.DropDownHeight=360;comboBox.IntegralHeight=false;comboBox.DropDownWidth=300;
            comboBox.BackColor=Theme.Ivory;comboBox.ForeColor=Theme.Ink;
            comboBox.AccessibleDescription="Choose an effect. Click the full field or use Alt + Down.";
            comboBox.DrawItem+=(s,e)=>{
                if(e.Index<0)return;bool highlighted=(e.State&DrawItemState.Selected)!=0;
                using(var fill=new System.Drawing.Drawing2D.LinearGradientBrush(e.Bounds,highlighted?Theme.Pearl:Theme.White,Theme.White,0f))e.Graphics.FillRectangle(fill,e.Bounds);
                string name=Convert.ToString(comboBox.Items[e.Index]);string text=name;bool list=(e.State&DrawItemState.ComboBoxEdit)==0;
                if(text=="Four zones"&&list)text+=" · zone settings";
                var bounds=e.Bounds;bounds.Inflate(-10,0);
                if(list&&ExtraEffects.Contains(name)){
                    var colours=ExtraEffects.Frame(new Request{Effect=name,Brightness=100},.5);bounds.Width-=56;
                    for(int zone=0;zone<4;zone++)using(var brush=new SolidBrush(colours[zone]))e.Graphics.FillRectangle(brush,e.Bounds.Right-52+zone*11,e.Bounds.Top+9,8,12);
                }
                if(highlighted)using(var marker=new SolidBrush(Theme.OrangeEnd))e.Graphics.FillRectangle(marker,e.Bounds.Left,e.Bounds.Top+3,3,e.Bounds.Height-6);
                using(var font=new Font(comboBox.Font,highlighted?FontStyle.Bold:FontStyle.Regular))TextRenderer.DrawText(e.Graphics,text,font,bounds,highlighted?Theme.OrangeInk:Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();            };
			ComboBox comboBox2 = comboBox;
			comboBox2.Items.AddRange(items);
			comboBox2.SelectedItem = (items.Contains(selected) ? selected : items[0]);
			parent.Controls.Add(comboBox2);
			return comboBox2;
		}

		private TrackBar Slider(Panel parent, int value, int x, int y, int maximum, string caption)
		{
			Label i = Label(caption + "  " + value + ((maximum == 100) ? "%" : ""), 9f);
			i.SetBounds(x, y, 280, 24);
			parent.Controls.Add(i);
			TrackBar track = new WheelSafeTrackBar
			{
				Minimum = ((maximum == 9) ? 1 : 0),
				Maximum = maximum,
				Value = value,
				TickStyle = TickStyle.None,
				Location = new Point(x - 4, y + 24),
				Width = Math.Min(265, Math.Max(150, parent.Width - x - 22)),
				Height = 38
			};
			track.ValueChanged += delegate
			{
				i.Text = caption + "  " + track.Value + ((maximum == 100) ? "%" : "");
			};
			parent.Controls.Add(track);
			if (x >= 300)
			{
				Action reflow = delegate
				{
					if (parent.IsDisposed)
					{
						return;
					}
					float num = (float)parent.DeviceDpi / 96f;
					int num2 = parent.ClientSize.Width / 2 + (int)(12f * num);
					i.Left = num2;
					i.Width = Math.Max(80, parent.ClientSize.Width - num2 - (int)(22f * num));
					track.Left = num2 - (int)(4f * num);
					track.Width = Math.Max(80, parent.ClientSize.Width - num2 - (int)(22f * num));
					foreach (TrackBar item in parent.Controls.OfType<TrackBar>())
					{
						if (item != track)
						{
							item.Width = Math.Min((int)(265f * num), Math.Max(80, parent.ClientSize.Width / 2 - (int)(32f * num)));
						}
					}
				};
				parent.SizeChanged += delegate
				{
					if (parent.IsHandleCreated && !parent.IsDisposed)
					{
						parent.BeginInvoke(reflow);
					}
				};
				reflow();
			}
			return track;
		}

		private Button ColorButton(Panel p, string value, string caption, int x, int y, Action<string> update)
		{
			Button button = Button(caption, delegate
			{
			});
			button.SetBounds(x, y, 128, 38);
			Action<string> paint = delegate(string hex)
			{
				((RaisedButton)button).ColourSwatch = true;
				button.BackColor = LightingColour.Vivid(ColorTranslator.FromHtml(hex));
				button.ForeColor = LightingColour.TextOn(button.BackColor);
				button.Text = ((button.Tag == null) ? caption : Convert.ToString(button.Tag));
				button.AccessibleName = button.Text + " " + hex;
			};
			paint(value);
			button.Click += delegate
			{
				using (ColorDialog colorDialog = new ColorDialog
				{
					FullOpen = true,
					Color = button.BackColor
				})
				{
					if (colorDialog.ShowDialog(this) == DialogResult.OK)
					{
						string obj = LightingColour.Hex(LightingColour.Vivid(colorDialog.Color));
						update(obj);
						paint(obj);
					}
				}
			};
			p.Controls.Add(button);
			return button;
		}

		private void Lighting()
		{
			SoftPanel softPanel = Card("A glow for your keyboard", "Preview your colours and effect below. Nothing changes until Apply.", 526);
			Label keyboardHint = softPanel.Controls.OfType<Label>().Last();
			TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
			tableLayoutPanel.Location = new Point(22, 96);
			tableLayoutPanel.Width = softPanel.Width - 44;
			tableLayoutPanel.Height = 112;
			tableLayoutPanel.ColumnCount = 2;
			tableLayoutPanel.RowCount = 1;
			tableLayoutPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			softPanel.Controls.Add(tableLayoutPanel2);
			EffectSection panel = new EffectSection();
			panel.Dock = DockStyle.Fill;
			panel.Margin = new Padding(0, 0, 8, 0);
			EffectSection panel2 = panel;
			EffectSection panel3 = new EffectSection();
			panel3.Dock = DockStyle.Fill;
			panel3.Margin = new Padding(8, 0, 0, 0);
            panel3.Accent = Color.FromArgb(111,0,255);
            panel.Accent = Color.FromArgb(0,113,232);
			EffectSection panel4 = panel3;
			tableLayoutPanel2.Controls.Add(panel2, 0, 0);
			tableLayoutPanel2.Controls.Add(panel4, 1, 0);
			RaisedButton officialTab = (RaisedButton)Button("Official · 7 effects", delegate
			{
				keyboard.Effect = kbEffect.Text;
				keyboardChoiceChanged();
                kbEffect.Focus();kbEffect.DroppedDown=true;
			});
			officialTab.Dock = DockStyle.Top;
			officialTab.Height = 34;officialTab.Dropdown=true;
			panel2.Controls.Add(officialTab);
			RaisedButton extraTab = (RaisedButton)Button("Extra · 10 animations", delegate
			{
				keyboard.Effect = kbExtraEffect.Text;
				keyboardChoiceChanged();
                kbExtraEffect.Focus();kbExtraEffect.DroppedDown=true;
			});
			extraTab.Dock = DockStyle.Top;
			extraTab.Height = 34;extraTab.Dropdown=true;
			panel4.Controls.Add(extraTab);
			kbEffect = Effect(panel2, new string[7] { "Static", "Breathing", "Wave", "Clock", "Rainbow", "Flow", "Four zones" }, keyboard.Effect, 48);
			kbEffect.Width = panel2.Width - 24;
			kbEffect.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			kbEffect.Left = 12;
			kbExtraEffect = Effect(panel4, ExtraEffects.Names, keyboard.Effect, 48);
			kbExtraEffect.Width = panel4.Width - 24;
			kbExtraEffect.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			kbExtraEffect.Left = 12;
			var officialHint=Label("Built-in controller effects",8f);officialHint.SetBounds(12,82,panel2.Width-24,22);officialHint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;panel2.Controls.Add(officialHint);
            var extraHint=Label("App animations · four zones",8f);extraHint.SetBounds(12,82,panel4.Width-24,22);extraHint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;panel4.Controls.Add(extraHint);
            zoneButtons = new Button[4];
			TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
			tableLayoutPanel3.Location = new Point(22, 221);
			tableLayoutPanel3.Width = softPanel.Width - 44;
			tableLayoutPanel3.Height = 42;
			tableLayoutPanel3.ColumnCount = 4;
			tableLayoutPanel3.RowCount = 1;
			tableLayoutPanel3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			TableLayoutPanel tableLayoutPanel4 = tableLayoutPanel3;
			for (int i = 0; i < 4; i++)
			{
				tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
			}
			softPanel.Controls.Add(tableLayoutPanel4);
			for (int i = 0; i < 4; i++)
			{
				int z = i;
				zoneButtons[i] = ColorButton(tableLayoutPanel4, keyboard.Colors[i], "Zone " + (i + 1), 0, 0, delegate(string hex)
				{
					keyboard.Colors[z] = hex;
				});
				tableLayoutPanel4.SetCellPosition(zoneButtons[i], new TableLayoutPanelCellPosition(i, 0));
				zoneButtons[i].Dock = DockStyle.Fill;
				zoneButtons[i].Margin = new Padding(0, 0, (i != 3) ? 8 : 0, 0);
			}
			kbBright = Slider(softPanel, keyboard.Brightness, 22, 273, 100, "Brightness");
			kbBright.ValueChanged += delegate
			{
				keyboard.Brightness = kbBright.Value;
			};
			Label zoneLabel = Label("Zone effect", 9f);
			zoneLabel.SetBounds(320, 273, 200, 24);
			softPanel.Controls.Add(zoneLabel);
			ComboBox zones = Effect(softPanel, new string[4] { "Static", "Breathing", "Clock", "Rainbow" }, keyboard.ZoneEffect, 297);
			zones.Left = 320;
			zones.SelectedIndexChanged += delegate
			{
				keyboard.ZoneEffect = zones.Text;
			};
			Panel speedPanel = new Panel
			{
				Location = new Point(310, 273),
				Width = 245,
				Height = 65
			};
			softPanel.Controls.Add(speedPanel);
			TrackBar animationSpeed = Slider(speedPanel, keyboard.Speed, 4, 0, 9, "Animation speed");
			animationSpeed.ValueChanged += delegate
			{
				keyboard.Speed = animationSpeed.Value;
			};
            var direction=Effect(softPanel,new[]{"Left → Right","Right → Left"},keyboard.Direction==1?"Right → Left":"Left → Right",347);
            direction.AccessibleName="Animation direction";direction.Left=320;
            direction.SelectedIndexChanged+=(s,e)=>{keyboard.Direction=direction.SelectedIndex;};
            Action layoutKeyboardSliders=()=>{
                if(softPanel.IsDisposed)return;float s=softPanel.DeviceDpi/96f;int left=softPanel.ClientSize.Width/2+(int)(12*s);
                kbBright.Width=Math.Max(90,softPanel.ClientSize.Width/2-(int)(34*s));
                speedPanel.Left=left;speedPanel.Width=Math.Max(90,softPanel.ClientSize.Width-left-(int)(22*s));
                animationSpeed.Width=speedPanel.ClientSize.Width;
                foreach(Label speedCaption in speedPanel.Controls.OfType<Label>())speedCaption.Width=Math.Max(60,speedPanel.ClientSize.Width-speedCaption.Left);
                direction.Left=left;direction.Width=speedPanel.Width;
                zoneLabel.Left=left;zoneLabel.Width=speedPanel.Width;zones.Left=left;zones.Width=Math.Min((int)(230*s),speedPanel.Width);
            };
            softPanel.SizeChanged+=(s,e)=>{if(softPanel.IsHandleCreated&&!softPanel.IsDisposed)softPanel.BeginInvoke(layoutKeyboardSliders);};layoutKeyboardSliders();
			CheckBox awake = new CheckBox
			{
				Text = "Keep keyboard awake",
				Checked = keyboard.KeepAwake,
				AutoSize = true,
				Location = new Point(22, 352)
			};
			awake.CheckedChanged += delegate
			{
				keyboard.KeepAwake = awake.Checked;
			};
			softPanel.Controls.Add(awake);
			Button button = Button("Apply keyboard", async delegate
			{
                if(busy)return;
                applyKeyboardButton.Enabled=false;
                try{await QueueKeyboard(Copy(keyboard,"keyboard"));}finally{if(!IsDisposed)applyKeyboardButton.Enabled=true;}
			});
            applyKeyboardButton=(RaisedButton)button;
            applyKeyboardButton.Accent=Color.FromArgb(244,117,20);
            applyKeyboardButton.Font=new Font("Segoe UI",11,FontStyle.Bold);
            var keyboardActions=new Panel{Location=new Point(22,392),Size=new Size(softPanel.Width-44,62),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
			softPanel.Controls.Add(keyboardActions);
			stopKeyboardButton = Button("Stop animation", async delegate
			{
                if(busy)return;
                bool stopped=keyboardTask!=null;
                try{Store.SaveFlag("resume-extra",false);}catch(Exception preferenceError){Store.Log(preferenceError.Message);}
				await StopKeyboardAnimation();
                if(stopped){((RaisedButton)stopKeyboardButton).Flash(lastKeyboardStopOk);shortcutToast.Notify(new FeedbackNotice{Kind=lastKeyboardStopOk?FeedbackKind.Message:FeedbackKind.Error,Title=lastKeyboardStopOk?"Animation paused":"Animation stopped with an error",Detail=lastKeyboardStopOk?"Last colour held. Your keyboard stays lit.":keyboardResult});}
			});
            ((RaisedButton)stopKeyboardButton).Accent=Color.FromArgb(226,79,70);
            stopKeyboardButton.Font=new Font("Segoe UI",11,FontStyle.Bold);
			stopKeyboardButton.Enabled = keyboardTask != null;
            keyboardActions.Controls.Add(button);keyboardActions.Controls.Add(stopKeyboardButton);
            Action arrangeActions=()=>{int gap=16,w=Math.Min(242,(keyboardActions.ClientSize.Width-gap)/2),left=(keyboardActions.ClientSize.Width-2*w-gap)/2;button.SetBounds(left,0,w,62);stopKeyboardButton.SetBounds(left+w+gap,0,w,62);};
            keyboardActions.Resize+=(s,e)=>arrangeActions();arrangeActions();
			kbOutcome = Label(keyboardResult, 9f);
			kbOutcome.SetBounds(22, 472, softPanel.Width - 44, 38);
			kbOutcome.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			softPanel.Controls.Add(kbOutcome);
			keyboardChoiceChanged = delegate
			{
				bool flag = ExtraEffects.Contains(keyboard.Effect);
				officialTab.Selected = !flag;
                panel2.Selected = !flag;panel4.Selected = flag;
                officialHint.Text="Click to choose · 7 effects";
                extraHint.Text="Click to choose · 10 animations";
                button.Text=flag?"Apply Extra effect":"Apply Official effect";
				extraTab.Selected = flag;
				speedPanel.Visible = flag;
                direction.Visible=flag;direction.Enabled=ExtraEffects.HasDirection(keyboard.Effect);
				ComboBox comboBox = zones;
				bool visible = (zoneLabel.Visible = !flag && keyboard.Effect == "Four zones");
				comboBox.Visible = visible;
				keyboardHint.Text = (flag ? (ExtraEffects.Description(keyboard.Effect) + " Four-zone animation · keep Essentials open.") : "Official controller effects · vivid colours. Preview first, then Apply.");
				for (int j = 0; j < 4; j++)
				{
					zoneButtons[j].Visible = !flag || j < 2;
					zoneButtons[j].Enabled = (flag ? (j < 2 && ExtraEffects.UsesColours(keyboard.Effect)) : (keyboard.Effect == "Four zones" || (j == 0 && keyboard.Effect != "Rainbow")));
					zoneButtons[j].Text = (flag ? ((j == 0) ? "Colour 1" : "Colour 2") : ("Zone " + (j + 1)));
					zoneButtons[j].Tag = zoneButtons[j].Text;
				}
			};
			kbEffect.SelectedIndexChanged += delegate
			{
				if (!syncingKeyboardChoice)
				{
					keyboard.Effect = kbEffect.Text;
					keyboardChoiceChanged();
				}
			};
			kbExtraEffect.SelectedIndexChanged += delegate
			{
				if (!syncingKeyboardChoice)
				{
					keyboard.Effect = kbExtraEffect.Text;
					keyboardChoiceChanged();
				}
			};
			keyboardChoiceChanged();
			AttachLightingPreview(softPanel, () => Copy(keyboard, "keyboard"), "keyboard");
			SoftPanel softPanel2 = Card("A soft glow at the back", "Device state is shown below. Preview a new look before switching it on.", 438);
			rearEffect = Effect(softPanel2, new string[6] { "Static", "Breathing", "Loop", "Cover", "Rhythm", "Beat" }, rear.Effect, 96);
			rearEffect.SelectedIndexChanged += delegate
			{
				rear.Effect = rearEffect.Text;
				RearEdited();
			};
			rearColor = ColorButton(softPanel2, rear.Colors[0], "Bar colour", 213, 93, delegate(string hex)
			{
				rear.Colors[0] = hex;
				RearEdited();
			});
			ColorButton(softPanel2, rear.Colors[1], "Colour 2", 355, 93, delegate(string hex)
			{
				rear.Colors[1] = hex;
				RearEdited();
			});
			rearBright = Slider(softPanel2, rear.Brightness, 22, 145, 100, "Brightness");
			rearBright.ValueChanged += delegate
			{
				rear.Brightness = rearBright.Value;
				RearEdited();
			};
			rearSpeed = Slider(softPanel2, rear.Speed, 310, 145, 9, "Breathing speed");
			rearSpeed.ValueChanged += delegate
			{
				rear.Speed = rearSpeed.Value;
				RearEdited();
			};
			rearSpeed.Enabled = rearEffect.Text == "Breathing";
			rearEffect.SelectedIndexChanged += delegate
			{
				rearSpeed.Enabled = rearEffect.Text == "Breathing";
			};
			Button button2 = Button("Apply rear bar", async delegate
			{
				rear.Effect = rearEffect.Text;
				rear.Brightness = rearBright.Value;
				rear.Speed = rearSpeed.Value;
				if (rear.Effect == "Rhythm" || rear.Effect == "Beat")
				{
					await StartMusic();
				}
				else
				{
					await Run(Copy(rear, "rear"), "rear");
				}
			});
			button2.SetBounds(22, 230, 174, 37);
			softPanel2.Controls.Add(button2);
			Button button3 = Button("Turn bar off", async delegate
			{
				await Run(new Request
				{
					Action = "rear-off"
				}, null);
			});
			button3.SetBounds(208, 230, 145, 37);
			softPanel2.Controls.Add(button3);
			rearDraftToggle = new CheckBox
			{
				Text = "Preview selected look · does not switch the light on",
				Checked = rearState.PreviewDraft,
				AutoSize = true,
				Location = new Point(22, 276)
			};
			rearDraftToggle.CheckedChanged += delegate
			{
				rearState.PreviewDraft = rearDraftToggle.Checked;
				UpdateRearIndicator();
			};
			softPanel2.Controls.Add(rearDraftToggle);
			rearOutcome = Label(rearState.Caption(rear), 10f, true);
			rearOutcome.SetBounds(22, 313, softPanel2.Width - 44, 48);
			rearOutcome.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			softPanel2.Controls.Add(rearOutcome);
			Button button4 = Button("Stop music", async delegate
			{
				await StopMusic();
				status.Text = rearResult;
			});
			button4.SetBounds(365, 230, 145, 37);
			softPanel2.Controls.Add(button4);
			Label label = Label("Grey = off. Preview mode shows a draft; Apply sends it to the device.\nSimulated colours and coverage may differ from the physical LEDs.", 9f);
			label.SetBounds(22, 373, softPanel2.Width - 44, 48);
			label.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			softPanel2.Controls.Add(label);
			rearIllustration = AttachLightingPreview(softPanel2, () => rearState.Illustration(rear), "rear");
			rearIllustration.Caption = () => rearState.Caption(rear);
			SoftPanel softPanel3 = Card("Keep it simple", "Successful lighting changes are remembered. Running Extra animations can resume after sign-in.\nSet brightness to zero to turn the keyboard off.", 111);
		}

		private static Request Copy(Request r, string action)
		{
			Request request = new Request();
			request.Action = action;
            request.Feedback = r.Feedback;request.FeedbackSequence=r.FeedbackSequence;
			request.Effect = r.Effect;
			request.Colors = (string[])r.Colors.Clone();
			request.Brightness = r.Brightness;
			request.Speed = r.Speed;
			request.Mode = r.Mode;
			request.KeepAwake = r.KeepAwake;
			request.ZoneEffect = r.ZoneEffect;
            request.Direction=r.Direction;
			return request;
		}

		private void SettingsPage()
		{
            var soundCard=Card("Little sounds","A soft two-note chime after a confirmed performance mode change.",145);
            var sounds=new CheckBox{Text="Play performance mode sounds",Checked=modeSounds,AutoSize=true,Location=new Point(22,99)};
            sounds.CheckedChanged+=(s,e)=>{modeSounds=sounds.Checked;try{Directory.CreateDirectory(Store.DirectoryPath);File.WriteAllText(Path.Combine(Store.DirectoryPath,"mode-sounds.txt"),modeSounds.ToString());}catch(Exception error){status.Text="Sound preference: "+error.Message;}};soundCard.Controls.Add(sounds);
            var screenCard=Card("Full-screen screenshots","Press PrtScn once. The shared Insert / PrtScn key works without Fn.",175);
            var screenInfo=Label("Saved as PNG in Pictures / GTBOOK Essentials Screenshots.\nAlso copied to the clipboard when it is available.",9);screenInfo.SetBounds(22,91,screenCard.Width-44,44);screenInfo.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;screenCard.Controls.Add(screenInfo);
            var openShots=Button("Open screenshots",OpenScreenshots);openShots.SetBounds(22,135,185,32);screenCard.Controls.Add(openShots);
			SoftPanel softPanel = Card("Cooling", "Let the firmware cool automatically, or use full fans when you need them.", 158);
			TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
			tableLayoutPanel.Location = new Point(22, 100);
			tableLayoutPanel.Size = new Size(softPanel.Width - 44, 45);
			tableLayoutPanel.ColumnCount = 2;
			tableLayoutPanel.RowCount = 1;
			tableLayoutPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
			for (int i = 0; i < 2; i++)
			{
				tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			}
			tableLayoutPanel2.Controls.Add(Button("Fan Max", async delegate
			{
				await Run(new Request
				{
					Action = "fan",
					Enabled = true
				}, null);
			}));
			tableLayoutPanel2.Controls.Add(Button("Auto fans", async delegate
			{
				await Run(new Request
				{
					Action = "auto"
				}, null);
			}));
			foreach (Control control in tableLayoutPanel2.Controls)
			{
				control.Dock = DockStyle.Fill;
				control.Margin = new Padding(0, 0, 6, 4);
			}
			softPanel.Controls.Add(tableLayoutPanel2);
            var charging=Card("USB charging while off","Save the laptop's firmware setting for USB power during sleep or shutdown.",226);
            usbOn=(RaisedButton)Button("Charging ON",async delegate{await Run(new Request{Action="usb",Enabled=true},null);});
            usbOff=(RaisedButton)Button("Charging OFF",async delegate{await Run(new Request{Action="usb",Enabled=false},null);});
            usbOn.SetBounds(22,96,180,42);usbOff.SetBounds(214,96,180,42);charging.Controls.Add(usbOn);charging.Controls.Add(usbOff);
            usbState=Label("Reading firmware setting…",10,true);usbState.SetBounds(22,146,charging.Width-44,27);usbState.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;charging.Controls.Add(usbState);
            var chargingHelp=Label("Use a charging-capable USB port. Power while shut down depends on firmware, the port and AC/battery state.",9);chargingHelp.SetBounds(22,180,charging.Width-44,40);chargingHelp.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;charging.Controls.Add(chargingHelp);
			SoftPanel softPanel2 = Card("GPU mode", "Choose dGPU, Hybrid or iGPU. Save your work and restart Windows after applying.", 232);
            gpuTouched=false;
            gpuSelection=Effect(softPanel2,GpuModes.Names,"",103);gpuSelection.SelectedIndex=-1;
            gpuSelection.SelectedIndexChanged+=(s,e)=>{if(!syncingGpu)gpuTouched=true;UpdateGpuUi();};
            gpuApply=Button("Save GPU mode",async delegate{if(gpuSelection.SelectedIndex<0)return;await Run(new Request{Action="gpu",Mode=gpuSelection.SelectedIndex},"gpu-pending");});
            gpuApply.SetBounds(213,99,190,40);gpuApply.Enabled=false;softPanel2.Controls.Add(gpuApply);
            page.Controls.SetChildIndex(softPanel2,0);
            gpuRestart=Button("Restart now",RestartWindows);gpuRestart.SetBounds(418,99,160,40);gpuRestart.Visible=false;softPanel2.Controls.Add(gpuRestart);
            gpuState=Label("Reading the saved GPU setting…",9.5f);gpuState.SetBounds(22,154,softPanel2.Width-44,61);gpuState.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;softPanel2.Controls.Add(gpuState);UpdateGpuUi();
            SoftPanel softPanel3 = Card("Make yourself at home", "Windows-key lock lasts while this app is running. Closing the app releases it.", 225);
			CheckBox lockKey = new CheckBox
			{
				Text = "Disable Windows key while gaming",
				Checked = keyLock.Enabled,
				AutoSize = true,
				Location = new Point(22, 98)
			};
			bool revertingLock = false;
			lockKey.CheckedChanged += delegate
			{
				if (!revertingLock)
				{
					if (ConfirmPreview(new Request
					{
						Action = "windows-key",
						Enabled = lockKey.Checked
					}))
					{
						try
						{
							keyLock.Set(lockKey.Checked);
							status.Text = (lockKey.Checked ? "Windows key locked. Uncheck here or exit GTBook to release it." : "Windows key restored.");
							return;
						}
						catch (Exception ex3)
						{
							status.Text = ex3.Message;
							return;
						}
					}
					revertingLock = true;
					lockKey.Checked = !lockKey.Checked;
					revertingLock = false;
				}
			};
			softPanel3.Controls.Add(lockKey);
			CheckBox checkBox = new CheckBox();
			checkBox.Text = "Start GTBook with Windows";
			checkBox.Checked = false;
			checkBox.Enabled = false;
			checkBox.AutoSize = true;
			checkBox.Location = new Point(22, 138);
			CheckBox checkBox2 = checkBox;
			softPanel3.Controls.Add(checkBox2);
			LoadStartupCheckbox(checkBox2);
			Button button2 = Button("Hide to tray", delegate
			{
				Hide();
			});
			button2.SetBounds(22, 176, 160, 34);
			softPanel3.Controls.Add(button2);
			Button button3 = Button("Reset lighting choices", delegate
			{
				keyboard.Effect = "Static";
				rear.Effect = "Static";
				keyboard.Brightness = (rear.Brightness = 65);
				for (int j = 0; j < 4; j++)
				{
					keyboard.Colors[j] = (rear.Colors[j] = "#7600FF");
				}
				RearEdited();
				ClearCachedPage("Lighting");
				ShowPage("Lighting");
				status.Text = "Default choices selected. Press Apply to send them to the device.";
			});
			page.Controls.Add(button3);
			button3.Width = 220;
			SoftPanel softPanel4 = Card("Language", "Original OEM translations are used for control labels. Help text stays in English.", 145);
			ComboBox choice = Effect(softPanel4, Languages.Names, Languages.Current, 99);
			choice.SelectedIndexChanged += delegate
			{
				try
				{
					Languages.Select(choice.Text);
					ClearCachedPages();
					ShowPage("Settings");
					status.Text = "Language preference saved.";
				}
				catch (Exception ex2)
				{
					status.Text = ex2.Message;
				}
			};
			SoftPanel softPanel5 = Card("Laptop shortcut keys", "Same F1 / F2 / F3 mapping as the previous Toolkit. Works from the system tray too.", 245);
			CheckBox enable = new CheckBox
			{
				Text = "Use Toolkit F1 / F2 / F3 controls",
				Checked = fnKeys,
				AutoSize = true,
				Location = new Point(22, 95)
			};
			bool revertingKeys = false;
			enable.CheckedChanged += delegate
			{
				if (!revertingKeys)
				{
					bool @checked = enable.Checked;
					if (ConfirmPreview(new Request
					{
						Action = "software-fn",
						Enabled = @checked
					}))
					{
						fnKeys = @checked;
						if (laptopKeys != null)
						{
							laptopKeys.MediaKeys = @checked;
						}
						try
						{
							Directory.CreateDirectory(Store.DirectoryPath);
							File.WriteAllText(Path.Combine(Store.DirectoryPath, "fn-keys.txt"), @checked.ToString());
							status.Text = "Laptop shortcut preference saved.";
							return;
						}
						catch (Exception ex)
						{
							status.Text = ex.Message;
							return;
						}
					}
					revertingKeys = true;
					enable.Checked = !@checked;
					revertingKeys = false;
				}
			};
			softPanel5.Controls.Add(enable);
			Label label = Label("F1  Screen brightness −10%     F2  Screen brightness +10%\nF3 tap  Keyboard brightness     F3 hold 0.8 seconds  Next effect once\nRelease F3 before holding again. Shift / Ctrl / Alt keeps normal F1–F3.\nNative volume, mute and other media keys remain handled by Windows.", 9f);
			label.SetBounds(22, 132, softPanel5.Width - 44, 96);
			label.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			softPanel5.Controls.Add(label);
		}
        private void OpenScreenshots(){try{Directory.CreateDirectory(Screenshots.Folder);Process.Start(new ProcessStartInfo(Screenshots.Folder){UseShellExecute=true});}catch(Exception e){status.Text=e.Message;}}
        private async Task TakeScreenshot(){
            if(preview||takingScreenshot||IsDisposed)return;takingScreenshot=true;
            try{
                shortcutToast.Hide();await Task.Delay(65);
                using(var capture=await Task.Run(()=>Screenshots.Capture())){
                    bool copied=Screenshots.CopyToClipboard(capture.Image);
                    if(!IsDisposed){status.Text="Screenshot saved: "+capture.Path+(copied?" · copied to clipboard":"");shortcutToast.Notify("Screenshot saved");}
                }
            }catch(Exception e){if(!IsDisposed){status.Text="Screenshot failed: "+e.Message;shortcutToast.Notify("Screenshot could not be saved");}Store.Log("Screenshot: "+e.Message);}
            finally{takingScreenshot=false;if(closeWhenDone&&!IsDisposed)Close();}
        }

		private async void LoadStartupCheckbox(CheckBox box)
		{
			try
			{
				int num = ((!preview && await Task.Run(() => Startup.Enabled)) ? 1 : 0);
				bool enabled = (byte)num != 0;
				if (box.IsDisposed)
				{
					return;
				}
				box.Checked = enabled;
				box.Enabled = true;
				bool reverting = false;
				box.CheckedChanged += async delegate
				{
					if (!reverting)
					{
						bool desired = box.Checked;
						if (ConfirmPreview(new Request
						{
							Action = "startup",
							Enabled = desired
						}))
						{
							box.Enabled = false;
							try
							{
								await Task.Run(delegate
								{
									Startup.Set(desired);
								});
								status.Text = "Startup preference saved.";
								return;
							}
							catch (Exception ex2)
							{
								status.Text = ex2.Message;
								if (!box.IsDisposed)
								{
									reverting = true;
									box.Checked = !desired;
									reverting = false;
								}
								return;
							}
							finally
							{
								if (!box.IsDisposed)
								{
									box.Enabled = true;
								}
							}
						}
						reverting = true;
						box.Checked = !desired;
						reverting = false;
					}
				};
			}
			catch (Exception ex)
			{
				if (!box.IsDisposed)
				{
					box.Enabled = true;
					status.Text = ex.Message;
				}
			}
		}

		private async Task StartMusic()
		{
			if (preview)
			{
				status.Text = "Layout preview does not access hardware.";
				return;
			}
			if (!ConfirmPreview(Copy(rear, "rear-music")))
			{
				status.Text = "Kept current rear lighting.";
				return;
			}
			await operations.WaitAsync();
			if (base.IsDisposed)
			{
				operations.Release();
				return;
			}
			busy = true;
			try
			{
				await StopMusic();
				musicStop = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\GTBookMusicStop");
				musicStop.Reset();
				Request request = Copy(rear, "rear-music");
				musicSelection = request;
				rearState.Starting();
				UpdateRearIndicator();
				EventWaitHandle stopSignal = musicStop;
				musicTask = Task.Factory.StartNew(() => WorkerClient.Run(request, int.MaxValue, stopSignal),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
				Task<Reply> pending = musicTask;
				status.Text = "Starting music sync… If no light reacts, check playback and the status below.";
				await Task.WhenAny(pending, Task.Delay(1500));
				if (pending.IsCompleted)
				{
					await StopMusic();
					status.Text = rearResult;
					return;
				}
				rearResult = "Music worker active · " + rear.Effect + " · check the bar visually";
				status.Text = rearResult;
				if (rearOutcome != null && !rearOutcome.IsDisposed)
				{
					rearOutcome.Text = rearResult;
				}
				WatchMusic(pending);
			}
			finally
			{
				busy = false;
				operations.Release();
				if (closeWhenDone)
				{
					Close();
				}
			}
		}

		private async Task StartKeyboardAnimation(Request request, bool fromShortcut,bool silent=false)
		{
			if (preview)
			{
				status.Text = "Preview only · no keyboard commands sent.";
				return;
			}
			request = Copy(request, "keyboard-animation");
			if (!fromShortcut && !ConfirmPreview(request))
			{
				status.Text = "Kept current keyboard lighting.";
				return;
			}
			await operations.WaitAsync();
			if (base.IsDisposed)
			{
				operations.Release();
				return;
			}
			busy = true;
			try
			{
				string failure = null;
				try
				{
					using(var owner=Process.GetCurrentProcess()){request.OwnerProcessId=owner.Id;request.OwnerStartedTicks=owner.StartTime.ToUniversalTime().Ticks;}
                    request.ArmKeyboardAwake=request.KeepAwake&&!keyboardAwakeConfigured;
                    if(keyboardTask!=null&&!keyboardTask.IsCompleted&&keyboardChannel!=null){
                        var channel=keyboardChannel;request.KeyboardSession=channel.Id;
                        var updateTime=Stopwatch.StartNew();
                        var updated=await Task.Run(()=>channel.Update(request));
                        if(!updated.Ok)throw new IOException(updated.Message);
                        await Task.Run(()=>Store.Log("Keyboard live update | "+request.Effect+" | "+request.Brightness+"% | "+updateTime.ElapsedMilliseconds+" ms"));
                        await RecordKeyboardAnimation(request,silent);return;
                    }
					await StopKeyboardAnimation();
					using (EventWaitHandle ready = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\GTBookKeyboardReady"))
					{
						ready.Reset();
						keyboardStop = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\GTBookKeyboardStop");
						keyboardStop.Reset();
                        keyboardChannel=new KeyboardChannel();request.KeyboardSession=keyboardChannel.Id;
						using (Process process = Process.GetCurrentProcess())
						{
							request.OwnerProcessId = process.Id;
							request.OwnerStartedTicks = process.StartTime.ToUniversalTime().Ticks;
						}
						EventWaitHandle signal = keyboardStop;
						keyboardTask = Task.Factory.StartNew(() => WorkerClient.Run(request, int.MaxValue, signal),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
						Task<Reply> pending = keyboardTask;
						status.Text = "Starting Extra · " + request.Effect + "…";
						Stopwatch timer = Stopwatch.StartNew();
						while (!pending.IsCompleted && !ready.WaitOne(0) && timer.ElapsedMilliseconds < 8000)
						{
							await Task.Delay(40);
						}
						if (pending.IsCompleted)
						{
							Reply result = await pending;
							await StopKeyboardAnimation();
							status.Text = result.Message;
							if (result.Ok)
							{
								Store.Save("keyboard", request);
                                Store.SaveFlag("resume-extra",false);
							}
                            if(!silent)NotifyApplied(request,result);
						}
						else if (ready.WaitOne(0))
						{
							keyboardResult = request.Brightness==0?"Keyboard lights off":"Streaming · " + request.Effect + " · four zones";
							status.Text = keyboardResult;
							if (kbOutcome != null && !kbOutcome.IsDisposed)
							{
								kbOutcome.Text = keyboardResult;
							}
							if (stopKeyboardButton != null && !stopKeyboardButton.IsDisposed)
							{
								stopKeyboardButton.Enabled = true;
							}
							try
							{
								await Task.Run(()=>{Store.Save("keyboard",request);Store.SaveFlag("resume-extra",request.Brightness>0);});
							}
							catch (Exception ex)
							{
								Label label = status;
								label.Text = label.Text + " · preferences not saved: " + ex.Message;
							}
							WatchKeyboardAnimation(pending);
                            if(!silent)NotifyApplied(request,new Reply{Ok=true});
                            if(request.KeepAwake)keyboardAwakeConfigured=true;
						}
						else
						{
							await StopKeyboardAnimation();
							status.Text = "Extra animation did not become ready. Retry after checking the connection.";
                            if(!silent)NotifyApplied(request,Reply.Fail(status.Text));
						}
					}
				}
				catch (Exception ex)
				{
					failure = ex.Message;
				}
				if (failure != null)
				{
					await StopKeyboardAnimation();
                    keyboardAwakeConfigured=false;
					status.Text = "Keyboard animation: " + failure;
                    if(!silent)NotifyApplied(request,Reply.Fail(status.Text));
				}
			}
			finally
			{
				busy = false;
				operations.Release();
				if (closeWhenDone)
				{
					Close();
				}
			}
		}

        private async Task RecordKeyboardAnimation(Request request,bool silent=false){
            if(request.KeepAwake)keyboardAwakeConfigured=true;
            keyboardResult=request.Brightness==0?"Keyboard lights off":"Streaming · "+request.Effect+" · four zones";
            status.Text=keyboardResult;
            if(kbOutcome!=null&&!kbOutcome.IsDisposed)kbOutcome.Text=keyboardResult;
            if(stopKeyboardButton!=null&&!stopKeyboardButton.IsDisposed)stopKeyboardButton.Enabled=true;
            if(!silent)NotifyApplied(request,new Reply{Ok=true});
            try{await Task.Run(()=>{Store.Save("keyboard",request);Store.SaveFlag("resume-extra",request.Brightness>0);});}catch(Exception error){status.Text+=" · preferences not saved: "+error.Message;}
        }
		private async void WatchKeyboardAnimation(Task<Reply> pending)
		{
			try
			{
				await pending;
				if (!base.IsDisposed && keyboardTask == pending)
				{
					await StopKeyboardAnimation();
					status.Text = keyboardResult;
				}
			}
			catch (Exception ex)
			{
				if (!base.IsDisposed)
				{
					status.Text = ex.Message;
				}
			}
		}

		private async Task StopKeyboardAnimation()
		{
			if (keyboardTask == null)
			{
				if (keyboardCleanup != null)
				{
					await keyboardCleanup;
				}
                if(keyboardChannel!=null){keyboardChannel.Dispose();keyboardChannel=null;}
                if(keyboardStop!=null){keyboardStop.Dispose();keyboardStop=null;}
				return;
			}
			Task<Reply> pending = keyboardTask;
			EventWaitHandle signal = keyboardStop;
            var channel=keyboardChannel;keyboardChannel=null;
			keyboardTask = null;
			keyboardStop = null;
			signal.Set();
			keyboardCleanup = FinishKeyboardAnimation(pending, signal,channel);
			await keyboardCleanup;
		}

		private async Task FinishKeyboardAnimation(Task<Reply> pending, EventWaitHandle signal,KeyboardChannel channel)
		{
			try
			{
				Reply result = await pending;
				keyboardResult = result.Message;
                lastKeyboardStopOk=result.Ok;
				Store.Log("Extra keyboard | " + result.Message);
			}
			catch (Exception ex)
			{
				keyboardResult = "Animation stopped: " + ex.Message;
                lastKeyboardStopOk=false;
			}
			finally
			{
				signal.Dispose();
                if(channel!=null)channel.Dispose();
			}
			if (kbOutcome != null && !kbOutcome.IsDisposed)
			{
				kbOutcome.Text = keyboardResult;
			}
			if (stopKeyboardButton != null && !stopKeyboardButton.IsDisposed)
			{
				stopKeyboardButton.Enabled = false;
			}
		}

		private async void WatchMusic(Task<Reply> pending)
		{
			try
			{
				await pending;
				if (!base.IsDisposed && musicTask == pending)
				{
					await StopMusic();
					status.Text = rearResult;
				}
			}
			catch (Exception ex)
			{
				if (!base.IsDisposed)
				{
					status.Text = ex.Message;
				}
			}
		}

		private async Task StopMusic()
		{
			if (musicTask == null)
			{
				if (musicCleanup != null)
				{
					await musicCleanup;
				}
				return;
			}
			Task<Reply> pending = musicTask;
			EventWaitHandle signal = musicStop;
			musicTask = null;
			musicStop = null;
			signal.Set();
			musicCleanup = FinishMusic(pending, signal);
			await musicCleanup;
		}

		private async Task FinishMusic(Task<Reply> pending, EventWaitHandle signal)
		{
			try
			{
				Reply result = await pending;
				rearResult = result.Message;
				if (result.Ok)
				{
					Request request = Copy(musicSelection ?? rear, "rear");
					request.Effect = "Static";
					rearState.Applied(request);
				}
				else
				{
					rearState.Failed();
				}
				UpdateRearIndicator();
				Store.Log("music | " + result.Message);
			}
			catch (Exception ex)
			{
				rearResult = "Music stopped: " + ex.Message;
				rearState.Failed();
				UpdateRearIndicator();
			}
			finally
			{
				signal.Dispose();
			}
		}

		private void PowerChanged(object sender, PowerModeChangedEventArgs e)
		{
			if (preview || base.IsDisposed || !base.IsHandleCreated)
			{
				return;
			}
			try
			{
				BeginInvoke((Action)async delegate
				{
					try
					{
						UpdateBattery();UpdateUiPolling();
						if (e.Mode == PowerModes.Suspend)
						{
                            suspended=true;keyboardAwakeConfigured=false;
                            if(laptopKeys!=null)laptopKeys.ResetGestures();
							await StopKeyboardAnimation();
							await StopMusic();
						}
						else if (e.Mode == PowerModes.Resume)
						{
                            suspended=false;
                            await InitializeHardware();
						}
					}
					catch (Exception ex2)
					{
						Store.Log(ex2.Message);
					}
				});
			}
			catch (InvalidOperationException)
			{
			}
		}

		private void Diagnostics()
		{
			SoftPanel softPanel = Card("Your GTBook, in detail", "Device, Windows, processor, memory, graphics, display, storage and battery.", 535);
			details.SetBounds(22, 93, softPanel.Width - 44, 416);
			details.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			details.Multiline = true;
			details.ReadOnly = true;
			details.ScrollBars = ScrollBars.Vertical;
			details.BorderStyle = BorderStyle.None;
			details.BackColor = Color.White;
			details.ForeColor = ink;
			details.Font = new Font("Segoe UI", 10f);
            details.Text=preview?DeviceInformation.Example():deviceReport.Length>0?deviceReport:"Loading device information…";
			softPanel.Controls.Add(details);
			FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
			flowLayoutPanel.Height = 48;
			flowLayoutPanel.Width = 640;
			flowLayoutPanel.WrapContents = false;
			FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
			flowLayoutPanel2.Controls.Add(Button("Refresh info", async delegate
			{
                await LoadDeviceInfo(true);
			}));
            var originalControls=Button("Original controls",OpenOriginal);originalControls.Enabled=File.Exists(Path.Combine(Hardware.OemDirectory,"ControlCenter.exe"));flowLayoutPanel2.Controls.Add(originalControls);
			flowLayoutPanel2.Controls.Add(Button("Save diagnostics", SaveDiagnostics));
			page.Controls.Add(flowLayoutPanel2);
			var featureCard=Card("Original features, fewer distractions", "Power modes, Fan Max, USB charging, GPU modes, Windows-key lock and native lighting.\nMusic uses your default playback device. Charge-limit controls are not in this GTBook OEM UI.", 138);
            featureCard.Controls.OfType<Label>().Last().Height=68;
			Card("Driver & startup", "The installer enables administrator-level tray startup at sign-in. Uses Windows ACPI/WMI and the laptop's firmware interface.\nClose other RGB/control utilities before using hardware controls.", 133);
		}

        private string ConnectionReport(){return string.Join("\r\n",snapshot.Select(kv=>kv.Key+": "+(string.IsNullOrEmpty(kv.Value)?"None":kv.Value)));}
        private async Task LoadDeviceInfo(bool force=false){
            if(preview||loadingDeviceInfo||IsDisposed)return;
            if(!force&&deviceReport.Length>0&&DateTime.UtcNow-deviceInfoUpdated<TimeSpan.FromMinutes(5))return;
            loadingDeviceInfo=true;
            if(deviceReport.Length==0)details.Text="Loading device information…";
            try{
                var reply=await Task.Run(()=>WorkerClient.Run(new Request{Action="device-info"},20000));
                if(IsDisposed)return;
                string report;
                if(reply.Ok&&reply.Data.TryGetValue("device-report",out report)){
                    deviceReport=report;deviceInfoUpdated=DateTime.UtcNow;details.Text=report;
                }else{
                    details.Text=(deviceReport.Length>0?deviceReport+"\r\n\r\n":"")+"Could not refresh device information. "+reply.Message;
                }
            }catch(Exception error){if(!IsDisposed)details.Text="Could not load device information. "+error.Message;}
            finally{loadingDeviceInfo=false;}
        }
		private void OpenOriginal()
		{
			string text = Path.Combine(Hardware.OemDirectory, "ControlCenter.exe");
			try
			{
				if (!File.Exists(text))
				{
					throw new FileNotFoundException("Original ControlCenter.exe was not found.");
				}
				ProcessStartInfo processStartInfo = new ProcessStartInfo(text);
				processStartInfo.WorkingDirectory = Hardware.OemDirectory;
				processStartInfo.UseShellExecute = true;
				Process.Start(processStartInfo);
				snapshot["conflict"] = "Original ControlCenter was launched. Close it before using toolkit hardware controls.";
				UpdateValues();
				status.Text = snapshot["conflict"];
			}
			catch (Exception ex)
			{
				status.Text = ex.Message;
			}
		}

		private void SaveDiagnostics()
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = "Text file|*.txt";
			saveFileDialog.FileName = "GTBook-diagnostics.txt";
			using (SaveFileDialog saveFileDialog2 = saveFileDialog)
			{
				if (saveFileDialog2.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}
				try
				{
					File.WriteAllText(saveFileDialog2.FileName, "GTBOOK Essentials "+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version+"\r\n" + DateTime.Now.ToString("s") + "\r\n\r\n" + details.Text + "\r\n\r\n" + ConnectionReport()+"\r\n\r\n"+status.Text);
					status.Text = "Diagnostics saved locally.";
				}
				catch (Exception ex)
				{
					status.Text = ex.Message;
				}
			}
		}

		private void UpdateBattery()
		{
			PowerStatus powerStatus = SystemInformation.PowerStatus;
			batteryValue.Text = ((powerStatus.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery || powerStatus.BatteryLifePercent < 0f) ? "Unavailable" : (Math.Round(powerStatus.BatteryLifePercent * 100f) + "%" + ((powerStatus.PowerLineStatus == PowerLineStatus.Online) ? " · AC" : "")));
		}

		private void UpdateValues()
		{
			modeValue.Text = (snapshot.ContainsKey("mode") ? snapshot["mode"] : "Not read");
			cpuValue.Text = (snapshot.ContainsKey("cpu") ? snapshot["cpu"] : "Not read");
			coolingValue.Text = (snapshot.ContainsKey("cooling") ? snapshot["cooling"] : "Not read");
			foreach (Control control in page.Controls)
			{
				MarkMode(control, modeValue.Text);
			}
			extra.Text = string.Join("   ·   ", from x in new string[3] { "gpu", "cpu-fan", "gpu-fan" }.Where(snapshot.ContainsKey)
				select ((x == "gpu") ? "GPU" : ((x == "cpu-fan") ? "CPU fan" : "GPU fan")) + ": " + (snapshot[x].StartsWith("Unavailable") ? "Unavailable" : snapshot[x]));
			Label label = extra;
			label.Text = label.Text + "\r\n" + SystemStats.Summary();
			UpdateRearIndicator();
            UpdateGpuUi();
            UpdateUsbUi();
            UpdateReadingsStamp();
		}

        private void UpdateUsbUi(){
            if(usbState==null||usbState.IsDisposed)return;string value;
            bool known=snapshot.TryGetValue("usb",out value)&&(value=="On"||value=="Off");
            usbState.Text=known?"Firmware setting: "+value.ToUpperInvariant()+" · read back from device":"Firmware setting not confirmed · Refresh readings to retry";
            usbState.ForeColor=known?Theme.OrangeInk:Theme.Muted;usbOn.Selected=known&&value=="On";usbOff.Selected=known&&value=="Off";
        }
		private void MarkMode(Control parent, string current)
		{
			foreach (Control control in parent.Controls)
			{
				RaisedButton raisedButton = control as RaisedButton;
				if (raisedButton != null && new string[3] { "Quiet", "Balanced", "Gaming" }.Contains(Convert.ToString(raisedButton.Tag ?? raisedButton.Text)))
				{
					raisedButton.Selected = Convert.ToString(raisedButton.Tag ?? raisedButton.Text) == current;
                    raisedButton.Accent=raisedButton.Selected?Color.FromArgb(244,117,20):Color.Empty;
				}
				MarkMode(control, current);
			}
		}

        private void NotifyApplied(Request request,Reply reply)
        {
            if(request.FeedbackSequence!=0&&request.FeedbackSequence!=keyboardFeedbackSequence)return;
            if(IsDisposed)return;
            var notice=FeedbackNotice.Applied(request,reply);
            if(notice.Kind==FeedbackKind.Mode){
                bool changed=confirmedMode!=notice.Mode;confirmedMode=notice.Mode;
                if(modeObserver!=null)modeObserver.Confirm(notice.Mode);
                if(changed&&modeSounds&&!preview){if(modeChime==null)modeChime=new ModeChime();modeChime.Play(notice.Mode);}
                if(!changed)notice.Detail="Already active. You're all set.";
            }
            if((request.Action=="keyboard"||request.Action=="keyboard-animation")&&applyKeyboardButton!=null&&!applyKeyboardButton.IsDisposed)applyKeyboardButton.Flash(notice.Kind!=FeedbackKind.Error);
            shortcutToast.Notify(notice);
        }

        private void TrackPerformanceMode()
        {
            if(preview||IsDisposed||!snapshot.ContainsKey("mode"))return;
            int mode=PerformanceModes.Index(snapshot["mode"]);if(mode<0)return;
            if(confirmedMode<0)confirmedMode=mode;
            else if(confirmedMode!=mode){var reply=new Reply{Ok=true};reply.Data["mode"]=PerformanceModes.Names[mode];NotifyApplied(new Request{Action="mode",Mode=mode},reply);}
            if(modeObserver!=null){modeObserver.Confirm(mode);return;}
            if(!snapshot.ContainsKey("supported")||snapshot["supported"]!="True"||snapshot.ContainsKey("conflict")&&snapshot["conflict"].Length>0)return;
            modeObserver=new ModeObserver(mode,()=>!busy&&!suspended,observed=>{
                try{if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)(()=>{
                    if(IsDisposed||modeObserver==null||!modeObserver.IsCurrent(observed)||confirmedMode==observed)return;
                    snapshot["mode"]=PerformanceModes.Names[observed];UpdateValues();
                    var reply=new Reply{Ok=true};reply.Data["mode"]=PerformanceModes.Names[observed];NotifyApplied(new Request{Action="mode",Mode=observed},reply);
                }));}catch(InvalidOperationException){}
            });
        }

        private async Task RefreshReadings(){
            if(preview||IsDisposed||refreshingReadings||busy||keyboardPending>0)return;
            refreshingReadings=true;
            int revision=settingsRevision;
            try{
                var reply=await Task.Run(()=>WorkerClient.Run(new Request{Action="refresh"}));
                if(IsDisposed||busy||revision!=settingsRevision)return;
                hardwareReady=reply.Ok&&reply.Data.ContainsKey("mode");readingsFailed=!reply.Ok;
                if(reply.Ok){
                    foreach(var entry in reply.Data)snapshot[entry.Key]=entry.Value;
                    readingsUpdatedUtc=DateTime.UtcNow;
                    if(reply.Data.ContainsKey("rear-power"))rearState.ObservePower(reply.Data["rear-power"]=="On");
                    else if(reply.Data.ContainsKey("rear-power-read-error"))rearState.Failed();
                    UpdateValues();TrackPerformanceMode();
                }else UpdateReadingsStamp();
            }finally{refreshingReadings=false;}
        }
		private async Task Run(Request r, string saveName, bool background = false, bool fromShortcut = false)
		{
            if(background&&r.Action=="refresh"){await RefreshReadings();return;}
            if(!background){settingsRevision++;if(r.Action=="mode"||r.Action=="screen-brightness"||r.Action=="auto"||r.Action=="fan"||r.Action=="usb")shortcutToast.Notify(FeedbackNotice.Pending(r));}
			if (preview)
			{
				status.Text = "Layout preview does not access hardware.";
				return;
			}
			r = new JavaScriptSerializer().Deserialize<Request>(new JavaScriptSerializer().Serialize(r));
			if ((r.Action == "keyboard" || r.Action == "keyboard-animation") && ExtraEffects.Contains(r.Effect))
			{
				await StartKeyboardAnimation(r, fromShortcut);
				return;
			}
			if (!background && !fromShortcut && r.Action != "refresh" && r.Action != "probe" && !ConfirmPreview(r))
			{
				status.Text = "Kept current settings.";
				return;
			}
			if (!background)
			{
				await operations.WaitAsync();
			}
			else if (!operations.Wait(0))
			{
				return;
			}
			if (base.IsDisposed)
			{
				operations.Release();
				return;
			}
			busy = true;
			if (!background&&r.Action!="screen-brightness")
			{
				page.Enabled = false;
				base.UseWaitCursor = true;
				status.ForeColor = muted;
				status.Text = "Updating your GTBook…";
			}
			try
			{
				if (r.Action == "keyboard")
				{
					await StopKeyboardAnimation();
                    r.ArmKeyboardAwake=r.KeepAwake&&!keyboardAwakeConfigured;
				}
				if (r.Action != "screen-brightness"&&r.Action!="refresh"&&r.Action!="probe")
				{
					await StopMusic();
				}
				Reply reply = await Task.Run(() => WorkerClient.Run(r));
                if(r.Action=="keyboard"&&reply.Ok&&r.KeepAwake)keyboardAwakeConfigured=true;
                if(r.Action=="refresh"){
                    hardwareReady=reply.Ok&&reply.Data.ContainsKey("mode");
                    if(reply.Ok){readingsUpdatedUtc=DateTime.UtcNow;if(readingsFailed||status.Text.StartsWith("Getting to know")){status.Text="Readings updated · "+DateTime.Now.ToString("HH:mm:ss");status.ForeColor=ink;}readingsFailed=false;}else readingsFailed=true;
                }
				foreach (KeyValuePair<string, string> datum in reply.Data)
				{
					snapshot[datum.Key] = datum.Value;
				}
				if (reply.Data.ContainsKey("rear-power"))
				{
					rearState.ObservePower(reply.Data["rear-power"] == "On");
				}
				else if (reply.Data.ContainsKey("rear-power-read-error"))
				{
					rearState.Failed();
				}
				if (!reply.Ok)
				{
                    if(r.Action=="usb")snapshot.Remove("usb");
                    if(r.Action=="keyboard")keyboardAwakeConfigured=false;
					if (r.Action == "mode")
					{
						snapshot.Remove("mode");
					}
					if (r.Action == "auto" || r.Action == "fan")
					{
						snapshot.Remove("cooling");
					}
				}
				if (!background || !reply.Ok)
				{
					status.Text = reply.Message;
					status.ForeColor = (reply.Ok ? ink : Color.FromArgb(153, 63, 85));
				}
                if(!background&&(fromShortcut||r.Action=="mode"||r.Action=="keyboard"))NotifyApplied(r,reply);
				if (reply.Ok && r.Action == "probe" && snapshot.ContainsKey("conflict") && snapshot["conflict"].Length > 0)
				{
					status.Text = snapshot["conflict"];
				}
				if (reply.Ok && r.Action == "probe" && snapshot["supported"] != "True")
				{
					status.Text = "Device detected. Native controls are available for GL613 BIOS 1.00 only. See Device & help.";
				}
				if (saveName != null && reply.Ok)
				{
					try
					{
						await Task.Run(()=>{Store.Save(saveName,r);if(r.Action=="keyboard")Store.SaveFlag("resume-extra",false);});
					}
					catch (Exception ex)
					{
						Label label = status;
						label.Text = label.Text + " Preferences could not be saved: " + ex.Message;
					}
				}
				if (r.Action == "keyboard")
				{
					keyboardResult = (reply.Ok ? ("Command sent · " + DateTime.Now.ToString("HH:mm:ss")) : "Not confirmed · see status below");
					if (kbOutcome != null && !kbOutcome.IsDisposed)
					{
						kbOutcome.Text = keyboardResult;
					}
				}
				if (r.Action.StartsWith("rear"))
				{
					rearResult = (reply.Ok ? reply.Message : "Not confirmed · see status below");
					if (reply.Ok)
					{
						rearState.Applied(r);
					}
					else
					{
						rearState.Failed();
					}
					UpdateRearIndicator();
				}
				if (!background || !reply.Ok)
				{
					Store.Log(r.Action + " | " + (reply.Ok ? "OK" : "FAILED") + " | " + reply.Message);
				}
				UpdateValues();
                if(reply.Ok&&reply.Data.ContainsKey("mode"))TrackPerformanceMode();
                if(reply.Ok&&r.Action=="gpu"){
                    GpuPending.Save(bootGpuRaw,r.Mode+1);UpdateGpuUi();
                    using(var prompt=new ApplyPreview(r,"GPU request saved",true))if(prompt.ShowDialog(this)==DialogResult.OK)RestartWindows();
                }
			}
			catch (Exception ex)
			{
				status.Text = ex.Message;
				Store.Log(ex.ToString());
                if(r.Action=="usb"){snapshot.Remove("usb");UpdateUsbUi();}
                if(r.Action=="keyboard")keyboardAwakeConfigured=false;
                if(!background&&(fromShortcut||r.Action=="mode"||r.Action=="keyboard"))NotifyApplied(r,Reply.Fail(ex.Message));
			}
			finally
			{
				busy = false;
				page.Enabled = true;
				base.UseWaitCursor = false;
				operations.Release();
				if (closeWhenDone)
				{
					Close();
				}
			}
		}
	}
}
