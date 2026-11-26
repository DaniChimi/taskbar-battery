using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaskbarBattery
{
    // Logitech devices that Windows sees over USB: behind a Unifying, Bolt or LIGHTSPEED receiver (up to six each),
    // on a cable, or on their own dongle (headsets). Windows stores no battery level for them, so they are read over
    // HID++ 2.0 (or, for the newer headsets, over their own protocol). Bluetooth devices are not handled here.
    //
    // All device I/O runs on one worker thread. The channels stay open: battery events and receiver link events
    // arrive at once, and each online device is also read every PollInterval as a safety net.
    sealed class LogitechUsbMonitor : IDisposable
    {
        static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
        static readonly TimeSpan OfflineRetry = TimeSpan.FromSeconds(20);
        static readonly TimeSpan FailureRetry = TimeSpan.FromSeconds(4);

        const ushort FeatureDeviceName = 0x0005;
        const ushort FeatureDeviceInfo = 0x0003;
        const ushort FeatureBatteryStatus = 0x1000;
        const ushort FeatureBatteryVoltage = 0x1001;
        const ushort FeatureUnifiedBattery = 0x1004;
        const ushort FeatureAdcMeasurement = 0x1F20;
        const ushort FeatureWirelessStatus = 0x1D4B;

        readonly BlockingCollection<Action> work = new BlockingCollection<Action>();
        readonly Thread worker;
        readonly Dictionary<string, Endpoint> endpoints = new Dictionary<string, Endpoint>(StringComparer.OrdinalIgnoreCase);
        readonly object snapshotLock = new object();
        List<BatteryDevice> snapshot = new List<BatteryDevice>();
        volatile bool stopping;
        bool rescanRequested = true;
        bool dirty;

        // Raised on the worker thread when the list of devices or a reading changed.
        public event Action Changed;

        public LogitechUsbMonitor()
        {
            worker = new Thread(Run);
            worker.IsBackground = true;
            worker.Name = "Logitech USB";
            worker.Start();
        }

        // The online devices with a reading, as of now.
        public List<BatteryDevice> Devices()
        {
            lock (snapshotLock)
                return new List<BatteryDevice>(snapshot);
        }

        // Looks for receivers and devices plugged in or removed since the last look.
        public void Rescan()
        {
            Post(delegate { rescanRequested = true; });
        }

        // Rescan, and read every device now.
        public void Refresh()
        {
            Post(delegate
            {
                rescanRequested = true;
                foreach (Source source in AllSources())
                    source.NextCheck = DateTime.UtcNow;
            });
        }

        void Post(Action action)
        {
            if (!stopping)
            {
                try { work.Add(action); }
                catch (InvalidOperationException) { }
            }
        }

        void Run()
        {
            while (!stopping)
            {
                Action action;
                try
                {
                    if (work.TryTake(out action, 250))
                    {
                        action();
                        if (work.Count > 0)
                            continue;
                    }
                    Tick();
                }
                catch (InvalidOperationException)
                {
                    break; // shutting down
                }
                catch (Exception ex)
                {
                    Log.Write("Logitech USB: " + ex.Message);
                }
            }
        }

        void Tick()
        {
            if (rescanRequested)
            {
                rescanRequested = false;
                Discover();
            }
            DateTime now = DateTime.UtcNow;
            foreach (Source source in AllSources())
            {
                if (now < source.NextCheck)
                    continue;
                Check(source);
                if (work.Count > 0)
                    break; // keep notifications quick
            }
            if (dirty)
                Publish();
        }

        IEnumerable<Source> AllSources()
        {
            return endpoints.Values.SelectMany(e => e.Sources).ToList();
        }

        // --- discovery ---

        void Discover()
        {
            List<HidCollection> collections;
            try
            {
                collections = Hid.Enumerate(LogitechHidpp.LogitechVendorId).Where(c => c.IsUsb).ToList();
            }
            catch (Exception ex)
            {
                Log.Write("Logitech USB enumeration failed: " + ex.Message);
                return;
            }
            Dictionary<string, List<HidCollection>> interfaces = collections
                .GroupBy(c => c.InterfaceKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (string key in endpoints.Keys.ToList())
            {
                if (interfaces.ContainsKey(key) && !endpoints[key].IsClosed)
                    continue;
                Log.Write("Logitech USB removed: " + endpoints[key]);
                endpoints[key].Dispose();
                endpoints.Remove(key);
                dirty = true;
            }

            foreach (KeyValuePair<string, List<HidCollection>> entry in interfaces)
            {
                if (endpoints.ContainsKey(entry.Key))
                    continue;
                Endpoint endpoint = null;
                try
                {
                    HidCollection headset = entry.Value.Find(c => c.UsagePage == CenturionChannel.UsagePage && c.OutputReportLength >= 64);
                    if (headset != null)
                        endpoint = HeadsetEndpoint.Open(this, headset);
                    if (endpoint == null)
                        endpoint = HidppEndpoint.Open(this, entry.Value);
                }
                catch (Exception ex)
                {
                    Log.Write("Logitech USB open failed: " + ex.Message);
                }
                if (endpoint == null)
                    continue;
                Log.Write("Logitech USB added: " + endpoint);
                endpoints[entry.Key] = endpoint;
            }
        }

        // --- reading ---

        // Reads one source; two failures in a row, or a receiver saying the device is gone, mark it offline.
        void Check(Source source)
        {
            if (source.Endpoint.IsClosed)
            {
                rescanRequested = true;
                return;
            }
            LogitechHidpp.Reading reading = null;
            try
            {
                if (!source.Ready)
                    source.Ready = source.Init();
                if (source.Ready)
                    reading = source.Read();
            }
            catch (Exception ex)
            {
                Log.Write("Logitech USB read " + source + ": " + ex.Message);
            }

            DateTime now = DateTime.UtcNow;
            if (reading != null)
            {
                source.Failures = 0;
                source.NextCheck = now + PollInterval;
                Apply(source, reading);
                return;
            }

            source.Failures++;
            bool wasOnline = source.Online;
            if (source.Failures >= 2 || !source.Online || source.GoneOffline)
                SetOffline(source);
            source.GoneOffline = false;
            source.NextCheck = now + (source.Online ? FailureRetry : OfflineRetry);
            if (wasOnline && !source.Online)
                Log.Write("Logitech USB offline: " + source);
        }

        void Apply(Source source, LogitechHidpp.Reading reading)
        {
            if (!source.Online)
            {
                source.Online = true;
                source.OnlineSince = DateTime.Now;
                Log.Write("Logitech USB online: " + source + " " + source.Name);
            }
            if (source.Reading == null || source.Reading.Percent != reading.Percent || source.Reading.Charge != reading.Charge)
                dirty = true;
            source.Reading = reading;
            source.ReadAt = DateTime.Now;
        }

        void SetOffline(Source source)
        {
            if (source.Online)
                dirty = true;
            source.Online = false;
        }

        // Builds the snapshot: one device per id (a device on a cable and on a receiver at once counts once).
        void Publish()
        {
            dirty = false;
            var devices = new Dictionary<string, BatteryDevice>(StringComparer.OrdinalIgnoreCase);
            foreach (Source source in AllSources())
            {
                if (!source.Online || source.Reading == null)
                    continue;
                var device = new BatteryDevice(source.Id, source.Name ?? "Logitech", source.Endpoint.Path, source.Kind,
                    source.Reading.Percent, source.ReadAt, true, source.OnlineSince, "Logitech");
                device.Charge = source.Reading.Charge;
                device.Connection = source.Connection;
                BatteryDevice existing;
                if (!devices.TryGetValue(source.Id, out existing) || existing.LastUpdated < device.LastUpdated)
                    devices[source.Id] = device;
            }
            lock (snapshotLock)
                snapshot = devices.Values.ToList();
            Action handler = Changed;
            if (handler != null)
                handler();
        }

        public void Dispose()
        {
            stopping = true;
            work.CompleteAdding();
            if (worker.IsAlive)
                worker.Join(2000);
            foreach (Endpoint endpoint in endpoints.Values)
                endpoint.Dispose();
            endpoints.Clear();
        }

        // ===========================================================================================================
        // Endpoints (one per USB interface) and sources (one per device behind it)
        // ===========================================================================================================

        abstract class Endpoint : IDisposable
        {
            public readonly List<Source> Sources = new List<Source>();
            public abstract bool IsClosed { get; }
            public abstract string Path { get; }
            public abstract void Dispose();
        }

        abstract class Source
        {
            public Endpoint Endpoint;
            public string Id;
            public string Name;
            public DeviceKind Kind;
            public bool Ready;
            public bool Online;
            public bool GoneOffline;
            public int Failures;
            public DateTime NextCheck = DateTime.MinValue;
            public DateTime? OnlineSince;
            public DateTime ReadAt;
            public LogitechHidpp.Reading Reading;
            public ConnectionKind Connection = ConnectionKind.Receiver;

            // Feature discovery; false while the device cannot be reached.
            public abstract bool Init();

            // Null when the device cannot be reached.
            public abstract LogitechHidpp.Reading Read();
        }

        // --- HID++: receivers, devices on a cable, older headsets on their own dongle ---

        sealed class HidppEndpoint : Endpoint
        {
            LogitechUsbMonitor owner;
            public HidppChannel Channel;
            public bool Receiver;

            public override bool IsClosed
            {
                get { return Channel.IsClosed; }
            }

            public override string Path
            {
                get { return Channel.Info.Path; }
            }

            public static Endpoint Open(LogitechUsbMonitor owner, List<HidCollection> sameInterface)
            {
                HidppChannel channel = HidppChannel.Open(sameInterface);
                if (channel == null)
                    return null;
                var endpoint = new HidppEndpoint { owner = owner, Channel = channel };

                // A device answers a ping at index 0xFF; a receiver rejects it with a HID++ 1.0 error.
                HidppReply ping = channel.Request(0xFF, 0, 1, 600, 0, 0, 0x5A);
                if (ping != null && ping.Error >= 0x100)
                    endpoint.Receiver = true;
                else if (ping == null && sameInterface.Any(c => c.Product != null && c.Product.IndexOf("Receiver", StringComparison.OrdinalIgnoreCase) >= 0))
                    endpoint.Receiver = true;

                if (endpoint.Receiver)
                    endpoint.AddPairedSlots();
                else
                {
                    // Logitech gives its receivers and dongles product ids 0xC5xx; anything else is the device itself
                    // on a USB cable.
                    ushort pid = channel.Info.ProductId;
                    endpoint.Sources.Add(new HidppSource(endpoint, 0xFF)
                    {
                        Connection = pid >= 0xC500 && pid <= 0xC5FF ? ConnectionKind.Receiver : ConnectionKind.Usb,
                    });
                }

                channel.Notification += delegate(byte[] report) { owner.Post(delegate { endpoint.OnNotification(report); }); };
                channel.Closed += delegate { owner.Post(delegate { owner.rescanRequested = true; }); };
                return endpoint;
            }

            // Slots from the receiver's pairing registers (Unifying, LIGHTSPEED). Receivers without them (Bolt) get
            // all six slots; the empty ones simply stay offline.
            void AddPairedSlots()
            {
                for (byte slot = 1; slot <= 6; slot++)
                {
                    HidppReply pairing = Channel.Register(0xFF, 0x83, 0xB5, 500, (byte)(0x1F + slot));
                    if (pairing == null || !pairing.Ok)
                        continue;
                    int wirelessPid = (pairing[7] << 8) | pairing[8];
                    if (wirelessPid == 0)
                        continue;
                    var source = new HidppSource(this, slot)
                    {
                        WirelessPid = wirelessPid,
                        Kind = KindFromReceiver(pairing[11] & 0x0F),
                    };
                    source.Id = IdFor(wirelessPid);

                    HidppReply name = Channel.Register(0xFF, 0x83, 0xB5, 500, (byte)(0x3F + slot));
                    if (name != null && name.Ok)
                    {
                        int length = Math.Min((int)name[5], 14);
                        var text = new StringBuilder();
                        for (int i = 0; i < length; i++)
                            text.Append((char)name[6 + i]);
                        if (text.ToString().Trim().Length > 0)
                            source.Name = text.ToString().Trim();
                    }
                    Sources.Add(source);
                }
                if (Sources.Count == 0)
                {
                    for (byte slot = 1; slot <= 6; slot++)
                        Sources.Add(new HidppSource(this, slot));
                }
            }

            void OnNotification(byte[] report)
            {
                byte index = report[1];
                HidppSource source = Sources.OfType<HidppSource>().FirstOrDefault(s => s.Index == index);

                // Receiver: a device connected or disconnected. Bit 6 of byte 4 = no link; bytes 5-6 = wireless pid.
                if (report[2] == 0x41 && Receiver)
                {
                    bool up = (report[4] & 0x40) == 0;
                    int wirelessPid = (report[6] << 8) | report[5];
                    if (source == null)
                    {
                        if (!up || wirelessPid == 0)
                            return;
                        source = new HidppSource(this, index) { WirelessPid = wirelessPid };
                        source.Id = IdFor(wirelessPid);
                        Sources.Add(source);
                    }
                    if (up)
                    {
                        source.Ready = false; // may have been re-paired or restarted: discover again
                        source.NextCheck = DateTime.UtcNow.AddMilliseconds(1500);
                    }
                    else
                    {
                        owner.SetOffline(source);
                        source.NextCheck = DateTime.UtcNow + OfflineRetry;
                    }
                    return;
                }

                // Anything the device sends means it is awake: look at it now.
                if (source != null && !source.Online && report[2] != 0x8F && report[2] != 0xFF)
                {
                    DateTime soon = DateTime.UtcNow.AddMilliseconds(300);
                    if (source.NextCheck > soon)
                        source.NextCheck = soon;
                }
                if (source == null || !source.Ready || (report[3] & 0x0F) != 0)
                    return; // only events, which carry software id 0

                if (report[2] == source.BatteryIndex && report.Length >= 7)
                {
                    LogitechHidpp.Reading reading = source.Parse(report);
                    if (reading != null)
                    {
                        source.Failures = 0;
                        owner.Apply(source, reading);
                    }
                }
                else if (source.WirelessIndex != 0 && report[2] == source.WirelessIndex)
                {
                    // Wireless device status: the device reconnected.
                    source.Ready = false;
                    source.NextCheck = DateTime.UtcNow.AddMilliseconds(1500);
                }
            }

            public override void Dispose()
            {
                Channel.Dispose();
            }

            public override string ToString()
            {
                return "HID++ " + Channel.Info.ProductId.ToString("X4") + " " + (Channel.Info.Product ?? "") + (Receiver ? " (receiver)" : "");
            }
        }

        sealed class HidppSource : Source
        {
            readonly HidppEndpoint endpoint;
            public readonly byte Index;
            public int WirelessPid;
            public byte BatteryIndex;
            public byte WirelessIndex;
            ushort batteryFeature;
            bool percentSupported = true;

            public HidppSource(HidppEndpoint endpoint, byte index)
            {
                this.endpoint = endpoint;
                Endpoint = endpoint;
                Index = index;
                Id = "LOGI-P" + endpoint.Channel.Info.ProductId.ToString("X4") + (index != 0xFF ? "-" + index : "");
            }

            HidppChannel Channel
            {
                get { return endpoint.Channel; }
            }

            byte FeatureIndex(ushort feature)
            {
                HidppReply reply = Channel.Request(Index, 0, 0, 1000, (byte)(feature >> 8), (byte)feature);
                if (reply == null)
                    throw new TimeoutException();
                if (!reply.Ok)
                {
                    if (reply.Error >= 0x100)
                        throw new TimeoutException(); // receiver: the device cannot be reached
                    return 0;
                }
                return reply[4];
            }

            public override bool Init()
            {
                // The receiver answers at once for an absent device; a sleeping one may take a second or two.
                HidppReply ping = Channel.Request(Index, 0, 1, 2500, 0, 0, 0x5A);
                if (ping == null || !ping.Ok)
                    return false;
                if (ping[4] < 2)
                    return false; // HID++ 1.0 devices are not supported

                try
                {
                    byte nameIndex = FeatureIndex(FeatureDeviceName);
                    string name = nameIndex != 0 ? ReadName(nameIndex) : null;
                    if (nameIndex != 0)
                    {
                        HidppReply type = Channel.Request(Index, nameIndex, 2, 1000);
                        if (type != null && type.Ok)
                        {
                            DeviceKind kind = KindFromHidpp(type[4]);
                            if (kind != DeviceKind.Other)
                                Kind = kind;
                        }
                    }

                    // The wireless pid identifies the device on any connection (receiver or cable).
                    if (WirelessPid == 0)
                    {
                        byte infoIndex = FeatureIndex(FeatureDeviceInfo);
                        if (infoIndex != 0)
                        {
                            HidppReply info = Channel.Request(Index, infoIndex, 0, 1000);
                            if (info != null && info.Ok)
                                WirelessPid = WirelessPidFrom(info.Data);
                        }
                    }

                    batteryFeature = 0;
                    foreach (ushort feature in new[] { FeatureUnifiedBattery, FeatureBatteryStatus, FeatureBatteryVoltage, FeatureAdcMeasurement })
                    {
                        byte index = FeatureIndex(feature);
                        if (index == 0)
                            continue;
                        BatteryIndex = index;
                        batteryFeature = feature;
                        break;
                    }
                    if (batteryFeature == 0)
                        return false;
                    if (batteryFeature == FeatureUnifiedBattery)
                    {
                        // get_capabilities: bit 1 of the flags says whether a percentage is reported.
                        HidppReply capabilities = Channel.Request(Index, BatteryIndex, 0, 1000);
                        percentSupported = capabilities == null || !capabilities.Ok || (capabilities[5] & 0x02) != 0;
                    }
                    WirelessIndex = FeatureIndex(FeatureWirelessStatus);

                    if (WirelessPid != 0)
                        Id = IdFor(WirelessPid);
                    if (name != null)
                        Name = name;
                    if (Name == null)
                        Name = Channel.Info.Product;
                    if (Index == 0xFF && Kind == DeviceKind.Other && Channel.Info.Product != null &&
                        Channel.Info.Product.IndexOf("Headset", StringComparison.OrdinalIgnoreCase) >= 0)
                        Kind = DeviceKind.Headphones;
                    // A wireless headset answers through its own dongle, not over a cable.
                    if (Index == 0xFF && Kind == DeviceKind.Headphones)
                        Connection = ConnectionKind.Receiver;
                    Log.Write("Logitech USB device " + this + ": " + Name + ", battery feature " + batteryFeature.ToString("X4"));
                    return true;
                }
                catch (TimeoutException)
                {
                    return false;
                }
            }

            string ReadName(byte index)
            {
                HidppReply count = Channel.Request(Index, index, 0, 1000);
                if (count == null || !count.Ok)
                    return null;
                int length = Math.Min((int)count[4], 64);
                var name = new StringBuilder();
                while (name.Length < length)
                {
                    HidppReply part = Channel.Request(Index, index, 1, 1000, (byte)name.Length);
                    if (part == null || !part.Ok)
                        break;
                    int before = name.Length;
                    for (int i = 4; i < part.Data.Length && name.Length < length; i++)
                    {
                        if (part.Data[i] == 0)
                        {
                            length = name.Length;
                            break;
                        }
                        name.Append((char)part.Data[i]);
                    }
                    if (name.Length == before)
                        break;
                }
                string result = name.ToString().Trim();
                return result.Length > 0 ? result : null;
            }

            // Device information (0x0003, function 0): bytes 9-10 are the transports (Bluetooth, BLE, eQuad, USB),
            // followed by one model id per transport; the eQuad one is the wireless pid.
            static int WirelessPidFrom(byte[] report)
            {
                if (report.Length < 19)
                    return 0;
                int transports = (report[9] << 8) | report[10];
                int slot = 0;
                for (int bit = 0; bit < 4; bit++)
                {
                    if ((transports & (1 << bit)) == 0)
                        continue;
                    int id = (report[11 + slot * 2] << 8) | report[12 + slot * 2];
                    if (bit == 2)
                        return id;
                    slot++;
                    if (slot >= 3)
                        break;
                }
                return 0;
            }

            public override LogitechHidpp.Reading Read()
            {
                int function = batteryFeature == FeatureUnifiedBattery ? 1 : 0;
                HidppReply reply = Channel.Request(Index, BatteryIndex, function, 2500);
                if (reply == null)
                    return null;
                if (!reply.Ok)
                {
                    if (reply.Error >= 0x100)
                        GoneOffline = true; // the receiver says the device is gone
                    else
                        Ready = false; // the feature table may have changed: discover again
                    return null;
                }
                return Parse(reply.Data);
            }

            // The same layout for the reply and for the device's own battery events.
            public LogitechHidpp.Reading Parse(byte[] r)
            {
                if (r.Length < 7)
                    return null;
                var reading = new LogitechHidpp.Reading { Feature = "0x" + batteryFeature.ToString("X4") };
                switch (batteryFeature)
                {
                    case FeatureUnifiedBattery:
                        reading.Percent = r[4];
                        if (!percentSupported || (reading.Percent == 0 && r[5] != 0))
                        {
                            // Only a coarse level: full, good, low, critical.
                            reading.Percent = (r[5] & 8) != 0 ? 100 : (r[5] & 4) != 0 ? 50 : (r[5] & 2) != 0 ? 20 : 5;
                        }
                        reading.Charge = r[6] == 1 || r[6] == 2 ? ChargeState.Charging
                            : r[6] == 3 ? ChargeState.Full : ChargeState.Discharging;
                        break;
                    case FeatureBatteryStatus:
                        reading.Percent = r[4];
                        reading.Charge = r[6] == 1 || r[6] == 2 || r[6] == 4 ? ChargeState.Charging
                            : r[6] == 3 ? ChargeState.Full : ChargeState.Discharging;
                        if (reading.Charge == ChargeState.Full && reading.Percent == 0)
                            reading.Percent = 100;
                        break;
                    case FeatureBatteryVoltage:
                    {
                        int millivolts = (r[4] << 8) | r[5];
                        reading.Percent = PercentFromVoltage(millivolts);
                        reading.Charge = ChargeState.Discharging;
                        if ((r[6] & 0x80) != 0)
                        {
                            int charging = r[6] & 0x07;
                            reading.Charge = charging == 0 ? ChargeState.Charging
                                : charging == 1 ? ChargeState.Full : ChargeState.Discharging;
                        }
                        break;
                    }
                    case FeatureAdcMeasurement:
                    {
                        int millivolts = (r[4] << 8) | r[5];
                        if (millivolts < 1000)
                            return null; // headset off: the dongle reports a few leftover millivolts
                        reading.Percent = PercentFromVoltage(millivolts);
                        reading.Charge = (r[6] & 0x04) != 0 ? ChargeState.Full
                            : (r[6] & 0x02) != 0 ? ChargeState.Charging : ChargeState.Discharging;
                        break;
                    }
                    default:
                        return null;
                }
                reading.Percent = Math.Max(0, Math.Min(100, reading.Percent));
                return reading;
            }

            public override string ToString()
            {
                return Id + "@" + Index.ToString("X2");
            }
        }

        // --- newer headsets (their own protocol, see CenturionChannel) ---

        sealed class HeadsetEndpoint : Endpoint
        {
            public CenturionChannel Channel;

            public override bool IsClosed
            {
                get { return Channel.IsClosed; }
            }

            public override string Path
            {
                get { return Channel.Info.Path; }
            }

            public static Endpoint Open(LogitechUsbMonitor owner, HidCollection info)
            {
                CenturionChannel channel = CenturionChannel.Open(info);
                if (channel == null)
                    return null;
                var endpoint = new HeadsetEndpoint { Channel = channel };
                var source = new HeadsetSource
                {
                    Endpoint = endpoint,
                    Channel = channel,
                    Id = "LOGI-C" + info.ProductId.ToString("X4"),
                    Name = info.Product,
                    Kind = DeviceKind.Headphones,
                };
                endpoint.Sources.Add(source);
                channel.Notification += delegate(byte[] frame)
                {
                    owner.Post(delegate
                    {
                        LogitechHidpp.Reading reading = channel.ParseBatteryEvent(frame);
                        if (reading != null)
                        {
                            source.Failures = 0;
                            owner.Apply(source, reading);
                            return;
                        }
                        // Power on or off and other events: look again shortly.
                        DateTime soon = DateTime.UtcNow.AddMilliseconds(1200);
                        if (source.NextCheck > soon)
                            source.NextCheck = soon;
                    });
                };
                channel.Closed += delegate { owner.Post(delegate { owner.rescanRequested = true; }); };
                return endpoint;
            }

            public override void Dispose()
            {
                Channel.Dispose();
            }

            public override string ToString()
            {
                return "headset " + Channel.Info.ProductId.ToString("X4") + " " + (Channel.Info.Product ?? "");
            }
        }

        sealed class HeadsetSource : Source
        {
            public CenturionChannel Channel;

            public override bool Init()
            {
                return true;
            }

            public override LogitechHidpp.Reading Read()
            {
                return Channel.ReadBattery();
            }

            public override string ToString()
            {
                return Id;
            }
        }

        // --- helpers ---

        // A device known by its wireless pid keeps the same id on a receiver and on a cable.
        static string IdFor(int wirelessPid)
        {
            return "LOGI-W" + wirelessPid.ToString("X4");
        }

        // Li-ion discharge curve (millivolts to percent) for devices that report only a voltage.
        static readonly int[,] VoltageCurve =
        {
            { 4186, 100 }, { 4067, 90 }, { 3989, 80 }, { 3922, 70 }, { 3859, 60 }, { 3811, 50 },
            { 3778, 40 }, { 3751, 30 }, { 3717, 20 }, { 3671, 10 }, { 3646, 5 }, { 3579, 2 }, { 3500, 0 },
        };

        static int PercentFromVoltage(int millivolts)
        {
            if (millivolts >= VoltageCurve[0, 0])
                return 100;
            for (int i = 1; i < VoltageCurve.GetLength(0); i++)
            {
                if (millivolts < VoltageCurve[i, 0])
                    continue;
                double t = (double)(millivolts - VoltageCurve[i, 0]) / (VoltageCurve[i - 1, 0] - VoltageCurve[i, 0]);
                return (int)Math.Round(VoltageCurve[i, 1] + t * (VoltageCurve[i - 1, 1] - VoltageCurve[i, 1]));
            }
            return 0;
        }

        // Device type from the receiver's pairing information.
        static DeviceKind KindFromReceiver(int type)
        {
            switch (type)
            {
                case 1: return DeviceKind.Keyboard;
                case 2: return DeviceKind.Mouse;
                case 3: return DeviceKind.Keyboard; // numpad
                case 8: return DeviceKind.Mouse;    // trackball
                default: return DeviceKind.Other;
            }
        }

        // Device type from the device name feature (0x0005, function 2).
        static DeviceKind KindFromHidpp(int type)
        {
            switch (type)
            {
                case 0: return DeviceKind.Keyboard;
                case 2: return DeviceKind.Keyboard; // numpad
                case 3:
                case 4:
                case 5: return DeviceKind.Mouse;    // mouse, touchpad, trackball
                case 8: return DeviceKind.Headphones;
                case 10:
                case 11:
                case 12:
                case 18: return DeviceKind.Gamepad; // wheels and gamepads
                default: return DeviceKind.Other;
            }
        }
    }
}
