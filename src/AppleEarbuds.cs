using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarBattery
{
    // Battery of AirPods and Beats, read from the Bluetooth LE advertisements they broadcast while out of the case or
    // with the lid open ("proximity pairing" messages, Apple company id 0x004C, type 0x07). Windows itself shows at
    // most one level for them. The advertisements carry the model and, in steps of 10%, the left bud, the right bud
    // and the case, each with a charging flag. No connection is opened and nothing is sent to the earbuds.
    //
    // Manufacturer data layout (after the company id):
    //   [0] 0x07  [1] length  [2] prefix  [3..4] model, little endian (the product id of the Device ID record)
    //   [5] status: bit 0x20 clear = left and right swapped in the next two bytes
    //   [6] battery: high nibble, low nibble = right, left (swapped as above); 0..10 = tens of percent, 15 = absent
    //   [7] high nibble = charging flags (0x1 right, 0x2 left, swapped as above; 0x4 case), low nibble = case level
    static class AppleEarbuds
    {
        public const ushort AppleCompanyId = 0x004C;
        public const byte ProximityPairing = 0x07;

        // Bluetooth SIG vendor id of Apple in the Device ID record of classic Bluetooth service nodes.
        const string AppleVendorMarker = "VID&0001004C";

        // Product ids of Apple and Beats audio devices share this high byte.
        const int AudioProductFamily = 0x2000;

        const int AbsentLevel = 15;

        // Over-ear and neckband models: one battery, reported on one side.
        static readonly HashSet<int> SingleBattery = new HashSet<int>
        {
            0x200A, // AirPods Max
            0x201F, // AirPods Max (USB-C)
            0x2003, // Powerbeats3
            0x2005, // BeatsX
            0x2006, // Beats Solo3
            0x2009, // Beats Studio3
            0x2010, // Beats Flex
            0x2017, // Beats Studio Pro
            0x2025, // Beats Solo 4
        };

        // A decoded advertisement: either the parts (earbuds) or one level (single-battery models).
        public sealed class Advertisement
        {
            public int ProductId;
            public List<BatteryPart> Parts;
            public int Level = -1;
            public bool Charging;
        }

        // Paired Apple and Beats audio devices, by Bluetooth address, with the id of one of their service nodes (it
        // carries the vendor and product id). One enumeration of the Bluetooth devices, without opening a connection.
        public static Dictionary<string, string> FindDevices()
        {
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IntPtr set = Native.SetupDiGetClassDevs(IntPtr.Zero, "BTHENUM", IntPtr.Zero,
                Native.DIGCF_PRESENT | Native.DIGCF_ALLCLASSES);
            if (set == Native.INVALID_HANDLE_VALUE)
                return found;

            try
            {
                for (int index = 0; ; index++)
                {
                    var data = new Native.SP_DEVINFO_DATA();
                    data.cbSize = Marshal.SizeOf(typeof(Native.SP_DEVINFO_DATA));
                    if (!Native.SetupDiEnumDeviceInfo(set, index, ref data))
                        break;

                    var buffer = new StringBuilder(512);
                    int required;
                    if (!Native.SetupDiGetDeviceInstanceId(set, ref data, buffer, buffer.Capacity, out required))
                        continue;

                    string id = buffer.ToString();
                    if (id.IndexOf(AppleVendorMarker, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if ((ProductIdOf(id) & 0xFF00) != AudioProductFamily)
                        continue;
                    string address = NothingEarbuds.ParseServiceAddress(id);
                    if (address != null)
                        found[address] = id;
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }
            return found;
        }

        // The product id in a service node id ("..._VID&0001004C_PID&2014\..."), or 0.
        public static int ProductIdOf(string instanceId)
        {
            if (instanceId == null)
                return 0;
            int at = instanceId.IndexOf("PID&", StringComparison.OrdinalIgnoreCase);
            int value;
            if (at < 0 || instanceId.Length < at + 8 ||
                !int.TryParse(instanceId.Substring(at + 4, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return 0;
            return value;
        }

        public static bool IsSingleBattery(int productId)
        {
            return SingleBattery.Contains(productId);
        }

        // Decodes the manufacturer data of an Apple advertisement (company id already removed); null when it is not a
        // proximity pairing message of an audio device or carries no level at all.
        public static Advertisement Parse(byte[] data)
        {
            if (data == null || data.Length < 8 || data[0] != ProximityPairing)
                return null;

            int productId = data[3] | (data[4] << 8);
            if ((productId & 0xFF00) != AudioProductFamily)
                return null;

            bool swapped = (data[5] & 0x20) == 0;
            int high = data[6] >> 4;
            int low = data[6] & 0x0F;
            int flags = data[7] >> 4;
            int left = swapped ? high : low;
            int right = swapped ? low : high;
            int caseLevel = data[7] & 0x0F;
            bool leftCharging = (flags & (swapped ? 0x2 : 0x1)) != 0;
            bool rightCharging = (flags & (swapped ? 0x1 : 0x2)) != 0;
            bool caseCharging = (flags & 0x4) != 0;

            var advertisement = new Advertisement { ProductId = productId };
            if (IsSingleBattery(productId))
            {
                int level = IsLevel(right) ? right : left;
                if (!IsLevel(level))
                    return null;
                advertisement.Level = Percent(level);
                advertisement.Charging = IsLevel(right) ? rightCharging : leftCharging;
                return advertisement;
            }

            var parts = new List<BatteryPart>();
            if (IsLevel(left))
                parts.Add(new BatteryPart(PartKind.Left, Percent(left), leftCharging));
            if (IsLevel(right))
                parts.Add(new BatteryPart(PartKind.Right, Percent(right), rightCharging));
            if (IsLevel(caseLevel))
                parts.Add(new BatteryPart(PartKind.Case, Percent(caseLevel), caseCharging));
            if (parts.Count == 0)
                return null;
            advertisement.Parts = parts;
            return advertisement;
        }

        static bool IsLevel(int nibble)
        {
            return nibble <= 10;
        }

        static int Percent(int nibble)
        {
            return nibble * 10;
        }

        // The same levels and charging flags, so repeated advertisements do not redraw the interface.
        public static bool SameReading(Advertisement a, Advertisement b)
        {
            if (a == null || b == null)
                return a == b;
            if (a.Level != b.Level || a.Charging != b.Charging)
                return false;
            if (a.Parts == null || b.Parts == null)
                return a.Parts == b.Parts;
            if (a.Parts.Count != b.Parts.Count)
                return false;
            for (int i = 0; i < a.Parts.Count; i++)
            {
                if (a.Parts[i].Kind != b.Parts[i].Kind || a.Parts[i].Percent != b.Parts[i].Percent ||
                    a.Parts[i].IsCharging != b.Parts[i].IsCharging)
                    return false;
            }
            return true;
        }

        public static string Describe(Advertisement a)
        {
            if (a.Parts == null)
                return a.Level + "%" + (a.Charging ? " charging" : "");
            var parts = new List<string>();
            foreach (BatteryPart part in a.Parts)
                parts.Add(part.Kind + " " + part.Percent + "%" + (part.IsCharging ? " charging" : ""));
            return string.Join(", ", parts.ToArray());
        }
    }
}
