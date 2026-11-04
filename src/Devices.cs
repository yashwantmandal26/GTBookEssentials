using System;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace GTBook {
    public sealed class RearDevice : IDisposable {
        readonly SerialPort port;
        public static string FindPort() {
            using (var query = new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'ACPI%PNP0501%'"))
            using (var devices = query.Get()) {
                foreach (ManagementObject device in devices) {
                    if (!Convert.ToString(device["PNPDeviceID"]).Equals(@"ACPI\PNP0501\SERIALIOUART1", StringComparison.OrdinalIgnoreCase)) continue;
                    Match m = Regex.Match(Convert.ToString(device["Name"]), @"\((COM\d+)\)", RegexOptions.IgnoreCase);
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            throw new IOException("Internal rear-light UART not found. No unrelated COM port was opened.");
        }
        public RearDevice(string name) {
            port = new SerialPort(name, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 400, WriteTimeout = 600, DtrEnable = false, RtsEnable = false };
            try { port.Open(); } catch { port.Dispose(); throw; }
        }
        public void Send(byte[] packet) {
            port.DiscardInBuffer(); port.Write(packet, 0, packet.Length);
            // Require the four-byte controller acknowledgement. This proves receipt,
            // not that every lighting parameter is visually implemented by firmware.
            byte[] ack = new byte[4]; int offset = 0;
            while (offset < 4) offset += port.Read(ack, offset, 4-offset);
            if (ack[0] != 0x34 || ack[1] != 0x0e || ack[2] != (packet[2] | 0x80) || ack[3] != (byte)((ack[0] + ack[1] + ack[2]) & 255)) throw new IOException("Rear controller returned an unexpected acknowledgement.");
        }
        public void Dispose() { port.Dispose(); }
    }
    public sealed class KeyboardDevice : IDisposable {
        SafeFileHandle handle;
        [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public int Size; public Guid Guid; public int Flags; public IntPtr Reserved; }
        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr device);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteFile(SafeFileHandle file, byte[] data, uint count, out uint written, IntPtr overlapped);
        public KeyboardDevice() {
            Guid guid; HidD_GetHidGuid(out guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
            if (set == new IntPtr(-1)) throw new Win32Exception();
            try {
                for (uint i = 0; ; i++) {
                    var data = new InterfaceData { Size = Marshal.SizeOf(typeof(InterfaceData)) };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data)) {
                        int error = Marshal.GetLastWin32Error();
                        if (error != 259) throw new Win32Exception(error);
                        break;
                    }
                    uint size; SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out size, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal((int)size);
                    try {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, size, out size, IntPtr.Zero)) throw new Win32Exception();
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4)).ToLowerInvariant();
                        if (!path.Contains("vid_340e&pid_8002") || !path.Contains("mi_01")) continue;
                        var opened = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                        if (opened.IsInvalid) { opened.Dispose(); throw new IOException("Keyboard controller is busy or access was denied. Close other RGB utilities."); }
                        handle = opened; return;
                    } finally { Marshal.FreeHGlobal(detail); }
                }
                throw new IOException("Keyboard controller 340E:8002 / MI_01 was not found.");
            } finally { SetupDiDestroyDeviceInfoList(set); }
        }
        public void Send(byte[] packet) {
            uint written;
            if (!WriteFile(handle, packet, (uint)packet.Length, out written, IntPtr.Zero)) throw new Win32Exception();
            if (written != packet.Length) throw new IOException("Incomplete keyboard write. Some zones may have changed; retry after Refresh.");
        }
        public void Dispose() { if (handle != null) handle.Dispose(); }
    }
}
