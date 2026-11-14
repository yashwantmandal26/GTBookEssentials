using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GTBook {
    internal static class Launcher {
        [STAThread]
        static int Main(string[] args) {
            bool isStartup = args != null && args.Length > 0 && Array.IndexOf(args, "--startup") >= 0;

            // 1. If an instance is already running, signal it to show
            try {
                using (var signal = EventWaitHandle.OpenExisting(@"Local\GTBookEssentialsShow")) {
                    if (!isStartup) signal.Set();
                    return 0;
                }
            } catch {}

            // 2. Resolve target GTBook.exe
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exe = Path.Combine(baseDir, "GTBook.exe");
            if (!File.Exists(exe)) {
                exe = @"C:\Program Files\GTBOOK Essentials\GTBook.exe";
            }

            // 3. If launching in foreground and not already running, try running the scheduled task for elevation
            bool startedViaTask = false;
            if (!isStartup) {
                try {
                    var psi = new ProcessStartInfo("schtasks.exe", "/run /tn \"GTBookStable\"") {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (var p = Process.Start(psi)) {
                        p.WaitForExit(3000);
                        if (p.ExitCode == 0) {
                            startedViaTask = true;
                        }
                    }
                } catch {}

                if (startedViaTask) {
                    for (int i = 0; i < 30; i++) {
                        Thread.Sleep(100);
                        try {
                            using (var signal = EventWaitHandle.OpenExisting(@"Local\GTBookEssentialsShow")) {
                                signal.Set();
                                return 0;
                            }
                        } catch {}
                    }
                }
            }

            // 4. Fallback: launch target executable directly without circular task recursion
            try {
                if (File.Exists(exe)) {
                    string arguments = isStartup ? "--startup" : "";
                    var psi = new ProcessStartInfo(exe, arguments) {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exe),
                        WindowStyle = isStartup ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
                    };
                    Process.Start(psi);
                }
            } catch {}

            return 0;
        }
    }
}
