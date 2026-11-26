using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TaskbarBattery
{
    // The application: scans the battery sources, draws the tray icon, opens the flyout and owns the menu.
    sealed class TrayApp : ApplicationContext
    {
        const int ScanIntervalMs = 30 * 1000;
        const int MaxTooltipLength = 63;

        // A click on the tray icon that closes the flyout must not reopen it straight away.
        static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

        readonly Settings settings = Settings.Load();
        readonly NotifyIcon notifyIcon;
        readonly Flyout flyout;
        readonly ContextMenuStrip menu;
        readonly System.Windows.Forms.Timer scanTimer;

        // Drives the charging animation; runs only while the shown icon is animated.
        const int AnimationIntervalMs = 400;
        readonly System.Windows.Forms.Timer animationTimer;
        int animationFrame;

        readonly SynchronizationContext ui;

        List<BatteryDevice> connected = new List<BatteryDevice>();
        DateTime lastScan = DateTime.Now;
        bool lastReadFailed;
        bool scanning;
        bool rescanRequested;

        readonly Dictionary<string, HidppBatteryListener> listeners =
            new Dictionary<string, HidppBatteryListener>(StringComparer.OrdinalIgnoreCase);

        Icon currentIcon;
        IntPtr currentIconHandle = IntPtr.Zero;
        string iconKey;

        public TrayApp()
        {
            flyout = new Flyout();
            flyout.RefreshRequested += delegate { Scan(); };

            // Creating the first form installed the Windows Forms context for this thread.
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            menu = new ContextMenuStrip();
            StyleDropDown(menu, false);
            menu.Opening += delegate
            {
                if (flyout.Visible)
                    flyout.Hide();
                RebuildMenu();
            };

            notifyIcon = new NotifyIcon();
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    ToggleFlyout();
            };

            animationTimer = new System.Windows.Forms.Timer { Interval = AnimationIntervalMs };
            animationTimer.Tick += delegate
            {
                animationFrame = (animationFrame + 1) % TrayIconRenderer.AnimationFrames;
                UpdateIcon(false);
            };

            UpdateIcon(true);
            notifyIcon.Visible = true;
            Scan();

            scanTimer = new System.Windows.Forms.Timer { Interval = ScanIntervalMs };
            scanTimer.Tick += delegate { Scan(); };
            scanTimer.Start();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
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

            ThreadPool.QueueUserWorkItem(delegate
            {
                List<BatteryDevice> result;
                bool failed = false;
                try
                {
                    result = ReadConnectedDevices();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Battery read failed: " + ex);
                    result = new List<BatteryDevice>();
                    failed = true;
                }
                ui.Post(delegate { ApplyScan(result, failed); }, null);
            });
        }

        static List<BatteryDevice> ReadConnectedDevices()
        {
            List<BatteryDevice> devices = BluetoothBatterySource.Read().Where(d => d.IsConnected).ToList();

            // Logitech devices also report their charging state over HID++.
            List<BatteryDevice> logitech = devices.Where(d => d.Manufacturer == "Logitech").ToList();
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
                        Debug.WriteLine("HID++ read failed for " + device.Name + ": " + ex);
                    }
                }
            }
            return devices;
        }

        void ApplyScan(List<BatteryDevice> devices, bool failed)
        {
            connected = devices;
            lastReadFailed = failed;
            lastScan = DateTime.Now;
            scanning = false;

            UpdateIcon(false);
            if (flyout.Visible)
                flyout.SetData(connected, lastScan, lastReadFailed);
            SyncListeners();

            if (rescanRequested)
            {
                rescanRequested = false;
                Scan();
            }
        }

        // One HID++ listener per connected Logitech device; listeners whose device left (or that ended) are dropped,
        // and a later scan starts them again.
        void SyncListeners()
        {
            var wanted = new HashSet<string>(
                connected.Where(d => d.Manufacturer == "Logitech").Select(d => d.Address),
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
        }

        void OnBatteryNotification(string address, LogitechHidpp.Reading reading)
        {
            BatteryDevice device = connected.FirstOrDefault(d => string.Equals(d.Address, address, StringComparison.OrdinalIgnoreCase));
            if (device == null)
                return;

            device.ApplyVendorReading(reading.Percent, reading.Charge);
            UpdateIcon(false);
            if (flyout.Visible)
                flyout.SetData(connected, lastScan, lastReadFailed);
        }

        // The chosen device if it is connected, otherwise the connected device with the least battery.
        BatteryDevice ShownDevice()
        {
            BatteryDevice chosen = connected.FirstOrDefault(d => string.Equals(d.Address, settings.TrayDevice, StringComparison.OrdinalIgnoreCase));
            return chosen ?? connected.OrderBy(d => d.Percent).FirstOrDefault();
        }

        void UpdateIcon(bool force)
        {
            BatteryDevice shown = ShownDevice();
            bool animated = TrayIconRenderer.IsAnimated(shown, settings.IconStyle);
            if (animated && !animationTimer.Enabled)
            {
                animationFrame = 0;
                animationTimer.Start();
            }
            else if (!animated && animationTimer.Enabled)
            {
                animationTimer.Stop();
            }

            int size = SystemInformation.SmallIconSize.Width;
            string key = string.Join("|", size, settings.IconStyle, Theme.IsLight, animated ? animationFrame : -1,
                shown == null ? "-" : shown.Address + ":" + shown.Percent + ":" + shown.Kind + ":" + shown.Charge);
            if (force || key != iconKey)
            {
                IntPtr handle = TrayIconRenderer.Render(shown, settings.IconStyle, size, animationFrame);
                Icon icon = Icon.FromHandle(handle);
                notifyIcon.Icon = icon;
                ReleaseIcon();
                currentIcon = icon;
                currentIconHandle = handle;
                iconKey = key;
            }

            string tooltip = BuildTooltip();
            notifyIcon.Text = tooltip.Length > MaxTooltipLength ? tooltip.Substring(0, MaxTooltipLength) : tooltip;
        }

        void ReleaseIcon()
        {
            if (currentIcon != null)
                currentIcon.Dispose();
            if (currentIconHandle != IntPtr.Zero)
                Native.DestroyIcon(currentIconHandle);
            currentIcon = null;
            currentIconHandle = IntPtr.Zero;
        }

        string BuildTooltip()
        {
            if (lastReadFailed)
                return Strings.Get(Strings.ReadError);
            if (connected.Count == 0)
                return Strings.Get(Strings.NoDevices);
            return string.Join("\n", connected.Select(d =>
                Strings.Format(d.IsCharging ? Strings.TooltipCharging : Strings.TooltipLine, d.Name, d.Percent)).ToArray());
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
            flyout.ShowNear(new Rectangle(Cursor.Position, new Size(1, 1)), connected, lastScan, lastReadFailed);
        }

        // --- menu ---

        void RebuildMenu()
        {
            menu.Items.Clear();

            var iconMenu = new ToolStripMenuItem(Strings.Get(Strings.TrayIcon));
            StyleDropDown(iconMenu.DropDown, true);
            iconMenu.DropDownItems.Add(StyleChoice(Strings.StyleBattery, TrayIconStyle.Battery));
            iconMenu.DropDownItems.Add(StyleChoice(Strings.StylePercent, TrayIconStyle.Percent));
            iconMenu.DropDownItems.Add(StyleChoice(Strings.StyleDevice, TrayIconStyle.Device));
            menu.Items.Add(iconMenu);

            var deviceMenu = new ToolStripMenuItem(Strings.Get(Strings.ShownDevice));
            StyleDropDown(deviceMenu.DropDown, true);
            deviceMenu.DropDownItems.Add(DeviceChoice(Strings.Get(Strings.AutomaticLowest), null));
            if (connected.Count > 0)
                deviceMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (BatteryDevice device in connected)
                deviceMenu.DropDownItems.Add(DeviceChoice(device.Name, device.Address));
            menu.Items.Add(deviceMenu);

            menu.Items.Add(new ToolStripSeparator());

            var refresh = new ToolStripMenuItem(Strings.Get(Strings.Refresh));
            refresh.Click += delegate { Scan(); };
            menu.Items.Add(refresh);

            menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem(Strings.Get(Strings.Exit));
            exit.Click += delegate { ExitThread(); };
            menu.Items.Add(exit);

            SpaceItems(menu.Items);
        }

        // Windows 11 menu metrics: about 32 px per item and a little air around separators (at 100% scale).
        static void SpaceItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                var menuItem = item as ToolStripMenuItem;
                if (menuItem != null)
                {
                    menuItem.Padding = new Padding(6, 7, 6, 7);
                    if (menuItem.HasDropDownItems)
                        SpaceItems(menuItem.DropDownItems);
                }
                else if (item is ToolStripSeparator)
                {
                    item.AutoSize = false;
                    item.Height = 9;
                }
            }
        }

        ToolStripMenuItem StyleChoice(string textKey, TrayIconStyle style)
        {
            var item = new ToolStripMenuItem(Strings.Get(textKey));
            item.Checked = settings.IconStyle == style;
            item.Click += delegate
            {
                settings.IconStyle = style;
                settings.Save();
                UpdateIcon(true);
            };
            return item;
        }

        ToolStripMenuItem DeviceChoice(string text, string address)
        {
            var item = new ToolStripMenuItem(text);
            item.Checked = address == null
                ? settings.TrayDevice == null
                : string.Equals(settings.TrayDevice, address, StringComparison.OrdinalIgnoreCase);
            item.Click += delegate
            {
                settings.TrayDevice = address;
                settings.Save();
                UpdateIcon(true);
            };
            return item;
        }

        // showChecks: reserve the column for check marks; only the submenus with choices need it.
        static void StyleDropDown(ToolStripDropDown dropDown, bool showChecks)
        {
            dropDown.Renderer = new MenuRenderer();
            dropDown.Font = new Font(Theme.TextFont, 9.5f);
            dropDown.Padding = new Padding(4, 4, 4, 4);
            var menuDropDown = dropDown as ToolStripDropDownMenu;
            if (menuDropDown != null)
            {
                menuDropDown.ShowImageMargin = false;
                menuDropDown.ShowCheckMargin = showChecks;
                // The check column is sized from the image size: keep it narrow.
                menuDropDown.ImageScalingSize = new Size(12, 12);
            }
            dropDown.Opened += delegate
            {
                int corner = Native.DWMWCP_ROUND;
                Native.DwmSetWindowAttribute(dropDown.Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            };
        }

        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // Theme or accent changes: redraw the icon in the new colours.
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color)
                UpdateIcon(true);
        }

        protected override void ExitThreadCore()
        {
            scanTimer.Stop();
            animationTimer.Stop();
            foreach (HidppBatteryListener listener in listeners.Values)
                listener.Dispose();
            listeners.Clear();
            flyout.Hide();
            notifyIcon.Visible = false;
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                scanTimer.Dispose();
                animationTimer.Dispose();
                notifyIcon.Dispose();
                menu.Dispose();
                flyout.Dispose();
                ReleaseIcon();
            }
            base.Dispose(disposing);
        }
    }
}
