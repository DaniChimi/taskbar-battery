using System;
using System.Collections.Generic;
using System.IO;

namespace TaskbarBattery
{
    // Reads battery and charging state from Logitech devices over HID++ 2.0, through the vendor-defined HID collection
    // that Logitech devices expose alongside the mouse/keyboard ones (also over Bluetooth LE).
    // Messages go to the device directly (index 0xFF) as long reports:
    //   [0x11, device index, feature index, function << 4 | software id, parameters...]
    // The root feature (index 0) maps a feature id to its index on this device.
    static class LogitechHidpp
    {
        public const ushort LogitechVendorId = 0x046D;

        const byte LongReportId = 0x11;
        const byte ShortReportId = 0x10;
        const byte DirectDeviceIndex = 0xFF;
        const byte SoftwareId = 0x0B;
        const byte ErrorFeatureIndex = 0xFF;
        const int ReplyTimeoutMs = 800;

        const ushort FeatureUnifiedBattery = 0x1004;
        const ushort FeatureBatteryStatus = 0x1000;

        public sealed class Reading
        {
            public int Percent;
            public ChargeState Charge;
            public string Feature;
        }

        // Finds the HID++ collection of the Bluetooth device with this address (the address is part of the HID path)
        // and reads its battery. Returns null when the device has no HID++ channel or does not answer.
        public static Reading Read(string bluetoothAddress, List<HidCollection> logitechCollections)
        {
            HidCollection collection = FindCollection(bluetoothAddress, logitechCollections);
            if (collection == null)
                return null;

            using (HidStream stream = Hid.Open(collection))
            {
                if (stream == null)
                    return null;

                BatteryFeature feature = FindBatteryFeature(stream);
                if (feature == null)
                    return null;

                // get_status (0x1004, function 1) and get_battery_level_status (0x1000, function 0) answer with the
                // same layout as the change notifications.
                byte[] reply = Request(stream, feature.Index, feature.Unified ? (byte)1 : (byte)0, null);
                return reply == null ? null : Parse(reply, feature);
            }
        }

        // The HID++ collection of the Bluetooth device with this address (the address is part of the HID path).
        public static HidCollection FindCollection(string bluetoothAddress, List<HidCollection> logitechCollections)
        {
            string needle = bluetoothAddress.ToLowerInvariant();
            foreach (HidCollection collection in logitechCollections)
            {
                if (IsHidppCollection(collection) && collection.Path.ToLowerInvariant().IndexOf(needle, StringComparison.Ordinal) >= 0)
                    return collection;
            }
            return null;
        }

        // Where the battery feature sits on this device: Unified Battery (0x1004) if present, else Battery Status (0x1000).
        public sealed class BatteryFeature
        {
            public byte Index;
            public bool Unified;
        }

        public static BatteryFeature FindBatteryFeature(HidStream stream)
        {
            byte index;
            if (TryGetFeatureIndex(stream, FeatureUnifiedBattery, out index))
                return new BatteryFeature { Index = index, Unified = true };
            if (TryGetFeatureIndex(stream, FeatureBatteryStatus, out index))
                return new BatteryFeature { Index = index, Unified = false };
            return null;
        }

        // A battery change notification: the device sends it unprompted, with function 0 and software id 0,
        // whenever the level or the charging state changes. Returns null for any other report.
        public static Reading ParseNotification(byte[] report, BatteryFeature feature)
        {
            if (report == null || report.Length < 7 || (report[0] != LongReportId && report[0] != ShortReportId))
                return null;
            if (report[2] != feature.Index || report[3] != 0x00)
                return null;
            return Parse(report, feature);
        }

        // Unified Battery: [state of charge %, level flags, charging status, external power].
        // Battery Status:  [level %, next level %, status].
        static Reading Parse(byte[] report, BatteryFeature feature)
        {
            return new Reading
            {
                Percent = report[4],
                Charge = feature.Unified ? FromUnifiedBattery(report[6]) : FromBatteryStatus(report[6]),
                Feature = feature.Unified ? "0x1004" : "0x1000",
            };
        }

        // Vendor-defined usage page with room for a long (20-byte) report.
        public static bool IsHidppCollection(HidCollection collection)
        {
            return collection.UsagePage >= 0xFF00 && collection.OutputReportLength >= 20 && collection.InputReportLength >= 20;
        }

        static bool TryGetFeatureIndex(HidStream stream, ushort featureId, out byte index)
        {
            index = 0;
            byte[] reply = Request(stream, 0x00, 0, new[] { (byte)(featureId >> 8), (byte)featureId });
            if (reply == null || reply[4] == 0)
                return false;
            index = reply[4];
            return true;
        }

        // Sends one request and waits for the matching reply, skipping unrelated notifications.
        static byte[] Request(HidStream stream, byte featureIndex, byte function, byte[] parameters)
        {
            var report = new byte[20];
            report[0] = LongReportId;
            report[1] = DirectDeviceIndex;
            report[2] = featureIndex;
            report[3] = (byte)((function << 4) | SoftwareId);
            if (parameters != null)
                Array.Copy(parameters, 0, report, 4, Math.Min(parameters.Length, 16));

            if (!stream.Write(report))
                return null;

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(ReplyTimeoutMs);
            while (true)
            {
                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0)
                    return null;

                byte[] reply = stream.Read(remaining);
                if (reply == null)
                    return null;
                if (reply.Length < 7 || (reply[0] != LongReportId && reply[0] != ShortReportId))
                    continue;

                // Error reply: [id, device, 0xFF, feature index, function | software id, error code].
                if (reply[2] == ErrorFeatureIndex && reply[3] == featureIndex && reply[4] == report[3])
                    return null;
                if (reply[2] == featureIndex && reply[3] == report[3])
                    return reply;
            }
        }

        static ChargeState FromUnifiedBattery(byte status)
        {
            switch (status)
            {
                case 0: return ChargeState.Discharging;
                case 1: // charging
                case 2: // charging slowly
                    return ChargeState.Charging;
                case 3: return ChargeState.Full;
                default: return ChargeState.Unknown;
            }
        }

        static ChargeState FromBatteryStatus(byte status)
        {
            switch (status)
            {
                case 0: return ChargeState.Discharging;
                case 1: // recharging
                case 2: // almost full
                case 4: // slow recharge
                    return ChargeState.Charging;
                case 3: return ChargeState.Full;
                default: return ChargeState.Unknown;
            }
        }
    }
}
