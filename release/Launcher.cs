using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GTBook {
    internal static class Launcher {
        [STAThread]
        static int Main(string[] args) {
            try {
                using (var signal = EventWaitHandle.OpenExisting(@"Local\GTBookEssentialsShow")) {
                    signal.Set();
                    return 0;
                }
            } catch {}

            bool startedViaTask = false;
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

            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string exe = Path.Combine(baseDir, "GTBook.exe");
                if (!File.Exists(exe)) {
                    exe = @"C:\Program Files\GTBOOK Essentials\GTBook.exe";
                }
                if (File.Exists(exe)) {
                    var psi = new ProcessStartInfo(exe) {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exe)
                    };
                    Process.Start(psi);
                }
            } catch {}

            return 0;
        }
    }
}
