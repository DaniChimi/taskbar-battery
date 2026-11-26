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
                List<BatteryDevice> devices = BluetoothBatterySource.Read();
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
