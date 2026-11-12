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
		private Color ink {get{return Theme.Ink;}}

		private Color muted {get{return Theme.Muted;}}
        private ThemeMotion themeMotion;
        private Task themePreferenceSave=Task.FromResult(0);

		private readonly Panel body = new GradientSurface();

		private readonly Panel sidebar = new GradientSurface{Sidebar=true};
        private readonly Label pageEyebrow=new Label();

		private readonly Label status = new Label();

		private readonly Label heading = new Label();

		private readonly Label subtitle = new Label();

		private readonly Label modeValue = new Label();

		private readonly Label cpuValue = new Label();

		private readonly Label batteryValue = new Label();

		private readonly Label coolingValue = new Label();

		private readonly MetricTile modeTile = new MetricTile { Title = "POWER MODE" };
		private readonly MetricTile cpuTile = new MetricTile { Title = "CPU TEMPERATURE" };
		private readonly MetricTile batteryTile = new MetricTile { Title = "BATTERY" };
		private readonly MetricTile gpuTile = new MetricTile { Title = "GPU TEMPERATURE", Compact = true };
		private readonly MetricTile cpuFanTile = new MetricTile { Title = "CPU FAN", Compact = true };
		private readonly MetricTile gpuFanTile = new MetricTile { Title = "GPU FAN", Compact = true };
		private readonly MetricTile memoryTile = new MetricTile { Title = "MEMORY", Compact = true };
		private readonly MetricTile diskTile = new MetricTile { Title = "SYSTEM DISK", Compact = true };
		private readonly StatusPill readingsPill = new StatusPill { RightAligned = true };
		private readonly StatusPill coolingPill = new StatusPill { RightAligned = false };
		private readonly StatusPill healthSyncPill = new StatusPill { RightAligned = true };

		private readonly TextBox details = new TextBox();
        private DeviceSpecView specView;
        private string deviceReport="";
        private bool loadingDeviceInfo;
        private DateTime deviceInfoUpdated=DateTime.MinValue;

		private readonly Dictionary<string, string> snapshot = new Dictionary<string, string>();

		private readonly List<Button> navigation = new List<Button>();
        private RaisedButton settingsNavButton;
        private readonly StatusPill updateStatusPill = new StatusPill { RightAligned = true };
        private UpdateCheckResult latestUpdate;
        private bool checkingUpdates, downloadingUpdate;
        private int downloadProgress = -1;
        private DateTime lastUpdateCheckUtc = DateTime.MinValue;
        private RaisedButton updateActionButton, updateGithubButton;
        private Label updateSummaryLabel, updateDetailsLabel;
        private Panel updateCardRef;

		private readonly Request keyboard;

		private readonly Request rear;

		private readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();

		private FlowLayoutPanel page;

		private volatile bool busy;
        private volatile bool suspended;
        private ModeObserver modeObserver;
        private ModeChime modeChime;
        private bool modeSounds=true;
        private bool autoEnergySaver=true;
        private CheckBox overviewEnergySaver, settingsEnergySaver;
        private int confirmedMode=-1;
        private RaisedButton applyKeyboardButton;
        private RaisedButton performanceModeButton;
        private readonly PerformanceHoldGesture performanceHold=new PerformanceHoldGesture();
        private readonly Stopwatch performanceHoldClock=Stopwatch.StartNew();
        private bool lastKeyboardStopOk=true;
        private bool hardwareReady,initializingHardware;
        private readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        private ComboBox gpuSelection;
        private Label gpuState;
        private Button gpuApply;
        private bool gpuTouched,syncingGpu;
        private bool takingControl,readingsFailed;
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
        private Button rearSecondColor;
        private RaisedButton rearApplyButton,rearOffButton,rearStopMusicButton;
        private Panel rearEditor;
        private Label rearEffectHint;
        private bool rearBusy;

		private Label kbOutcome;

		private Label rearOutcome;

		private string selectedPage;

		private readonly Dictionary<string, Control[]> cachedPages = new Dictionary<string, Control[]>();

		private BufferedPanel lightingTabs, settingsTabs;
		private RaisedButton keyboardTabBtn, rearTabBtn, settingsTabPrimaryBtn, settingsTabSecondaryBtn;
		private SoftPanel keyboardCardRef, rearCardRef, settingsPrefCard, settingsPowerCard, settingsKeysCard;
		private bool lightingShowRear, settingsShowSecondary;

		private string keyboardResult = "Not applied in this session";

		private string rearResult = "Not applied in this session";

		private readonly KeyLock keyLock = new KeyLock();

		private readonly NotifyIcon tray = new NotifyIcon();
        private Icon trayIcon;
        private ContextMenuStrip trayMenu;
        private PictureBox brandLogo;
        private Bitmap brandBitmap;

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
        private readonly float initialDpiScale;
        private readonly Dictionary<Control, Action> cardReflows = new Dictionary<Control, Action>();
        private FormWindowState lastWindowState = FormWindowState.Normal;
        private bool resizingWindow;

        private float DpiScale {
            get {
                int dpi = IsHandleCreated ? DeviceDpi : 0;
                return dpi > 0 ? dpi / 96f : initialDpiScale;
            }
        }
        private int S(float px) { return (int)Math.Round(px * DpiScale); }
        private Padding SPadding(int left, int top, int right, int bottom) { return new Padding(S(left), S(top), S(right), S(bottom)); }
        private int MeasureLabelHeight(Label lbl, int width, int minHeight = 0) {
            if (lbl == null || lbl.IsDisposed || string.IsNullOrEmpty(lbl.Text)) return minHeight;
            int measured = TextRenderer.MeasureText(lbl.Text, lbl.Font, new Size(Math.Max(S(40), width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
            return Math.Max(minHeight, measured + S(6));
        }
        private void ReflowCardHeader(SoftPanel card, Label desc) {
            if (card == null || card.IsDisposed) return;
            Label title = card.Controls.OfType<Label>().FirstOrDefault(l => l != desc);
            if (title != null && !title.IsDisposed) {
                int tw = Math.Max(S(80), card.Width - S(44));
                title.Location = new Point(S(22), S(12));
                title.Width = tw;
                title.Height = MeasureLabelHeight(title, tw, S(28));
            }
            if (desc != null && !desc.IsDisposed) {
                int dw = Math.Max(S(80), card.Width - S(46));
                desc.Location = new Point(S(23), title != null && !title.IsDisposed ? (title.Bottom + S(2)) : S(42));
                desc.Width = dw;
                desc.Height = MeasureLabelHeight(desc, dw, S(20));
            }
        }
        private void ConfigureWrappingCheckBox(CheckBox cb, int cardWidth, int leftPx = 22) {
            if (cb == null || cb.IsDisposed) return;
            cb.UseMnemonic = false;
            cb.AutoSize = false;
            int totalW = Math.Max(S(140), cardWidth - S(leftPx + 22));
            int textAvailW = Math.Max(S(80), totalW - S(26));
            int singleH = TextRenderer.MeasureText("Ag", cb.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Height;
            int textH = TextRenderer.MeasureText(cb.Text ?? "", cb.Font, new Size(textAvailW, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
            bool multiLine = textH > singleH + S(4);
            cb.CheckAlign = multiLine ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft;
            cb.TextAlign = multiLine ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft;
            cb.Size = new Size(totalW, Math.Max(S(24), textH + S(6)));
        }
        private void RegisterCardReflow(Control card, Action reflow) {
            if (card != null && reflow != null) cardReflows[card] = reflow;
        }
        private void TriggerCardReflow(Control card) {
            Action reflow;
            if (card != null && !card.IsDisposed && cardReflows.TryGetValue(card, out reflow)) reflow();
        }
        protected override CreateParams CreateParams {
            get {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }
        protected override void OnPaintBackground(PaintEventArgs e) {
            using (var b = new SolidBrush(Theme.Ivory)) e.Graphics.FillRectangle(b, e.ClipRectangle);
        }
        protected override void OnResizeBegin(EventArgs e) {
            resizingWindow = true;
            SetPreviewAnimation(page, false);
            base.OnResizeBegin(e);
        }
        protected override void OnResizeEnd(EventArgs e) {
            base.OnResizeEnd(e);
            resizingWindow = false;
            SetPreviewAnimation(page, Visible && WindowState != FormWindowState.Minimized);
            UpdateUiPolling();
        }

		public MainWindow(bool preview = false, bool startup = false)
		{
			MainWindow mainWindow = this;
			this.preview = preview;
            keyboard = preview ? new Request() : Store.Load("keyboard");
            rear = preview ? new Request() : Store.Load("rear");
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) {
                initialDpiScale = g.DpiX > 0 ? g.DpiX / 96f : 1f;
            }
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            var initialPalette=Theme.Palette;
            if(!preview)Theme.SetDepth(Store.Flag("dark-theme")?1:0);
            themeMotion=new ThemeMotion(()=>Application.OpenForms.Cast<Form>().Cast<Control>().Concat(new Control[]{this}).Concat(cachedPages.Values.SelectMany(controls=>controls)).ToArray());
            Disposed+=(s,e)=>themeMotion.Dispose();
            HandleCreated+=(s,e)=>Theme.ApplyChrome(this);
            keyboardCommands=new LatestCommandPump<Request>(r=>Run(r,"keyboard",false,!string.IsNullOrEmpty(r.Feedback)));
            if(!preview){try{string preference=Path.Combine(Store.DirectoryPath,"mode-sounds.txt");bool enabled;if(File.Exists(preference)&&bool.TryParse(File.ReadAllText(preference),out enabled))modeSounds=enabled;}catch{}}
            autoEnergySaver=preview||Store.Flag("auto-energy-saver",true);
            for(int i=0;i<4;i++){keyboard.Colors[i]=LightingColour.VividHex(keyboard.Colors[i]);rear.Colors[i]=LightingColour.VividHex(rear.Colors[i]);}
			Text = AppIdentity.Name + (preview ? " (layout preview)" : "");
            Icon=AppIdentity.LoadIcon(64);
			base.Name = "GTBookStable";
            Rectangle workArea = Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080);
            int targetW = S(1080), targetH = S(800), minW = S(760), minH = S(560);
            if (!preview) {
                targetW = Math.Min(targetW, Math.Max(S(640), workArea.Width - S(24)));
                targetH = Math.Min(targetH, Math.Max(S(520), workArea.Height - S(40)));
                minW = Math.Min(minW, targetW);
                minH = Math.Min(minH, targetH);
            } else {
                minW = Math.Min(minW, 760);
                minH = Math.Min(minH, 560);
            }
			base.ClientSize = new Size(targetW, targetH);
			MinimumSize = new Size(minW, minH);
			base.StartPosition = FormStartPosition.CenterScreen;
			base.AutoScaleMode = AutoScaleMode.None;
			if (startup)
			{
				base.Opacity = 0.0;
				base.ShowInTaskbar = false;
			}
			Font = new Font("Segoe UI", 10f);
			BackColor = Theme.Ivory;
			ForeColor = ink;
			sidebar.Dock = DockStyle.Left;
			sidebar.Width = S(210);
			sidebar.BackColor = Theme.Pearl;
			base.Controls.Add(body);
			base.Controls.Add(sidebar);
			body.Dock = DockStyle.Fill;
            brandBitmap=AppIdentity.LoadLogo();
            brandLogo=new PictureBox{Image=brandBitmap,SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(S(22),S(26)),Size=new Size(S(48),S(48)),BackColor=Color.Transparent};sidebar.Controls.Add(brandLogo);
			Label label = Label("GTBOOK", 15f, true);
			label.SetBounds(S(80), S(26), S(126), S(32));
			sidebar.Controls.Add(label);
			Label label2 = Label("Essentials", 11f);
			label2.SetBounds(S(82), S(60), S(112), S(24));
			sidebar.Controls.Add(label2);
			int num = 147;
            var themeSwitch=new ThemeSwitch{Location=new Point(S(16),S(101)),Size=new Size(S(178),S(38)),Font=new Font("Segoe UI",9f)};
            themeSwitch.Click+=async (s,e)=>{
                themeMotion.Toggle();themeSwitch.AccessibleDescription=themeMotion.DarkTarget?"Dark theme selected. Click to switch to light.":"Light theme selected. Click to switch to dark.";
                if(!preview)try{bool dark=themeMotion.DarkTarget;themePreferenceSave=themePreferenceSave.ContinueWith(_=>Store.SaveFlag("dark-theme",dark),TaskScheduler.Default);await themePreferenceSave;}catch(Exception error){Store.Log(error.Message);}
            };
            sidebar.Controls.Add(themeSwitch);
			string[] array = new string[4] { "Overview", "Lighting", "Settings", "Device & help" };
			foreach (string text in array)
			{
				string target = text;
				Button button = Button(text, delegate
				{
					mainWindow.ShowPage(target);
				});
				button.SetBounds(S(16), S(num), S(178), S(48));
                ((RaisedButton)button).Navigation=true;
                button.Font=new Font("Segoe UI",10,FontStyle.Regular);
                if(target=="Settings")settingsNavButton=(RaisedButton)button;
				sidebar.Controls.Add(button);
				navigation.Add(button);
				num += 57;
			}
            var quoteRail=new BufferedPanel{Dock=DockStyle.Bottom,Height=S(205),BackColor=Color.Transparent};
            var gpuHeader=Label("GPU MODE",8f,true);gpuHeader.ForeColor=Theme.OrangeInk;gpuHeader.SetBounds(S(16),S(18),S(178),S(18));quoteRail.Controls.Add(gpuHeader);
            gpuTouched=false;
            gpuSelection=Effect(quoteRail,GpuModes.Names,"",28);
            gpuSelection.SetBounds(S(16),S(40),S(178),S(32));gpuSelection.DropDownWidth=S(178);gpuSelection.SelectedIndex=-1;
            gpuSelection.SelectedIndexChanged+=(s,e)=>{if(!syncingGpu)gpuTouched=true;UpdateGpuUi();};
            gpuApply=Button("Save GPU mode",async delegate{if(gpuSelection.SelectedIndex<0)return;await Run(new Request{Action="gpu",Mode=gpuSelection.SelectedIndex},"gpu-pending");});
            gpuApply.SetBounds(S(16),S(92),S(178),S(34));gpuApply.Enabled=false;quoteRail.Controls.Add(gpuApply);
            gpuRestart=Button("Restart now",RestartWindows);gpuRestart.SetBounds(S(16),S(134),S(178),S(32));gpuRestart.Visible=false;quoteRail.Controls.Add(gpuRestart);
            gpuState=new Label();
            var ver=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string appVer="v"+ver.Major+"."+ver.Minor+(ver.Build>0?"."+ver.Build:"");
            Label label3=Label(appVer,8.5f);label3.SetBounds(S(16),S(176),S(178),S(20));label3.ForeColor=muted;quoteRail.Controls.Add(label3);
            sidebar.Controls.Add(quoteRail);
            UpdateGpuUi();
			BufferedPanel panel = new BufferedPanel
			{
				Dock = DockStyle.Top,
				Height = S(92),
                BackColor=Color.Transparent
			};
            pageEyebrow.SetBounds(S(30),S(6),S(620),S(16));pageEyebrow.UseMnemonic=false;pageEyebrow.Font=new Font("Segoe UI",8,FontStyle.Bold);pageEyebrow.ForeColor=Theme.OrangeInk;pageEyebrow.BackColor=Color.Transparent;panel.Controls.Add(pageEyebrow);
			heading.SetBounds(S(28), S(24), S(620), S(38));
			heading.Font = new Font("Segoe UI", 20.5f, FontStyle.Bold);
			heading.ForeColor = ink;
            heading.BackColor=Color.Transparent;
			subtitle.SetBounds(S(30), S(64), S(690), S(22));
			subtitle.ForeColor = muted;
            subtitle.BackColor=Color.Transparent;
			panel.Controls.Add(heading);
			panel.Controls.Add(subtitle);
            panel.Resize+=(s,e)=>{
                int width=Math.Max(S(120),panel.ClientSize.Width-S(56));
                heading.Width=width;pageEyebrow.Width=width;subtitle.Width=width;
                bool hasEyebrow=!string.IsNullOrEmpty(pageEyebrow.Text);
                pageEyebrow.Visible=hasEyebrow;
                pageEyebrow.Top=S(8);
                heading.Top=hasEyebrow?Math.Max(S(24),pageEyebrow.Bottom+S(1)):S(14);
                subtitle.Top=heading.Bottom+S(2);
                int subH=MeasureLabelHeight(subtitle,width,S(22));
                if(subtitle.Height!=subH)subtitle.Height=subH;
                int needH=Math.Max(hasEyebrow?S(92):S(82),subtitle.Bottom+S(6));
                if(panel.Height!=needH)panel.Height=needH;
            };
			BufferedPanel panel2 = new BufferedPanel
			{
				Dock = DockStyle.Bottom,
				Height = S(44),
                BackColor=Color.Transparent,
				Padding = SPadding(28, 6, 24, 8)
			};
			status.Dock = DockStyle.Fill;
			status.Font = new Font("Segoe UI", 10f);
			status.ForeColor = muted;
            status.BackColor=Color.Transparent;
			status.Text = "Getting to know your GTBook…";
			panel2.Controls.Add(status);
			BufferedPanel panel3 = new BufferedPanel
			{
				Dock = DockStyle.Fill,
                BackColor=Color.Transparent,
				Padding = SPadding(26, 0, 22, 0)
			};
			page = new GradientFlowPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				AutoScroll = false,
				Padding = SPadding(0, 0, 0, 6)
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
				ClearCachedPages(true);
				clock.Stop();
				clock.Dispose();
				keyLock.Dispose();
                tray.Visible=false;
                tray.ContextMenuStrip=null;
                tray.Icon=null;
				tray.Dispose();
                if(trayMenu!=null){trayMenu.Dispose();trayMenu=null;}
                if(trayIcon!=null){trayIcon.Dispose();trayIcon=null;}
                if(brandLogo!=null&&!brandLogo.IsDisposed)brandLogo.Image=null;
                if(brandBitmap!=null){brandBitmap.Dispose();brandBitmap=null;}
                Icon.Dispose();
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
                    var _updateTask=mainWindow.CheckForUpdatesAfterStartupAsync();
                    await mainWindow.CloseOfficialOnOpen();
					await mainWindow.InitializeHardware();
					if (Store.Warning.Length > 0)
					{
						Label label4 = mainWindow.status;
						label4.Text = label4.Text + "\n" + Store.Warning;
					}
				}
			};
			trayIcon = AppIdentity.LoadIcon(32);
			tray.Icon = trayIcon;
			tray.Text = AppIdentity.Name;
			tray.Visible = true;
			trayMenu = new ContextMenuStrip
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
			tray.ContextMenuStrip = trayMenu;
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
                TriggerImmediateRefreshIfVisible();
			};
			base.Resize += delegate
			{
                if (WindowState != lastWindowState) {
                    lastWindowState = WindowState;
                    SetPreviewAnimation(page, !resizingWindow && base.Visible && base.WindowState != FormWindowState.Minimized);
                    UpdateUiPolling();
                    TriggerImmediateRefreshIfVisible();
                }
			};
			ShowPage("Overview");
			Theme.Apply(new Control[]{this},initialPalette);
		}

        private void TriggerImmediateRefreshIfVisible(){
            if(preview||IsDisposed||!IsHandleCreated||!Visible||WindowState==FormWindowState.Minimized||selectedPage!="Overview"||busy||takingControl||refreshingReadings)return;
            if((DateTime.UtcNow-readingsUpdatedUtc).TotalMilliseconds>1500){
                BeginInvoke((Action)async delegate{if(!IsDisposed&&!busy)await Run(new Request{Action="refresh"},null,true);});
            }
        }
        private void UpdateUiPolling(){
            bool onBattery=SystemInformation.PowerStatus.PowerLineStatus!=PowerLineStatus.Online;
            int interval=ReadingRefresh.Interval(onBattery);
            if(!healthSyncPill.IsDisposed){
                healthSyncPill.PillText=preview?"Preview":("Auto · "+(interval/1000)+"s");
                healthSyncPill.DotColor=preview?muted:Color.FromArgb(35,169,108);
            }
            if(preview)return;
            clock.Interval=interval;
            if(Visible&&WindowState!=FormWindowState.Minimized&&selectedPage=="Overview")clock.Start();else clock.Stop();
        }
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
        private void UpdateReadingsStamp(){
            string text=preview?"PREVIEW":readingsFailed?"AUTO · retrying…":readingsUpdatedUtc==DateTime.MinValue?"AUTO · syncing…":"LIVE · "+readingsUpdatedUtc.ToLocalTime().ToString("HH:mm:ss");
            if(!readingsPill.IsDisposed){
                readingsPill.PillText=text;
                readingsPill.DotColor=preview?muted:readingsFailed?Color.FromArgb(244,145,36):readingsUpdatedUtc==DateTime.MinValue?Theme.OrangeEnd:Color.FromArgb(35,169,108);
            }
        }
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
                if(!IsDisposed)status.Text="Hardware is not ready yet. Shortcuts are active; auto-refresh will sync automatically.";
            }catch(OperationCanceledException){}finally{initializingHardware=false;}
        }
        private void UpdateGpuUi(){
            if(gpuSelection==null||gpuSelection.IsDisposed)return;
            int raw=0;
            bool known=snapshot.ContainsKey("gpu-raw")&&int.TryParse(snapshot["gpu-raw"],out raw)&&raw>=1&&raw<=3;
            if(!known){
                int pnp=GpuModes.DetectPnpMode();
                if(pnp>=1&&pnp<=3){raw=pnp;known=true;snapshot["gpu-raw"]=raw.ToString();if(!snapshot.ContainsKey("gpu-mode"))snapshot["gpu-mode"]=GpuModes.Name((uint)raw);}
            }
            if(known&&!gpuTouched){syncingGpu=true;gpuSelection.SelectedIndex=raw-1;syncingGpu=false;}
            bool supported=!snapshot.ContainsKey("supported")||snapshot["supported"]=="True";
            if(gpuApply!=null&&!gpuApply.IsDisposed)gpuApply.Enabled=supported&&GpuPending.Ready&&gpuSelection.SelectedIndex>=0;
            if(bootGpuRaw==0)bootGpuRaw=GpuPending.Pending?GpuPending.Before:(known?raw:(gpuSelection.SelectedIndex>=0?gpuSelection.SelectedIndex+1:1));
            if(gpuRestart!=null&&!gpuRestart.IsDisposed){
                gpuRestart.Visible=GpuPending.Pending;
            }
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
			raisedButton.Height = S(38);
			raisedButton.Width = S(140);
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
                float s=DpiScale;
                int clientW=page.ClientSize.Width;
                int available=Math.Max(S(240),clientW-page.Padding.Horizontal-(int)(4*s)),gap=(int)(12*s);
                if(selectedPage=="Lighting"&&keyboardCardRef!=null&&rearCardRef!=null){
                    bool wide=available>=(int)(1160*s);
                    page.FlowDirection=wide?FlowDirection.LeftToRight:FlowDirection.TopDown;
                    page.WrapContents=wide;
                    if(lightingTabs!=null&&!lightingTabs.IsDisposed){
                        lightingTabs.Visible=!wide;
                        if(!wide){
                            int tabW=Math.Max(S(320),Math.Min(available,(int)(1100*s)));
                            lightingTabs.Width=tabW;lightingTabs.Height=S(36);lightingTabs.Margin=new Padding(0,0,0,S(8));
                            int bw=Math.Min(S(220),Math.Max(S(110),(tabW-S(10))/2));
                            if(keyboardTabBtn!=null&&!keyboardTabBtn.IsDisposed){keyboardTabBtn.SetBounds(0,0,bw,S(34));keyboardTabBtn.Selected=!lightingShowRear;}
                            if(rearTabBtn!=null&&!rearTabBtn.IsDisposed){rearTabBtn.SetBounds(bw+S(10),0,bw,S(34));rearTabBtn.Selected=lightingShowRear;}
                        }
                        page.SetFlowBreak(lightingTabs,false);
                    }
                    if(wide){
                        int colW=(available-gap)/2;
                        keyboardCardRef.Visible=true;rearCardRef.Visible=true;
                        keyboardCardRef.Width=colW;rearCardRef.Width=colW;
                        keyboardCardRef.Margin=new Padding(0,0,gap,0);rearCardRef.Margin=Padding.Empty;
                        page.SetFlowBreak(keyboardCardRef,false);page.SetFlowBreak(rearCardRef,true);
                        TriggerCardReflow(keyboardCardRef);TriggerCardReflow(rearCardRef);
                    }else{
                        int cardW=Math.Max(S(320),Math.Min(available,(int)(1100*s)));
                        keyboardCardRef.Visible=!lightingShowRear;rearCardRef.Visible=lightingShowRear;
                        var active=lightingShowRear?rearCardRef:keyboardCardRef;
                        active.Width=cardW;active.Margin=Padding.Empty;
                        page.SetFlowBreak(keyboardCardRef,false);page.SetFlowBreak(rearCardRef,false);
                        TriggerCardReflow(active);
                    }
                }else if(selectedPage=="Settings"&&settingsPrefCard!=null&&settingsPowerCard!=null&&settingsKeysCard!=null&&updateCardRef!=null){
                    bool twoCol=available>=(int)(580*s);
                    if(settingsTabs!=null&&!settingsTabs.IsDisposed){
                        settingsTabs.Visible=!twoCol;
                        if(!twoCol){
                            settingsTabs.Width=available;settingsTabs.Height=S(36);settingsTabs.Margin=new Padding(0,0,0,S(8));
                            int bw=Math.Max(S(110),(available-S(10))/2);
                            if(settingsTabPrimaryBtn!=null&&!settingsTabPrimaryBtn.IsDisposed){settingsTabPrimaryBtn.SetBounds(0,0,bw,S(34));settingsTabPrimaryBtn.Selected=!settingsShowSecondary;}
                            if(settingsTabSecondaryBtn!=null&&!settingsTabSecondaryBtn.IsDisposed){settingsTabSecondaryBtn.SetBounds(bw+S(10),0,bw,S(34));settingsTabSecondaryBtn.Selected=settingsShowSecondary;}
                        }
                        page.SetFlowBreak(settingsTabs,false);
                    }
                    if(twoCol){
                        page.FlowDirection=FlowDirection.LeftToRight;page.WrapContents=true;
                        int colW=(available-gap)/2;
                        Control[] cards=new Control[]{settingsPrefCard,settingsPowerCard,settingsKeysCard,updateCardRef};
                        foreach(Control c in cards){c.Visible=true;c.Width=colW;TriggerCardReflow(c);}
                        int row1H=Math.Max(settingsPrefCard.Height,settingsPowerCard.Height);
                        settingsPrefCard.Height=row1H;settingsPowerCard.Height=row1H;
                        int row2H=Math.Max(settingsKeysCard.Height,updateCardRef.Height);
                        settingsKeysCard.Height=row2H;updateCardRef.Height=row2H;
                        settingsPrefCard.Margin=new Padding(0,0,gap,S(10));
                        settingsPowerCard.Margin=new Padding(0,0,0,S(10));
                        settingsKeysCard.Margin=new Padding(0,0,gap,0);
                        updateCardRef.Margin=Padding.Empty;
                        page.SetFlowBreak(settingsPrefCard,false);page.SetFlowBreak(settingsPowerCard,true);
                        page.SetFlowBreak(settingsKeysCard,false);page.SetFlowBreak(updateCardRef,true);
                    }else{
                        page.FlowDirection=FlowDirection.TopDown;page.WrapContents=false;
                        settingsPrefCard.Visible=!settingsShowSecondary;settingsPowerCard.Visible=!settingsShowSecondary;
                        settingsKeysCard.Visible=settingsShowSecondary;updateCardRef.Visible=settingsShowSecondary;
                        Control[] visible=settingsShowSecondary?new Control[]{settingsKeysCard,updateCardRef}:new Control[]{settingsPrefCard,settingsPowerCard};
                        for(int i=0;i<visible.Length;i++){
                            visible[i].Width=available;
                            visible[i].Margin=new Padding(0,0,0,i==0?S(8):0);
                            page.SetFlowBreak(visible[i],false);
                            TriggerCardReflow(visible[i]);
                        }
                    }
                }else{
                    page.FlowDirection=FlowDirection.TopDown;page.WrapContents=false;
                    int cardW=Math.Max(S(320),Math.Min(available,(int)(1100*s)));
                    int count=page.Controls.Count;
                    for(int i=0;i<count;i++){
                        Control control=page.Controls[i];
                        control.Width=cardW;
                        control.Margin=new Padding(0,0,0,i<count-1?S(10):0);
                        page.SetFlowBreak(control,false);
                        TriggerCardReflow(control);
                    }
                }
                page.ResumeLayout(true);
            }finally{
                if(fittingCards){
                    try{page.ResumeLayout(false);}catch{}
                    fittingCards=false;
                }
            }
		}

		public void RenderPages(string directory)
		{
			Directory.CreateDirectory(directory);
            ShortcutToast.RenderExamples(directory);
			MinimumSize = Size.Empty;
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
            lightingShowRear = true;
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
			foreach(string effect in RearLighting.Names){
                rearEffect.SelectedItem=effect;rearState.PreviewDraft=true;UpdateRearIndicator();Application.DoEvents();
                using(var image=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(directory,"Rear-preview-"+effect+".png"));}
            }
			rearState.PreviewDraft=false;UpdateRearIndicator();
            lightingShowRear = false;FitCards();
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
            foreach(string effect in ExtraEffects.Names.Skip(10)){
                keyboard.Effect=effect;SyncKeyboardEditor();Application.DoEvents();
                using(var image=new Bitmap(fullKeyboard.Width,fullKeyboard.Height)){fullKeyboard.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(directory,"Effect-"+effect.Replace(" ","-")+".png"));}
            }
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
        internal void VerifyRearControls(){
            ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-32000,-32000);Show();
            lightingShowRear=true;
            foreach(int width in new[]{860,1080,1920}){
                ClientSize=new Size(width,1080);ShowPage("Lighting");PerformLayout();page.PerformLayout();Application.DoEvents();
                foreach(string effect in RearLighting.Names){
                    rearEffect.SelectedItem=effect;UpdateRearIndicator();
                    if(rearSecondColor.Enabled!=RearLighting.UsesSecondColour(effect)||rearSpeed.Enabled!=(effect=="Breathing"))throw new Exception("Wrong rear controls: "+effect);
                    if(rearStopMusicButton.Enabled)throw new Exception("Stop music is enabled without a worker.");
                    if(rearApplyButton.Text!=(RearLighting.IsMusic(effect)?"Start music sync":"Apply to bar"))throw new Exception("Unclear rear action label.");
                    foreach(Control control in new Control[]{rearEffect,rearColor,rearSecondColor,rearApplyButton,rearOffButton,rearStopMusicButton}){
                        if(control.Left<0||control.Right>control.Parent.ClientSize.Width||control.Height<30)throw new Exception("Rear control clipped at "+width);
                    }
                    if(rearApplyButton.Right>=rearOffButton.Left||rearOffButton.Right>=rearStopMusicButton.Left||rearApplyButton.Height<50)throw new Exception("Rear actions overlap or are too small.");
                }
            }
            rearBright.Value=0;if(rearApplyButton.Text!="Apply · turn off")throw new Exception("Zero brightness does not explain the off action.");
        }
        internal void VerifyThemeMotion(){
            ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-32000,-32000);Show();
            foreach(string title in new[]{"Overview","Lighting","Settings","Device & help"})ShowPage(title);
            ShowPage("Lighting");var swatch=zoneButtons[0].BackColor;
            themeMotion.Toggle();themeMotion.Advance(160);
            if(Math.Abs(Theme.Depth-.5f)>.001)throw new Exception("Theme did not animate through its midpoint.");
            themeMotion.Toggle();themeMotion.Advance(0);
            if(Math.Abs(Theme.Depth-.5f)>.001)throw new Exception("Rapid theme reversal jumped.");
            themeMotion.Advance(320);
            if(Theme.Depth!=0||themeMotion.Running)throw new Exception("Light transition did not settle and stop.");
            themeMotion.Toggle();themeMotion.Advance(320);
            if(Theme.Depth!=1||themeMotion.Running)throw new Exception("Dark transition did not settle and stop.");
            foreach(string title in new[]{"Overview","Lighting","Settings","Device & help"}){
                ShowPage(title);Application.DoEvents();
                if(heading.ForeColor.ToArgb()!=Theme.Ink.ToArgb())throw new Exception("Cached heading retained light colours.");
                var firstCard=page.Controls.OfType<SoftPanel>().First(c=>c.Visible);
                if(firstCard.BackColor.ToArgb()!=Theme.White.ToArgb())throw new Exception("Cached card retained light colours: "+title);
            }
            if(zoneButtons[0].BackColor.ToArgb()!=swatch.ToArgb())throw new Exception("Theme changed the hardware colour selection.");
            if(details.BackColor.ToArgb()!=Theme.White.ToArgb())throw new Exception("Device details retained a white background.");
            themeMotion.Toggle();themeMotion.Advance(320);
            if(heading.ForeColor.ToArgb()!=Theme.Ink.ToArgb())throw new Exception("Light colours were not restored.");
        }
        internal void VerifyLightingLayout(){
            ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-32000,-32000);Show();
            ClientSize=new Size(1920,1080);ShowPage("Lighting");PerformLayout();page.PerformLayout();Application.DoEvents();
            if(keyboardCardRef.Top!=rearCardRef.Top||rearCardRef.Left<keyboardCardRef.Right||rearCardRef.Right>page.ClientSize.Width)throw new Exception("Lighting cards are not beside each other inside the viewport.");
            if(rearCardRef.Bottom>page.ClientSize.Height)throw new Exception("Rear-light controls require scrolling in the wide layout.");
            ClientSize=new Size(860,800);lightingShowRear=false;FitCards();PerformLayout();page.PerformLayout();Application.DoEvents();
            if(!lightingTabs.Visible||!keyboardCardRef.Visible||rearCardRef.Visible)throw new Exception("Compact lighting should show Keyboard tab only without stacking.");
            if(keyboardCardRef.Bottom>page.ClientSize.Height)throw new Exception("Keyboard lighting overflowed compact viewport: "+keyboardCardRef.Bottom+" > "+page.ClientSize.Height);
            rearTabBtn.PerformClick();PerformLayout();page.PerformLayout();Application.DoEvents();
            if(keyboardCardRef.Visible||!rearCardRef.Visible||rearCardRef.Bottom>page.ClientSize.Height)throw new Exception("Rear lighting overflowed compact viewport.");
            lightingShowRear=false;FitCards();
            var motion=keyboardCardRef.Controls.OfType<ComboBox>().Single(control=>control.AccessibleName=="Animation direction");
            motion.SelectedIndex=1;
            syncingKeyboardChoice=true;kbExtraEffect.SelectedIndex=-1;syncingKeyboardChoice=false;
            foreach(string name in ExtraEffects.Names){
                kbExtraEffect.SelectedItem=name;keyboardChoiceChanged();
                if(keyboard.Effect!=name||keyboard.Direction!=1)throw new Exception("Effect picker lost the selection or saved direction: "+name);
                Protocol.Validate(Copy(keyboard,"keyboard-animation"));
                if(motion.Enabled!=ExtraEffects.HasDirection(name))throw new Exception("Misleading motion control for "+name);
                if(!motion.Enabled&&motion.Text.Contains("Left"))throw new Exception("Symmetric effect still displays left/right: "+name);
                if(zoneButtons[0].Enabled!=ExtraEffects.UsesColours(name)||zoneButtons[1].Enabled!=ExtraEffects.UsesColours(name))throw new Exception("Palette controls disagree with the selected effect: "+name);
            }
        }
        internal void VerifyAllPagesNoTextClipping(){
            MinimumSize=Size.Empty;
            ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-32000,-32000);Show();
            if((CreateParams.ExStyle&0x02000000)==0)throw new Exception("MainWindow missing WS_EX_COMPOSITED double-buffered resize protection.");
            if(page.AutoScroll)throw new Exception("MainWindow page should not enable AutoScroll.");
            foreach(int width in new[]{1920,860,1280,760,860,1080}){
                ClientSize=new Size(width,width==1920?1080:(width==760?560:800));
                foreach(string pageTitle in new[]{"Overview","Lighting","Settings","Device & help"}){
                    ShowPage(pageTitle);PerformLayout();page.PerformLayout();Application.DoEvents();
                    if(pageEyebrow.Visible&&pageEyebrow.Bottom>heading.Top)throw new Exception("Header eyebrow overlaps heading in "+pageTitle+" @ "+width+": eyebrow="+pageEyebrow.Bounds+" heading="+heading.Bounds);
                    if(heading.Bottom>subtitle.Top||subtitle.Bottom>heading.Parent.ClientSize.Height)throw new Exception("Header label clipped in "+pageTitle+" @ "+width+": heading="+heading.Bounds+" sub="+subtitle.Bounds+" panel="+heading.Parent.ClientSize);
                    foreach(Control card in page.Controls){
                        if(!card.Visible)continue;
                        if(width>=860&&card.Bottom>page.ClientSize.Height+2)throw new Exception("Vertical overflow without scroll in "+pageTitle+" @ "+width+": bottom="+card.Bottom+" viewport="+page.ClientSize.Height+" card="+card.Text);
                        CheckRecursiveFit(card,pageTitle,width);
                    }
                    if(pageTitle=="Device & help"&&specView!=null&&!specView.IsDisposed&&width>=860){
                        specView.SelectCategory(1);PerformLayout();page.PerformLayout();Application.DoEvents();
                        foreach(Control card in page.Controls){
                            if(card.Visible&&card.Bottom>page.ClientSize.Height+2)throw new Exception("Vertical overflow in Device & help All @ "+width+": bottom="+card.Bottom+" viewport="+page.ClientSize.Height);
                        }
                        specView.SelectCategory(0);
                    }
                    if(pageTitle=="Overview"&&page.Controls.Count>=3){
                        var rightCard=page.Controls[0];
                        var rTitle=rightCard.Controls.OfType<Label>().FirstOrDefault();
                        var rGrid=rightCard.Controls.OfType<BufferedTableLayoutPanel>().FirstOrDefault();
                        if(rTitle!=null&&rGrid!=null&&rTitle.Bottom>rGrid.Top)throw new Exception("Right now header overlaps tiles at "+width+": title="+rTitle.Bounds+" grid="+rGrid.Bounds);
                        if(rTitle!=null&&rTitle.Right>readingsPill.Left)throw new Exception("Right now header overlaps readingsPill at "+width+": title="+rTitle.Bounds+" pill="+readingsPill.Bounds+" card="+rightCard.Size);
                        var healthCard=page.Controls[2];
                        var hLabels=healthCard.Controls.OfType<Label>().Take(2).ToArray();
                        if(hLabels.Length>0&&hLabels[0].Right>healthSyncPill.Left)throw new Exception("System health header overlaps pill at "+width+": title="+hLabels[0].Bounds+" pill="+healthSyncPill.Bounds);
                        if(hLabels.Length>1&&hLabels[0].Bottom>hLabels[1].Top)throw new Exception("System health header overlaps subtitle at "+width+": title="+hLabels[0].Bounds+" sub="+hLabels[1].Bounds);
                    }
                    if(pageTitle=="Settings"&&updateCardRef!=null&&!updateCardRef.IsDisposed&&updateCardRef.Visible){
                        var uTitle=updateCardRef.Controls.OfType<Label>().FirstOrDefault();
                        if(uTitle!=null&&uTitle.Right>updateStatusPill.Left)throw new Exception("App updates header overlaps pill at "+width+": title="+uTitle.Bounds+" pill="+updateStatusPill.Bounds);
                    }
                }
            }
        }
        private void CheckRecursiveFit(Control parent,string pageTitle,int winWidth){
            foreach(Control c in parent.Controls){
                if(!c.Visible)continue;
                if(c.Left<0||c.Top<0||c.Right>parent.ClientSize.Width+1||c.Bottom>parent.ClientSize.Height+1){
                    throw new Exception("Control clipped in "+pageTitle+" @ "+winWidth+": "+c.GetType().Name+" '"+c.Text+"' bounds="+c.Bounds+" parentClient="+parent.ClientSize);
                }
                if(c is Label&&!string.IsNullOrEmpty(c.Text)&&c.Width>0){
                    int needed=TextRenderer.MeasureText(c.Text,c.Font,new Size(Math.Max(10,c.Width),int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix).Height;
                    if(c.Height+2<needed)throw new Exception("Label height clipped in "+pageTitle+" @ "+winWidth+": '"+c.Text+"' h="+c.Height+" needed="+needed);
                }
                if(c is CheckBox&&!string.IsNullOrEmpty(c.Text)&&c.Width>S(26)){
                    int needed=TextRenderer.MeasureText(c.Text,c.Font,new Size(Math.Max(10,c.Width-S(24)),int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix).Height;
                    if(c.Height+2<needed)throw new Exception("CheckBox clipped in "+pageTitle+" @ "+winWidth+": '"+c.Text+"' h="+c.Height+" needed="+needed);
                }
                if(c.HasChildren&&!(c is TrackBar)&&!(c is ComboBox)&&!(c is DeviceSpecView))CheckRecursiveFit(c,pageTitle,winWidth);
            }
        }
        internal void VerifyPerformanceTurboUi(){
            ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-32000,-32000);Show();ShowPage("Overview");
            var modeGrid=page.Controls[1].Controls.OfType<BufferedTableLayoutPanel>().First();
            var buttons=modeGrid.Controls.OfType<RaisedButton>().ToArray();
            if(buttons.Length!=3)throw new Exception("Expected 3 performance buttons, found "+buttons.Length);
            if(!buttons.Select(b=>b.Text).SequenceEqual(new[]{"Quiet","Balanced","Performance"}))throw new Exception("Unexpected mode button labels");
            snapshot["mode"]="Turbo";UpdateValues();
            if(performanceModeButton.Text!="Turbo"||!performanceModeButton.Selected)throw new Exception("Turbo state did not promote Performance button");
            if(modeTile.DetailText!="Ultimate boost · 0% battery saving")throw new Exception("Wrong Turbo detail text: "+modeTile.DetailText);
            performanceModeButton.HoldProgress=0.5f;
            using(var bmp=new Bitmap(performanceModeButton.Width,performanceModeButton.Height))performanceModeButton.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));
            performanceModeButton.HoldProgress=0f;
            snapshot["mode"]="Balanced";UpdateValues();
            if(performanceModeButton.Text!="Performance"||performanceModeButton.Selected)throw new Exception("Leaving Turbo did not restore Performance label");
        }
        internal void VerifyUpdateBadgeUi(){
            ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-32000,-32000);Show();
            ShowPage("Settings");
            if(settingsNavButton==null||updateActionButton==null)throw new Exception("Update UI controls were not created on Settings.");
            if(page.Controls[page.Controls.Count-1]!=updateCardRef)throw new Exception("App updates card must be the last section on Settings.");
            latestUpdate=new UpdateCheckResult{Checked=true,UpdateAvailable=true,CurrentVersion="v2.1",LatestVersion="v9.9",AssetName="GTBOOK-Essentials-Setup.exe",DownloadUrl="https://github.com/yashwantmandal26/GTBookEssentials/releases/download/v9.9/GTBOOK-Essentials-Setup.exe",Summary="New version v9.9 is available on GitHub (current: v2.1)."};
            RefreshUpdateUi();
            if(!settingsNavButton.BadgeDot)throw new Exception("Settings nav button did not show orange dot when update is available.");
            if(updateActionButton.Text!="Update to v9.9"||!updateActionButton.Primary)throw new Exception("Update action button did not promote direct update.");
            using(var bmp=new Bitmap(settingsNavButton.Width,settingsNavButton.Height))settingsNavButton.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));
            latestUpdate=new UpdateCheckResult{Checked=true,UpdateAvailable=false,CurrentVersion="v2.1",LatestVersion="v2.1",Summary="You're up to date (v2.1)."};
            RefreshUpdateUi();
            if(settingsNavButton.BadgeDot)throw new Exception("Settings nav button kept orange dot after up-to-date result.");
            if(updateActionButton.Text!="Check for updates")throw new Exception("Up-to-date button text mismatch: "+updateActionButton.Text);
        }
#endif

		private SoftPanel Card(string title, string description, int height)
		{
			SoftPanel softPanel = new SoftPanel();
			softPanel.Padding = SPadding(22, 18, 22, 18);
			softPanel.Height = S(height);
			softPanel.Width = Math.Max(S(320), page.ClientSize.Width - S(8));
			softPanel.Margin = new Padding(0, 0, 0, S(8));
			SoftPanel softPanel2 = softPanel;
			Label label = Label(title, 13.5f, true);
			int titleW = Math.Max(S(80), softPanel2.Width - S(44));
			label.SetBounds(S(22), S(12), titleW, MeasureLabelHeight(label, titleW, S(28)));
			label.Anchor = AnchorStyles.Top | AnchorStyles.Left;
			softPanel2.Controls.Add(label);
			Label label2 = null;
			if (description.Length > 0)
			{
				label2 = Label(description, 9.5f);
				label2.SetBounds(S(23), label.Bottom + S(2), softPanel2.Width - S(46), S(24));
				label2.Anchor = AnchorStyles.Top | AnchorStyles.Left;
				label2.Height = MeasureLabelHeight(label2, label2.Width, S(20));
				softPanel2.Controls.Add(label2);
			}
			RegisterCardReflow(softPanel2, () => {
				int tw = Math.Max(S(80), softPanel2.Width - S(44));
				label.Width = tw;
				label.Height = MeasureLabelHeight(label, tw, S(28));
				if (label2 != null && !label2.IsDisposed) {
					label2.Top = label.Bottom + S(2);
					label2.Width = Math.Max(S(80), softPanel2.Width - S(46));
					label2.Height = MeasureLabelHeight(label2, label2.Width, S(20));
					int needed = label2.Bottom + S(14);
					if (softPanel2.Controls.Count == 2) softPanel2.Height = Math.Max(S(height), needed);
				}
			});
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
			}
			page.Controls.Clear();
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
            if(heading.Parent!=null&&!heading.Parent.IsDisposed){
                var hp=heading.Parent;
                int w=Math.Max(S(120),hp.ClientSize.Width-S(56));
                bool hasEyebrow=!string.IsNullOrEmpty(pageEyebrow.Text);
                pageEyebrow.Visible=hasEyebrow;
                pageEyebrow.Top=S(8);
                heading.Top=hasEyebrow?Math.Max(S(24),pageEyebrow.Bottom+S(1)):S(14);
                subtitle.Width=w;subtitle.Top=heading.Bottom+S(2);
                subtitle.Height=MeasureLabelHeight(subtitle,w,S(22));
                hp.Height=Math.Max(hasEyebrow?S(92):S(82),subtitle.Bottom+S(6));
            }
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
				Languages.Apply(page);
			}
			Languages.Apply(sidebar);
			selectedPage = title;
			UpdateValues();
			UpdateBattery();
			FitCards();
			page.ResumeLayout();
			SetPreviewAnimation(page, base.Visible && !resizingWindow);
            UpdateUiPolling();
            if(title=="Device & help"&&!preview)BeginInvoke((Action)async delegate{await LoadDeviceInfo();});
            if(title=="Settings"){RefreshUpdateUi();if(!preview)BeginInvoke((Action)async delegate{await CheckForUpdatesAsync(false);});}
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
				if (control.Top >= S(90))
				{
					control.Top += S(170);
				}
			}
			card.Height += S(170);
			LivePreview livePreview = new LivePreview();
			livePreview.Target = target;
			livePreview.Selection = selected;
			livePreview.Location = new Point(S(22), S(91));
			livePreview.Size = new Size(card.Width - S(44), S(150));
			livePreview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			LivePreview livePreview2 = livePreview;
			card.Controls.Add(livePreview2);
			return livePreview2;
		}

		private void RearEdited()
		{
			rearState.Edited(rear);
			UpdateRearIndicator();
		}

		private void UpdateRearIndicator()
		{
            if(rearApplyButton!=null&&!rearApplyButton.IsDisposed){
                bool music=RearLighting.IsMusic(rear.Effect),editing=!rearBusy;
                rearApplyButton.Text=rear.Brightness==0?"Apply · turn off":music?"Start music sync":"Apply to bar";
                rearApplyButton.Enabled=editing;rearOffButton.Enabled=editing;
                rearStopMusicButton.Enabled=editing&&musicTask!=null&&!musicTask.IsCompleted;
                rearEditor.Enabled=editing;
                rearSecondColor.Enabled=RearLighting.UsesSecondColour(rear.Effect);
                rearSpeed.Enabled=rear.Effect=="Breathing";
                rearEffectHint.Text=RearLighting.Description(rear.Effect);
            }
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
				rearOutcome.ForeColor = ((rearState.Failure != null) ? Theme.Tone(Color.FromArgb(153,63,85),Color.FromArgb(255,158,151)) : Theme.Ink);
				TriggerCardReflow(rearOutcome.Parent);
			}
		}

        private async Task RearAction(Func<Task> action){
            if(rearBusy||busy)return;
            rearBusy=true;UpdateRearIndicator();
            try{await action();}catch(Exception error){rearState.Failed();status.Text=error.Message;Store.Log(error.ToString());}
            finally{rearBusy=false;if(!IsDisposed)UpdateRearIndicator();}
        }

        private async Task ApplyRear(){
            var request=Copy(rear,rear.Brightness==0?"rear-off":RearLighting.IsMusic(rear.Effect)?"rear-music":"rear");
            if(request.Action=="rear-music")await StartMusic(request);
            else await Run(request,"rear");
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
				string currentMode = snapshot.ContainsKey("mode") ? snapshot["mode"] : "";
				await Run(PerformanceModes.SwitchRequest(mode, confirmedMode, currentMode), null, false, true);
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
				string[] array = (ExtraEffects.Contains(keyboard.Effect) ? ExtraEffects.Names : new string[7] { "Static", "Breathing", "Wave", "Clock", "Rainbow", "Flow", "Four zones" });
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

		private void DisposeControlWithFonts(Control root)
		{
			if (root == null || root.IsDisposed) return;
			var customFonts = new List<Font>();
			CollectCustomFonts(root, customFonts);
			root.Dispose();
			foreach (Font f in customFonts)
			{
				try { f.Dispose(); } catch {}
			}
		}

		private void CollectCustomFonts(Control parent, List<Font> fonts)
		{
			Font baseFont = base.Font;
			if (parent.Font != null && !object.ReferenceEquals(parent.Font, baseFont) && !object.ReferenceEquals(parent.Font, Control.DefaultFont) && !parent.Font.IsSystemFont && !fonts.Contains(parent.Font))
			{
				fonts.Add(parent.Font);
			}
			foreach (Control child in parent.Controls)
			{
				CollectCustomFonts(child, fonts);
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
                    cardReflows.Remove(control);
					DisposeControlWithFonts(control);
				}
				cachedPages.Remove(title);
			}
			if (title == "Lighting") { lightingTabs = null; keyboardTabBtn = null; rearTabBtn = null; keyboardCardRef = null; rearCardRef = null; }
			if (title == "Settings") { settingsTabs = null; settingsTabPrimaryBtn = null; settingsTabSecondaryBtn = null; settingsPrefCard = null; settingsPowerCard = null; settingsKeysCard = null; updateCardRef = null; }
		}

		private void ClearCachedPages(bool disposing = false)
		{
			Control[] array = new Control[] { modeValue, cpuValue, batteryValue, coolingValue, details, extra, modeTile, cpuTile, batteryTile, gpuTile, cpuFanTile, gpuFanTile, memoryTile, diskTile, readingsPill, coolingPill, healthSyncPill, updateStatusPill };
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
                    cardReflows.Remove(control);
					DisposeControlWithFonts(control);
				}
			}
			cachedPages.Clear();
            cardReflows.Clear();
			lightingTabs = null; keyboardTabBtn = null; rearTabBtn = null; keyboardCardRef = null; rearCardRef = null;
			settingsTabs = null; settingsTabPrimaryBtn = null; settingsTabSecondaryBtn = null; settingsPrefCard = null; settingsPowerCard = null; settingsKeysCard = null; updateCardRef = null;
			if (disposing)
			{
				foreach (Control control in new Control[] { modeValue, cpuValue, batteryValue, coolingValue, details, extra, modeTile, cpuTile, batteryTile, gpuTile, cpuFanTile, gpuFanTile, memoryTile, diskTile, readingsPill, coolingPill, healthSyncPill, updateStatusPill, specView })
				{
					if (control != null && !control.IsDisposed) DisposeControlWithFonts(control);
				}
			}
		}

		private void Overview()
		{
			SoftPanel softPanel = Card("Right now", "", 132);
			Label rightNowTitle = softPanel.Controls.OfType<Label>().FirstOrDefault();
			if (rightNowTitle != null) rightNowTitle.Width = Math.Max(S(80), softPanel.Width - S(250));
			readingsPill.SetBounds(softPanel.Width - S(224), S(12), S(200), S(24));
			readingsPill.Anchor = AnchorStyles.Top | AnchorStyles.Left;
			softPanel.Controls.Add(readingsPill);
			readingsPill.BringToFront();
			UpdateReadingsStamp();

			var rightNowGrid = new BufferedTableLayoutPanel
			{
				Location = new Point(S(22), S(44)),
				Height = S(78),
				Width = softPanel.Width - S(44),
				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
				ColumnCount = 3,
				RowCount = 1,
				BackColor = Color.Transparent
			};
			for (int i = 0; i < 3; i++)
			{
				rightNowGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
			}
			rightNowGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			MetricTile[] topTiles = new MetricTile[] { modeTile, cpuTile, batteryTile };
			for (int i = 0; i < 3; i++)
			{
				topTiles[i].Dock = DockStyle.Fill;
				topTiles[i].Margin = new Padding(0, 0, i < 2 ? S(10) : 0, 0);
				rightNowGrid.Controls.Add(topTiles[i], i, 0);
			}
			softPanel.Controls.Add(rightNowGrid);
			RegisterCardReflow(softPanel, () => {
				readingsPill.SetBounds(softPanel.Width - S(224), S(12), S(200), S(24));
				if (rightNowTitle != null && !rightNowTitle.IsDisposed) {
					rightNowTitle.Location = new Point(S(22), S(12));
					rightNowTitle.Width = Math.Max(S(80), readingsPill.Left - rightNowTitle.Left - S(8));
					rightNowTitle.Height = MeasureLabelHeight(rightNowTitle, rightNowTitle.Width, S(28));
				}
				int gridTop = (rightNowTitle != null && !rightNowTitle.IsDisposed) ? Math.Max(S(40), Math.Max(rightNowTitle.Bottom, readingsPill.Bottom) + S(2)) : S(40);
				rightNowGrid.Location = new Point(S(22), gridTop);
				rightNowGrid.Width = Math.Max(S(160), softPanel.Width - S(44));
				rightNowGrid.Height = S(74);
				softPanel.Height = rightNowGrid.Bottom + S(10);
			});

			SoftPanel softPanel2 = Card("Performance & cooling", "", 176);
			Label perfSub = Label("Choose your pace · Hold Performance 1.5s for Turbo (Ultimate · 0% battery saving).", 9.5f);
			perfSub.SetBounds(S(23), S(42), softPanel2.Width - S(46), S(20));
			perfSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			softPanel2.Controls.Add(perfSub);

			var modeGrid = new BufferedTableLayoutPanel
			{
				Location = new Point(S(22), S(66)),
				Height = S(40),
				Width = softPanel2.Width - S(44),
				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
				ColumnCount = 3,
				RowCount = 1,
				BackColor = Color.Transparent
			};
			float[] colWidths = new float[] { 31f, 32f, 37f };
			for (int i = 0; i < 3; i++)
			{
				modeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, colWidths[i]));
			}
			modeGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			string[] array3 = new string[3] { "Quiet", "Balanced", "Performance" };
			for (int i = 0; i < 3; i++)
			{
				int mode = i;
				Button button = Button(array3[i], async delegate
				{
					string currentMode = snapshot.ContainsKey("mode") ? snapshot["mode"] : "";
					await Run(PerformanceModes.SwitchRequest(mode, confirmedMode, currentMode), null);
				});
				button.Tag = array3[i];
				button.Dock = DockStyle.Fill;
				button.Margin = new Padding(0, 0, i < 2 ? S(6) : 0, 0);
				if (mode == 2)
				{
					RaisedButton perfBtn = (RaisedButton)button;
					performanceModeButton = perfBtn;
					perfBtn.HoldAccent = Color.FromArgb(255, 65, 20);
					perfBtn.AccessibleDescription = "Click for Performance mode. Press and hold for 1.5 seconds for Turbo Ultimate Performance mode.";
					var holdTimer = new System.Windows.Forms.Timer { Interval = 25 };
					Action cancelHold = delegate
					{
						holdTimer.Stop();
						performanceHold.Cancel();
						if (!perfBtn.IsDisposed) perfBtn.HoldProgress = 0f;
					};
					perfBtn.Disposed += delegate { holdTimer.Dispose(); };
					holdTimer.Tick += async delegate
					{
						if (perfBtn.IsDisposed || !performanceHold.IsHolding) { holdTimer.Stop(); return; }
						long now = performanceHoldClock.ElapsedMilliseconds;
						perfBtn.HoldProgress = performanceHold.Progress(now);
						if (performanceHold.Poll(now))
						{
							holdTimer.Stop();
							perfBtn.HoldProgress = 0f;
							perfBtn.SuppressNextClick = true;
							await Run(new Request { Action = "mode", Mode = 3, RestoreAutoCooling = false }, null);
						}
					};
					perfBtn.MouseDown += (s, e) =>
					{
						if (e.Button != MouseButtons.Left || busy) return;
						perfBtn.SuppressNextClick = false;
						performanceHold.Begin(performanceHoldClock.ElapsedMilliseconds);
						holdTimer.Start();
					};
					perfBtn.MouseMove += (s, e) =>
					{
						if (performanceHold.IsHolding && !perfBtn.ClientRectangle.Contains(e.Location))
						{
							perfBtn.SuppressNextClick = true;
							cancelHold();
						}
					};
					perfBtn.MouseUp += async (s, e) =>
					{
						if (e.Button != MouseButtons.Left) return;
						holdTimer.Stop();
						perfBtn.HoldProgress = 0f;
						long now = performanceHoldClock.ElapsedMilliseconds;
						if (performanceHold.Poll(now))
						{
							perfBtn.SuppressNextClick = true;
							performanceHold.Cancel();
							await Run(new Request { Action = "mode", Mode = 3, RestoreAutoCooling = false }, null);
							return;
						}
						if (!performanceHold.IsHolding && performanceHold.TriggeredTurbo)
						{
							perfBtn.SuppressNextClick = true;
						}
						performanceHold.Cancel();
					};
					perfBtn.MouseCaptureChanged += (s, e) =>
					{
						if (performanceHold.IsHolding) cancelHold();
					};
				}
				modeGrid.Controls.Add(button, i, 0);
			}
			softPanel2.Controls.Add(modeGrid);

			Button button2 = Button("Restore auto cooling", async delegate
			{
				await Run(new Request
				{
					Action = "auto"
				}, null);
			});
			button2.SetBounds(S(22), S(112), S(200), S(34));
			softPanel2.Controls.Add(button2);
			coolingPill.SetBounds(S(232), S(116), S(260), S(24));
			softPanel2.Controls.Add(coolingPill);

			overviewEnergySaver = new CheckBox
			{
				Text = "Turn on Windows Energy saver in Quiet mode (cool, quiet & best battery)",
				Checked = autoEnergySaver,
				Location = new Point(S(22), S(152)),
				Font = new Font("Segoe UI", 9.5f),
				ForeColor = ink
			};
			ConfigureWrappingCheckBox(overviewEnergySaver, softPanel2.Width);
			overviewEnergySaver.CheckedChanged += (s, e) => {
				if (overviewEnergySaver.Checked != autoEnergySaver) SetAutoEnergySaver(overviewEnergySaver.Checked);
			};
			softPanel2.Controls.Add(overviewEnergySaver);
			RegisterCardReflow(softPanel2, () => {
				int w = Math.Max(S(160), softPanel2.Width - S(44));
				if (softPanel2.Controls.Count > 0 && softPanel2.Controls[0] is Label) {
					Label t = (Label)softPanel2.Controls[0];
					t.Location = new Point(S(22), S(12));
					t.Width = w;
					t.Height = MeasureLabelHeight(t, w, S(28));
				}
				perfSub.Left = S(23);
				perfSub.Width = Math.Max(S(160), softPanel2.Width - S(46));
				perfSub.Top = softPanel2.Controls.Count > 0 ? (softPanel2.Controls[0].Bottom + S(2)) : S(42);
				perfSub.Height = MeasureLabelHeight(perfSub, perfSub.Width, S(20));
				modeGrid.SetBounds(S(22), perfSub.Bottom + S(5), w, S(38));
				int coolTop = modeGrid.Bottom + S(5);
				int btnW = Math.Min(S(200), Math.Max(S(130), w / 2));
				button2.SetBounds(S(22), coolTop, btnW, S(32));
				coolingPill.SetBounds(button2.Right + S(10), coolTop + S(4), Math.Max(S(120), softPanel2.Width - button2.Right - S(32)), S(24));
				ConfigureWrappingCheckBox(overviewEnergySaver, softPanel2.Width);
				overviewEnergySaver.Location = new Point(S(22), button2.Bottom + S(5));
				softPanel2.Height = Math.Max(S(154), overviewEnergySaver.Bottom + S(8));
			});

			SoftPanel softPanel3 = Card("System health", "", 218);
			Label healthTitle = softPanel3.Controls.OfType<Label>().FirstOrDefault();
			if (healthTitle != null) healthTitle.Width = Math.Max(S(80), softPanel3.Width - S(230));
			healthSyncPill.SetBounds(softPanel3.Width - S(204), S(12), S(180), S(24));
			healthSyncPill.Anchor = AnchorStyles.Top | AnchorStyles.Left;
			softPanel3.Controls.Add(healthSyncPill);
			healthSyncPill.BringToFront();
			Label healthSub = Label("Temperatures, fans, memory and storage · updates automatically", 9.5f);
			healthSub.SetBounds(S(23), S(42), softPanel3.Width - S(46), S(20));
			healthSub.Anchor = AnchorStyles.Top | AnchorStyles.Left;
			softPanel3.Controls.Add(healthSub);

			var healthTopGrid = new BufferedTableLayoutPanel
			{
				Location = new Point(S(22), S(66)),
				Height = S(66),
				Width = softPanel3.Width - S(44),
				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
				ColumnCount = 3,
				RowCount = 1,
				BackColor = Color.Transparent
			};
			for (int i = 0; i < 3; i++)
			{
				healthTopGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
			}
			healthTopGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			MetricTile[] sensorTiles = new MetricTile[] { gpuTile, cpuFanTile, gpuFanTile };
			for (int i = 0; i < 3; i++)
			{
				sensorTiles[i].Dock = DockStyle.Fill;
				sensorTiles[i].Margin = new Padding(0, 0, i < 2 ? S(10) : 0, 0);
				healthTopGrid.Controls.Add(sensorTiles[i], i, 0);
			}
			softPanel3.Controls.Add(healthTopGrid);

			var healthBottomGrid = new BufferedTableLayoutPanel
			{
				Location = new Point(S(22), S(138)),
				Height = S(76),
				Width = softPanel3.Width - S(44),
				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
				ColumnCount = 2,
				RowCount = 1,
				BackColor = Color.Transparent
			};
			for (int i = 0; i < 2; i++)
			{
				healthBottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			}
			healthBottomGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			MetricTile[] usageTiles = new MetricTile[] { memoryTile, diskTile };
			for (int i = 0; i < 2; i++)
			{
				usageTiles[i].Dock = DockStyle.Fill;
				usageTiles[i].Margin = new Padding(0, 0, i == 0 ? S(10) : 0, 0);
				healthBottomGrid.Controls.Add(usageTiles[i], i, 0);
			}
			softPanel3.Controls.Add(healthBottomGrid);
			RegisterCardReflow(softPanel3, () => {
				healthSyncPill.SetBounds(softPanel3.Width - S(204), S(12), S(180), S(24));
				if (healthTitle != null && !healthTitle.IsDisposed) {
					healthTitle.Location = new Point(S(22), S(12));
					healthTitle.Width = Math.Max(S(80), healthSyncPill.Left - healthTitle.Left - S(8));
					healthTitle.Height = MeasureLabelHeight(healthTitle, healthTitle.Width, S(28));
				}
				int w = Math.Max(S(160), softPanel3.Width - S(44));
				healthSub.Left = S(23);
				healthSub.Width = Math.Max(S(160), softPanel3.Width - S(46));
				healthSub.Top = (healthTitle != null && !healthTitle.IsDisposed) ? Math.Max(S(40), Math.Max(healthTitle.Bottom, healthSyncPill.Bottom) + S(2)) : S(40);
				healthSub.Height = MeasureLabelHeight(healthSub, healthSub.Width, S(20));
				healthTopGrid.SetBounds(S(22), healthSub.Bottom + S(5), w, S(62));
				healthBottomGrid.SetBounds(S(22), healthTopGrid.Bottom + S(5), w, S(72));
				softPanel3.Height = healthBottomGrid.Bottom + S(10);
			});
		}

		private ComboBox Effect(Panel parent, string[] items, string selected, int y)
		{
			ComboBox comboBox = new WheelSafeComboBox();
			comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
			comboBox.Location = new Point(S(22), y);
			comboBox.Width = S(174);
            comboBox.FlatStyle=FlatStyle.Standard;
            comboBox.Font=new Font("Segoe UI",10f);
            comboBox.DrawMode=DrawMode.OwnerDrawFixed;comboBox.ItemHeight=S(30);
            comboBox.DropDownHeight=S(360);comboBox.IntegralHeight=false;comboBox.DropDownWidth=S(300);
            comboBox.BackColor=Theme.Ivory;comboBox.ForeColor=Theme.Ink;
            comboBox.AccessibleDescription="Choose an effect. Click the full field or use Alt + Down.";
            comboBox.DrawItem+=(s,e)=>{
                if(e.Index<0)return;bool highlighted=(e.State&DrawItemState.Selected)!=0;
                float ds=comboBox.DeviceDpi>0?comboBox.DeviceDpi/96f:DpiScale;
                using(var fill=new System.Drawing.Drawing2D.LinearGradientBrush(e.Bounds,highlighted?Theme.Pearl:Theme.White,Theme.White,0f))e.Graphics.FillRectangle(fill,e.Bounds);
                string name=Convert.ToString(comboBox.Items[e.Index]);string text=name;bool list=(e.State&DrawItemState.ComboBoxEdit)==0;
                if(text=="Four zones"&&list)text+=" · zone settings";
                var bounds=e.Bounds;bounds.Inflate(-(int)(10*ds),0);
                if(list&&ExtraEffects.Contains(name)){
                    var colours=ExtraEffects.Frame(new Request{Effect=name,Brightness=100},.5);bounds.Width-=(int)(56*ds);
                    int sw=(int)Math.Max(6,8*ds),sh=(int)Math.Max(9,12*ds),step=(int)(11*ds),sy=e.Bounds.Top+(e.Bounds.Height-sh)/2,sx=e.Bounds.Right-(int)(52*ds);
                    for(int zone=0;zone<4;zone++)using(var brush=new SolidBrush(colours[zone]))e.Graphics.FillRectangle(brush,sx+zone*step,sy,sw,sh);
                }
                if(highlighted)using(var marker=new SolidBrush(Theme.OrangeEnd))e.Graphics.FillRectangle(marker,e.Bounds.Left,e.Bounds.Top+(int)(3*ds),(int)Math.Max(2,3*ds),e.Bounds.Height-(int)(6*ds));
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
			i.SetBounds(x, y, S(280), S(24));
			parent.Controls.Add(i);
			TrackBar track = new WheelSafeTrackBar
			{
				Minimum = ((maximum == 9) ? 1 : 0),
				Maximum = maximum,
				Value = value,
				TickStyle = TickStyle.None,
				Location = new Point(x - S(4), y + S(24)),
				Width = Math.Min(S(265), Math.Max(S(150), parent.Width - x - S(22))),
				Height = S(38)
			};
			track.ValueChanged += delegate
			{
				i.Text = caption + "  " + track.Value + ((maximum == 100) ? "%" : "");
			};
			parent.Controls.Add(track);
			if (x >= S(300))
			{
				bool adjusting = false;
				Action reflow = delegate
				{
					if (adjusting || parent.IsDisposed)
					{
						return;
					}
					adjusting = true;
					try
					{
						float num = (float)parent.DeviceDpi / 96f;
						int num2 = parent.ClientSize.Width / 2 + (int)(12f * num);
						i.Left = num2;
						i.Width = Math.Max(S(80), parent.ClientSize.Width - num2 - (int)(22f * num));
						track.Left = num2 - (int)(4f * num);
						track.Width = Math.Max(S(80), parent.ClientSize.Width - num2 - (int)(22f * num));
						foreach (TrackBar item in parent.Controls.OfType<TrackBar>())
						{
							if (item != track)
							{
								item.Width = Math.Min((int)(265f * num), Math.Max(S(80), parent.ClientSize.Width / 2 - (int)(32f * num)));
							}
						}
					}
					finally
					{
						adjusting = false;
					}
				};
				parent.SizeChanged += delegate
				{
					if (!parent.IsDisposed)
					{
						reflow();
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
			button.SetBounds(x, y, S(128), S(38));
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
			lightingTabs = new BufferedPanel { Height = S(36), BackColor = Color.Transparent };
			keyboardTabBtn = (RaisedButton)Button("Keyboard zone", delegate { lightingShowRear = false; FitCards(); SetPreviewAnimation(page, Visible && !resizingWindow); });
			rearTabBtn = (RaisedButton)Button("Rear light bar", delegate { lightingShowRear = true; FitCards(); SetPreviewAnimation(page, Visible && !resizingWindow); });
			keyboardTabBtn.Navigation = false; rearTabBtn.Navigation = false;
			lightingTabs.Controls.Add(keyboardTabBtn);
			lightingTabs.Controls.Add(rearTabBtn);
			page.Controls.Add(lightingTabs);

			SoftPanel softPanel = Card("A glow for your keyboard", "Preview your colours and effect below. Nothing changes until Apply.", 526);
			keyboardCardRef = softPanel;
			Label keyboardHint = softPanel.Controls.OfType<Label>().Last();
			var tableLayoutPanel2 = new BufferedTableLayoutPanel
			{
				Location = new Point(S(22), S(96)),
				Width = softPanel.Width - S(44),
				Height = S(104),
				ColumnCount = 2,
				RowCount = 1,
				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
				BackColor = Color.Transparent
			};
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			softPanel.Controls.Add(tableLayoutPanel2);
			EffectSection panel2 = new EffectSection
			{
				Dock = DockStyle.Fill,
				Margin = new Padding(0, 0, S(8), 0),
				Accent = Color.FromArgb(0, 113, 232)
			};
			EffectSection panel4 = new EffectSection
			{
				Dock = DockStyle.Fill,
				Margin = new Padding(S(8), 0, 0, 0),
				Accent = Color.FromArgb(111, 0, 255)
			};
			tableLayoutPanel2.Controls.Add(panel2, 0, 0);
			tableLayoutPanel2.Controls.Add(panel4, 1, 0);
			RaisedButton officialTab = (RaisedButton)Button("Official · 7 effects", delegate
			{
				keyboard.Effect = kbEffect.Text;
				keyboardChoiceChanged();
                kbEffect.Focus();kbEffect.DroppedDown=true;
			});
			officialTab.Dock = DockStyle.Top;
			officialTab.Height = S(32);officialTab.Dropdown=true;
			panel2.Controls.Add(officialTab);
			RaisedButton extraTab = (RaisedButton)Button("Extra · " + ExtraEffects.Names.Length + " animations", delegate
			{
				keyboard.Effect = kbExtraEffect.Text;
				keyboardChoiceChanged();
                kbExtraEffect.Focus();kbExtraEffect.DroppedDown=true;
			});
			extraTab.Dock = DockStyle.Top;
			extraTab.Height = S(32);extraTab.Dropdown=true;
			panel4.Controls.Add(extraTab);
			kbEffect = Effect(panel2, new string[7] { "Static", "Breathing", "Wave", "Clock", "Rainbow", "Flow", "Four zones" }, keyboard.Effect, S(40));
			kbEffect.Width = panel2.Width - S(24);
			kbEffect.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			kbEffect.Left = S(12);
			kbExtraEffect = Effect(panel4, ExtraEffects.Names, keyboard.Effect, S(40));
			kbExtraEffect.Width = panel4.Width - S(24);
			kbExtraEffect.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			kbExtraEffect.Left = S(12);
			var officialHint=Label("Built-in controller effects",8f);officialHint.SetBounds(S(12),S(76),panel2.Width-S(24),S(20));officialHint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;panel2.Controls.Add(officialHint);
            var extraHint=Label("App animations · four zones",8f);extraHint.SetBounds(S(12),S(76),panel4.Width-S(24),S(20));extraHint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;panel4.Controls.Add(extraHint);
            zoneButtons = new Button[4];
			var tableLayoutPanel4 = new BufferedTableLayoutPanel
			{
				Location = new Point(S(22), S(214)),
				Width = softPanel.Width - S(44),
				Height = S(38),
				ColumnCount = 4,
				RowCount = 1,
				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
				BackColor = Color.Transparent
			};
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
				zoneButtons[i].Margin = new Padding(0, 0, (i != 3) ? S(8) : 0, 0);
			}
			int kbBrightCaptionIdx = softPanel.Controls.Count;
			kbBright = Slider(softPanel, keyboard.Brightness, S(22), S(262), 100, "Brightness");
			Label kbBrightCaption = (Label)softPanel.Controls[kbBrightCaptionIdx];
			kbBright.ValueChanged += delegate
			{
				keyboard.Brightness = kbBright.Value;
			};
			Label zoneLabel = Label("Zone effect", 9f);
			zoneLabel.SetBounds(S(320), S(262), S(200), S(22));
			softPanel.Controls.Add(zoneLabel);
			ComboBox zones = Effect(softPanel, new string[4] { "Static", "Breathing", "Clock", "Rainbow" }, keyboard.ZoneEffect, S(286));
			zones.Left = S(320);
			zones.SelectedIndexChanged += delegate
			{
				keyboard.ZoneEffect = zones.Text;
			};
			var speedPanel = new BufferedPanel
			{
				Location = new Point(S(310), S(262)),
				Width = S(245),
				Height = S(58),
				BackColor = Theme.White
			};
			softPanel.Controls.Add(speedPanel);
			TrackBar animationSpeed = Slider(speedPanel, keyboard.Speed, S(4), 0, 9, "Animation speed");
			animationSpeed.ValueChanged += delegate
			{
				keyboard.Speed = animationSpeed.Value;
			};
            var direction=Effect(softPanel,new[]{"Left → Right","Right → Left"},keyboard.Direction==1?"Right → Left":"Left → Right",S(324));
            direction.AccessibleName="Animation direction";direction.Left=S(320);
            direction.SelectedIndexChanged+=(s,e)=>{keyboard.Direction=direction.SelectedIndex;};
			CheckBox awake = new CheckBox
			{
				Text = "Keep keyboard awake",
				Checked = keyboard.KeepAwake,
				Location = new Point(S(22), S(334))
			};
			ConfigureWrappingCheckBox(awake, softPanel.Width);
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
            applyKeyboardButton.Font=new Font("Segoe UI",10.5f,FontStyle.Bold);
            var keyboardActions=new BufferedPanel{Location=new Point(S(22),S(366)),Size=new Size(softPanel.Width-S(44),S(48)),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,BackColor=Color.Transparent};
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
            stopKeyboardButton.Font=new Font("Segoe UI",10.5f,FontStyle.Bold);
			stopKeyboardButton.Enabled = keyboardTask != null;
            keyboardActions.Controls.Add(button);keyboardActions.Controls.Add(stopKeyboardButton);
            Action arrangeActions=()=>{int gap=S(14),w=Math.Min(S(242),Math.Max(S(90),(keyboardActions.ClientSize.Width-gap)/2)),left=(keyboardActions.ClientSize.Width-2*w-gap)/2;button.SetBounds(left,0,w,S(48));stopKeyboardButton.SetBounds(left+w+gap,0,w,S(48));};
            keyboardActions.Resize+=(s,e)=>arrangeActions();arrangeActions();
			kbOutcome = Label(keyboardResult, 9f);
			kbOutcome.SetBounds(S(22), S(422), softPanel.Width - S(44), S(28));
			kbOutcome.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			softPanel.Controls.Add(kbOutcome);
			LivePreview previewWidget = AttachLightingPreview(softPanel, () => Copy(keyboard, "keyboard"), "keyboard");
			bool layingOutKeyboard = false;
			Action layoutKeyboardCard = () => {
				if (layingOutKeyboard || softPanel.IsDisposed) return;
				layingOutKeyboard = true;
				try {
					float s = softPanel.DeviceDpi > 0 ? softPanel.DeviceDpi / 96f : DpiScale;
					int padX = S(20);
					int innerW = Math.Max(S(200), softPanel.Width - 2 * padX);
					if (softPanel.Controls.Count > 0 && softPanel.Controls[0] is Label) {
						Label t = (Label)softPanel.Controls[0];
						t.Location = new Point(padX, S(12));
						t.Width = innerW;
						t.Height = MeasureLabelHeight(t, innerW, S(28));
						keyboardHint.Top = t.Bottom + S(2);
					}
					keyboardHint.Left = padX;
					keyboardHint.Width = innerW;
					keyboardHint.Height = MeasureLabelHeight(keyboardHint, innerW, S(20));
					previewWidget.SetBounds(padX, keyboardHint.Bottom + S(6), innerW, S(112));
					int effTop = previewWidget.Bottom + S(8);
					tableLayoutPanel2.Width = innerW;
					int halfEffW = Math.Max(S(80), (innerW - S(16)) / 2 - S(24));
					officialHint.Top = kbEffect.Bottom + S(4);
					officialHint.Width = halfEffW;
					officialHint.Height = MeasureLabelHeight(officialHint, halfEffW, S(18));
					extraHint.Top = kbExtraEffect.Bottom + S(4);
					extraHint.Width = halfEffW;
					extraHint.Height = MeasureLabelHeight(extraHint, halfEffW, S(18));
					int effH = Math.Max(S(102), Math.Max(officialHint.Bottom, extraHint.Bottom) + S(6));
					tableLayoutPanel2.SetBounds(padX, effTop, innerW, effH);
					tableLayoutPanel4.SetBounds(padX, tableLayoutPanel2.Bottom + S(8), innerW, S(38));
					int sliderTop = tableLayoutPanel4.Bottom + S(8);
					int left = softPanel.ClientSize.Width / 2 + (int)(10 * s);
					int leftTrackW = Math.Max(S(90), softPanel.ClientSize.Width / 2 - padX - (int)(10 * s));
					kbBrightCaption.SetBounds(padX, sliderTop, leftTrackW, S(22));
					kbBright.SetBounds(padX - S(4), sliderTop + S(22), leftTrackW, S(34));
					speedPanel.Left = left;
					speedPanel.Top = sliderTop;
					speedPanel.Width = Math.Max(S(90), softPanel.ClientSize.Width - left - padX);
					foreach (Label speedCaption in speedPanel.Controls.OfType<Label>()) {
						speedCaption.Location = new Point(S(4), 0);
						speedCaption.Width = Math.Max(S(60), speedPanel.ClientSize.Width - speedCaption.Left);
						speedCaption.Height = S(22);
					}
					animationSpeed.Location = new Point(0, S(22));
					animationSpeed.Width = speedPanel.ClientSize.Width;
					speedPanel.Height = Math.Max(S(56), animationSpeed.Bottom + S(2));
					zoneLabel.SetBounds(left, sliderTop, speedPanel.Width, S(22));
					zones.Location = new Point(left, sliderTop + S(22));
					zones.Width = Math.Min((int)(230 * s), speedPanel.Width);
					direction.Location = new Point(left, speedPanel.Bottom + S(2));
					direction.Width = speedPanel.Width;
					int rightColBottom = speedPanel.Visible ? direction.Bottom : (zones.Visible ? zones.Bottom : kbBright.Bottom);
					int afterSliderTop = Math.Max(kbBright.Bottom, rightColBottom) + S(4);
					ConfigureWrappingCheckBox(awake, softPanel.Width, 20);
					awake.Location = new Point(padX, afterSliderTop);
					keyboardActions.SetBounds(padX, awake.Bottom + S(8), innerW, S(48));
					arrangeActions();
					kbOutcome.Width = innerW;
					kbOutcome.Location = new Point(padX, keyboardActions.Bottom + S(6));
					kbOutcome.Height = MeasureLabelHeight(kbOutcome, innerW, S(20));
					softPanel.Height = kbOutcome.Bottom + S(10);
				} finally {
					layingOutKeyboard = false;
				}
			};
			RegisterCardReflow(softPanel, layoutKeyboardCard);
			keyboardChoiceChanged = delegate
			{
				bool flag = ExtraEffects.Contains(keyboard.Effect);
				officialTab.Selected = !flag;
                panel2.Selected = !flag;panel4.Selected = flag;
                officialHint.Text="Click to choose · 7 effects";
                extraHint.Text="Click to choose · "+ExtraEffects.Names.Length+" animations";
                button.Text=flag?"Apply Extra effect":"Apply Official effect";
				extraTab.Selected = flag;
				speedPanel.Visible = flag;
                direction.Visible=flag;direction.Enabled=ExtraEffects.HasDirection(keyboard.Effect);
                string motion=keyboard.Effect=="Zoom Bloom"?"Centre ↔ Edges":keyboard.Effect=="Starlight"?"Gentle twinkle":"Symmetric motion";
                direction.Items[0]=direction.Enabled?"Left → Right":motion;
                direction.Items[1]=direction.Enabled?"Right → Left":motion;
				ComboBox comboBox = zones;
				bool visible = (zoneLabel.Visible = !flag && keyboard.Effect == "Four zones");
				comboBox.Visible = visible;
				keyboardHint.Text = (flag ? (ExtraEffects.Description(keyboard.Effect) + " Four-zone animation · keep Essentials open.") : "Official controller effects · brightness 0% turns off.");
				for (int j = 0; j < 4; j++)
				{
					zoneButtons[j].Visible = !flag || j < 2;
					zoneButtons[j].Enabled = (flag ? (j < 2 && ExtraEffects.UsesColours(keyboard.Effect)) : (keyboard.Effect == "Four zones" || (j == 0 && keyboard.Effect != "Rainbow")));
					zoneButtons[j].Text = (flag ? ((j == 0) ? "Colour 1" : "Colour 2") : ("Zone " + (j + 1)));
					zoneButtons[j].Tag = zoneButtons[j].Text;
				}
				layoutKeyboardCard();
			};
			Action applyOfficialChoice = delegate
			{
				if (!syncingKeyboardChoice)
				{
					keyboard.Effect = kbEffect.Text;
					keyboardChoiceChanged();
				}
			};
			kbEffect.SelectedIndexChanged += delegate { applyOfficialChoice(); };
			kbEffect.SelectionChangeCommitted += delegate { applyOfficialChoice(); };
			Action applyExtraChoice = delegate
			{
				if (!syncingKeyboardChoice)
				{
					keyboard.Effect = kbExtraEffect.Text;
					keyboardChoiceChanged();
				}
			};
			kbExtraEffect.SelectedIndexChanged += delegate { applyExtraChoice(); };
			kbExtraEffect.SelectionChangeCommitted += delegate { applyExtraChoice(); };
			keyboardChoiceChanged();
            BuildRearCard();
		}

        private void BuildRearCard(){
            var card=Card("Rear light bar","Choose a look, preview it, then apply it to your device.",520);
            rearCardRef=card;
            Label cardDesc=card.Controls.OfType<Label>().Last();
            rearIllustration=new LivePreview{Target="rear",Selection=()=>rearState.Illustration(rear),Caption=()=>rearState.Caption(rear),Badge=()=>rearState.Badge};
            rearIllustration.SetBounds(S(20),S(68),card.Width-S(40),S(110));rearIllustration.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;card.Controls.Add(rearIllustration);
            rearEditor=new BufferedPanel{Location=new Point(S(20),S(186)),Size=new Size(card.Width-S(40),S(168)),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,BackColor=Color.Transparent};card.Controls.Add(rearEditor);
            var effectLabel=Label("Effect",9,true);var firstLabel=Label("Colour 1",9,true);var secondLabel=Label("Colour 2",9,true);
            rearEditor.Controls.Add(effectLabel);rearEditor.Controls.Add(firstLabel);rearEditor.Controls.Add(secondLabel);
            rearEffect=Effect(rearEditor,RearLighting.Names,rear.Effect,S(22));rearEffect.AccessibleName="Rear light effect";
            rearColor=ColorButton(rearEditor,rear.Colors[0],"Colour 1",0,S(22),hex=>{rear.Colors[0]=hex;RearEdited();});
            rearSecondColor=ColorButton(rearEditor,rear.Colors[1],"Colour 2",0,S(22),hex=>{rear.Colors[1]=hex;RearEdited();});
            rearEffectHint=Label(RearLighting.Description(rear.Effect),9);rearEffectHint.SetBounds(0,S(66),rearEditor.Width,S(28));rearEffectHint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;rearEditor.Controls.Add(rearEffectHint);
            var brightnessPanel=new BufferedPanel{Location=new Point(0,S(98)),Size=new Size(S(250),S(58)),BackColor=Theme.White};var speedPanel=new BufferedPanel{Location=new Point(S(280),S(98)),Size=new Size(S(250),S(58)),BackColor=Theme.White};
            rearEditor.Controls.Add(brightnessPanel);rearEditor.Controls.Add(speedPanel);
            rearBright=Slider(brightnessPanel,rear.Brightness,S(4),0,100,"Brightness");rearSpeed=Slider(speedPanel,rear.Speed,S(4),0,9,"Breathing speed");
            rearEffect.SelectedIndexChanged+=(s,e)=>{rear.Effect=rearEffect.Text;RearEdited();};
            rearBright.ValueChanged+=(s,e)=>{rear.Brightness=rearBright.Value;RearEdited();};rearSpeed.ValueChanged+=(s,e)=>{rear.Speed=rearSpeed.Value;RearEdited();};
            rearDraftToggle=new CheckBox{Text="Preview selected look · device stays unchanged",Checked=rearState.PreviewDraft,Location=new Point(S(20),S(362))};
            ConfigureWrappingCheckBox(rearDraftToggle,card.Width,20);
            rearDraftToggle.CheckedChanged+=(s,e)=>{rearState.PreviewDraft=rearDraftToggle.Checked;UpdateRearIndicator();};card.Controls.Add(rearDraftToggle);
            var actions=new BufferedPanel{Location=new Point(S(20),S(392)),Size=new Size(card.Width-S(40),S(50)),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,BackColor=Color.Transparent};card.Controls.Add(actions);
            rearApplyButton=(RaisedButton)Button("Apply to bar",async()=>await RearAction(ApplyRear));rearApplyButton.Accent=Theme.OrangeEnd;rearApplyButton.Primary=true;
            rearOffButton=(RaisedButton)Button("Turn off",async()=>await RearAction(async()=>await Run(new Request{Action="rear-off"},null)));rearOffButton.Accent=Color.FromArgb(226,79,70);
            rearStopMusicButton=(RaisedButton)Button("Stop music",async()=>await RearAction(async()=>{await StopMusic();status.Text=rearResult;}));
            foreach(var button in new[]{rearApplyButton,rearOffButton,rearStopMusicButton}){button.Font=new Font("Segoe UI",10f,FontStyle.Bold);actions.Controls.Add(button);}
            rearOutcome=Label(rearState.Caption(rear),9.5f,true);rearOutcome.SetBounds(S(20),S(450),card.Width-S(40),S(26));rearOutcome.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;card.Controls.Add(rearOutcome);
            var note=Label("Simulated preview · music follows playback.",8.5f);note.SetBounds(S(20),S(486),card.Width-S(210),S(26));note.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;card.Controls.Add(note);
            var check=Button("Check status",async()=>await RearAction(async()=>await Run(new Request{Action="refresh"},null)));check.SetBounds(card.Width-S(176),S(480),S(156),S(36));check.Anchor=AnchorStyles.Top|AnchorStyles.Right;card.Controls.Add(check);
            bool layingOutRear=false;
            Action layout=()=>{
                if(layingOutRear||card.IsDisposed)return;
                layingOutRear=true;
                try{
                    float s=card.DeviceDpi>0?card.DeviceDpi/96f:DpiScale;
                    int padX=S(20),innerW=Math.Max(S(200),card.Width-2*padX);
                    if(card.Controls.Count>0&&card.Controls[0] is Label){
                        Label t=(Label)card.Controls[0];
                        t.Location=new Point(padX,S(12));
                        t.Width=innerW;
                        t.Height=MeasureLabelHeight(t,innerW,S(28));
                        cardDesc.Top=t.Bottom+S(2);
                    }
                    cardDesc.Left=padX;
                    cardDesc.Width=innerW;
                    cardDesc.Height=MeasureLabelHeight(cardDesc,innerW,S(20));
                    rearIllustration.SetBounds(padX,cardDesc.Bottom+S(6),innerW,S(110));
                    rearEditor.Location=new Point(padX,rearIllustration.Bottom+S(8));
                    rearEditor.Width=innerW;
                    int gap=(int)(10*s),width=rearEditor.ClientSize.Width,col=Math.Max(S(60),(width-2*gap)/3);
                    effectLabel.SetBounds(0,0,col,(int)(20*s));firstLabel.SetBounds(col+gap,0,col,(int)(20*s));secondLabel.SetBounds(2*(col+gap),0,col,(int)(20*s));
                    rearEffect.SetBounds(0,(int)(22*s),col,(int)(36*s));rearColor.SetBounds(col+gap,(int)(22*s),col,(int)(38*s));rearSecondColor.SetBounds(2*(col+gap),(int)(22*s),col,(int)(38*s));
                    int rowBottom=Math.Max(rearEffect.Bottom,rearColor.Bottom);
                    rearEffectHint.Location=new Point(0,rowBottom+S(4));
                    rearEffectHint.Width=width;
                    rearEffectHint.Height=MeasureLabelHeight(rearEffectHint,width,S(22));
                    int half=Math.Max(S(80),(width-gap)/2);
                    int sliderRowTop=rearEffectHint.Bottom+S(4);
                    rearBright.Location=new Point(0,S(22));rearBright.Width=half;
                    rearSpeed.Location=new Point(0,S(22));rearSpeed.Width=half;
                    int sliderH=Math.Max(S(56),Math.Max(rearBright.Bottom,rearSpeed.Bottom)+S(2));
                    brightnessPanel.SetBounds(0,sliderRowTop,half,sliderH);
                    speedPanel.SetBounds(half+gap,sliderRowTop,half,sliderH);
                    foreach(var pPanel in new[]{brightnessPanel,speedPanel})foreach(Label label in pPanel.Controls.OfType<Label>()){label.Location=new Point(S(4),0);label.Width=Math.Max(S(40),pPanel.Width-label.Left);label.Height=S(22);}
                    rearEditor.Height=brightnessPanel.Bottom+S(2);
                    ConfigureWrappingCheckBox(rearDraftToggle,card.Width,20);
                    rearDraftToggle.Location=new Point(padX,rearEditor.Bottom+S(6));
                    actions.SetBounds(padX,rearDraftToggle.Bottom+S(6),innerW,S(50));
                    int actionWidth=actions.ClientSize.Width,small=Math.Max(S(64),(actionWidth-2*gap)/4),primary=actionWidth-2*small-2*gap;
                    rearApplyButton.SetBounds(0,0,primary,actions.Height);rearOffButton.SetBounds(primary+gap,0,small,actions.Height);rearStopMusicButton.SetBounds(primary+small+2*gap,0,small,actions.Height);
                    rearOutcome.Location=new Point(padX,actions.Bottom+S(6));
                    rearOutcome.Width=innerW;
                    rearOutcome.Height=MeasureLabelHeight(rearOutcome,innerW,S(22));
                    int footerTop=rearOutcome.Bottom+S(6);
                    int checkW=S(156),checkH=S(36);
                    check.SetBounds(card.Width-padX-checkW,footerTop,checkW,checkH);
                    note.Location=new Point(padX,footerTop+S(4));
                    note.Width=Math.Max(S(80),check.Left-padX-S(12));
                    note.Height=MeasureLabelHeight(note,note.Width,S(22));
                    card.Height=Math.Max(check.Bottom,note.Bottom)+S(10);
                }finally{
                    layingOutRear=false;
                }
            };
            RegisterCardReflow(card,layout);
            layout();UpdateRearIndicator();
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
            settingsTabs = new BufferedPanel { Height = S(36), BackColor = Color.Transparent, Visible = false };
            settingsTabPrimaryBtn = (RaisedButton)Button("General & power", delegate { settingsShowSecondary = false; FitCards(); });
            settingsTabSecondaryBtn = (RaisedButton)Button("Shortcuts & updates", delegate { settingsShowSecondary = true; FitCards(); });
            settingsTabPrimaryBtn.Navigation = false; settingsTabSecondaryBtn.Navigation = false;
            settingsTabs.Controls.Add(settingsTabPrimaryBtn);
            settingsTabs.Controls.Add(settingsTabSecondaryBtn);
            page.Controls.Add(settingsTabs);

            var prefCard = Card("General & startup", "Sounds, Energy saver, startup, and language.", 250);
            settingsPrefCard = prefCard;
            Label prefDesc = prefCard.Controls.OfType<Label>().Last();

            var sounds = new CheckBox { Text = "Play performance mode sounds", Checked = modeSounds, Font = new Font("Segoe UI", 9.5f), ForeColor = ink };
            sounds.CheckedChanged += (s, e) => { modeSounds = sounds.Checked; try { Directory.CreateDirectory(Store.DirectoryPath); File.WriteAllText(Path.Combine(Store.DirectoryPath, "mode-sounds.txt"), modeSounds.ToString()); } catch (Exception error) { status.Text = "Sound preference: " + error.Message; } };
            prefCard.Controls.Add(sounds);

            settingsEnergySaver = new CheckBox { Text = "Turn on Windows Energy saver in Quiet mode", Checked = autoEnergySaver, Font = new Font("Segoe UI", 9.5f), ForeColor = ink };
            settingsEnergySaver.CheckedChanged += (s, e) => { if (settingsEnergySaver.Checked != autoEnergySaver) SetAutoEnergySaver(settingsEnergySaver.Checked); };
            prefCard.Controls.Add(settingsEnergySaver);

            CheckBox lockKey = new CheckBox { Text = "Disable Windows key while gaming", Checked = keyLock.Enabled, Font = new Font("Segoe UI", 9.5f), ForeColor = ink };
            bool revertingLock = false;
            lockKey.CheckedChanged += delegate {
                if (!revertingLock) {
                    if (ConfirmPreview(new Request { Action = "windows-key", Enabled = lockKey.Checked })) {
                        try { keyLock.Set(lockKey.Checked); status.Text = (lockKey.Checked ? "Windows key locked. Uncheck here or exit GTBook to release it." : "Windows key restored."); return; }
                        catch (Exception ex3) { status.Text = ex3.Message; return; }
                    }
                    revertingLock = true; lockKey.Checked = !lockKey.Checked; revertingLock = false;
                }
            };
            prefCard.Controls.Add(lockKey);

            CheckBox startupBox = new CheckBox { Text = "Start GTBook with Windows", Checked = false, Enabled = false, Font = new Font("Segoe UI", 9.5f), ForeColor = ink };
            prefCard.Controls.Add(startupBox);
            LoadStartupCheckbox(startupBox);

            Label langLabel = Label("Language", 8.5f, true);
            prefCard.Controls.Add(langLabel);
            ComboBox choice = Effect(prefCard, Languages.Names, Languages.Current, S(180));
            choice.SelectedIndexChanged += delegate {
                try { Languages.Select(choice.Text); ClearCachedPages(); ShowPage("Settings"); status.Text = "Language preference saved."; }
                catch (Exception ex2) { status.Text = ex2.Message; }
            };
            Button hideBtn = Button("Hide to tray", delegate { Hide(); });
            prefCard.Controls.Add(hideBtn);

            RegisterCardReflow(prefCard, () => {
                ReflowCardHeader(prefCard, prefDesc);
                int padX = S(20), innerW = Math.Max(S(160), prefCard.Width - 2 * padX);
                prefDesc.Left = padX; prefDesc.Width = innerW;
                int y = prefDesc.Bottom + S(8);
                foreach (var cb in new[] { sounds, settingsEnergySaver, lockKey, startupBox }) {
                    ConfigureWrappingCheckBox(cb, prefCard.Width, 20);
                    cb.Location = new Point(padX, y);
                    y = cb.Bottom + S(6);
                }
                langLabel.SetBounds(padX, y + S(2), innerW, S(18));
                int ctrlTop = langLabel.Bottom + S(4);
                int gap = S(10);
                int langW = Math.Max(S(110), (innerW - gap) / 2);
                int hideW = Math.Max(S(100), innerW - langW - gap);
                choice.SetBounds(padX, ctrlTop, langW, S(34));
                hideBtn.SetBounds(choice.Right + gap, ctrlTop - S(2), hideW, S(36));
                prefCard.Height = Math.Max(choice.Bottom, hideBtn.Bottom) + S(12);
            });

            var powerCard = Card("Cooling, USB & tools", "Firmware fans, off-mode USB charging, captures, and reset.", 250);
            settingsPowerCard = powerCard;
            Label powerDesc = powerCard.Controls.OfType<Label>().Last();
            Button fanMaxBtn = Button("Fan Max", async delegate { await Run(new Request { Action = "fan", Enabled = true }, null); });
            Button autoFanBtn = Button("Auto fans", async delegate { await Run(new Request { Action = "auto" }, null); });
            powerCard.Controls.Add(fanMaxBtn); powerCard.Controls.Add(autoFanBtn);

            Label usbHdr = Label("USB CHARGING WHILE OFF", 8f, true);
            usbHdr.ForeColor = Theme.OrangeInk;
            powerCard.Controls.Add(usbHdr);
            usbOn = (RaisedButton)Button("Charging ON", async delegate { await Run(new Request { Action = "usb", Enabled = true }, null); });
            usbOff = (RaisedButton)Button("Charging OFF", async delegate { await Run(new Request { Action = "usb", Enabled = false }, null); });
            powerCard.Controls.Add(usbOn); powerCard.Controls.Add(usbOff);
            usbState = Label("Reading firmware setting…", 9.5f, true);
            powerCard.Controls.Add(usbState);

            Button openShots = Button("Open screenshots", OpenScreenshots);
            Button resetLights = Button("Reset lighting", delegate {
                keyboard.Effect = "Static"; rear.Effect = "Static";
                keyboard.Brightness = (rear.Brightness = 65);
                for (int j = 0; j < 4; j++) keyboard.Colors[j] = (rear.Colors[j] = "#7600FF");
                RearEdited(); ClearCachedPage("Lighting"); ShowPage("Lighting");
                status.Text = "Default choices selected. Press Apply to send them to the device.";
            });
            powerCard.Controls.Add(openShots); powerCard.Controls.Add(resetLights);

            RegisterCardReflow(powerCard, () => {
                ReflowCardHeader(powerCard, powerDesc);
                int padX = S(20), innerW = Math.Max(S(160), powerCard.Width - 2 * padX), gap = S(8);
                powerDesc.Left = padX; powerDesc.Width = innerW;
                int bw = Math.Max(S(80), (innerW - gap) / 2);
                int y = powerDesc.Bottom + S(8);
                fanMaxBtn.SetBounds(padX, y, bw, S(36));
                autoFanBtn.SetBounds(fanMaxBtn.Right + gap, y, bw, S(36));
                usbHdr.SetBounds(padX, fanMaxBtn.Bottom + S(10), innerW, S(18));
                int uTop = usbHdr.Bottom + S(4);
                usbOn.SetBounds(padX, uTop, bw, S(36));
                usbOff.SetBounds(usbOn.Right + gap, uTop, bw, S(36));
                usbState.Location = new Point(padX, usbOn.Bottom + S(6));
                usbState.Width = innerW;
                usbState.Height = MeasureLabelHeight(usbState, innerW, S(20));
                int toolsTop = usbState.Bottom + S(10);
                openShots.SetBounds(padX, toolsTop, bw, S(36));
                resetLights.SetBounds(openShots.Right + gap, toolsTop, bw, S(36));
                powerCard.Height = openShots.Bottom + S(12);
            });

            SoftPanel keysCard = Card("Laptop shortcut keys", "Works from the tray too · PrtScn captures full screen.", 200);
            settingsKeysCard = keysCard;
            Label keysDesc = keysCard.Controls.OfType<Label>().Last();
            CheckBox enable = new CheckBox { Text = "Use Toolkit F1 / F2 / F3 controls", Checked = fnKeys, Font = new Font("Segoe UI", 9.5f), ForeColor = ink };
            bool revertingKeys = false;
            enable.CheckedChanged += delegate {
                if (!revertingKeys) {
                    bool @checked = enable.Checked;
                    if (ConfirmPreview(new Request { Action = "software-fn", Enabled = @checked })) {
                        fnKeys = @checked;
                        if (laptopKeys != null) laptopKeys.MediaKeys = @checked;
                        try { Directory.CreateDirectory(Store.DirectoryPath); File.WriteAllText(Path.Combine(Store.DirectoryPath, "fn-keys.txt"), @checked.ToString()); status.Text = "Laptop shortcut preference saved."; return; }
                        catch (Exception ex) { status.Text = ex.Message; return; }
                    }
                    revertingKeys = true; enable.Checked = !@checked; revertingKeys = false;
                }
            };
            keysCard.Controls.Add(enable);
            Label keysGuide = Label("F1 / F2: Screen brightness −10% / +10%\nF3 tap: Keyboard brightness · F3 hold 0.8s: Next effect\nHold Shift / Ctrl / Alt for normal F1–F3 · media uses Windows.", 9f);
            keysCard.Controls.Add(keysGuide);
            RegisterCardReflow(keysCard, () => {
                ReflowCardHeader(keysCard, keysDesc);
                int padX = S(20), innerW = Math.Max(S(160), keysCard.Width - 2 * padX);
                keysDesc.Left = padX; keysDesc.Width = innerW;
                ConfigureWrappingCheckBox(enable, keysCard.Width, 20);
                enable.Location = new Point(padX, keysDesc.Bottom + S(8));
                keysGuide.Location = new Point(padX, enable.Bottom + S(6));
                keysGuide.Width = innerW;
                keysGuide.Height = MeasureLabelHeight(keysGuide, innerW, S(54));
                keysCard.Height = keysGuide.Bottom + S(12);
            });

            SoftPanel updateCard = Card("App updates", "Check GitHub releases and install directly.", 200);
            updateCardRef = updateCard;
            Label updateTitle = updateCard.Controls.OfType<Label>().FirstOrDefault();
            Label updateDesc = updateCard.Controls.OfType<Label>().LastOrDefault(l => l != updateTitle);
            if (updateTitle != null) updateTitle.Width = Math.Max(S(80), updateCard.Width - S(210));
            updateStatusPill.SetBounds(updateCard.Width - S(190), S(12), S(170), S(24));
            updateStatusPill.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            updateCard.Controls.Add(updateStatusPill);
            updateStatusPill.BringToFront();

            updateSummaryLabel = Label("", 10f, true);
            updateCard.Controls.Add(updateSummaryLabel);
            updateDetailsLabel = Label("", 9f);
            updateCard.Controls.Add(updateDetailsLabel);
            updateActionButton = (RaisedButton)Button("Check for updates", async delegate { await StartAppUpdateAsync(); });
            updateCard.Controls.Add(updateActionButton);
            updateGithubButton = (RaisedButton)Button("GitHub releases", delegate {
                try {
                    string url = (latestUpdate != null && AppUpdater.IsTrustedDownloadUrl(latestUpdate.HtmlUrl)) ? latestUpdate.HtmlUrl : AppUpdater.ReleasesPageUrl;
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                } catch (Exception ex) { status.Text = ex.Message; }
            });
            updateCard.Controls.Add(updateGithubButton);

            RegisterCardReflow(updateCard, () => {
                int padX = S(20), innerW = Math.Max(S(160), updateCard.Width - 2 * padX), gap = S(8);
                int pillW = Math.Min(S(172), Math.Max(S(120), innerW / 2));
                updateStatusPill.SetBounds(updateCard.Width - padX - pillW, S(12), pillW, S(24));
                if (updateTitle != null && !updateTitle.IsDisposed) {
                    updateTitle.Location = new Point(padX, S(12));
                    updateTitle.Width = Math.Max(S(80), updateStatusPill.Left - updateTitle.Left - S(6));
                    updateTitle.Height = MeasureLabelHeight(updateTitle, updateTitle.Width, S(28));
                }
                if (updateDesc != null && !updateDesc.IsDisposed) {
                    updateDesc.Left = padX;
                    updateDesc.Width = innerW;
                    updateDesc.Top = (updateTitle != null && !updateTitle.IsDisposed) ? Math.Max(updateTitle.Bottom, updateStatusPill.Bottom) + S(2) : S(42);
                    updateDesc.Height = MeasureLabelHeight(updateDesc, innerW, S(20));
                }
                int contentTop = (updateDesc != null && !updateDesc.IsDisposed) ? updateDesc.Bottom + S(6) : S(68);
                updateSummaryLabel.Location = new Point(padX, contentTop);
                updateSummaryLabel.Width = innerW;
                updateSummaryLabel.Height = MeasureLabelHeight(updateSummaryLabel, innerW, S(22));
                updateDetailsLabel.Location = new Point(padX, updateSummaryLabel.Bottom + S(4));
                updateDetailsLabel.Width = innerW;
                updateDetailsLabel.Height = MeasureLabelHeight(updateDetailsLabel, innerW, S(20));
                int btnTop = updateDetailsLabel.Bottom + S(10);
                int actionW = Math.Max(S(95), (innerW - gap) / 2);
                int ghW = Math.Max(S(95), innerW - actionW - gap);
                updateActionButton.SetBounds(padX, btnTop, actionW, S(36));
                updateGithubButton.SetBounds(updateActionButton.Right + gap, btnTop, ghW, S(36));
                updateCard.Height = updateActionButton.Bottom + S(12);
            });
            RefreshUpdateUi();
		}

        private async Task CheckForUpdatesAfterStartupAsync() {
            try {
                await Task.Delay(1200, lifetime.Token);
                if (!preview && !IsDisposed) await CheckForUpdatesAsync(false);
            } catch (OperationCanceledException) {}
        }

        private async Task CheckForUpdatesAsync(bool force) {
            if (preview || IsDisposed || checkingUpdates || downloadingUpdate) return;
            if (!force && lastUpdateCheckUtc != DateTime.MinValue && (DateTime.UtcNow - lastUpdateCheckUtc).TotalMinutes < 5) return;
            checkingUpdates = true;
            RefreshUpdateUi();
            try {
                var result = await AppUpdater.CheckLatestAsync();
                if (IsDisposed) return;
                latestUpdate = result;
                if (result.Checked) lastUpdateCheckUtc = DateTime.UtcNow;
                if (result.UpdateAvailable && selectedPage != "Settings") {
                    status.Text = "Update available: " + result.LatestVersion + " on GitHub. Open Settings -> App updates to install.";
                    status.ForeColor = Theme.OrangeInk;
                }
            } finally {
                checkingUpdates = false;
                if (!IsDisposed) RefreshUpdateUi();
            }
        }

        private async Task StartAppUpdateAsync() {
            if (preview || IsDisposed || checkingUpdates || downloadingUpdate) return;
            if (latestUpdate == null || !latestUpdate.UpdateAvailable) {
                await CheckForUpdatesAsync(true);
                if (!IsDisposed && latestUpdate != null) {
                    status.Text = latestUpdate.Summary;
                    status.ForeColor = latestUpdate.UpdateAvailable ? Theme.OrangeInk : (latestUpdate.Checked ? ink : Color.FromArgb(153, 63, 85));
                }
                return;
            }
            if (string.IsNullOrEmpty(latestUpdate.DownloadUrl)) {
                try {
                    Process.Start(new ProcessStartInfo(latestUpdate.HtmlUrl) { UseShellExecute = true });
                    status.Text = "Opened GitHub Releases in your browser to download " + latestUpdate.LatestVersion + ".";
                } catch (Exception ex) { status.Text = ex.Message; }
                return;
            }
            downloadingUpdate = true;
            downloadProgress = 0;
            latestUpdate.Error = "";
            RefreshUpdateUi();
            try {
                status.Text = "Downloading GTBOOK Essentials " + latestUpdate.LatestVersion + " from GitHub…";
                status.ForeColor = ink;
                await AppUpdater.DownloadAndLaunchAsync(latestUpdate, pct => {
                    if (IsDisposed || !IsHandleCreated) return;
                    try {
                        BeginInvoke((Action)(() => {
                            if (IsDisposed) return;
                            downloadProgress = pct;
                            RefreshUpdateUi();
                        }));
                    } catch (InvalidOperationException) {}
                }, lifetime.Token);
                if (!IsDisposed) {
                    downloadProgress = 100;
                    status.Text = "Update installer launched for " + latestUpdate.LatestVersion + ". Follow the installer to complete the update.";
                    status.ForeColor = Theme.OrangeInk;
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                if (!IsDisposed) {
                    status.Text = "Update failed: " + ex.Message;
                    status.ForeColor = Color.FromArgb(153, 63, 85);
                    if (latestUpdate != null) latestUpdate.Error = ex.Message;
                }
                Store.Log("App update error: " + ex);
            } finally {
                downloadingUpdate = false;
                if (!IsDisposed) RefreshUpdateUi();
            }
        }

        private void RefreshUpdateUi() {
            bool hasUpdate = latestUpdate != null && latestUpdate.UpdateAvailable;
            if (settingsNavButton != null && !settingsNavButton.IsDisposed) {
                settingsNavButton.BadgeDot = hasUpdate;
                settingsNavButton.AccessibleName = hasUpdate ? "Settings (Update available)" : "Settings";
            }
            if (settingsTabSecondaryBtn != null && !settingsTabSecondaryBtn.IsDisposed) {
                settingsTabSecondaryBtn.BadgeDot = hasUpdate;
            }
            string currentVer = AppUpdater.FormatVersion(AppUpdater.CurrentVersion);
            if (!updateStatusPill.IsDisposed) {
                if (downloadingUpdate) {
                    updateStatusPill.PillText = downloadProgress >= 0 ? ("Downloading · " + downloadProgress + "%") : "Downloading…";
                    updateStatusPill.DotColor = Theme.OrangeEnd;
                } else if (checkingUpdates) {
                    updateStatusPill.PillText = "Checking GitHub…";
                    updateStatusPill.DotColor = Theme.OrangeEnd;
                } else if (hasUpdate) {
                    updateStatusPill.PillText = "Update · " + latestUpdate.LatestVersion;
                    updateStatusPill.DotColor = Theme.OrangeEnd;
                } else if (latestUpdate != null && !latestUpdate.Checked) {
                    updateStatusPill.PillText = "Offline / Retry";
                    updateStatusPill.DotColor = Color.FromArgb(220, 56, 56);
                } else if (latestUpdate != null && latestUpdate.Checked) {
                    updateStatusPill.PillText = latestUpdate.NoPublishedReleases ? ("Installed · " + currentVer) : ("Up to date · " + currentVer);
                    updateStatusPill.DotColor = Color.FromArgb(35, 169, 108);
                } else {
                    updateStatusPill.PillText = "Installed · " + currentVer;
                    updateStatusPill.DotColor = muted;
                }
            }
            if (updateSummaryLabel != null && !updateSummaryLabel.IsDisposed) {
                if (downloadingUpdate) {
                    updateSummaryLabel.Text = "Downloading " + (latestUpdate != null ? latestUpdate.LatestVersion : "update") + (downloadProgress >= 0 ? " (" + downloadProgress + "%)" : "") + "…";
                    updateSummaryLabel.ForeColor = Theme.OrangeInk;
                } else if (checkingUpdates) {
                    updateSummaryLabel.Text = "Checking GitHub releases for " + AppUpdater.Owner + "/" + AppUpdater.Repo + "…";
                    updateSummaryLabel.ForeColor = ink;
                } else if (hasUpdate) {
                    updateSummaryLabel.Text = latestUpdate.Summary;
                    updateSummaryLabel.ForeColor = Theme.OrangeInk;
                } else if (latestUpdate != null) {
                    updateSummaryLabel.Text = latestUpdate.Summary;
                    updateSummaryLabel.ForeColor = latestUpdate.Checked ? ink : Color.FromArgb(153, 63, 85);
                } else {
                    updateSummaryLabel.Text = "Current version: " + currentVer + ". Checks GitHub Releases automatically.";
                    updateSummaryLabel.ForeColor = ink;
                }
            }
            if (updateDetailsLabel != null && !updateDetailsLabel.IsDisposed) {
                if (latestUpdate != null && !string.IsNullOrEmpty(latestUpdate.Error)) {
                    updateDetailsLabel.Text = "Details: " + latestUpdate.Error;
                } else if (hasUpdate) {
                    string sizeText = latestUpdate.AssetSize > 0 ? (" (" + (latestUpdate.AssetSize / (1024.0 * 1024.0)).ToString("0.0") + " MB)") : "";
                    string notes = latestUpdate.ReleaseNotes ?? "";
                    if (notes.Length > 200) notes = notes.Substring(0, 197).TrimEnd() + "…";
                    updateDetailsLabel.Text = string.IsNullOrEmpty(latestUpdate.AssetName)
                        ? (string.IsNullOrEmpty(notes) ? ("Release page: " + latestUpdate.HtmlUrl) : notes)
                        : ("Package: " + latestUpdate.AssetName + sizeText + (string.IsNullOrEmpty(notes) ? "" : ("\n" + notes)));
                } else if (latestUpdate != null && latestUpdate.NoPublishedReleases) {
                    updateDetailsLabel.Text = "No releases published yet on github.com/" + AppUpdater.Owner + "/" + AppUpdater.Repo + ". Pushing a v* tag publishes updates automatically.";
                } else {
                    updateDetailsLabel.Text = "Source: github.com/" + AppUpdater.Owner + "/" + AppUpdater.Repo + "/releases";
                }
            }
            if (updateActionButton != null && !updateActionButton.IsDisposed) {
                if (downloadingUpdate) {
                    updateActionButton.Text = downloadProgress >= 0 ? ("Downloading " + downloadProgress + "%") : "Downloading…";
                    updateActionButton.Enabled = false;
                    updateActionButton.Primary = true;
                    updateActionButton.Accent = Theme.OrangeEnd;
                } else if (checkingUpdates) {
                    updateActionButton.Text = "Checking…";
                    updateActionButton.Enabled = false;
                    updateActionButton.Primary = false;
                    updateActionButton.Accent = Color.Empty;
                } else if (hasUpdate) {
                    updateActionButton.Text = string.IsNullOrEmpty(latestUpdate.DownloadUrl) ? ("Download " + latestUpdate.LatestVersion) : ("Update to " + latestUpdate.LatestVersion);
                    updateActionButton.Enabled = true;
                    updateActionButton.Primary = true;
                    updateActionButton.Accent = Theme.OrangeEnd;
                } else {
                    updateActionButton.Text = "Check for updates";
                    updateActionButton.Enabled = true;
                    updateActionButton.Primary = false;
                    updateActionButton.Accent = Color.Empty;
                }
            }
            if (updateCardRef != null && !updateCardRef.IsDisposed) {
                TriggerCardReflow(updateCardRef);
                if (page != null && !page.IsDisposed && selectedPage == "Settings") page.PerformLayout();
            }
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

		private async Task StartMusic(Request request)
		{
            settingsRevision++;
			if (preview)
			{
				status.Text = "Layout preview does not access hardware.";
				return;
			}
			if (!ConfirmPreview(request))
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
                using(var owner=Process.GetCurrentProcess()){request.OwnerProcessId=owner.Id;request.OwnerStartedTicks=owner.StartTime.ToUniversalTime().Ticks;}
				musicSelection = request;
				rearState.Starting();
				UpdateRearIndicator();
				EventWaitHandle stopSignal = musicStop;
                using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,RearMusic.ReadyName)){
                ready.Reset();
				musicTask = Task.Factory.StartNew(() => WorkerClient.Run(request, int.MaxValue, stopSignal),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
				Task<Reply> pending = musicTask;
				status.Text = "Starting music sync… If no light reacts, check playback and the status below.";
                var startup=Stopwatch.StartNew();
                while(!ready.WaitOne(0)&&!pending.IsCompleted&&startup.ElapsedMilliseconds<8000)await Task.WhenAny(pending,Task.Delay(50));
				if (pending.IsCompleted||!ready.WaitOne(0))
				{
                    bool timedOut=!pending.IsCompleted;
					await StopMusic();
                    if(timedOut){rearState.Failed();rearResult="Music sync did not acknowledge its first frame. Use Check status before retrying.";UpdateRearIndicator();}
					status.Text = rearResult;
					return;
				}
                rearState.Applied(request);UpdateRearIndicator();
				rearResult = "Music sync active · " + request.Effect + " · play audio on your default output";
				status.Text = rearResult;
				if (rearOutcome != null && !rearOutcome.IsDisposed)
				{
					rearOutcome.Text = rearResult;
				}
				WatchMusic(pending);
                try{await Task.Run(()=>Store.Save("rear",request));}catch(Exception error){status.Text+=" · Preferences could not be saved: "+error.Message;}
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
                    if(keyboardTask!=null&&!keyboardTask.IsCompleted&&keyboardChannel!=null){
                        request.ArmKeyboardAwake=false;
                        var channel=keyboardChannel;request.KeyboardSession=channel.Id;
                        var updateTime=Stopwatch.StartNew();
                        var updated=await Task.Run(()=>channel.Update(request));
                        if(!updated.Ok)throw new IOException(updated.Message);
                        await Task.Run(()=>Store.Log("Keyboard live update | "+request.Effect+" | "+request.Brightness+"% | "+updateTime.ElapsedMilliseconds+" ms"));
                        await RecordKeyboardAnimation(request,silent);return;
                    }
					await StopKeyboardAnimation();
                    request.ArmKeyboardAwake=request.KeepAwake;
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
								TriggerCardReflow(kbOutcome.Parent);
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
            if(kbOutcome!=null&&!kbOutcome.IsDisposed){kbOutcome.Text=keyboardResult;TriggerCardReflow(kbOutcome.Parent);}
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
                keyboardAwakeConfigured=false;
				return;
			}
			Task<Reply> pending = keyboardTask;
			EventWaitHandle signal = keyboardStop;
            var channel=keyboardChannel;keyboardChannel=null;
			keyboardTask = null;
			keyboardStop = null;
            keyboardAwakeConfigured=false;
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
				TriggerCardReflow(kbOutcome.Parent);
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
                    rearState.Edited(rear);
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
						if (!preview && confirmedMode == 3 && e.Mode == PowerModes.StatusChange) EnergySaver.SyncAsync(3, autoEnergySaver);
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
			Label diagDesc = softPanel.Controls.OfType<Label>().Last();
			details.SetBounds(0, 0, 0, 0);
			details.Visible = false;
			details.Multiline = true;
			details.ReadOnly = true;
			details.BorderStyle = BorderStyle.None;
			details.BackColor = Theme.White;
			details.ForeColor = ink;
			details.Font = new Font("Segoe UI", 10f);
            details.Text=preview?DeviceInformation.Example():deviceReport.Length>0?deviceReport:"Loading device information…";
			softPanel.Controls.Add(details);

            if(specView==null||specView.IsDisposed)specView=new DeviceSpecView();
            if(specView.Parent!=null)specView.Parent.Controls.Remove(specView);
            int specTop = Math.Max(S(78), diagDesc.Bottom + S(10));
            specView.SetBounds(S(22), specTop, softPanel.Width - S(44), S(380));
            specView.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            specView.SetReport(details.Text);
			var flowLayoutPanel2 = new BufferedPanel
			{
				Height = S(48),
				Width = S(640),
				BackColor = Color.Transparent
			};
            Action syncDiagHeight = () => {
                if (softPanel.IsDisposed || specView.IsDisposed) return;
                ReflowCardHeader(softPanel, diagDesc);
                int top = Math.Max(S(74), diagDesc.Bottom + S(8));
                int maxW = Math.Max(S(160), softPanel.Width - S(44));
                int estActionH = maxW < S(490) ? S(96) : S(48);
                int viewportH = page != null && !page.IsDisposed ? page.ClientSize.Height : S(650);
                int maxSpecH = Math.Max(S(200), viewportH - top - S(16) - estActionH - page.Padding.Vertical - S(12));
                specView.Left = S(22);
                specView.Top = top;
                specView.Width = maxW;
                specView.MaxHeight = maxSpecH;
                specView.RecalculateLayout();
                softPanel.Height = specView.Bottom + S(12);
            };
            specView.OnHeightChanged = () => {
                syncDiagHeight();
                if(page!=null&&!page.IsDisposed)page.PerformLayout();
            };
            specView.OnRefreshBattery = async () => {
                await LoadDeviceInfo(true);
            };
            softPanel.Controls.Add(specView);
            RegisterCardReflow(softPanel, syncDiagHeight);
            syncDiagHeight();
            var refreshBtn=Button("Refresh info", async delegate
			{
                await LoadDeviceInfo(true);
			});
            refreshBtn.SetBounds(0, 0, S(135), S(42));
			flowLayoutPanel2.Controls.Add(refreshBtn);
            var originalControls=Button("Original controls",OpenOriginal);
            originalControls.SetBounds(refreshBtn.Right + S(10), 0, S(165), S(42));
            originalControls.Enabled=File.Exists(Path.Combine(Hardware.OemDirectory,"ControlCenter.exe"));
            flowLayoutPanel2.Controls.Add(originalControls);
            var saveDiagBtn=Button("Save diagnostics", SaveDiagnostics);
            saveDiagBtn.SetBounds(originalControls.Right + S(10), 0, S(165), S(42));
			flowLayoutPanel2.Controls.Add(saveDiagBtn);
			Action layoutDiagActions = () => {
				if (flowLayoutPanel2.IsDisposed) return;
				int maxW = Math.Max(S(180), flowLayoutPanel2.Width);
				int gap = S(10), rowGap = S(8), btnH = S(42);
				int x = 0, y = 0;
				Control[] btns = { refreshBtn, originalControls, saveDiagBtn };
				int[] widths = { S(135), S(165), S(165) };
				for (int i = 0; i < btns.Length; i++) {
					int bw = Math.Min(widths[i], maxW);
					if (x > 0 && x + bw > maxW) {
						x = 0;
						y += btnH + rowGap;
					}
					btns[i].SetBounds(x, y, bw, btnH);
					x += bw + gap;
				}
				flowLayoutPanel2.Height = y + btnH + S(6);
			};
			RegisterCardReflow(flowLayoutPanel2, layoutDiagActions);
			layoutDiagActions();
			page.Controls.Add(flowLayoutPanel2);
		}

        private string ConnectionReport(){return string.Join("\r\n",snapshot.Select(kv=>kv.Key+": "+(string.IsNullOrEmpty(kv.Value)?"None":kv.Value)));}
        private async Task LoadDeviceInfo(bool force=false){
            if(preview||loadingDeviceInfo||IsDisposed)return;
            if(!force&&deviceReport.Length>0&&DateTime.UtcNow-deviceInfoUpdated<TimeSpan.FromMinutes(5))return;
            loadingDeviceInfo=true;
            if(specView!=null&&!specView.IsDisposed)specView.SetRefreshing(true);
            if(deviceReport.Length==0){details.Text="Loading device information…";if(specView!=null&&!specView.IsDisposed)specView.SetReport(details.Text);}
            try{
                var reply=await Task.Run(()=>WorkerClient.Run(new Request{Action="device-info"},20000));
                if(IsDisposed)return;
                string report;
                if(reply.Ok&&reply.Data.TryGetValue("device-report",out report)){
                    deviceReport=report;deviceInfoUpdated=DateTime.UtcNow;details.Text=report;
                    if(specView!=null&&!specView.IsDisposed)specView.SetReport(report);
                }else{
                    string err=(deviceReport.Length>0?deviceReport+"\r\n\r\n":"")+"Could not refresh device information. "+reply.Message;
                    details.Text=err;
                    if(specView!=null&&!specView.IsDisposed)specView.SetReport(err);
                }
            }catch(Exception error){
                if(!IsDisposed){
                    string err="Could not load device information. "+error.Message;
                    details.Text=err;
                    if(specView!=null&&!specView.IsDisposed)specView.SetReport(err);
                }
            }
            finally{
                loadingDeviceInfo=false;
                if(specView!=null&&!specView.IsDisposed)specView.SetRefreshing(false);
            }
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

		private static int ParseDegrees(string text)
		{
			if (string.IsNullOrEmpty(text)) return -1;
			int val = 0;
			bool any = false;
			foreach (char c in text)
			{
				if (char.IsDigit(c)) { val = val * 10 + (c - '0'); any = true; }
				else if (any) break;
			}
			return any ? val : -1;
		}

		private void UpdateBattery()
		{
			PowerStatus powerStatus = SystemInformation.PowerStatus;
			bool noBattery = powerStatus.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery || powerStatus.BatteryLifePercent < 0f;
			int pct = noBattery ? -1 : (int)Math.Round(powerStatus.BatteryLifePercent * 100f);
			bool ac = powerStatus.PowerLineStatus == PowerLineStatus.Online;
			bool charging = !noBattery && (powerStatus.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0;
			batteryValue.Text = noBattery ? "Unavailable" : (pct + "%" + (ac ? " · AC" : ""));
			if (!batteryTile.IsDisposed)
			{
				batteryTile.ValueText = noBattery ? "Unavailable" : (pct + "%" + (ac ? " · AC" : ""));
				batteryTile.DetailText = noBattery ? "No battery detected" : (charging ? "Charging" : (ac ? "Plugged in" : "On battery"));
				batteryTile.Progress = pct;
				bool low = !noBattery && !ac && pct < 20;
				batteryTile.DotColor = noBattery ? muted : (low ? Color.FromArgb(220, 56, 56) : Color.FromArgb(35, 169, 108));
				batteryTile.BarStart = low ? Color.FromArgb(245, 110, 80) : Color.FromArgb(35, 169, 108);
				batteryTile.BarEnd = low ? Color.FromArgb(220, 56, 56) : Color.FromArgb(56, 198, 134);
			}
		}

		private void UpdateCoolingTile(string coolingText)
		{
			if (!coolingPill.IsDisposed)
			{
				bool full = coolingText == "Full fan override";
				bool auto = coolingText == "Firmware auto";
				coolingPill.PillText = (string.IsNullOrEmpty(coolingText) || coolingText == "Not read") ? (preview ? "Preview" : "Syncing…") : coolingText;
				coolingPill.DotColor = full ? Theme.OrangeEnd : (auto ? Color.FromArgb(35, 169, 108) : muted);
			}
		}

		private void UpdateModeTile(string modeText)
		{
			if (modeTile.IsDisposed) return;
			bool known = modeText == "Quiet" || modeText == "Balanced" || modeText == "Performance" || modeText == "Turbo";
			modeTile.ValueText = known ? modeText : (preview ? "Preview" : "Syncing…");
			if (modeText == "Quiet")
			{
				modeTile.DetailText = autoEnergySaver ? "Energy saver active" : "Cool & quiet";
				modeTile.DotColor = Color.FromArgb(45, 165, 140);
			}
			else if (modeText == "Balanced")
			{
				modeTile.DetailText = "Adaptive power & cooling";
				modeTile.DotColor = Color.FromArgb(244, 145, 36);
			}
			else if (modeText == "Performance")
			{
				modeTile.DetailText = "High performance";
				modeTile.DotColor = Theme.OrangeEnd;
			}
			else if (modeText == "Turbo")
			{
				modeTile.DetailText = "Ultimate boost · 0% battery saving";
				modeTile.DotColor = Color.FromArgb(235, 68, 42);
			}
			else
			{
				modeTile.DetailText = preview ? "Hardware disconnected" : "Waiting for firmware";
				modeTile.DotColor = muted;
			}
		}

		private void UpdateValues()
		{
			string modeText = snapshot.ContainsKey("mode") ? snapshot["mode"] : "Not read";
			string cpuText = snapshot.ContainsKey("cpu") ? snapshot["cpu"] : "Not read";
			string coolingText = snapshot.ContainsKey("cooling") ? snapshot["cooling"] : "Not read";
			modeValue.Text = modeText;
			cpuValue.Text = cpuText;
			coolingValue.Text = coolingText;
			UpdateModeTile(modeText);
			UpdateCoolingTile(coolingText);

			if (!cpuTile.IsDisposed)
			{
				int cpuTemp = ParseDegrees(cpuText);
				bool hasCpu = cpuTemp >= 0;
				cpuTile.ValueText = hasCpu ? cpuText : (preview ? "-- °C" : "Syncing…");
				cpuTile.Progress = hasCpu ? Math.Min(100, cpuTemp) : -1;
				if (!hasCpu)
				{
					cpuTile.DetailText = preview ? "Hardware disconnected" : "Reading sensor…";
					cpuTile.DotColor = muted;
				}
				else if (cpuTemp < 55)
				{
					cpuTile.DetailText = "Cool · normal load";
					cpuTile.DotColor = Color.FromArgb(35, 169, 108);
					cpuTile.BarStart = Color.FromArgb(35, 169, 108);
					cpuTile.BarEnd = Color.FromArgb(65, 200, 140);
				}
				else if (cpuTemp < 75)
				{
					cpuTile.DetailText = "Normal operating temp";
					cpuTile.DotColor = Color.FromArgb(244, 155, 45);
					cpuTile.BarStart = Theme.OrangeStart;
					cpuTile.BarEnd = Theme.OrangeEnd;
				}
				else if (cpuTemp < 88)
				{
					cpuTile.DetailText = "Warm under load";
					cpuTile.DotColor = Theme.OrangeEnd;
					cpuTile.BarStart = Color.FromArgb(244, 145, 36);
					cpuTile.BarEnd = Color.FromArgb(235, 78, 42);
				}
				else
				{
					cpuTile.DetailText = "High temperature";
					cpuTile.DotColor = Color.FromArgb(220, 56, 56);
					cpuTile.BarStart = Color.FromArgb(239, 105, 36);
					cpuTile.BarEnd = Color.FromArgb(220, 56, 56);
				}
			}

			foreach (Control control in page.Controls)
			{
				MarkMode(control, modeValue.Text);
			}

			string gpuRawText = snapshot.ContainsKey("gpu") ? snapshot["gpu"] : "";
			bool gpuMissing = string.IsNullOrEmpty(gpuRawText) || gpuRawText.StartsWith("Unavailable");
			bool iGpuOnly = snapshot.ContainsKey("gpu-mode") && snapshot["gpu-mode"] == "iGPU only";
			if (!gpuTile.IsDisposed)
			{
				if (!gpuMissing)
				{
					gpuTile.ValueText = gpuRawText;
					gpuTile.DetailText = "Discrete GPU active";
					gpuTile.DotColor = Theme.OrangeEnd;
				}
				else if (snapshot.ContainsKey("gpu"))
				{
					gpuTile.ValueText = iGpuOnly ? "Idle" : "Asleep";
					gpuTile.DetailText = iGpuOnly ? "iGPU only · dGPU off" : "Discrete GPU inactive";
					gpuTile.DotColor = muted;
				}
				else
				{
					gpuTile.ValueText = preview ? "-- °C" : "Syncing…";
					gpuTile.DetailText = preview ? "Hardware disconnected" : "Reading sensor…";
					gpuTile.DotColor = muted;
				}
			}

			bool fullFans = coolingText == "Full fan override";
			string cpuFanText = snapshot.ContainsKey("cpu-fan") && !snapshot["cpu-fan"].StartsWith("Unavailable") ? snapshot["cpu-fan"] : "";
			if (!cpuFanTile.IsDisposed)
			{
				bool has = !string.IsNullOrEmpty(cpuFanText);
				cpuFanTile.ValueText = has ? cpuFanText : (preview ? "-- RPM" : "Syncing…");
				cpuFanTile.DetailText = !has ? (preview ? "Hardware disconnected" : "Reading fan…") : fullFans ? "Max fan override" : (cpuFanText == "0 RPM" ? "Silent / idle" : "Firmware controlled");
				cpuFanTile.DotColor = !has ? muted : fullFans ? Theme.OrangeEnd : Color.FromArgb(35, 169, 108);
			}

			string gpuFanText = snapshot.ContainsKey("gpu-fan") && !snapshot["gpu-fan"].StartsWith("Unavailable") ? snapshot["gpu-fan"] : "";
			if (!gpuFanTile.IsDisposed)
			{
				bool has = !string.IsNullOrEmpty(gpuFanText);
				gpuFanTile.ValueText = has ? gpuFanText : (preview ? "-- RPM" : "Syncing…");
				gpuFanTile.DetailText = !has ? (preview ? "Hardware disconnected" : "Reading fan…") : fullFans ? "Max fan override" : (gpuFanText == "0 RPM" ? "Silent / idle" : "Firmware controlled");
				gpuFanTile.DotColor = !has ? muted : fullFans ? Theme.OrangeEnd : Color.FromArgb(35, 169, 108);
			}

			var sys = SystemStats.Read();
			if (!memoryTile.IsDisposed)
			{
				memoryTile.ValueText = sys.MemoryValue;
				memoryTile.DetailText = sys.MemoryDetail;
				memoryTile.Progress = sys.MemoryPercent;
				bool highMem = sys.MemoryPercent >= 85;
				memoryTile.DotColor = sys.MemoryPercent < 0 ? muted : highMem ? Color.FromArgb(220, 56, 56) : Theme.OrangeEnd;
				memoryTile.BarStart = highMem ? Color.FromArgb(244, 145, 36) : Theme.OrangeStart;
				memoryTile.BarEnd = highMem ? Color.FromArgb(220, 56, 56) : Theme.OrangeEnd;
			}
			if (!diskTile.IsDisposed)
			{
				diskTile.ValueText = sys.DiskValue;
				diskTile.DetailText = sys.DiskDetail;
				diskTile.Progress = sys.DiskPercent;
				bool highDisk = sys.DiskPercent >= 88;
				diskTile.DotColor = sys.DiskPercent < 0 ? muted : highDisk ? Color.FromArgb(220, 56, 56) : Theme.OrangeEnd;
				diskTile.BarStart = highDisk ? Color.FromArgb(244, 145, 36) : Theme.OrangeStart;
				diskTile.BarEnd = highDisk ? Color.FromArgb(220, 56, 56) : Theme.OrangeEnd;
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
            usbState.Text=known?"Firmware setting: "+value.ToUpperInvariant()+" · read back from device":"Firmware setting not confirmed · syncing automatically";
            usbState.ForeColor=known?Theme.OrangeInk:Theme.Muted;usbOn.Selected=known&&value=="On";usbOff.Selected=known&&value=="Off";
            TriggerCardReflow(usbState.Parent);
        }
		private void MarkMode(Control parent, string current)
		{
			foreach (Control control in parent.Controls)
			{
				RaisedButton raisedButton = control as RaisedButton;
				if (raisedButton != null)
				{
					string key = Convert.ToString(raisedButton.Tag ?? raisedButton.Text);
					if (key == "Quiet" || key == "Balanced")
					{
						raisedButton.Selected = key == current;
						raisedButton.Accent = raisedButton.Selected ? Color.FromArgb(244, 117, 20) : Color.Empty;
					}
					else if (key == "Performance")
					{
						bool turbo = current == "Turbo";
						string targetLabel = Languages.Translate(turbo ? "Turbo" : "Performance");
						if (raisedButton.Text != targetLabel) raisedButton.Text = targetLabel;
						raisedButton.Selected = turbo || current == "Performance";
						raisedButton.Accent = turbo ? Color.FromArgb(255, 65, 20) : (raisedButton.Selected ? Color.FromArgb(244, 117, 20) : Color.Empty);
					}
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
                if(modeObserver!=null)modeObserver.Confirm(notice.Mode>2?2:notice.Mode);
                if(changed&&modeSounds&&!preview){if(modeChime==null)modeChime=new ModeChime();modeChime.Play(notice.Mode);}
                if(!preview)EnergySaver.SyncAsync(notice.Mode,autoEnergySaver);
                if(!changed)notice.Detail="Already active. You're all set.";
                else if(notice.Mode==3)notice.Detail="Ultimate Performance · Full fans ON";
                else if(autoEnergySaver&&notice.Mode==0)notice.Detail="Energy saver active · Quiet & cool";
                else if(autoEnergySaver&&(notice.Mode==1||notice.Mode==2))notice.Detail="Energy saver turned off";
            }
            if((request.Action=="keyboard"||request.Action=="keyboard-animation")&&applyKeyboardButton!=null&&!applyKeyboardButton.IsDisposed)applyKeyboardButton.Flash(notice.Kind!=FeedbackKind.Error);
            shortcutToast.Notify(notice);
        }

        private void SetAutoEnergySaver(bool enabled)
        {
            autoEnergySaver = enabled;
            try { Store.SaveFlag("auto-energy-saver", enabled); } catch (Exception ex) { Store.Log("Save auto-energy-saver: " + ex.Message); }
            if (overviewEnergySaver != null && !overviewEnergySaver.IsDisposed && overviewEnergySaver.Checked != enabled) overviewEnergySaver.Checked = enabled;
            if (settingsEnergySaver != null && !settingsEnergySaver.IsDisposed && settingsEnergySaver.Checked != enabled) settingsEnergySaver.Checked = enabled;
            UpdateModeTile(modeValue.Text);
            if (!preview && confirmedMode >= 0)
            {
                if (enabled) {
                    EnergySaver.SyncAsync(confirmedMode, true);
                } else if (confirmedMode == 0) {
                    EnergySaver.DisableEnergySaverInQuietAsync();
                }
            }
        }

        private void TrackPerformanceMode()
        {
            if(preview||IsDisposed||!snapshot.ContainsKey("mode"))return;
            int mode=PerformanceModes.Index(snapshot["mode"]);if(mode<0)return;
            if(confirmedMode<0){confirmedMode=mode;if(!preview)EnergySaver.SyncAsync(mode,autoEnergySaver);}
            else if(confirmedMode!=mode && !(confirmedMode==3 && mode==2)){var reply=new Reply{Ok=true};reply.Data["mode"]=PerformanceModes.Names[mode];NotifyApplied(new Request{Action="mode",Mode=mode},reply);}
            if(modeObserver!=null){modeObserver.Confirm(mode>2?2:mode);return;}
            if(!snapshot.ContainsKey("supported")||snapshot["supported"]!="True"||snapshot.ContainsKey("conflict")&&snapshot["conflict"].Length>0)return;
            modeObserver=new ModeObserver(mode>2?2:mode,()=>!busy&&!suspended,observed=>{
                try{if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)(async()=>{
                    if(IsDisposed||modeObserver==null||!modeObserver.IsCurrent(observed)||confirmedMode==observed)return;
                    if(confirmedMode==3&&observed==2)return;
                    bool wasTurbo = confirmedMode == 3;
                    snapshot["mode"]=PerformanceModes.Names[observed];UpdateValues();
                    var reply=new Reply{Ok=true};reply.Data["mode"]=PerformanceModes.Names[observed];
                    if (wasTurbo) {
                        try {
                            var autoReply = await Task.Run(()=>WorkerClient.Run(new Request{Action="auto"}));
                            if (autoReply.Ok) { snapshot["cooling"]="Firmware auto"; coolingValue.Text="Firmware auto"; UpdateCoolingTile("Firmware auto"); }
                        } catch {}
                    }
                    NotifyApplied(new Request{Action="mode",Mode=observed},reply);
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
                    foreach(var entry in reply.Data){
                        if(entry.Key=="mode" && confirmedMode==3){
                            string cooling = reply.Data.ContainsKey("cooling") ? reply.Data["cooling"] : (snapshot.ContainsKey("cooling") ? snapshot["cooling"] : "");
                            if(entry.Value==PerformanceModes.Names[2] && cooling=="Full fan override"){
                                snapshot["mode"]="Turbo";
                                continue;
                            }
                        }
                        snapshot[entry.Key]=entry.Value;
                    }
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
            if(!background){
                settingsRevision++;
                if(r.Action=="mode"||r.Action=="screen-brightness"||r.Action=="auto"||r.Action=="fan"||r.Action=="usb")shortcutToast.Notify(FeedbackNotice.Pending(r));
                if(r.Action=="mode"&&r.Mode>=0&&r.Mode<4){
                    MarkMode(page,PerformanceModes.Names[r.Mode]);
                    modeValue.Text=PerformanceModes.Names[r.Mode];
                    UpdateModeTile(PerformanceModes.Names[r.Mode]);
                    status.Text=PerformanceModes.Names[r.Mode]+" mode";status.ForeColor=ink;
                }
            }
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
			if (!background&&r.Action!="screen-brightness"&&r.Action!="mode")
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
                    r.ArmKeyboardAwake=r.KeepAwake&&!keyboardAwakeConfigured&&r.Feedback!="keyboard-brightness";
				}
				if (RearLighting.StopsMusic(r.Action))
				{
					await StopMusic();
				}
				Reply reply = await Task.Run(() => WorkerClient.Run(r));
                if(r.Action=="keyboard"&&reply.Ok&&r.KeepAwake)keyboardAwakeConfigured=true;
                if(r.Action=="refresh"){
                    hardwareReady=reply.Ok&&reply.Data.ContainsKey("mode");
                    if(!reply.Ok&&!background)rearState.Failed();
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
						TriggerCardReflow(kbOutcome.Parent);
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
                if(r.Action.StartsWith("rear")){rearState.Failed();UpdateRearIndicator();}
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
