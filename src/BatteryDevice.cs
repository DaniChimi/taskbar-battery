using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskbarBattery
{
    enum DeviceKind { Other, Mouse, Keyboard, Gamepad, Headphones, Phone }

    enum ChargeState { Unknown, Discharging, Charging, Full }

    // The batteries of a device made of several pieces, such as true wireless earbuds.
    enum PartKind { Left, Right, Case }

    sealed class BatteryPart
    {
        public BatteryPart(PartKind kind, int percent, bool charging)
        {
            Kind = kind;
            Percent = Math.Max(0, Math.Min(100, percent));
            IsCharging = charging;
        }

        public PartKind Kind { get; private set; }
        public int Percent { get; private set; }
        public bool IsCharging { get; private set; }

        public bool IsBud
        {
            get { return Kind != PartKind.Case; }
        }
    }

    // A device that reports its battery level, as seen at the time of the last scan.
    sealed class BatteryDevice
    {
        public BatteryDevice(string address, string name, string instanceId, DeviceKind kind,
            int percent, DateTime? lastUpdated, bool isConnected, DateTime? connectedSince, string manufacturer)
        {
            Address = address;
            Name = name;
            InstanceId = instanceId;
            Kind = kind;
            Percent = Math.Max(0, Math.Min(100, percent));
            LastUpdated = lastUpdated;
            IsConnected = isConnected;
            ConnectedSince = connectedSince;
            Manufacturer = manufacturer;
        }

        // Stable identifier across scans (the Bluetooth address).
        public string Address { get; private set; }
        public string Name { get; private set; }
        public string InstanceId { get; private set; }
        public DeviceKind Kind { get; private set; }
        public int Percent { get; private set; }

        // Local time at which Windows last received a battery report, if known.
        public DateTime? LastUpdated { get; private set; }

        public bool IsConnected { get; private set; }

        // Local time of the current connection, if known.
        public DateTime? ConnectedSince { get; private set; }

        // Maker name derived from the vendor id, or null when the vendor is not known.
        public string Manufacturer { get; private set; }

        // Reported by the vendor protocol (HID++ for Logitech); Unknown when the device has no such channel,
        // because Windows does not expose a charging state for Bluetooth devices.
        public ChargeState Charge { get; set; }

        public bool IsCharging
        {
            get { return Charge == ChargeState.Charging; }
        }

        // Applies a reading from the vendor protocol, which is fresher than the level Windows stored.
        // A zero level is ignored: devices that only report coarse levels send 0 there.
        public void ApplyVendorReading(int percent, ChargeState charge)
        {
            if (percent > 0 && percent <= 100)
                Percent = percent;
            Charge = charge;
        }

        // Earbuds read over their own control channel: Windows has no level for them, so they are shown only once
        // the channel has reported (see HasReading).
        public bool IsEarbuds { get; set; }

        // Left bud, right bud and case, for earbuds; null for single-battery devices.
        public List<BatteryPart> Parts { get; private set; }

        public bool HasReading
        {
            get { return !IsEarbuds || Parts != null; }
        }

        // Applies an earbuds reading. The device level is the emptier bud, the one that will stop first (the case
        // counts only when no bud reported); it is charging when every reported bud charges, full when they are all
        // at 100% on charge.
        public void ApplyParts(List<BatteryPart> parts, DateTime time)
        {
            Parts = parts;
            LastUpdated = time;
            List<BatteryPart> buds = parts.Where(p => p.IsBud).ToList();
            List<BatteryPart> basis = buds.Count > 0 ? buds : parts;
            Percent = basis.Min(p => p.Percent);
            if (basis.All(p => p.IsCharging))
                Charge = basis.All(p => p.Percent >= 100) ? ChargeState.Full : ChargeState.Charging;
            else
                Charge = ChargeState.Discharging;
        }

        // Percentage at or below which a battery counts as low; follows the user's setting.
        public static int LowThreshold = 20;

        public bool IsLow
        {
            get { return Percent <= LowThreshold && !IsCharging; }
        }

        // Name chosen by the user, or null to use the name Windows reports.
        public string CustomName { get; set; }

        // The name to show everywhere in the interface.
        public string DisplayName
        {
            get { return string.IsNullOrEmpty(CustomName) ? Name : CustomName; }
        }

        // Maps a Windows device container category (for example "Input.Mouse") to a device kind.
        public static DeviceKind KindFromCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                return DeviceKind.Other;

            string c = category.ToLowerInvariant();
            if (c.Contains("mouse")) return DeviceKind.Mouse;
            if (c.Contains("keyboard")) return DeviceKind.Keyboard;
            if (c.Contains("gaming") || c.Contains("gamepad")) return DeviceKind.Gamepad;
            if (c.Contains("headset") || c.Contains("headphone") || c.Contains("audio")) return DeviceKind.Headphones;
            if (c.Contains("phone")) return DeviceKind.Phone;
            return DeviceKind.Other;
        }
    }
}
