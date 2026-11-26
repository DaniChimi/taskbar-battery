using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TaskbarBattery
{
    // The application: scans the battery sources, keeps the tray icons and the flyout up to date, listens to
    // HID++ notifications and sends user notifications. The context menu lives in TrayApp.Menu.cs.
    sealed partial class TrayApp : ApplicationContext, ISettingsHost
    {
        const int ScanIntervalMs = 30 * 1000;

        // Drives the charging animation; runs only while a shown icon is animated.
        const int AnimationIntervalMs = 400;

        // A click on a tray icon that closes the flyout must not reopen it straight away.
        static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

        readonly Settings settings = Settings.Load();
        readonly Notifier notifier = new Notifier();
        readonly Flyout flyout;
        readonly ContextMenuStrip menu;
        readonly System.Windows.Forms.Timer scanTimer;
        readonly System.Windows.Forms.Timer animationTimer;
        readonly SynchronizationContext ui;

        // The icon Windows already knows: the single icon, or the first device's with one icon per device.
        readonly IconSlot mainSlot;
        // With one icon per device: the extra icons, for every visible device after the first.
        readonly Dictionary<string, IconSlot> deviceSlots = new Dictionary<string, IconSlot>(StringComparer.OrdinalIgnoreCase);

        int animationFrame;

        readonly Dictionary<string, HidppBatteryListener> listeners =
            new Dictionary<string, HidppBatteryListener>(StringComparer.OrdinalIgnoreCase);

        // Nothing / CMF earbuds: one listener per connected pair, and the last reading of each, which every scan
        // applies again (Windows itself has no level for them).
        readonly Dictionary<string, EarbudsBatteryListener> earbudsListeners =
            new Dictionary<string, EarbudsBatteryListener>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, EarbudsReading> earbudsReadings =
            new Dictionary<string, EarbudsReading>(StringComparer.OrdinalIgnoreCase);

        // Logitech devices on USB receivers, cables and dongles; Bluetooth ones are read through Windows and HID++ above.
        readonly LogitechUsbMonitor logitechUsb;

        // AirPods and Beats: their advertisements, heard while Apple earbuds are connected; the readings go to
        // earbudsReadings like those of the control channel. Null until first needed, or when Windows lacks the API.
        AppleAdvertisementWatcher appleWatcher;
        // Set when this Windows has no advertisement API: the scan is not tried again.
        bool appleWatcherUnavailable;
        string lastAppleWatcherError;
        readonly Dictionary<string, AppleEarbuds.Advertisement> appleLast =
            new Dictionary<string, AppleEarbuds.Advertisement>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, AdvertiserLock> appleLocks =
            new Dictionary<string, AdvertiserLock>(StringComparer.OrdinalIgnoreCase);

        // The sender a pair of earbuds is matched to. Another sender of the same model (someone else's pair nearby)
        // takes over only when clearly stronger or once the locked one has gone quiet, which also follows the
        // address the earbuds change every few minutes.
        sealed class AdvertiserLock
        {
            public ulong Sender;
            public short Signal;
            public DateTime LastHeard;
        }

        static readonly TimeSpan AdvertiserSilence = TimeSpan.FromSeconds(15);
        const int AdvertiserTakeOverMargin = 10;

        // A repeated reading only moves the card's time forward, at most this often.
        static readonly TimeSpan AppleTimeRefresh = TimeSpan.FromMinutes(1);

        // Advertisements weaker than this come from earbuds too far away to be the user's own (another pair of the
        // same model nearby); earbuds in the ears or on the desk are well above it.
        const short AppleMinimumSignal = -70;

        // Parts for earbuds; one level (Parts null) for single-battery headphones.
        sealed class EarbudsReading
        {
            public List<BatteryPart> Parts;
            public int Level;
            public bool Charging;
            public DateTime Time;

            public void ApplyTo(BatteryDevice device)
            {
                if (Parts != null)
                    device.ApplyParts(Parts, Time);
                else
                    device.ApplyLevel(Level, Charging, Time);
            }
        }

        // Every connected device, hidden ones included, and earbuds that have not reported yet.
        List<BatteryDevice> connected = new List<BatteryDevice>();
        DateTime lastScan = DateTime.Now;
        bool lastReadFailed;
        bool scanning;
        bool rescanRequested;
        string lastLoggedState;

        public TrayApp()
        {
            Log.Write("TaskbarBattery started.");
            BatteryDevice.LowThreshold = settings.LowThreshold;
            Autostart.RefreshPath();

            flyout = new Flyout();
            flyout.RefreshRequested += delegate { RefreshNow(); };
            flyout.SettingsRequested += delegate { OpenSettings(); };

            // Creating the first form installed the Windows Forms context for this thread.
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            menu = new FluentMenu();
            MenuStyle.Apply(menu, false);
            mainSlot = new IconSlot(ToggleFlyout, ShowTrayMenu);

            animationTimer = new System.Windows.Forms.Timer { Interval = AnimationIntervalMs };
            animationTimer.Tick += delegate
            {
                animationFrame = (animationFrame + 1) % TrayIconRenderer.AnimationFrames;
                UpdateIcons(false);
            };

            logitechUsb = new LogitechUsbMonitor();
            logitechUsb.Changed += delegate { ui.Post(delegate { ApplyLogitechUsb(); }, null); };

            UpdateIcons(true);
            Scan();

            scanTimer = new System.Windows.Forms.Timer { Interval = ScanIntervalMs };
            scanTimer.Tick += delegate { Scan(); };
            scanTimer.Start();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        // Connected devices with a battery level, hidden ones included: what the settings window lists.
        List<BatteryDevice> Reported
        {
            get { return connected.Where(d => d.HasReading).ToList(); }
        }

        // Reported devices the user has not hidden: what the icons, the flyout and the notifications show.
        List<BatteryDevice> Visible
        {
            get { return Reported.Where(d => !settings.Hidden.Contains(d.Address)).ToList(); }
        }

        // --- scanning ---

        // The refresh button and menu item: read every device now, USB ones included.
        void RefreshNow()
        {
            logitechUsb.Refresh();
            Scan();
        }

        // Reads on a worker thread (a sleeping device can take a moment to answer HID++) and applies on the UI thread.
        void Scan()
        {
            if (scanning)
            {
                rescanRequested = true;
                return;
            }
            scanning = true;
            logitechUsb.Rescan();

            ThreadPool.QueueUserWorkItem(delegate
            {
                List<BatteryDevice> result;
                bool failed = false;
                try
                {
                    result = ReadConnectedDevices();
                    result.AddRange(logitechUsb.Devices());
                }
                catch (Exception ex)
                {
                    Log.Write("Battery read failed: " + ex.Message);
                    result = new List<BatteryDevice>();
                    failed = true;
                }
                ui.Post(delegate { ApplyScan(result, failed); }, null);
            });
        }

        static List<BatteryDevice> ReadConnectedDevices()
        {
            Dictionary<string, string> earbuds;
            try
            {
                earbuds = NothingEarbuds.FindDevices();
                foreach (KeyValuePair<string, string> apple in AppleEarbuds.FindDevices())
                    earbuds[apple.Key] = apple.Value;
            }
            catch (Exception ex)
            {
                Log.Write("Earbuds lookup failed: " + ex.Message);
                earbuds = null;
            }
            List<BatteryDevice> devices = BluetoothBatterySource.Read(earbuds).Where(d => d.IsConnected).ToList();

            // Logitech Bluetooth devices also report their charging state over HID++.
            List<BatteryDevice> logitech = devices.Where(IsLogitechBluetooth).ToList();
            if (logitech.Count > 0)
            {
                List<HidCollection> collections = Hid.Enumerate(LogitechHidpp.LogitechVendorId);
                foreach (BatteryDevice device in logitech)
                {
                    try
                    {
                        LogitechHidpp.Reading reading = LogitechHidpp.Read(device.Address, collections);
                        if (reading != null)
                            device.ApplyVendorReading(reading.Percent, reading.Charge);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("HID++ read failed for " + device.Name + ": " + ex.Message);
                    }
                }
            }
            return devices;
        }

        void ApplyScan(List<BatteryDevice> devices, bool failed)
        {
            foreach (BatteryDevice device in devices)
            {
                string name;
                device.CustomName = settings.Names.TryGetValue(device.Address, out name) ? name : null;

                EarbudsReading reading;
                if (device.IsEarbuds && earbudsReadings.TryGetValue(device.Address, out reading))
                    reading.ApplyTo(device);

                // A sleeping device may miss the HID++ query: keep the charging state it last reported (the
                // listener corrects it as soon as the device sends a notification).
                if (device.Charge == ChargeState.Unknown)
                {
                    BatteryDevice previous = connected.FirstOrDefault(d => d.Address == device.Address);
                    if (previous != null)
                        device.Charge = previous.Charge;
                }
            }

            connected = devices;
            lastReadFailed = failed;
            lastScan = DateTime.Now;
            scanning = false;

            Refresh();
            SyncListeners();

            if (rescanRequested)
            {
                rescanRequested = false;
                Scan();
            }
        }

        // Pushes the current state to the icons, the flyout, the notifications and the log.
        void Refresh()
        {
            List<BatteryDevice> visible = Visible;
            UpdateIcons(false);
            if (flyout.Visible)
                flyout.SetData(visible, lastScan, lastReadFailed);
            if (settingsWindow != null && !settingsWindow.IsDisposed)
                settingsWindow.UpdateDevices(Reported, lastReadFailed);

            foreach (Notice notice in notifier.Process(visible, settings))
                ShowNotice(notice);

            LogStateChange(visible);
        }

        // Logs the device list only when it changes, so the log records events rather than every scan.
        void LogStateChange(List<BatteryDevice> visible)
        {
            string state = lastReadFailed
                ? "read failed"
                : visible.Count == 0
                    ? "no device connected"
                    : string.Join("; ", visible.Select(d => d.DisplayName + " " + d.Percent + "% " + d.Charge + DescribeParts(d)).ToArray());
            if (state == lastLoggedState)
                return;
            lastLoggedState = state;
            Log.Write("Devices: " + state);
        }

        static string DescribeParts(BatteryDevice device)
        {
            if (device.Parts == null)
                return string.Empty;
            return " (" + string.Join(", ", device.Parts.Select(p => p.Kind + " " + p.Percent + "%" + (p.IsCharging ? " charging" : "")).ToArray()) + ")";
        }

        // One HID++ listener per connected Logitech device; listeners whose device left (or that ended) are dropped,
        // and a later scan starts them again.
        void SyncListeners()
        {
            var wanted = new HashSet<string>(
                connected.Where(IsLogitechBluetooth).Select(d => d.Address),
                StringComparer.OrdinalIgnoreCase);

            foreach (string address in listeners.Keys.ToList())
            {
                if (!wanted.Contains(address) || !listeners[address].IsAlive)
                {
                    listeners[address].Dispose();
                    listeners.Remove(address);
                }
            }

            foreach (string address in wanted)
            {
                if (listeners.ContainsKey(address))
                    continue;
                var listener = new HidppBatteryListener(address);
                listener.Changed += delegate(string source, LogitechHidpp.Reading reading)
                {
                    ui.Post(delegate { OnBatteryNotification(source, reading); }, null);
                };
                listeners[address] = listener;
            }

            SyncEarbudsListeners();
        }

        // One listener per connected pair of earbuds. A running listener is asked for a fresh reading at each scan;
        // readings of earbuds that left are dropped.
        void SyncEarbudsListeners()
        {
            var present = new HashSet<string>(
                connected.Where(d => d.IsEarbuds).Select(d => d.Address),
                StringComparer.OrdinalIgnoreCase);
            foreach (string address in earbudsReadings.Keys.Where(a => !present.Contains(a)).ToList())
                earbudsReadings.Remove(address);
            foreach (string address in appleLast.Keys.Where(a => !present.Contains(a)).ToList())
                appleLast.Remove(address);
            foreach (string address in appleLocks.Keys.Where(a => !present.Contains(a)).ToList())
                appleLocks.Remove(address);

            SyncAppleWatcher();

            // AirPods and Beats have no control channel: only the other earbuds get a listener.
            var wanted = new HashSet<string>(
                connected.Where(d => d.IsEarbuds && !IsAppleEarbuds(d)).Select(d => d.Address),
                StringComparer.OrdinalIgnoreCase);

            foreach (string address in earbudsListeners.Keys.ToList())
            {
                if (!wanted.Contains(address) || !earbudsListeners[address].IsAlive)
                {
                    earbudsListeners[address].Dispose();
                    earbudsListeners.Remove(address);
                }
            }

            foreach (string address in wanted)
            {
                EarbudsBatteryListener existing;
                if (earbudsListeners.TryGetValue(address, out existing))
                {
                    existing.RequestUpdate();
                    continue;
                }
                var listener = new EarbudsBatteryListener(address);
                listener.Changed += delegate(string source, List<BatteryPart> parts)
                {
                    ui.Post(delegate { OnEarbudsReading(source, parts); }, null);
                };
                earbudsListeners[address] = listener;
            }
        }

        static bool IsAppleEarbuds(BatteryDevice device)
        {
            return device.IsEarbuds && device.Manufacturer == "Apple";
        }

        // Scans for advertisements only while Apple earbuds are connected.
        void SyncAppleWatcher()
        {
            bool wanted = connected.Any(IsAppleEarbuds);
            if (!wanted)
            {
                if (appleWatcher != null)
                    appleWatcher.Stop();
                return;
            }
            if (appleWatcherUnavailable)
                return;
            try
            {
                if (appleWatcher == null)
                    appleWatcher = CreateAppleWatcher();
                // Starting again also recovers a scan the radio aborted (Bluetooth turned off and on).
                appleWatcher.Start();
                lastAppleWatcherError = null;
            }
            catch (TypeLoadException ex)
            {
                appleWatcherUnavailable = true;
                Log.Write("Bluetooth advertisement scan unavailable: " + ex.Message);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                // REGDB_E_CLASSNOTREG: this Windows has no advertisement watcher at all.
                if (ex.ErrorCode == unchecked((int)0x80040154))
                    appleWatcherUnavailable = true;
                if (ex.Message != lastAppleWatcherError)
                    Log.Write("Bluetooth advertisement scan failed: " + ex.Message);
                lastAppleWatcherError = ex.Message;
            }
            catch (Exception ex)
            {
                // Radio off or busy: tried again at the next scan, logged once.
                if (ex.Message != lastAppleWatcherError)
                    Log.Write("Bluetooth advertisement scan failed: " + ex.Message);
                lastAppleWatcherError = ex.Message;
            }
        }

        AppleAdvertisementWatcher CreateAppleWatcher()
        {
            var watcher = new AppleAdvertisementWatcher();
            watcher.Received += delegate(byte[] raw, AppleEarbuds.Advertisement advertisement, ulong sender, short signal)
            {
                if (advertisement != null && signal >= AppleMinimumSignal)
                    ui.Post(delegate { OnAppleAdvertisement(advertisement, sender, signal); }, null);
            };
            return watcher;
        }

        // Advertisements carry a random address, not the paired one: they are matched by model, then to one sender
        // (see AdvertiserLock). With two connected pairs of the same model the reading is ambiguous and dropped.
        void OnAppleAdvertisement(AppleEarbuds.Advertisement advertisement, ulong sender, short signal)
        {
            List<BatteryDevice> matches = connected
                .Where(d => IsAppleEarbuds(d) && d.ProductId == advertisement.ProductId)
                .ToList();
            if (matches.Count != 1)
                return;
            BatteryDevice device = matches[0];

            DateTime now = DateTime.Now;
            AdvertiserLock locked;
            if (appleLocks.TryGetValue(device.Address, out locked) && locked.Sender != sender &&
                now - locked.LastHeard < AdvertiserSilence && signal < locked.Signal + AdvertiserTakeOverMargin)
                return;
            appleLocks[device.Address] = new AdvertiserLock { Sender = sender, Signal = signal, LastHeard = now };

            AppleEarbuds.Advertisement last;
            EarbudsReading previous;
            if (appleLast.TryGetValue(device.Address, out last) && AppleEarbuds.SameReading(last, advertisement))
            {
                // Same levels: only keep the card's time current.
                if (earbudsReadings.TryGetValue(device.Address, out previous) && now - previous.Time >= AppleTimeRefresh)
                {
                    previous.Time = now;
                    previous.ApplyTo(device);
                    Refresh();
                }
                return;
            }
            appleLast[device.Address] = advertisement;

            var reading = new EarbudsReading
            {
                Parts = advertisement.Parts,
                Level = advertisement.Level,
                Charging = advertisement.Charging,
                Time = now,
            };
            earbudsReadings[device.Address] = reading;
            reading.ApplyTo(device);
            Refresh();
        }

        // Logitech devices from the USB monitor carry ids of their own ("LOGI-..."), not a Bluetooth address.
        static bool IsLogitechUsb(BatteryDevice device)
        {
            return device.Address.StartsWith("LOGI-", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsLogitechBluetooth(BatteryDevice device)
        {
            return device.Manufacturer == "Logitech" && !IsLogitechUsb(device);
        }

        // The USB monitor saw a change (a reading, a device connecting or leaving): swap its devices into the list
        // without waiting for the next scan.
        void ApplyLogitechUsb()
        {
            List<BatteryDevice> usb = logitechUsb.Devices();
            foreach (BatteryDevice device in usb)
            {
                string name;
                device.CustomName = settings.Names.TryGetValue(device.Address, out name) ? name : null;
            }
            connected = connected.Where(d => !IsLogitechUsb(d)).Concat(usb).ToList();
            Refresh();
        }

        void OnEarbudsReading(string address, List<BatteryPart> parts)
        {
            var reading = new EarbudsReading { Parts = parts, Time = DateTime.Now };
            earbudsReadings[address] = reading;

            BatteryDevice device = connected.FirstOrDefault(d => string.Equals(d.Address, address, StringComparison.OrdinalIgnoreCase));
            if (device == null)
                return;
            reading.ApplyTo(device);
            Refresh();
        }

        void OnBatteryNotification(string address, LogitechHidpp.Reading reading)
        {
            BatteryDevice device = connected.FirstOrDefault(d => string.Equals(d.Address, address, StringComparison.OrdinalIgnoreCase));
            if (device == null)
                return;

            device.ApplyVendorReading(reading.Percent, reading.Charge);
            Refresh();
        }

        // --- tray icons ---

        // The chosen device if it is visible, otherwise the visible device with the least battery.
        BatteryDevice ShownDevice(List<BatteryDevice> visible)
        {
            BatteryDevice chosen = visible.FirstOrDefault(d => string.Equals(d.Address, settings.TrayDevice, StringComparison.OrdinalIgnoreCase));
            return chosen ?? visible.OrderBy(d => d.Percent).FirstOrDefault();
        }

        void UpdateIcons(bool force)
        {
            List<BatteryDevice> visible = Visible;
            bool perDevice = settings.PerDeviceIcons && visible.Count > 0;
            bool animated;

            if (perDevice)
            {
                // The first device keeps the main icon: Windows 11 hides icons it has not seen before under the
                // overflow arrow, so only the extra devices get new ones.
                BatteryDevice first = visible[0];
                mainSlot.Update(first, settings.IconStyle, animationFrame, TooltipLine(first), force);

                List<BatteryDevice> others = visible.Skip(1).ToList();
                foreach (string address in deviceSlots.Keys.Where(a => !others.Any(d => d.Address == a)).ToList())
                {
                    deviceSlots[address].Dispose();
                    deviceSlots.Remove(address);
                }
                foreach (BatteryDevice device in others)
                {
                    IconSlot slot;
                    if (!deviceSlots.TryGetValue(device.Address, out slot))
                    {
                        slot = new IconSlot(ToggleFlyout, ShowTrayMenu);
                        deviceSlots[device.Address] = slot;
                    }
                    slot.Update(device, settings.IconStyle, animationFrame, TooltipLine(device), force);
                }
                animated = visible.Any(d => TrayIconRenderer.IsAnimated(d, settings.IconStyle));
            }
            else
            {
                DisposeDeviceSlots();
                BatteryDevice shown = ShownDevice(visible);
                mainSlot.Update(shown, settings.IconStyle, animationFrame, BuildTooltip(visible), force);
                animated = TrayIconRenderer.IsAnimated(shown, settings.IconStyle);
            }

            if (animated && !animationTimer.Enabled)
            {
                animationFrame = 0;
                animationTimer.Start();
            }
            else if (!animated && animationTimer.Enabled)
            {
                animationTimer.Stop();
            }
        }

        void DisposeDeviceSlots()
        {
            foreach (IconSlot slot in deviceSlots.Values)
                slot.Dispose();
            deviceSlots.Clear();
        }

        string BuildTooltip(List<BatteryDevice> visible)
        {
            if (lastReadFailed)
                return Strings.Get(Strings.ReadError);
            if (visible.Count == 0)
                return Strings.Get(Strings.NoDevices);
            return string.Join("\n", visible.Select(d => TooltipLine(d)).ToArray());
        }

        static string TooltipLine(BatteryDevice device)
        {
            if (device.Parts != null)
            {
                // Earbuds: every part on one line, as on the card, for example "CMF Buds 2: L 60% · Case --% · R 50%".
                var parts = new List<string>();
                foreach (PartKind kind in new[] { PartKind.Left, PartKind.Case, PartKind.Right })
                {
                    BatteryPart part = device.Parts.FirstOrDefault(p => p.Kind == kind);
                    parts.Add(part == null
                        ? PartLabel(kind) + " " + Strings.Get(Strings.PartUnknown)
                        : Strings.Format(part.IsCharging ? Strings.PartCharging : Strings.PartLevel, PartLabel(kind), part.Percent));
                }
                return device.DisplayName + ": " + string.Join(" · ", parts.ToArray());
            }
            return Strings.Format(device.IsCharging ? Strings.TooltipCharging : Strings.TooltipLine, device.DisplayName, device.Percent);
        }

        internal static string PartLabel(PartKind kind)
        {
            switch (kind)
            {
                case PartKind.Left: return Strings.Get(Strings.PartLeft);
                case PartKind.Right: return Strings.Get(Strings.PartRight);
                default: return Strings.Get(Strings.PartCase);
            }
        }

        // Toasts come from the device's own icon when it has an extra one, otherwise from the main icon.
        void ShowNotice(Notice notice)
        {
            IconSlot slot;
            if (!deviceSlots.TryGetValue(notice.Address ?? string.Empty, out slot))
                slot = mainSlot;
            slot.Show(notice);
            Log.Write("Notification: " + notice.Title + " / " + notice.Text);
        }

        void ToggleFlyout()
        {
            if (flyout.Visible)
            {
                flyout.Hide();
                return;
            }
            if (DateTime.Now - flyout.HiddenAt < ReopenGuard)
                return;

            Scan();
            flyout.ShowNear(new Rectangle(Cursor.Position, new Size(1, 1)), Visible, lastScan, lastReadFailed);
        }

        // --- settings window ---

        SettingsWindow settingsWindow;

        void OpenSettings()
        {
            if (flyout.Visible)
                flyout.Hide();
            if (settingsWindow == null || settingsWindow.IsDisposed)
            {
                settingsWindow = new SettingsWindow(this);
                settingsWindow.FormClosed += delegate { settingsWindow = null; };
                settingsWindow.Show();
            }
            if (settingsWindow.WindowState == FormWindowState.Minimized)
                settingsWindow.WindowState = FormWindowState.Normal;
            settingsWindow.Activate();
        }

        Settings ISettingsHost.Settings
        {
            get { return settings; }
        }

        List<BatteryDevice> ISettingsHost.ConnectedDevices
        {
            get { return Reported; }
        }

        void ISettingsHost.ApplyIconSettings()
        {
            UpdateIcons(true);
        }

        void ISettingsHost.ApplyLowThreshold()
        {
            BatteryDevice.LowThreshold = settings.LowThreshold;
            Refresh();
        }

        bool ISettingsHost.RenameDevice(BatteryDevice device, IWin32Window owner)
        {
            using (var dialog = new RenameDialog(device))
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                    return false;

                if (dialog.CustomName == null)
                    settings.Names.Remove(device.Address);
                else
                    settings.Names[device.Address] = dialog.CustomName;
                settings.Save();
                Log.Write("Renamed " + device.Name + " to " + (dialog.CustomName ?? "its original name") + ".");

                foreach (BatteryDevice same in connected.Where(d => d.Address == device.Address))
                    same.CustomName = dialog.CustomName;
                device.CustomName = dialog.CustomName;
                Refresh();
                return true;
            }
        }

        void ISettingsHost.SetHidden(string address, bool hidden)
        {
            if (hidden)
                settings.Hidden.Add(address);
            else
                settings.Hidden.Remove(address);
            settings.Save();
            Log.Write((hidden ? "Hidden: " : "Shown again: ") + address);
            Refresh();
        }

        void ISettingsHost.SendTestNotification()
        {
            ShowNotice(new Notice
            {
                Title = Strings.Get(Strings.TestNoticeTitle),
                Text = Strings.Get(Strings.TestNoticeText),
                Icon = ToolTipIcon.Info,
            });
        }

        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // Theme or accent changes: redraw the icons in the new colours.
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color)
                UpdateIcons(true);
        }

        protected override void ExitThreadCore()
        {
            Log.Write("TaskbarBattery exiting.");
            scanTimer.Stop();
            animationTimer.Stop();
            foreach (HidppBatteryListener listener in listeners.Values)
                listener.Dispose();
            listeners.Clear();
            foreach (EarbudsBatteryListener listener in earbudsListeners.Values)
                listener.Dispose();
            earbudsListeners.Clear();
            if (appleWatcher != null)
                appleWatcher.Dispose();
            logitechUsb.Dispose();
            flyout.Hide();
            if (settingsWindow != null && !settingsWindow.IsDisposed)
                settingsWindow.Close();
            mainSlot.Hide();
            DisposeDeviceSlots();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                scanTimer.Dispose();
                animationTimer.Dispose();
                mainSlot.Dispose();
                DisposeDeviceSlots();
                menu.Dispose();
                flyout.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
