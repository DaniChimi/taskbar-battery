using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarBattery
{
    // P/Invoke declarations: SetupAPI for device properties, user32 for icons and DPI, dwmapi for the flyout frame.
    static class Native
    {
        // --- SetupAPI ---

        public const int DIGCF_PRESENT = 0x2;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public const uint DEVPROP_TYPE_BYTE = 0x03;
        public const uint DEVPROP_TYPE_FILETIME = 0x10;
        public const uint DEVPROP_TYPE_BOOLEAN = 0x11;
        public const uint DEVPROP_TYPE_STRING = 0x12;
        public const uint DEVPROP_TYPE_STRING_LIST = 0x2012;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public int DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;

            public DEVPROPKEY(string fmtid, uint pid)
            {
                this.fmtid = new Guid(fmtid);
                this.pid = pid;
            }
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

        public const int DIGCF_ALLCLASSES = 0x4;

        // Every device of an enumerator (for example "BTHENUM"), whatever its class.
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr hwndParent, int flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, int memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
            StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiGetDeviceProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
            ref DEVPROPKEY propertyKey, out uint propertyType, byte[] propertyBuffer, int propertyBufferSize,
            out int requiredSize, int flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        // --- Windows ---

        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int CS_DROPSHADOW = 0x20000;

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr obj);

        // --- DWM (Windows 11 window frame) ---

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_BORDER_COLOR = 34;
        public const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

        // --- Winsock (Bluetooth RFCOMM) ---

        public const int AF_BTH = 32;
        public const int SOCK_STREAM = 1;
        public const int BTHPROTO_RFCOMM = 3;
        public static readonly IntPtr INVALID_SOCKET = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SOCKADDR_BTH
        {
            public ushort addressFamily;
            public ulong btAddr;
            public Guid serviceClassId;
            public uint port;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WSADATA
        {
            public short version;
            public short highVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 400)]
            public byte[] reserved;
        }

        [DllImport("ws2_32.dll")]
        public static extern int WSAStartup(ushort version, out WSADATA data);

        [DllImport("ws2_32.dll")]
        public static extern int WSAGetLastError();

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern IntPtr socket(int addressFamily, int type, int protocol);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int connect(IntPtr s, ref SOCKADDR_BTH address, int length);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int send(IntPtr s, byte[] buffer, int length, int flags);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int recv(IntPtr s, byte[] buffer, int length, int flags);

        [DllImport("ws2_32.dll")]
        public static extern int closesocket(IntPtr s);

        // --- Console ---

        public const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachConsole(int processId);
    }
}
