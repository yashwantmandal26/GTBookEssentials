using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace GTBook {
    public struct TurboPowerSetting {
        public string Name;
        public Guid SubGroup;
        public Guid Setting;
        public uint AcValue;
        public uint DcValue;
    }

    public static class EnergySaver {
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
            try {
                ulong state = WNF_PO_ENERGY_SAVER_OVERRIDE;
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

        public static bool SetEnergySaver(bool enabled) {
            bool ok = false;
            try {
                ulong state = WNF_PO_ENERGY_SAVER_OVERRIDE;
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

        public static void SetPowerOverlay(int mode) {
            try {
                PowerSetActiveOverlayScheme(ExpectedOverlay(mode));
            } catch (Exception ex) {
                Store.Log("Power overlay: " + ex.Message);
            }
        }

        static string ReadFriendlyName(Guid scheme) {
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

        static Guid FindExistingTurboScheme() {
            try {
                string savedPath = Path.Combine(Store.DirectoryPath, "turbo-scheme.txt");
                if (File.Exists(savedPath)) {
                    Guid saved;
                    if (Guid.TryParse(File.ReadAllText(savedPath).Trim(), out saved) && ReadFriendlyName(saved) != null) return saved;
                }
            } catch {}
            IntPtr buf = Marshal.AllocHGlobal(16);
            try {
                for (uint idx = 0; ; idx++) {
                    uint size = 16;
                    uint res = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16, idx, buf, ref size);
                    if (res != 0) break;
                    Guid candidate = (Guid)Marshal.PtrToStructure(buf, typeof(Guid));
                    if (ReadFriendlyName(candidate) == TurboSchemeFriendlyName) return candidate;
                }
            } catch {} finally {
                Marshal.FreeHGlobal(buf);
            }
            return Guid.Empty;
        }

        static Guid EnsureTurboScheme() {
            Guid turbo = FindExistingTurboScheme();
            if (turbo == Guid.Empty) {
                Guid[] templates = { UltimatePerformanceScheme, HighPerformanceScheme, BalancedScheme };
                for (int i = 0; i < templates.Length; i++) {
                    Guid src = templates[i];
                    IntPtr dstPtr;
                    if (PowerDuplicateScheme(IntPtr.Zero, ref src, out dstPtr) == 0 && dstPtr != IntPtr.Zero) {
                        try {
                            turbo = (Guid)Marshal.PtrToStructure(dstPtr, typeof(Guid));
                            break;
                        } finally {
                            LocalFree(dstPtr);
                        }
                    }
                }
                if (turbo != Guid.Empty) {
                    try {
                        byte[] nameBytes = Encoding.Unicode.GetBytes(TurboSchemeFriendlyName + "\0");
                        PowerWriteFriendlyName(IntPtr.Zero, ref turbo, IntPtr.Zero, IntPtr.Zero, nameBytes, (uint)nameBytes.Length);
                        Directory.CreateDirectory(Store.DirectoryPath);
                        File.WriteAllText(Path.Combine(Store.DirectoryPath, "turbo-scheme.txt"), turbo.ToString("D"));
                    } catch {}
                }
            }
            if (turbo != Guid.Empty) {
                for (int i = 0; i < TurboSettings.Length; i++) {
                    var item = TurboSettings[i];
                    Guid sub = item.SubGroup;
                    Guid set = item.Setting;
                    PowerWriteACValueIndex(IntPtr.Zero, ref turbo, ref sub, ref set, item.AcValue);
                    PowerWriteDCValueIndex(IntPtr.Zero, ref turbo, ref sub, ref set, item.DcValue);
                }
            }
            return turbo;
        }

        public static void Sync(int mode, bool enabled) {
            if (mode == 0) {
                try { var b = BalancedScheme; PowerSetActiveScheme(IntPtr.Zero, ref b); } catch {}
                SetPowerOverlay(0);
                if (enabled) {
                    SetEnergySaver(true);
                    Store.Log("Energy saver turned ON with Quiet mode (Best power efficiency).");
                }
            } else if (mode == 1 || mode == 2) {
                SetEnergySaver(false);
                try { var b = BalancedScheme; PowerSetActiveScheme(IntPtr.Zero, ref b); } catch {}
                SetPowerOverlay(mode);
                Store.Log("Energy saver turned OFF for " + (mode == 1 ? "Balanced" : "Performance") + " mode.");
            } else if (mode == 3) {
                SetEnergySaver(false);
                bool activatedTurbo = false;
                try {
                    Guid turbo = EnsureTurboScheme();
                    if (turbo != Guid.Empty) {
                        activatedTurbo = PowerSetActiveScheme(IntPtr.Zero, ref turbo) == 0;
                    }
                    if (!activatedTurbo) {
                        Guid high = UltimatePerformanceScheme;
                        if (PowerSetActiveScheme(IntPtr.Zero, ref high) != 0) {
                            high = HighPerformanceScheme;
                            activatedTurbo = PowerSetActiveScheme(IntPtr.Zero, ref high) == 0;
                        } else {
                            activatedTurbo = true;
                        }
                    }
                } catch (Exception ex) {
                    Store.Log("Turbo power scheme: " + ex.Message);
                }
                SetPowerOverlay(3);
                Store.Log("Turbo mode: " + TurboSchemeFriendlyName + " active (zero battery saving, max CPU/PCIe power" + (activatedTurbo ? ", custom Ultimate scheme" : ", overlay fallback") + ").");
            }
        }
    }
}
