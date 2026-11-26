using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace TaskbarBattery
{
    // A HID top-level collection as Windows exposes it: its path, ids and report sizes.
    sealed class HidCollection
    {
        public string Path;
        public ushort VendorId;
        public ushort ProductId;
        public ushort UsagePage;
        public ushort Usage;
        public int InputReportLength;
        public int OutputReportLength;
    }

    // Enumerates HID collections and opens them for report I/O.
    static class Hid
    {
        public static List<HidCollection> Enumerate(ushort vendorId)
        {
            var result = new List<HidCollection>();
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);

            IntPtr set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == Native.INVALID_HANDLE_VALUE)
                return result;

            try
            {
                for (int index = 0; ; index++)
                {
                    var iface = new SP_DEVICE_INTERFACE_DATA();
                    iface.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, index, ref iface))
                        break;

                    string path = GetInterfacePath(set, ref iface);
                    if (path == null)
                        continue;

                    HidCollection collection = Describe(path);
                    if (collection != null && collection.VendorId == vendorId)
                        result.Add(collection);
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }
            return result;
        }

        static string GetInterfacePath(IntPtr set, ref SP_DEVICE_INTERFACE_DATA iface)
        {
            int required;
            SetupDiGetDeviceInterfaceDetail(set, ref iface, IntPtr.Zero, 0, out required, IntPtr.Zero);
            if (required <= 0)
                return null;

            IntPtr buffer = Marshal.AllocHGlobal(required);
            try
            {
                // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 on 64-bit, 6 on 32-bit.
                Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(set, ref iface, buffer, required, out required, IntPtr.Zero))
                    return null;
                return Marshal.PtrToStringUni(buffer + 4);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // Opens with no access rights to read ids and capabilities, which works even on collections Windows owns.
        static HidCollection Describe(string path)
        {
            using (SafeFileHandle handle = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    return null;

                var attributes = new HIDD_ATTRIBUTES();
                attributes.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                if (!HidD_GetAttributes(handle, ref attributes))
                    return null;

                var collection = new HidCollection
                {
                    Path = path,
                    VendorId = attributes.VendorID,
                    ProductId = attributes.ProductID,
                };

                IntPtr preparsed;
                if (HidD_GetPreparsedData(handle, out preparsed))
                {
                    try
                    {
                        HIDP_CAPS caps;
                        if (HidP_GetCaps(preparsed, out caps) == HIDP_STATUS_SUCCESS)
                        {
                            collection.UsagePage = caps.UsagePage;
                            collection.Usage = caps.Usage;
                            collection.InputReportLength = caps.InputReportByteLength;
                            collection.OutputReportLength = caps.OutputReportByteLength;
                        }
                    }
                    finally
                    {
                        HidD_FreePreparsedData(preparsed);
                    }
                }
                return collection;
            }
        }

        public static HidStream Open(HidCollection collection)
        {
            SafeFileHandle handle = CreateFile(collection.Path, GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (handle.IsInvalid)
                return null;
            return new HidStream(handle, collection.InputReportLength, collection.OutputReportLength);
        }

        const int DIGCF_PRESENT = 0x2;
        const int DIGCF_DEVICEINTERFACE = 0x10;
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ = 0x1;
        const uint FILE_SHARE_WRITE = 0x2;
        const uint OPEN_EXISTING = 3;
        const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        const int HIDP_STATUS_SUCCESS = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")]
        static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool HidD_GetAttributes(SafeFileHandle device, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsedData);

        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid,
            int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, out int requiredSize, IntPtr deviceInfoData);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
            uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
    }

    // Report-level I/O on an opened HID collection, with a timeout on reads.
    sealed class HidStream : IDisposable
    {
        readonly SafeFileHandle handle;
        readonly FileStream stream;

        public HidStream(SafeFileHandle handle, int inputLength, int outputLength)
        {
            this.handle = handle;
            InputLength = inputLength;
            OutputLength = outputLength;
            stream = new FileStream(handle, FileAccess.ReadWrite, Math.Max(1, inputLength), true);
        }

        public int InputLength { get; private set; }
        public int OutputLength { get; private set; }

        // Writes one output report; the buffer is padded or cut to the collection's output report length.
        public bool Write(byte[] report)
        {
            var buffer = new byte[OutputLength];
            Array.Copy(report, buffer, Math.Min(report.Length, buffer.Length));
            try
            {
                stream.Write(buffer, 0, buffer.Length);
                return true;
            }
            catch (IOException)
            {
                Failed = true;
                return false;
            }
        }

        // True once a read or write failed, typically because the device went away; the stream is then unusable.
        public bool Failed { get; private set; }

        // Reads one input report, or returns null when nothing arrives within the timeout.
        public byte[] Read(int timeoutMs)
        {
            return Read(timeoutMs, null);
        }

        // Reads one input report. Returns null on timeout, when 'stop' is signalled, or on failure (see Failed).
        // Waiting costs nothing: the thread sleeps in the kernel until a report arrives or 'stop' is set.
        public byte[] Read(int timeoutMs, WaitHandle stop)
        {
            var buffer = new byte[InputLength];
            IAsyncResult pending;
            try
            {
                pending = stream.BeginRead(buffer, 0, buffer.Length, null, null);
            }
            catch (IOException)
            {
                Failed = true;
                return null;
            }

            WaitHandle[] waits = stop == null
                ? new[] { pending.AsyncWaitHandle }
                : new[] { pending.AsyncWaitHandle, stop };
            if (WaitHandle.WaitAny(waits, timeoutMs) != 0)
            {
                CancelIoEx(handle, IntPtr.Zero);
                try { stream.EndRead(pending); }
                catch (IOException) { }
                catch (OperationCanceledException) { }
                return null;
            }

            try
            {
                int count = stream.EndRead(pending);
                if (count <= 0)
                {
                    Failed = true;
                    return null;
                }
                if (count == buffer.Length)
                    return buffer;
                var trimmed = new byte[count];
                Array.Copy(buffer, trimmed, count);
                return trimmed;
            }
            catch (IOException)
            {
                Failed = true;
                return null;
            }
        }

        public void Dispose()
        {
            stream.Dispose();
            handle.Dispose();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);
    }
}
