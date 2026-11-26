using System;
using System.Collections.Generic;
using System.Threading;

namespace TaskbarBattery
{
    // Keeps the control channel of Nothing / CMF earbuds open: asks for the battery once connected, then waits for
    // the updates the earbuds send on their own (a bud taken out of the case, a level drop, charging). The thread
    // sleeps in the kernel between messages; RequestUpdate asks again, at each scan, as a safety net. It ends when
    // the earbuds disconnect or on Dispose; the owner recreates it on a later scan if needed.
    sealed class EarbudsBatteryListener : IDisposable
    {
        // Last failure logged per device, so a device that keeps refusing does not fill the log.
        static readonly Dictionary<string, string> lastFailure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        readonly string address;
        readonly Thread thread;
        volatile bool stopped;
        volatile RfcommSocket socket;
        int sequence;

        // Raised on the listener thread.
        public event Action<string, List<BatteryPart>> Changed;

        public EarbudsBatteryListener(string bluetoothAddress)
        {
            address = bluetoothAddress;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "Earbuds battery " + bluetoothAddress;
            thread.Start();
        }

        public bool IsAlive
        {
            get { return thread.IsAlive; }
        }

        void Run()
        {
            try
            {
                RfcommSocket connection = RfcommSocket.Connect(address, NothingEarbuds.ControlService);
                socket = connection;
                if (stopped)
                {
                    connection.Dispose();
                    return;
                }
                lock (lastFailure)
                    lastFailure.Remove(address);
                Log.Write("Earbuds listener started for " + address + ".");

                using (connection)
                {
                    RequestUpdate();
                    var reader = new NothingEarbuds.FrameReader();
                    var buffer = new byte[512];
                    while (!stopped)
                    {
                        int read = connection.Receive(buffer);
                        if (read == 0)
                            break;

                        foreach (byte[] frame in reader.Append(buffer, read))
                        {
                            if (NothingEarbuds.IsBatteryNotice(frame))
                            {
                                RequestUpdate();
                                continue;
                            }
                            List<BatteryPart> parts = NothingEarbuds.ParseBattery(frame);
                            Action<string, List<BatteryPart>> handler = Changed;
                            if (parts != null && parts.Count > 0 && handler != null)
                                handler(address, parts);
                        }
                    }
                }
                if (!stopped)
                    Log.Write("Earbuds listener for " + address + " ended: the earbuds went away.");
            }
            catch (Exception ex)
            {
                LogFailure(ex.Message);
            }
        }

        void LogFailure(string message)
        {
            lock (lastFailure)
            {
                string previous;
                if (lastFailure.TryGetValue(address, out previous) && previous == message)
                    return;
                lastFailure[address] = message;
            }
            Log.Write("Earbuds listener for " + address + " stopped: " + message);
        }

        // Asks the earbuds for their battery; the answer arrives through Changed. Does nothing before the connection.
        public void RequestUpdate()
        {
            RfcommSocket s = socket;
            if (s == null)
                return;
            byte next = (byte)(Interlocked.Increment(ref sequence) % 250 + 1);
            s.Send(NothingEarbuds.BatteryRequest(next));
        }

        // Closes the channel, which ends the pending read; does not wait for the thread.
        public void Dispose()
        {
            stopped = true;
            RfcommSocket s = socket;
            if (s != null)
                s.Dispose();
        }
    }
}
