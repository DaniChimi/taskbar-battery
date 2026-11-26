using System;
using System.Collections.Generic;
using System.Threading;

namespace TaskbarBattery
{
    // Reads one HID collection continuously on its own thread and raises every input report. The thread sleeps in
    // the kernel between reports. Writes go through a second handle (see HidppChannel), so a request never waits
    // for the pending read.
    sealed class HidReader : IDisposable
    {
        readonly HidStream stream;
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly Thread thread;

        public event Action<byte[]> Received;
        public event Action Closed;

        HidReader(HidStream stream, string name)
        {
            this.stream = stream;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = name;
        }

        public static HidReader Open(HidCollection collection, string name)
        {
            HidStream stream = Hid.Open(collection);
            return stream == null ? null : new HidReader(stream, name);
        }

        public void Start()
        {
            thread.Start();
        }

        void Run()
        {
            try
            {
                while (!stop.WaitOne(0))
                {
                    byte[] report = stream.Read(Timeout.Infinite, stop);
                    if (report == null)
                    {
                        if (stream.Failed)
                            break;
                        continue;
                    }
                    Action<byte[]> handler = Received;
                    if (handler != null)
                        handler(report);
                }
            }
            catch (Exception ex)
            {
                Log.Write("HID reader " + thread.Name + " stopped: " + ex.Message);
            }
            finally
            {
                stream.Dispose();
            }
            if (!stop.WaitOne(0))
            {
                Action closed = Closed;
                if (closed != null)
                    closed();
            }
        }

        public void Dispose()
        {
            stop.Set();
        }
    }

    // A reply to a HID++ request: Data is the whole report (parameters from index 4), or Error is set.
    sealed class HidppReply
    {
        public byte[] Data;

        // HID++ 2.0 error code, or 0x100 + a HID++ 1.0 error code (sent by receivers, for example for an absent device).
        public int Error;

        public bool Ok
        {
            get { return Data != null; }
        }

        public byte this[int index]
        {
            get { return Data != null && index < Data.Length ? Data[index] : (byte)0; }
        }
    }

    // The HID++ channel of one USB interface: a receiver (devices behind it at indexes 1 to 6) or a device on a cable
    // or on its own dongle (index 0xFF). Windows splits short (0x10, 7 bytes) and long (0x11, 20 bytes) reports into
    // two collections; both are read here and merged. Replies are matched to the pending request; everything else is
    // raised as a notification.
    sealed class HidppChannel : IDisposable
    {
        public const byte ShortReport = 0x10;
        public const byte LongReport = 0x11;
        const byte SoftwareId = 0x0B;

        readonly List<HidReader> readers = new List<HidReader>();
        readonly object requestLock = new object();
        readonly object pendingLock = new object();
        readonly AutoResetEvent replied = new AutoResetEvent(false);
        HidStream longWriter;
        HidStream shortWriter;
        byte[] pendingKey;
        HidppReply pendingReply;
        volatile bool closed;

        public HidCollection Info { get; private set; }

        public event Action<byte[]> Notification;
        public event Action Closed;

        HidppChannel(HidCollection info)
        {
            Info = info;
        }

        // Opens the HID++ collections of one USB interface; null when it has none.
        public static HidppChannel Open(List<HidCollection> sameInterface)
        {
            HidCollection longInfo = sameInterface.Find(c => c.UsagePage == 0xFF00 && c.OutputReportLength == 20)
                ?? sameInterface.Find(c => c.UsagePage >= 0xFF00 && c.OutputReportLength == 20 && c.InputReportLength == 20);
            if (longInfo == null)
                return null;

            var channel = new HidppChannel(longInfo);
            channel.longWriter = Hid.Open(longInfo);
            if (channel.longWriter == null || !channel.Attach(longInfo))
            {
                channel.Dispose();
                return null;
            }

            HidCollection shortInfo = sameInterface.Find(c => c.UsagePage == longInfo.UsagePage && c.InputReportLength == 7);
            if (shortInfo != null)
            {
                channel.shortWriter = Hid.Open(shortInfo);
                if (channel.shortWriter != null)
                    channel.Attach(shortInfo);
            }
            foreach (HidReader reader in channel.readers)
                reader.Start();
            return channel;
        }

        bool Attach(HidCollection collection)
        {
            HidReader reader = HidReader.Open(collection, "HID++ " + collection.ProductId.ToString("X4"));
            if (reader == null)
                return false;
            reader.Received += OnReport;
            reader.Closed += delegate
            {
                if (closed)
                    return;
                closed = true;
                Action handler = Closed;
                if (handler != null)
                    handler();
            };
            readers.Add(reader);
            return true;
        }

        public bool IsClosed
        {
            get { return closed; }
        }

        void OnReport(byte[] report)
        {
            if (report.Length < 4 || (report[0] != ShortReport && report[0] != LongReport))
                return;
            lock (pendingLock)
            {
                byte[] key = pendingKey;
                if (key != null && report[1] == key[0])
                {
                    HidppReply reply = null;
                    if (report[2] == key[1] && report[3] == key[2])
                        reply = new HidppReply { Data = report };
                    else if (report[2] == 0xFF && report[3] == key[1] && report[4] == key[2] && report.Length > 5)
                        reply = new HidppReply { Error = report[5] };
                    else if (report[2] == 0x8F && report[3] == key[1] && report[4] == key[2] && report.Length > 5)
                        reply = new HidppReply { Error = 0x100 + report[5] };
                    if (reply != null)
                    {
                        pendingReply = reply;
                        pendingKey = null;
                        replied.Set();
                        return;
                    }
                }
            }
            Action<byte[]> handler = Notification;
            if (handler != null)
                handler(report);
        }

        // HID++ 2.0 request: feature index and function. Null on timeout.
        public HidppReply Request(byte device, byte feature, int function, int timeoutMs, params byte[] parameters)
        {
            return Send(LongReport, device, feature, (byte)((function << 4) | SoftwareId), timeoutMs, parameters);
        }

        // HID++ 1.0 register access, used by receivers: 0x81 reads a short register, 0x83 a long one.
        public HidppReply Register(byte device, byte subId, byte address, int timeoutMs, params byte[] parameters)
        {
            return Send(ShortReport, device, subId, address, timeoutMs, parameters);
        }

        HidppReply Send(byte reportId, byte device, byte b2, byte b3, int timeoutMs, byte[] parameters)
        {
            if (closed)
                return null;
            // Each collection takes only its own report id: HID++ 2.0 requests go long, register requests short.
            HidStream writer = reportId == ShortReport ? shortWriter : longWriter;
            if (writer == null)
                return null;

            var report = new byte[reportId == ShortReport ? 7 : 20];
            report[0] = reportId;
            report[1] = device;
            report[2] = b2;
            report[3] = b3;
            if (parameters != null)
                Array.Copy(parameters, 0, report, 4, Math.Min(parameters.Length, report.Length - 4));

            lock (requestLock)
            {
                lock (pendingLock)
                {
                    pendingKey = new[] { device, b2, b3 };
                    pendingReply = null;
                    replied.Reset();
                }
                if (!writer.Write(report))
                {
                    lock (pendingLock)
                        pendingKey = null;
                    return null;
                }
                bool got = replied.WaitOne(timeoutMs);
                lock (pendingLock)
                {
                    pendingKey = null;
                    return got ? pendingReply : null;
                }
            }
        }

        public void Dispose()
        {
            closed = true;
            foreach (HidReader reader in readers)
                reader.Dispose();
            readers.Clear();
            if (longWriter != null)
                longWriter.Dispose();
            if (shortWriter != null)
                shortWriter.Dispose();
        }
    }

    // Logitech's newer headset protocol (PRO X 2 LIGHTSPEED and similar), used instead of HID++. Frames are 64-byte
    // reports with id 0x51 on the 0xFFA0 vendor collection: [0x51, payload length + 1, flags, payload...]. The headset
    // sits behind a bridge feature of its dongle, so its requests are wrapped:
    //   [bridge index, 0x10 | software id, size high, size low, 0x00, feature index, function | software id, ...].
    sealed class CenturionChannel : IDisposable
    {
        public const ushort UsagePage = 0xFFA0;
        const byte ReportId = 0x51;
        const byte SoftwareId = 0x0A;
        const ushort FeatureSet = 0x0001;
        const ushort Bridge = 0x0003;
        const ushort BatteryStateOfCharge = 0x0104;

        readonly HidReader reader;
        readonly HidStream writer;
        readonly object requestLock = new object();
        readonly Queue<byte[]> inbox = new Queue<byte[]>();
        readonly AutoResetEvent arrived = new AutoResetEvent(false);
        volatile bool waiting;
        volatile bool closed;
        int bridgeIndex = -1;
        int batteryIndex = -1;

        public HidCollection Info { get; private set; }

        // Frames the headset sends on its own (battery changes, power on and off).
        public event Action<byte[]> Notification;
        public event Action Closed;

        CenturionChannel(HidCollection info, HidReader reader, HidStream writer)
        {
            Info = info;
            this.reader = reader;
            this.writer = writer;
            reader.Received += OnFrame;
            reader.Closed += delegate
            {
                closed = true;
                Action handler = Closed;
                if (handler != null)
                    handler();
            };
        }

        public static CenturionChannel Open(HidCollection info)
        {
            if (info.UsagePage != UsagePage || info.OutputReportLength < 64)
                return null;
            HidStream writer = Hid.Open(info);
            if (writer == null)
                return null;
            HidReader reader = HidReader.Open(info, "Headset " + info.ProductId.ToString("X4"));
            if (reader == null)
            {
                writer.Dispose();
                return null;
            }
            var channel = new CenturionChannel(info, reader, writer);
            reader.Start();
            return channel;
        }

        public bool IsClosed
        {
            get { return closed; }
        }

        void OnFrame(byte[] frame)
        {
            if (frame.Length < 4 || frame[0] != ReportId)
                return;
            if (waiting)
            {
                lock (inbox)
                    inbox.Enqueue(frame);
                arrived.Set();
                return;
            }
            Action<byte[]> handler = Notification;
            if (handler != null)
                handler(frame);
        }

        byte[] Next(int timeoutMs)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (true)
            {
                lock (inbox)
                {
                    if (inbox.Count > 0)
                        return inbox.Dequeue();
                }
                int left = (int)(end - DateTime.UtcNow).TotalMilliseconds;
                if (left <= 0 || !arrived.WaitOne(left))
                    return null;
            }
        }

        static byte[] Payload(byte[] frame)
        {
            int length = frame[1];
            if (length <= 1 || length + 2 > frame.Length)
                return null;
            var payload = new byte[length - 1];
            Array.Copy(frame, 3, payload, 0, payload.Length);
            return payload;
        }

        // A request to the dongle itself; returns the parameters of the reply.
        byte[] Direct(byte feature, int function, params byte[] parameters)
        {
            var payload = new byte[2 + parameters.Length];
            payload[0] = feature;
            payload[1] = (byte)((function << 4) | SoftwareId);
            Array.Copy(parameters, 0, payload, 2, parameters.Length);
            return Exchange(payload, delegate(byte[] reply)
            {
                if (reply.Length < 2 || reply[0] != feature)
                    return null;
                var result = new byte[reply.Length - 2];
                Array.Copy(reply, 2, result, 0, result.Length);
                return result;
            });
        }

        // A request forwarded to the headset; null when it is off, out of range or did not answer.
        byte[] Bridged(byte feature, int function, params byte[] parameters)
        {
            if (bridgeIndex < 0)
                return null;
            int size = 3 + parameters.Length;
            var payload = new byte[4 + size];
            payload[0] = (byte)bridgeIndex;
            payload[1] = 0x10 | SoftwareId;
            payload[2] = (byte)((size >> 8) & 0x0F);
            payload[3] = (byte)size;
            payload[4] = 0x00;
            payload[5] = feature;
            payload[6] = (byte)((function << 4) | SoftwareId);
            Array.Copy(parameters, 0, payload, 7, parameters.Length);
            return Exchange(payload, delegate(byte[] reply)
            {
                // The headset's answer comes as a bridge event (function 1, software id 0).
                if (reply.Length < 7 || reply[0] != bridgeIndex || (reply[1] & 0xF0) != 0x10 || (reply[1] & 0x0F) != 0)
                    return null;
                if (reply[4] != 0x00)
                    return null;
                if (reply[5] == 0xFF)
                    return reply.Length > 6 && reply[6] == feature ? new byte[0] : null;
                if (reply[5] != feature)
                    return null;
                var result = new byte[reply.Length - 7];
                Array.Copy(reply, 7, result, 0, result.Length);
                return result;
            });
        }

        byte[] Exchange(byte[] payload, Func<byte[], byte[]> match)
        {
            lock (requestLock)
            {
                lock (inbox)
                    inbox.Clear();
                waiting = true;
                try
                {
                    var frame = new byte[64];
                    frame[0] = ReportId;
                    frame[1] = (byte)(payload.Length + 1);
                    Array.Copy(payload, 0, frame, 3, Math.Min(payload.Length, 61));
                    if (!writer.Write(frame))
                        return null;

                    for (int i = 0; i < 12; i++)
                    {
                        byte[] received = Next(400);
                        if (received == null)
                            return null;
                        byte[] content = Payload(received);
                        if (content != null)
                        {
                            byte[] result = match(content);
                            if (result != null)
                                return result;
                            if (content.Length >= 2 && (content[1] & 0x0F) == SoftwareId)
                                continue; // acknowledgement of our own request
                        }
                        Action<byte[]> handler = Notification;
                        if (handler != null)
                            handler(received);
                    }
                    return null;
                }
                finally
                {
                    waiting = false;
                }
            }
        }

        // Finds the bridge and the headset's battery feature; cheap once found.
        bool Discover()
        {
            if (bridgeIndex >= 0 && batteryIndex >= 0)
                return true;

            byte[] featureSet = Direct(0, 0, FeatureSet >> 8, FeatureSet & 0xFF);
            if (featureSet == null || featureSet.Length < 1 || featureSet[0] == 0)
                return false;
            byte[] count = Direct(featureSet[0], 0);
            if (count == null || count.Length < 1)
                return false;
            for (int i = 0; i < count[0] && bridgeIndex < 0; i++)
            {
                byte[] feature = Direct(featureSet[0], 1, (byte)i);
                if (feature != null && feature.Length >= 3 && ((feature[1] << 8) | feature[2]) == Bridge)
                    bridgeIndex = i;
            }
            if (bridgeIndex < 0)
                return false;

            byte[] headsetSet = Bridged(0, 0, FeatureSet >> 8, FeatureSet & 0xFF);
            if (headsetSet == null || headsetSet.Length < 1 || headsetSet[0] == 0)
                return false; // headset off: try again later
            byte[] headsetCount = Bridged(headsetSet[0], 0);
            if (headsetCount == null || headsetCount.Length < 1)
                return false;
            for (int i = 0; i < headsetCount[0]; i++)
            {
                byte[] feature = Bridged(headsetSet[0], 1, (byte)i);
                if (feature != null && feature.Length >= 3 && ((feature[1] << 8) | feature[2]) == BatteryStateOfCharge)
                {
                    batteryIndex = i;
                    break;
                }
            }
            return batteryIndex >= 0;
        }

        // The headset's battery, or null when it cannot be reached.
        public LogitechHidpp.Reading ReadBattery()
        {
            if (closed || !Discover())
                return null;
            byte[] reply = Bridged((byte)batteryIndex, 0);
            return reply == null || reply.Length < 3 ? null : Parse(reply);
        }

        // A battery event the headset sent on its own, or null for any other frame.
        public LogitechHidpp.Reading ParseBatteryEvent(byte[] frame)
        {
            byte[] p = Payload(frame);
            if (p == null || bridgeIndex < 0 || batteryIndex < 0)
                return null;
            if (p.Length >= 10 && p[0] == bridgeIndex && (p[1] & 0xF0) == 0x10 && p[4] == 0 && p[5] == batteryIndex)
            {
                var parameters = new byte[p.Length - 7];
                Array.Copy(p, 7, parameters, 0, parameters.Length);
                return Parse(parameters);
            }
            return null;
        }

        // Battery state of charge: [percent, ?, charging state].
        static LogitechHidpp.Reading Parse(byte[] p)
        {
            ChargeState charge;
            switch (p[2])
            {
                case 1:
                case 2: charge = ChargeState.Charging; break;
                case 3: charge = ChargeState.Full; break;
                default: charge = ChargeState.Discharging; break;
            }
            return new LogitechHidpp.Reading { Percent = Math.Min(100, (int)p[0]), Charge = charge, Feature = "headset" };
        }

        public void Dispose()
        {
            closed = true;
            reader.Dispose();
            writer.Dispose();
        }
    }
}
