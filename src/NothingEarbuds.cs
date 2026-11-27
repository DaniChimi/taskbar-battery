using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarBattery
{
    // Battery of Nothing and CMF earbuds, read over their control channel: an RFCOMM service that every model
    // working with the Nothing X app exposes. Windows itself gets no battery level from them, so this is the only
    // source. The earbuds report the left bud, the right bud and the case, each with its own charging flag.
    //
    // Frame layout (little endian):
    //   [0x55] [control: 2] [command: 2] [payload length: 2] [sequence: 1] [payload] [CRC-16: 2, when control & 0x20]
    // The CRC is CRC-16/MODBUS (reflected, polynomial 0xA001, initial value 0xFFFF) over everything before it.
    static class NothingEarbuds
    {
        public static readonly Guid ControlService = new Guid("aeac4a03-dff5-498f-843a-34487cf133eb");

        const byte StartOfFrame = 0x55;
        const int HeaderLength = 8;
        const int ControlCrc = 0x20;
        const ushort RequestFlag = 0x8000;

        // Requests carry the request flag; the earbuds answer with the same command without it.
        const ushort ReadBattery = 0xC007;
        // Sent by the earbuds on their own when a level or a charging state changes.
        const ushort BatteryChanged = 0xE001;

        // Control word of the requests: TWS headset, CRC present.
        const ushort RequestControl = 0x0160;

        const byte LeftBud = 0x02;
        const byte RightBud = 0x03;
        const byte Case = 0x04;

        // Paired devices that expose the control service, by Bluetooth address, with the id of that service node
        // (it carries the vendor id). One enumeration of the Bluetooth devices, without opening any connection.
        public static Dictionary<string, string> FindDevices()
        {
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string prefix = "BTHENUM\\{" + ControlService.ToString("D") + "}";

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
                    if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string address = ParseServiceAddress(id);
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

        // A service node id ends with "...&<12-digit address>_C00000000".
        internal static string ParseServiceAddress(string instanceId)
        {
            int amp = instanceId.LastIndexOf('&');
            if (amp < 0 || instanceId.Length < amp + 13)
                return null;
            string address = instanceId.Substring(amp + 1, 12);
            foreach (char c in address)
            {
                if (!Uri.IsHexDigit(c))
                    return null;
            }
            return address.ToUpperInvariant();
        }

        public static byte[] BatteryRequest(byte sequence)
        {
            var frame = new byte[HeaderLength + 2];
            frame[0] = StartOfFrame;
            frame[1] = RequestControl & 0xFF;
            frame[2] = RequestControl >> 8;
            frame[3] = ReadBattery & 0xFF;
            frame[4] = ReadBattery >> 8;
            frame[5] = 0;
            frame[6] = 0;
            frame[7] = sequence;
            ushort crc = Crc16(frame, HeaderLength);
            frame[8] = (byte)crc;
            frame[9] = (byte)(crc >> 8);
            return frame;
        }

        static ushort Crc16(byte[] data, int count)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < count; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                    crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
            }
            return crc;
        }

        // A battery update the earbuds sent on their own. It may list only the part that changed, so the listener
        // answers it by asking for the complete state instead of using it.
        public static bool IsBatteryNotice(byte[] frame)
        {
            return frame.Length >= HeaderLength && (ushort)((frame[3] | (frame[4] << 8)) | RequestFlag) == BatteryChanged;
        }

        // Battery parts in the answer to a battery request: every part that is connected right now. Null when the
        // frame is about something else.
        public static List<BatteryPart> ParseBattery(byte[] frame)
        {
            if (frame.Length < HeaderLength + 1)
                return null;
            ushort command = (ushort)((frame[3] | (frame[4] << 8)) | RequestFlag);
            if (command != ReadBattery)
                return null;

            int length = frame[5] | (frame[6] << 8);
            int count = frame[HeaderLength];
            var parts = new List<BatteryPart>();
            for (int i = 0; i < count; i++)
            {
                int at = HeaderLength + 1 + 2 * i;
                if (at + 1 >= HeaderLength + length || at + 1 >= frame.Length)
                    break;

                PartKind kind;
                switch (frame[at])
                {
                    case LeftBud: kind = PartKind.Left; break;
                    case RightBud: kind = PartKind.Right; break;
                    case Case: kind = PartKind.Case; break;
                    default: continue;
                }
                byte value = frame[at + 1];
                parts.Add(new BatteryPart(kind, value & 0x7F, (value & 0x80) != 0));
            }
            parts.Sort((a, b) => a.Kind.CompareTo(b.Kind));
            return parts;
        }

        // Splits the byte stream of the channel into frames, using the length field.
        public sealed class FrameReader
        {
            readonly List<byte> pending = new List<byte>();

            public List<byte[]> Append(byte[] buffer, int count)
            {
                for (int i = 0; i < count; i++)
                    pending.Add(buffer[i]);

                var frames = new List<byte[]>();
                while (true)
                {
                    int start = pending.IndexOf(StartOfFrame);
                    if (start < 0)
                    {
                        pending.Clear();
                        break;
                    }
                    if (start > 0)
                        pending.RemoveRange(0, start);
                    if (pending.Count < HeaderLength)
                        break;

                    int length = pending[5] | (pending[6] << 8);
                    int total = HeaderLength + length + ((pending[1] & ControlCrc) != 0 ? 2 : 0);
                    if (length > 1024)
                    {
                        // Not a real header: skip this start byte and look for the next one.
                        pending.RemoveAt(0);
                        continue;
                    }
                    if (pending.Count < total)
                        break;

                    byte[] frame = pending.GetRange(0, total).ToArray();
                    if (total > HeaderLength + length)
                    {
                        ushort crc = Crc16(frame, HeaderLength + length);
                        if (frame[total - 2] != (byte)crc || frame[total - 1] != (byte)(crc >> 8))
                        {
                            pending.RemoveAt(0);
                            continue;
                        }
                    }
                    frames.Add(frame);
                    pending.RemoveRange(0, total);
                }
                return frames;
            }
        }
    }
}
