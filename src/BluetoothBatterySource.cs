using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarBattery
{
    // Reads the battery level that Windows itself stores for paired Bluetooth devices.
    // Windows reads the standard GATT Battery Service (0x180F) and the HFP battery indicator, and keeps the last value
    // as a device property, so no vendor protocol is needed for devices that expose it.
    static class BluetoothBatterySource
    {
        // Devices whose connection state is unknown count as connected if they reported within this window.
        static readonly TimeSpan RecentReport = TimeSpan.FromHours(1);

        static readonly Guid BluetoothClass = new Guid("e0cbf06c-cd8b-4647-bb8a-263b43f0f974");

        // Battery level in percent (DEVPROP_TYPE_BYTE).
        static readonly Native.DEVPROPKEY BatteryLevel = new Native.DEVPROPKEY("104EA319-6EE2-4701-BD47-8DDBF425BBE5", 2);

        // Time of the last battery report (DEVPROP_TYPE_FILETIME).
        static readonly Native.DEVPROPKEY BatteryUpdated = new Native.DEVPROPKEY("104EA319-6EE2-4701-BD47-8DDBF425BBE5", 7);

        // Whether the device is currently connected (DEVPROP_TYPE_BOOLEAN).
        static readonly Native.DEVPROPKEY IsConnected = new Native.DEVPROPKEY("83DA6326-97A6-4088-9453-A1923F573B29", 15);

        // DEVPKEY_DeviceContainer_Category, for example "Input.Mouse" (DEVPROP_TYPE_STRING_LIST).
        static readonly Native.DEVPROPKEY Category = new Native.DEVPROPKEY("78C34FC8-104A-4ACA-9EA4-524D52996E57", 90);

        // DEVPKEY_Bluetooth_LastConnectedTime (DEVPROP_TYPE_FILETIME).
        static readonly Native.DEVPROPKEY LastConnected = new Native.DEVPROPKEY("2BD67D8B-8BEB-48D5-87E0-6CDA3428040A", 11);

        // DEVPKEY_Device_Children: the GATT service nodes, whose ids carry the vendor id (DEVPROP_TYPE_STRING_LIST).
        static readonly Native.DEVPROPKEY Children = new Native.DEVPROPKEY("4340a6c5-93fa-4706-972c-7b648008a5a7", 9);

        static readonly Native.DEVPROPKEY FriendlyName = new Native.DEVPROPKEY("a45c254e-df1c-4efd-8020-67d146a850e0", 14);
        static readonly Native.DEVPROPKEY DeviceName = new Native.DEVPROPKEY("b725f130-47ef-101a-a5f1-02608c9ebac0", 10);

        // Returns every paired device that reports a battery level, connected or not.
        public static List<BatteryDevice> Read()
        {
            var byAddress = new Dictionary<string, BatteryDevice>(StringComparer.OrdinalIgnoreCase);
            Guid classGuid = BluetoothClass;
            IntPtr set = Native.SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, Native.DIGCF_PRESENT);
            if (set == Native.INVALID_HANDLE_VALUE)
                throw new InvalidOperationException("SetupDiGetClassDevs failed with error " + Marshal.GetLastWin32Error());

            try
            {
                for (int index = 0; ; index++)
                {
                    var data = new Native.SP_DEVINFO_DATA();
                    data.cbSize = Marshal.SizeOf(typeof(Native.SP_DEVINFO_DATA));
                    if (!Native.SetupDiEnumDeviceInfo(set, index, ref data))
                        break;

                    BatteryDevice device = ReadDevice(set, ref data);
                    if (device == null)
                        continue;

                    // A dual-mode device appears both as BTHENUM and BTHLE: keep the connected or most recent one.
                    BatteryDevice existing;
                    if (!byAddress.TryGetValue(device.Address, out existing) || IsBetter(device, existing))
                        byAddress[device.Address] = device;
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }

            return byAddress.Values
                .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        static BatteryDevice ReadDevice(IntPtr set, ref Native.SP_DEVINFO_DATA data)
        {
            string instanceId = GetInstanceId(set, ref data);
            string address = ParseAddress(instanceId);
            if (address == null)
                return null;

            byte[] level = GetProperty(set, ref data, BatteryLevel, Native.DEVPROP_TYPE_BYTE);
            if (level == null || level.Length < 1)
                return null;

            DateTime? updated = GetFileTime(set, ref data, BatteryUpdated);

            bool connected;
            byte[] connectedRaw = GetProperty(set, ref data, IsConnected, Native.DEVPROP_TYPE_BOOLEAN);
            if (connectedRaw != null && connectedRaw.Length >= 1)
                connected = connectedRaw[0] != 0;
            else
                connected = updated.HasValue && DateTime.Now - updated.Value < RecentReport;

            string category = JoinList(GetStringList(set, ref data, Category));
            string name = GetString(set, ref data, FriendlyName) ?? GetString(set, ref data, DeviceName) ?? address;
            DateTime? connectedSince = connected ? GetFileTime(set, ref data, LastConnected) : null;
            string manufacturer = Vendors.FromDeviceIds(GetStringList(set, ref data, Children));

            return new BatteryDevice(address, name, instanceId, BatteryDevice.KindFromCategory(category),
                level[0], updated, connected, connectedSince, manufacturer);
        }

        static DateTime? GetFileTime(IntPtr set, ref Native.SP_DEVINFO_DATA data, Native.DEVPROPKEY key)
        {
            byte[] raw = GetProperty(set, ref data, key, Native.DEVPROP_TYPE_FILETIME);
            if (raw == null || raw.Length < 8)
                return null;
            long value = BitConverter.ToInt64(raw, 0);
            if (value <= 0)
                return null;
            return DateTime.FromFileTimeUtc(value).ToLocalTime();
        }

        static string JoinList(string[] parts)
        {
            return parts == null || parts.Length == 0 ? null : string.Join(",", parts);
        }

        static bool IsBetter(BatteryDevice candidate, BatteryDevice current)
        {
            if (candidate.IsConnected != current.IsConnected)
                return candidate.IsConnected;
            DateTime a = candidate.LastUpdated ?? DateTime.MinValue;
            DateTime b = current.LastUpdated ?? DateTime.MinValue;
            return a > b;
        }

        // Extracts the 12-digit Bluetooth address from a top-level device id such as "BTHLE\DEV_D01D39A84CA6\...".
        // Child service nodes ("BTHLEDEVICE\{uuid}_DEV_...") return null, so each device is counted once.
        internal static string ParseAddress(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            const string marker = "\\DEV_";
            int start = instanceId.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return null;

            start += marker.Length;
            if (instanceId.Length < start + 12)
                return null;

            string address = instanceId.Substring(start, 12);
            foreach (char c in address)
            {
                if (!Uri.IsHexDigit(c))
                    return null;
            }
            return address.ToUpperInvariant();
        }

        static string GetInstanceId(IntPtr set, ref Native.SP_DEVINFO_DATA data)
        {
            var buffer = new StringBuilder(512);
            int required;
            if (!Native.SetupDiGetDeviceInstanceId(set, ref data, buffer, buffer.Capacity, out required))
                return null;
            return buffer.ToString();
        }

        static string GetString(IntPtr set, ref Native.SP_DEVINFO_DATA data, Native.DEVPROPKEY key)
        {
            byte[] raw = GetProperty(set, ref data, key, Native.DEVPROP_TYPE_STRING);
            if (raw == null)
                return null;

            string value = Encoding.Unicode.GetString(raw).TrimEnd('\0').Trim();
            return value.Length == 0 ? null : value;
        }

        // Splits a REG_MULTI_SZ-style list.
        static string[] GetStringList(IntPtr set, ref Native.SP_DEVINFO_DATA data, Native.DEVPROPKEY key)
        {
            byte[] raw = GetProperty(set, ref data, key, Native.DEVPROP_TYPE_STRING_LIST);
            if (raw == null)
                return null;
            return Encoding.Unicode.GetString(raw).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        }

        static byte[] GetProperty(IntPtr set, ref Native.SP_DEVINFO_DATA data, Native.DEVPROPKEY key, uint expectedType)
        {
            uint type;
            int required;
            Native.SetupDiGetDeviceProperty(set, ref data, ref key, out type, null, 0, out required, 0);
            if (required <= 0)
                return null;

            var buffer = new byte[required];
            if (!Native.SetupDiGetDeviceProperty(set, ref data, ref key, out type, buffer, buffer.Length, out required, 0))
                return null;

            return type == expectedType ? buffer : null;
        }
    }
}
