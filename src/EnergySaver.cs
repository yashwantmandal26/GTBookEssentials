using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace GTBook {
    public struct TurboPowerSetting {
        public string Name;
        public Guid SubGroup;
        public Guid Setting;
        public uint AcValue;
        public uint DcValue;
    }

    public struct TurboOwnership {
        public bool Owned;
        public string PreviousScheme;
        public string TurboScheme;
    }

    public interface IPowerBackend {
        Guid GetActiveScheme();
        bool SetActiveScheme(Guid scheme);
        void SetActiveOverlay(Guid overlay);
        bool SetEnergySaver(bool enabled);
        bool IsEnergySaverActive();
        string ReadFriendlyName(Guid scheme);
        bool SchemeExists(Guid scheme);
        Guid DuplicateScheme(Guid sourceScheme);
        bool WriteFriendlyName(Guid scheme, string name);
        bool WriteSetting(Guid scheme, Guid subGroup, Guid setting, uint acValue, uint dcValue);
        List<Guid> EnumerateSchemes();
    }

    public class DefaultPowerBackend : IPowerBackend {
        [DllImport("ntdll.dll")]
        static extern int NtQueryWnfStateData(
            ref ulong StateName,
            IntPtr TypeId,
            IntPtr ExplicitScope,
            out uint ChangeStamp,
            IntPtr Buffer,
            ref uint BufferSize);

        [DllImport("ntdll.dll")]
        static extern int NtUpdateWnfStateData(
            ref ulong StateName,
            byte[] Buffer,
            uint Length,
            IntPtr TypeId,
            IntPtr ExplicitScope,
            uint MatchingChangeStamp,
            int CheckStamp);

        [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
        static extern uint PowerSetActiveOverlayScheme(Guid overlaySchemeGuid);

        [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
        static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

        [DllImport("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
        static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("powrprof.dll", EntryPoint = "PowerDuplicateScheme")]
        static extern uint PowerDuplicateScheme(IntPtr RootPowerKey, ref Guid SourceSchemeGuid, out IntPtr DestinationSchemeGuid);

        [DllImport("powrprof.dll", EntryPoint = "PowerWriteACValueIndex")]
        static extern uint PowerWriteACValueIndex(IntPtr RootPowerKey, ref Guid SchemeGuid, ref Guid SubGroupOfPowerSettingsGuid, ref Guid PowerSettingGuid, uint AcValueIndex);

        [DllImport("powrprof.dll", EntryPoint = "PowerWriteDCValueIndex")]
        static extern uint PowerWriteDCValueIndex(IntPtr RootPowerKey, ref Guid SchemeGuid, ref Guid SubGroupOfPowerSettingsGuid, ref Guid PowerSettingGuid, uint DcValueIndex);

        [DllImport("powrprof.dll", EntryPoint = "PowerReadFriendlyName")]
        static extern uint PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid, IntPtr PowerSettingGuid, IntPtr Buffer, ref uint BufferSize);

        [DllImport("powrprof.dll", EntryPoint = "PowerWriteFriendlyName")]
        static extern uint PowerWriteFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid, IntPtr PowerSettingGuid, byte[] Buffer, uint BufferSize);

        [DllImport("powrprof.dll", EntryPoint = "PowerEnumerate")]
        static extern uint PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid, uint AccessFlags, uint Index, IntPtr Buffer, ref uint BufferSize);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr hMem);

        public Guid GetActiveScheme() {
            IntPtr ptr = IntPtr.Zero;
            try {
                if (PowerGetActiveScheme(IntPtr.Zero, out ptr) == 0 && ptr != IntPtr.Zero) {
                    return (Guid)Marshal.PtrToStructure(ptr, typeof(Guid));
                }
            } catch {} finally {
                if (ptr != IntPtr.Zero) LocalFree(ptr);
            }
            return Guid.Empty;
        }

        public bool SetActiveScheme(Guid scheme) {
            try {
                return PowerSetActiveScheme(IntPtr.Zero, ref scheme) == 0;
            } catch {
                return false;
            }
        }

        public void SetActiveOverlay(Guid overlay) {
            try {
                PowerSetActiveOverlayScheme(overlay);
            } catch (Exception ex) {
                Store.Log("Power overlay: " + ex.Message);
            }
        }

        public bool SetEnergySaver(bool enabled) {
            bool ok = false;
            try {
                ulong state = EnergySaver.WNF_PO_ENERGY_SAVER_OVERRIDE;
                byte[] data = BitConverter.GetBytes(enabled ? 1 : 0);
                int status = NtUpdateWnfStateData(ref state, data, (uint)data.Length, IntPtr.Zero, IntPtr.Zero, 0, 0);
                ok = (status == 0);
            } catch (Exception ex) {
                Store.Log("Energy saver WNF update: " + ex.Message);
            }
            try {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power", true)) {
                    if (key != null) {
                        key.SetValue("EnergySaverState", enabled ? 1 : 0, RegistryValueKind.DWord);
                        ok = true;
                    }
                }
            } catch {}
            return ok;
        }

        public bool IsEnergySaverActive() {
            try {
                ulong state = EnergySaver.WNF_PO_ENERGY_SAVER_OVERRIDE;
                uint stamp, size = 4;
                IntPtr buffer = Marshal.AllocHGlobal(4);
                try {
                    int status = NtQueryWnfStateData(ref state, IntPtr.Zero, IntPtr.Zero, out stamp, buffer, ref size);
                    if (status == 0 && size >= 4) {
                        return Marshal.ReadInt32(buffer) == 1;
                    }
                } finally {
                    Marshal.FreeHGlobal(buffer);
                }
            } catch {}
            try {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power", false)) {
                    if (key != null) {
                        object val = key.GetValue("EnergySaverState");
                        if (val != null) return Convert.ToInt32(val) == 1;
                    }
                }
            } catch {}
            return false;
        }

        public string ReadFriendlyName(Guid scheme) {
            uint size = 0;
            uint probe = PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref size);
            if ((probe != 0 && probe != 234) || size == 0) return null;
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try {
                if (PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, buffer, ref size) != 0) return null;
                return (Marshal.PtrToStringUni(buffer) ?? "").TrimEnd('\0');
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public bool SchemeExists(Guid scheme) {
            return ReadFriendlyName(scheme) != null;
        }

        public Guid DuplicateScheme(Guid sourceScheme) {
            IntPtr dstPtr;
            if (PowerDuplicateScheme(IntPtr.Zero, ref sourceScheme, out dstPtr) == 0 && dstPtr != IntPtr.Zero) {
                try {
                    return (Guid)Marshal.PtrToStructure(dstPtr, typeof(Guid));
                } finally {
                    LocalFree(dstPtr);
                }
            }
            return Guid.Empty;
        }

        public bool WriteFriendlyName(Guid scheme, string name) {
            try {
                byte[] nameBytes = Encoding.Unicode.GetBytes(name + "\0");
                return PowerWriteFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, nameBytes, (uint)nameBytes.Length) == 0;
            } catch {
                return false;
            }
        }

        public bool WriteSetting(Guid scheme, Guid subGroup, Guid setting, uint acValue, uint dcValue) {
            try {
                bool ac = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref subGroup, ref setting, acValue) == 0;
                bool dc = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref subGroup, ref setting, dcValue) == 0;
                return ac && dc;
            } catch {
                return false;
            }
        }

        public List<Guid> EnumerateSchemes() {
            var list = new List<Guid>();
            IntPtr buf = Marshal.AllocHGlobal(16);
            try {
                for (uint idx = 0; ; idx++) {
                    uint size = 16;
                    uint res = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16, idx, buf, ref size);
                    if (res != 0) break;
                    list.Add((Guid)Marshal.PtrToStructure(buf, typeof(Guid)));
                }
            } catch {} finally {
                Marshal.FreeHGlobal(buf);
            }
            return list;
        }
    }

    public static class EnergySaver {
        public static IPowerBackend Backend = new DefaultPowerBackend();

        public const string TurboSchemeFriendlyName = "GTBOOK Ultimate Performance";
        public static readonly Guid UltimatePerformanceScheme = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");
        public static readonly Guid HighPerformanceScheme = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        public static readonly Guid BalancedScheme = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");

        public const ulong WNF_PO_ENERGY_SAVER_STATE = 0x41C6013DA3BC2075;
        public const ulong WNF_PO_ENERGY_SAVER_OVERRIDE = 0x41C6013DA3BC3075;

        public static readonly Guid BetterBatteryOverlay = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
        public static readonly Guid BalancedOverlay = new Guid("00000000-0000-0000-0000-000000000000");
        public static readonly Guid MaxPerformanceOverlay = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");

        static readonly Guid SubProcessor = new Guid("54533251-82be-4824-96c1-47b60b740d00");
        static readonly Guid SubPciExpress = new Guid("501a4d13-42af-4429-9fd1-a8218c268e20");
        static readonly Guid SubEnergySaver = new Guid("de830923-a562-41af-a086-e3a2c6bad2da");
        static readonly Guid SubUsb = new Guid("2a737441-1930-4402-8d77-b2bebba308a3");

        public static readonly TurboPowerSetting[] TurboSettings = new[] {
            new TurboPowerSetting { Name = "PROCTHROTTLEMIN", SubGroup = SubProcessor, Setting = new Guid("893dee8e-2bef-41e0-89c6-b55d0929964c"), AcValue = 100, DcValue = 100 },
            new TurboPowerSetting { Name = "PROCTHROTTLEMIN1", SubGroup = SubProcessor, Setting = new Guid("893dee8e-2bef-41e0-89c6-b55d0929964d"), AcValue = 100, DcValue = 100 },
            new TurboPowerSetting { Name = "PROCTHROTTLEMAX", SubGroup = SubProcessor, Setting = new Guid("bc5038f7-23e0-4960-96da-33abaf5935ec"), AcValue = 100, DcValue = 100 },
            new TurboPowerSetting { Name = "PROCTHROTTLEMAX1", SubGroup = SubProcessor, Setting = new Guid("bc5038f7-23e0-4960-96da-33abaf5935ed"), AcValue = 100, DcValue = 100 },
            new TurboPowerSetting { Name = "PROCTHROTTLEMAX2", SubGroup = SubProcessor, Setting = new Guid("bc5038f7-23e0-4960-96da-33abaf5935ee"), AcValue = 100, DcValue = 100 },
            new TurboPowerSetting { Name = "CPMINCORES", SubGroup = SubProcessor, Setting = new Guid("0cc5b647-c1df-4637-891a-dec35c318583"), AcValue = 100, DcValue = 100 },
            new TurboPowerSetting { Name = "CPMINCORES1", SubGroup = SubProcessor, Setting = new Guid("0cc5b647-c1df-4637-891a-dec35c318584"), AcValue = 100, DcValue = 100 },
            new TurboPowerSetting { Name = "PERFBOOSTMODE", SubGroup = SubProcessor, Setting = new Guid("be337238-0d82-4146-a960-4f3749d470c7"), AcValue = 2, DcValue = 2 },
            new TurboPowerSetting { Name = "PERFEPP", SubGroup = SubProcessor, Setting = new Guid("36687f9e-e3a5-4dbf-b1dc-15eb381c6863"), AcValue = 0, DcValue = 0 },
            new TurboPowerSetting { Name = "PERFEPP1", SubGroup = SubProcessor, Setting = new Guid("36687f9e-e3a5-4dbf-b1dc-15eb381c6864"), AcValue = 0, DcValue = 0 },
            new TurboPowerSetting { Name = "PERFEPP2", SubGroup = SubProcessor, Setting = new Guid("36687f9e-e3a5-4dbf-b1dc-15eb381c6865"), AcValue = 0, DcValue = 0 },
            new TurboPowerSetting { Name = "SYSCOOLPOL", SubGroup = SubProcessor, Setting = new Guid("94d3a615-a299-4be6-96be-d8ef08a4c67a"), AcValue = 1, DcValue = 1 },
            new TurboPowerSetting { Name = "ASPM", SubGroup = SubPciExpress, Setting = new Guid("ee12f906-d277-404b-b6da-e5fa1a576df5"), AcValue = 0, DcValue = 0 },
            new TurboPowerSetting { Name = "ESBATTTHRESHOLD", SubGroup = SubEnergySaver, Setting = new Guid("e69653ca-cf7f-4f05-aa73-cb833fa9064e"), AcValue = 0, DcValue = 0 },
            new TurboPowerSetting { Name = "ESPOLICY", SubGroup = SubEnergySaver, Setting = new Guid("5c5bb349-ad29-4ee2-9d0b-2b25270f7a81"), AcValue = 0, DcValue = 0 },
            new TurboPowerSetting { Name = "ESBRIGHTNESS", SubGroup = SubEnergySaver, Setting = new Guid("13d09884-f74e-474a-a852-b6bde8ad03a8"), AcValue = 100, DcValue = 100 },
            new TurboPowerSetting { Name = "USB_SELECTIVE_SUSPEND", SubGroup = SubUsb, Setting = new Guid("48e6b7a6-50f5-4782-a5d4-53bb8f07e226"), AcValue = 0, DcValue = 0 },
            new TurboPowerSetting { Name = "USB3_LPM", SubGroup = SubUsb, Setting = new Guid("d4e98f31-5ffe-4ce1-be31-1b38b384c009"), AcValue = 0, DcValue = 0 }
        };

        public static bool ShouldEnableEnergySaver(int mode, bool quietAutoEnergySaver) {
            if (mode == 3) return false;
            return mode == 0 && quietAutoEnergySaver;
        }

        public static Guid ExpectedOverlay(int mode) {
            if (mode == 0) return BetterBatteryOverlay;
            if (mode == 1) return BalancedOverlay;
            return MaxPerformanceOverlay;
        }

        public static bool IsEnergySaverActive() {
            return Backend.IsEnergySaverActive();
        }

        public static bool SetEnergySaver(bool enabled) {
            return Backend.SetEnergySaver(enabled);
        }

        public static void SetPowerOverlay(int mode) {
            Backend.SetActiveOverlay(ExpectedOverlay(mode));
        }

        static readonly object syncGate = new object();
        static long nextSequence = 0;
        static long latestProcessed = 0;
        static Guid cachedTurboScheme = Guid.Empty;
        static bool turboSettingsApplied;

        public static string CustomStoreDirectory = null;
        static string StoreDirectory {
            get { return CustomStoreDirectory ?? Store.DirectoryPath; }
        }

        public static TurboOwnership LoadOwnership() {
            lock (syncGate) {
                try {
                    string path = Path.Combine(StoreDirectory, "turbo-ownership.json");
                    if (File.Exists(path)) {
                        return new JavaScriptSerializer().Deserialize<TurboOwnership>(File.ReadAllText(path));
                    }
                } catch {}
                return new TurboOwnership { Owned = false, PreviousScheme = "", TurboScheme = "" };
            }
        }

        public static void SaveOwnership(TurboOwnership ownership) {
            lock (syncGate) {
                try {
                    Directory.CreateDirectory(StoreDirectory);
                    string path = Path.Combine(StoreDirectory, "turbo-ownership.json");
                    File.WriteAllText(path, new JavaScriptSerializer().Serialize(ownership));
                } catch (Exception ex) {
                    Store.Log("Save turbo ownership: " + ex.Message);
                }
            }
        }

        public static void ClearOwnership() {
            lock (syncGate) {
                try {
                    string path = Path.Combine(StoreDirectory, "turbo-ownership.json");
                    if (File.Exists(path)) File.Delete(path);
                    string oldPre = Path.Combine(StoreDirectory, "pre-turbo-scheme.txt");
                    if (File.Exists(oldPre)) File.Delete(oldPre);
                } catch (Exception ex) {
                    Store.Log("Clear turbo ownership: " + ex.Message);
                }
            }
        }

        static Guid FindExistingTurboScheme() {
            if (cachedTurboScheme != Guid.Empty && Backend.SchemeExists(cachedTurboScheme)) return cachedTurboScheme;
            try {
                string savedPath = Path.Combine(StoreDirectory, "turbo-scheme.txt");
                if (File.Exists(savedPath)) {
                    Guid saved;
                    if (Guid.TryParse(File.ReadAllText(savedPath).Trim(), out saved) && Backend.SchemeExists(saved)) {
                        cachedTurboScheme = saved;
                        return saved;
                    }
                }
            } catch {}
            try {
                foreach (var candidate in Backend.EnumerateSchemes()) {
                    if (Backend.ReadFriendlyName(candidate) == TurboSchemeFriendlyName) {
                        cachedTurboScheme = candidate;
                        return candidate;
                    }
                }
            } catch {}
            return Guid.Empty;
        }

        static Guid EnsureTurboScheme() {
            Guid turbo = FindExistingTurboScheme();
            if (turbo == Guid.Empty) {
                Guid[] templates = { UltimatePerformanceScheme, HighPerformanceScheme, BalancedScheme };
                for (int i = 0; i < templates.Length; i++) {
                    Guid src = templates[i];
                    Guid created = Backend.DuplicateScheme(src);
                    if (created != Guid.Empty) {
                        turbo = created;
                        break;
                    }
                }
                if (turbo != Guid.Empty) {
                    cachedTurboScheme = turbo;
                    turboSettingsApplied = false;
                    try {
                        Backend.WriteFriendlyName(turbo, TurboSchemeFriendlyName);
                        Directory.CreateDirectory(StoreDirectory);
                        File.WriteAllText(Path.Combine(StoreDirectory, "turbo-scheme.txt"), turbo.ToString("D"));
                    } catch {}
                }
            }
            if (turbo != Guid.Empty && !turboSettingsApplied) {
                for (int i = 0; i < TurboSettings.Length; i++) {
                    var item = TurboSettings[i];
                    Backend.WriteSetting(turbo, item.SubGroup, item.Setting, item.AcValue, item.DcValue);
                }
                turboSettingsApplied = true;
            }
            return turbo;
        }

        public static void ReconcileStartup(int currentConfirmedMode) {
            lock (syncGate) {
                var ownership = LoadOwnership();
                if (!ownership.Owned) return;

                Guid currentActive = Backend.GetActiveScheme();
                Guid turboGuid = Guid.Empty;
                Guid.TryParse(ownership.TurboScheme, out turboGuid);

                // If user selected an independent plan while app was closed, respect it and drop ownership
                if (currentActive != turboGuid && currentActive != UltimatePerformanceScheme && currentActive != HighPerformanceScheme) {
                    ClearOwnership();
                    Store.Log("User changed power plan while application was closed (" + currentActive + "); dropping turbo ownership.");
                    return;
                }

                // If app is starting in non-Turbo mode but Windows was left in Turbo plan:
                if (currentConfirmedMode >= 0 && currentConfirmedMode != 3) {
                    Guid prev = Guid.Empty;
                    Guid.TryParse(ownership.PreviousScheme, out prev);
                    if (prev != Guid.Empty && Backend.SchemeExists(prev)) {
                        Backend.SetActiveScheme(prev);
                        Store.Log("Startup reconciliation: restored user scheme " + prev);
                    } else {
                        Backend.SetActiveScheme(BalancedScheme);
                        Store.Log("Startup reconciliation: saved scheme missing; fell back to Balanced.");
                    }
                    ClearOwnership();
                }
            }
        }

        public static void Sync(int mode, bool enabled) {
            lock (syncGate) {
                var ownership = LoadOwnership();

                if (mode == 0 || mode == 1 || mode == 2) {
                    if (ownership.Owned) {
                        Guid active = Backend.GetActiveScheme();
                        Guid turboGuid = Guid.Empty;
                        Guid.TryParse(ownership.TurboScheme, out turboGuid);

                        // Only restore if the active plan is still the app-owned Turbo plan
                        if (active == turboGuid || active == UltimatePerformanceScheme || active == HighPerformanceScheme) {
                            Guid target = Guid.Empty;
                            Guid.TryParse(ownership.PreviousScheme, out target);
                            if (target != Guid.Empty && Backend.SchemeExists(target)) {
                                Backend.SetActiveScheme(target);
                                Store.Log("Restored previous power scheme: " + target);
                            } else {
                                // Safe explicit fallback
                                Backend.SetActiveScheme(BalancedScheme);
                                Store.Log("Saved power scheme was missing; fell back to Balanced.");
                            }
                        } else {
                            // User changed power scheme independently while Turbo was active!
                            Store.Log("User selected independent power scheme (" + active + "); respecting user selection.");
                        }
                        ClearOwnership();
                    }

                    // For Quiet/Balanced/Performance, DO NOT alter the user's base power plan!
                    // Only apply overlay and Energy Saver.
                    SetPowerOverlay(mode);
                    if (mode == 0) {
                        if (enabled) {
                            SetEnergySaver(true);
                            Store.Log("Energy saver turned ON with Quiet mode (Best power efficiency).");
                        }
                    } else {
                        SetEnergySaver(false);
                        Store.Log("Energy saver turned OFF for " + (mode == 1 ? "Balanced" : "Performance") + " mode.");
                    }
                } else if (mode == 3) {
                    SetEnergySaver(false);
                    Guid currentActive = Backend.GetActiveScheme();
                    Guid turbo = EnsureTurboScheme();

                    if (!ownership.Owned) {
                        Guid prev = (currentActive != turbo && currentActive != UltimatePerformanceScheme) ? currentActive : BalancedScheme;
                        SaveOwnership(new TurboOwnership {
                            Owned = true,
                            PreviousScheme = prev.ToString("D"),
                            TurboScheme = turbo.ToString("D")
                        });
                    }

                    bool activated = false;
                    try {
                        if (turbo != Guid.Empty) {
                            activated = Backend.SetActiveScheme(turbo);
                        }
                        if (!activated) {
                            Guid high = UltimatePerformanceScheme;
                            if (!Backend.SetActiveScheme(high)) {
                                high = HighPerformanceScheme;
                                activated = Backend.SetActiveScheme(high);
                            } else {
                                activated = true;
                            }
                        }
                    } catch (Exception ex) {
                        Store.Log("Turbo power scheme: " + ex.Message);
                    }
                    SetPowerOverlay(3);
                    Store.Log("Turbo mode: " + TurboSchemeFriendlyName + " active (zero battery saving, max CPU/PCIe power" + (activated ? ", custom Ultimate scheme" : ", overlay fallback") + ").");
                }
            }
        }

        public static void SyncAsync(int mode, bool enabled) {
            long seq = Interlocked.Increment(ref nextSequence);
            Task.Run(() => {
                lock (syncGate) {
                    if (seq < Volatile.Read(ref latestProcessed)) return;
                    Volatile.Write(ref latestProcessed, seq);
                    Sync(mode, enabled);
                }
            });
        }

        public static void DisableEnergySaverInQuietAsync() {
            Task.Run(() => {
                lock (syncGate) {
                    SetEnergySaver(false);
                    SetPowerOverlay(0);
                }
            });
        }
    }
}
