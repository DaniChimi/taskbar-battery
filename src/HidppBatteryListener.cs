using System;
using System.Diagnostics;
using System.Threading;

namespace TaskbarBattery
{
    // Keeps a Logitech device's HID++ channel open and waits for its battery notifications, so a plugged or
    // unplugged cable shows up at once instead of at the next scan. The thread sleeps in the kernel between
    // notifications: no polling, no CPU while nothing happens. It ends when the device goes away, when the
    // device has no battery feature, or on Dispose; the owner recreates it on a later scan if needed.
    sealed class HidppBatteryListener : IDisposable
    {
        readonly string address;
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly Thread thread;

        // Raised on the listener thread.
        public event Action<string, LogitechHidpp.Reading> Changed;

        public HidppBatteryListener(string bluetoothAddress)
        {
            address = bluetoothAddress;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "HID++ battery " + bluetoothAddress;
            thread.Start();
        }

        public bool IsAlive
        {
            get { return thread.IsAlive; }
        }

        void Run()
        {
            try
            {
                HidCollection collection = LogitechHidpp.FindCollection(address, Hid.Enumerate(LogitechHidpp.LogitechVendorId));
                if (collection == null)
                    return;

                using (HidStream stream = Hid.Open(collection))
                {
                    if (stream == null)
                        return;

                    LogitechHidpp.BatteryFeature feature = LogitechHidpp.FindBatteryFeature(stream);
                    if (feature == null)
                        return;

                    while (!stop.WaitOne(0))
                    {
                        byte[] report = stream.Read(Timeout.Infinite, stop);
                        if (report == null)
                        {
                            if (stream.Failed)
                                return;
                            continue;
                        }

                        LogitechHidpp.Reading reading = LogitechHidpp.ParseNotification(report, feature);
                        Action<string, LogitechHidpp.Reading> handler = Changed;
                        if (reading != null && handler != null)
                            handler(address, reading);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("HID++ listener for " + address + " stopped: " + ex);
            }
        }

        // Signals the thread to stop without waiting for it; it exits as soon as its pending read is cancelled.
        public void Dispose()
        {
            stop.Set();
        }
    }
}
