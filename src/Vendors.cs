using System;
using System.Collections.Generic;
using System.Globalization;

namespace TaskbarBattery
{
    // Maker names for the vendor ids found in Bluetooth LE device ids ("VID&02046d": source 02 = USB-IF vendor id,
    // source 01 = Bluetooth SIG company id). Only well-known makers are listed; unknown ids show no maker.
    static class Vendors
    {
        static readonly Dictionary<int, string> UsbVendors = new Dictionary<int, string>
        {
            { 0x046D, "Logitech" },
            { 0x045E, "Microsoft" },
            { 0x05AC, "Apple" },
            { 0x054C, "Sony" },
            { 0x057E, "Nintendo" },
            { 0x04E8, "Samsung" },
            { 0x1532, "Razer" },
            { 0x1038, "SteelSeries" },
            { 0x1B1C, "Corsair" },
            { 0x2DC8, "8BitDo" },
        };

        static readonly Dictionary<int, string> BluetoothCompanies = new Dictionary<int, string>
        {
            { 0x0006, "Microsoft" },
            { 0x004C, "Apple" },
            { 0x0075, "Samsung" },
            { 0x01DA, "Logitech" },
            { 0x02B0, "Nothing" },
        };

        // Looks for "VID&" followed by the source and a four-digit id in any of the given device ids: Bluetooth LE ids
        // have a two-digit source ("VID&02046d"), classic Bluetooth service ids a four-digit one ("VID&000102b0").
        public static string FromDeviceIds(IEnumerable<string> ids)
        {
            if (ids == null)
                return null;

            foreach (string id in ids)
            {
                int at = id.IndexOf("VID&", StringComparison.OrdinalIgnoreCase);
                if (at < 0 || id.Length < at + 10)
                    continue;

                int digits = 0;
                while (at + 4 + digits < id.Length && Uri.IsHexDigit(id[at + 4 + digits]))
                    digits++;
                int sourceLength = digits >= 8 ? 4 : 2;
                if (digits < sourceLength + 4)
                    continue;

                string source = id.Substring(at + 4 + sourceLength - 2, 2);
                int vendor;
                if (!int.TryParse(id.Substring(at + 4 + sourceLength, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vendor))
                    continue;

                string name;
                if (source == "02" && UsbVendors.TryGetValue(vendor, out name))
                    return name;
                if (source == "01" && BluetoothCompanies.TryGetValue(vendor, out name))
                    return name;
            }
            return null;
        }
    }
}
