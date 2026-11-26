using System;
using System.Collections.Generic;
using System.IO;

namespace TaskbarBattery
{
    // "--probe": prints what the battery sources see, for troubleshooting from a terminal.
    static class Probe
    {
        public static int Run()
        {
            Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
            var output = new StreamWriter(Console.OpenStandardOutput());
            output.AutoFlush = true;

            try
            {
                output.WriteLine();
                output.WriteLine("Bluetooth battery (Windows device properties):");
                Dictionary<string, string> earbuds = NothingEarbuds.FindDevices();
                List<BatteryDevice> devices = BluetoothBatterySource.Read(earbuds);
                if (devices.Count == 0)
                    output.WriteLine("  no device reports a battery level");

                foreach (BatteryDevice device in devices)
                {
                    output.WriteLine("  {0}: {1}%  kind={2}  connected={3}  maker={4}",
                        device.Name, device.Percent, device.Kind, device.IsConnected, device.Manufacturer ?? "?");
                    output.WriteLine("    updated {0}  connected since {1}",
                        Describe(device.LastUpdated), Describe(device.ConnectedSince));
                    output.WriteLine("    {0}", device.InstanceId);
                }

                output.WriteLine();
                output.WriteLine("Logitech HID collections:");
                List<HidCollection> collections = Hid.Enumerate(LogitechHidpp.LogitechVendorId);
                foreach (HidCollection c in collections)
                {
                    output.WriteLine("  page=0x{0:X4} usage=0x{1:X4} in={2} out={3} hidpp={4}",
                        c.UsagePage, c.Usage, c.InputReportLength, c.OutputReportLength, LogitechHidpp.IsHidppCollection(c));
                    output.WriteLine("    {0}", c.Path);
                }

                output.WriteLine();
                output.WriteLine("HID++ battery:");
                foreach (BatteryDevice device in devices)
                {
                    if (!device.IsConnected || device.Manufacturer != "Logitech")
                        continue;
                    LogitechHidpp.Reading reading = LogitechHidpp.Read(device.Address, collections);
                    if (reading == null)
                        output.WriteLine("  {0}: no answer", device.Name);
                    else
                        output.WriteLine("  {0}: {1}%  charge={2}  feature={3}", device.Name, reading.Percent, reading.Charge, reading.Feature);
                }

                output.WriteLine();
                output.WriteLine("Nothing / CMF earbuds (control channel):");
                if (earbuds.Count == 0)
                    output.WriteLine("  no paired earbuds expose the control service");
                foreach (BatteryDevice device in devices)
                {
                    if (!device.IsEarbuds)
                        continue;
                    if (!device.IsConnected)
                    {
                        output.WriteLine("  {0}: not connected", device.Name);
                        continue;
                    }
                    List<BatteryPart> parts = null;
                    using (var answered = new System.Threading.ManualResetEvent(false))
                    using (var listener = new EarbudsBatteryListener(device.Address))
                    {
                        listener.Changed += delegate(string address, List<BatteryPart> reported)
                        {
                            parts = reported;
                            answered.Set();
                        };
                        answered.WaitOne(TimeSpan.FromSeconds(5));
                    }
                    if (parts == null)
                    {
                        output.WriteLine("  {0}: no answer", device.Name);
                        continue;
                    }
                    var described = new List<string>();
                    foreach (BatteryPart part in parts)
                        described.Add(part.Kind + " " + part.Percent + "%" + (part.IsCharging ? " charging" : ""));
                    output.WriteLine("  {0}: {1}", device.Name, string.Join(", ", described.ToArray()));
                }

                output.WriteLine();
                output.WriteLine("Logitech USB (receivers, cables, dongles):");
                int usbInterfaces = collections.FindAll(c => c.IsUsb).Count;
                if (usbInterfaces == 0)
                {
                    output.WriteLine("  no Logitech USB device or receiver plugged in");
                }
                else
                {
                    using (var monitor = new LogitechUsbMonitor())
                    {
                        // Discovery and the first readings run on the monitor's thread; sleeping devices take a while.
                        System.Threading.Thread.Sleep(8000);
                        List<BatteryDevice> usb = monitor.Devices();
                        if (usb.Count == 0)
                            output.WriteLine("  no device answered (see the log for details)");
                        foreach (BatteryDevice device in usb)
                            output.WriteLine("  {0}: {1}%  charge={2}  kind={3}  id={4}",
                                device.Name, device.Percent, device.Charge, device.Kind, device.Address);
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                output.WriteLine("Probe failed: " + ex);
                return 1;
            }
        }

        static string Describe(DateTime? time)
        {
            return time.HasValue ? time.Value.ToString("yyyy-MM-dd HH:mm:ss") : "unknown";
        }
    }
}
