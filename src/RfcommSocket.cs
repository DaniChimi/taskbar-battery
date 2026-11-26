using System;
using System.Runtime.InteropServices;

namespace TaskbarBattery
{
    // A Bluetooth RFCOMM connection to one service of a paired device, through the Windows Bluetooth sockets.
    // Receive blocks in the kernel until data arrives; Dispose closes the socket, which ends a pending Receive.
    sealed class RfcommSocket : IDisposable
    {
        static readonly object startupLock = new object();
        static bool started;

        readonly object sendLock = new object();
        IntPtr handle;

        RfcommSocket(IntPtr handle)
        {
            this.handle = handle;
        }

        // Connects to the service with the given class id; Windows looks up its RFCOMM channel through SDP.
        // Throws when the device is out of range or does not accept the connection.
        public static RfcommSocket Connect(string address, Guid service)
        {
            EnsureStarted();
            IntPtr s = Native.socket(Native.AF_BTH, Native.SOCK_STREAM, Native.BTHPROTO_RFCOMM);
            if (s == Native.INVALID_SOCKET)
                throw new InvalidOperationException("socket failed with error " + Native.WSAGetLastError());

            var target = new Native.SOCKADDR_BTH
            {
                addressFamily = Native.AF_BTH,
                btAddr = Convert.ToUInt64(address, 16),
                serviceClassId = service,
                port = 0,
            };
            if (Native.connect(s, ref target, Marshal.SizeOf(typeof(Native.SOCKADDR_BTH))) != 0)
            {
                int error = Native.WSAGetLastError();
                Native.closesocket(s);
                throw new InvalidOperationException("connect failed with error " + error);
            }
            return new RfcommSocket(s);
        }

        static void EnsureStarted()
        {
            lock (startupLock)
            {
                if (started)
                    return;
                Native.WSADATA data;
                int error = Native.WSAStartup(0x0202, out data);
                if (error != 0)
                    throw new InvalidOperationException("WSAStartup failed with error " + error);
                started = true;
            }
        }

        // Safe to call from any thread while another one is blocked in Receive.
        public bool Send(byte[] data)
        {
            lock (sendLock)
            {
                IntPtr s = handle;
                return s != IntPtr.Zero && Native.send(s, data, data.Length, 0) == data.Length;
            }
        }

        // Waits for data; returns the number of bytes read, or 0 when the connection ended or was closed.
        public int Receive(byte[] buffer)
        {
            IntPtr s = handle;
            if (s == IntPtr.Zero)
                return 0;
            int read = Native.recv(s, buffer, buffer.Length, 0);
            return read > 0 ? read : 0;
        }

        public void Dispose()
        {
            IntPtr s;
            lock (sendLock)
            {
                s = handle;
                handle = IntPtr.Zero;
            }
            if (s != IntPtr.Zero)
                Native.closesocket(s);
        }
    }
}
