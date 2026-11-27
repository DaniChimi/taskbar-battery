using System;
using Windows.Storage.Streams;
using Windows.Devices.Bluetooth.Advertisement;

namespace TaskbarBattery
{
    // Listens passively to the Bluetooth LE advertisements of AirPods and Beats (see AppleEarbuds). Passive scanning
    // only receives what the earbuds broadcast anyway: nothing is sent. The owner starts it while Apple earbuds are
    // connected and stops it otherwise; Start again after the radio was turned off restarts the scan.
    sealed class AppleAdvertisementWatcher : IDisposable
    {
        readonly BluetoothLEAdvertisementWatcher watcher;

        // Raised on a thread-pool thread for every Apple proximity message: its raw manufacturer data, the decoded
        // reading (null when it is not an audio device or carries no level), the sender's (random, rotating) address
        // and the signal strength in dBm.
        public event Action<byte[], AppleEarbuds.Advertisement, ulong, short> Received;

        public AppleAdvertisementWatcher()
        {
            watcher = new BluetoothLEAdvertisementWatcher();
            watcher.ScanningMode = BluetoothLEScanningMode.Passive;
            var filter = new BluetoothLEManufacturerData();
            filter.CompanyId = AppleEarbuds.AppleCompanyId;
            var prefix = new DataWriter();
            prefix.WriteByte(AppleEarbuds.ProximityPairing);
            filter.Data = prefix.DetachBuffer();
            watcher.AdvertisementFilter.Advertisement.ManufacturerData.Add(filter);
            watcher.Received += OnReceived;
        }

        public bool IsRunning
        {
            get { return watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started; }
        }

        // Starts from a fresh, stopped or aborted (radio turned off) scan; a scan still stopping is left for the
        // next call.
        public void Start()
        {
            BluetoothLEAdvertisementWatcherStatus status = watcher.Status;
            if (status == BluetoothLEAdvertisementWatcherStatus.Created ||
                status == BluetoothLEAdvertisementWatcherStatus.Stopped ||
                status == BluetoothLEAdvertisementWatcherStatus.Aborted)
                watcher.Start();
        }

        public void Stop()
        {
            if (watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                watcher.Stop();
        }

        void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            Action<byte[], AppleEarbuds.Advertisement, ulong, short> handler = Received;
            if (handler == null)
                return;
            foreach (BluetoothLEManufacturerData data in args.Advertisement.ManufacturerData)
            {
                if (data.CompanyId != AppleEarbuds.AppleCompanyId)
                    continue;
                var raw = new byte[data.Data.Length];
                DataReader.FromBuffer(data.Data).ReadBytes(raw);
                handler(raw, AppleEarbuds.Parse(raw), args.BluetoothAddress, args.RawSignalStrengthInDBm);
            }
        }

        public void Dispose()
        {
            watcher.Received -= OnReceived;
            try
            {
                Stop();
            }
            catch (Exception)
            {
                // Stopping a scan the radio already aborted may fail; there is nothing left to release.
            }
        }
    }
}
