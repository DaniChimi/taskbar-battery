using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // What the settings window needs from the application.
    interface ISettingsHost
    {
        Settings Settings { get; }

        // Every connected device, hidden ones included.
        List<BatteryDevice> ConnectedDevices { get; }

        // Redraws the tray icons after an icon setting changed.
        void ApplyIconSettings();

        // Applies a new low-battery threshold everywhere.
        void ApplyLowThreshold();

        // Asks for a new name (dialog owned by 'owner'); returns true when the name changed.
        bool RenameDevice(BatteryDevice device, IWin32Window owner);

        void SetHidden(string address, bool hidden);

        void SendTestNotification();
    }

    // Every option, grouped in sections of Windows 11 style cards. Changes apply and are saved at once.
    sealed class SettingsWindow : Form, IMessageFilter
    {
        readonly ISettingsHost host;
        readonly float scale;
        readonly FlowLayoutPanel page;
        readonly Font titleFont;
        readonly Font descriptionFont;
        readonly Font sectionFont;
        readonly Font controlFont;
        DropDownButton shownDevice;

        public SettingsWindow(ISettingsHost host)
        {
            this.host = host;
            Palette p = Palette.Current;

            Text = Strings.Get(Strings.SettingsTitle);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = p.Background;
            ForeColor = p.Text;

            scale = DeviceDpi / 96f;
            titleFont = new Font(Theme.TextFont, 10.5f);
            descriptionFont = new Font(Theme.TextFont, 9f);
            sectionFont = new Font(Theme.TextFont, 11f, FontStyle.Bold);
            controlFont = new Font(Theme.TextFont, 9.5f);
            Font = controlFont;
            // Device column plus a settings column wide enough for the segmented rows to keep their descriptions
            // on one line.
            ClientSize = new Size(Px(DeviceColumnWidth + 640), Px(680));

            // The app icon in the title bar, as Windows 11 windows have; it also sets the title at its usual place.
            iconHandle = AppIcon.Create(Px(32));
            Icon = Icon.FromHandle(iconHandle);

            // Right column: the connected devices, drawn like the flyout, with rename and show/hide on each card.
            deviceList = new DeviceListView(scale);
            var devicesTitle = new Label
            {
                AutoSize = true,
                Text = Strings.Get(Strings.Title),
                Font = sectionFont,
                ForeColor = p.Text,
                BackColor = p.Background,
                Location = new Point(Px(14), Px(16)),
            };
            deviceColumn = new Panel { Dock = DockStyle.Right, Width = Px(DeviceColumnWidth), BackColor = p.Background };
            deviceColumn.Controls.Add(devicesTitle);
            deviceColumn.Controls.Add(deviceList);
            deviceList.Location = new Point(Px(12), devicesTitle.Bottom + Px(8));
            deviceList.Width = deviceColumn.Width - Px(12) - Px(24);
            deviceList.RenameRequested += delegate(BatteryDevice device)
            {
                if (host.RenameDevice(device, this))
                    RefreshShownDeviceNames();
            };
            deviceList.VisibilityChanged += delegate(BatteryDevice device, bool visible)
            {
                host.SetHidden(device.Address, !visible);
                RefreshShownDeviceNames();
            };
            UpdateDevices(host.ConnectedDevices, false);

            // Left column: the page grows to its content inside a clipping viewport; the themed scroll bar moves it.
            // Docking order: the device column takes the right edge first, then the scroll bar sits beside it.
            scrollBar = new FluentScrollBar(scale) { Dock = DockStyle.Right, BackColor = p.Background };
            viewport = new Panel { Dock = DockStyle.Fill, BackColor = p.Background };
            page = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Location = Point.Empty,
                Padding = new Padding(Px(24), Px(8), Px(10), Px(24)),
                BackColor = p.Background,
            };
            viewport.Controls.Add(page);
            Controls.Add(scrollBar);
            Controls.Add(deviceColumn);
            Controls.Add(viewport);
            viewport.BringToFront();

            scrollBar.ValueChanged += delegate { page.Top = -scrollBar.Value; };
            page.SizeChanged += delegate { UpdateScrollRange(); };
            viewport.Resize += delegate { UpdateScrollRange(); };

            Build();
        }

        // Logical width of the device column (the flyout's width plus margins).
        const float DeviceColumnWidth = 372f;

        readonly IntPtr iconHandle;

        readonly Panel viewport;
        readonly FluentScrollBar scrollBar;
        readonly Panel deviceColumn;
        readonly DeviceListView deviceList;

        // Called by the application on every scan and HID++ notification while the window is open.
        // Takes every connected device: hidden ones are listed too, dimmed, so they can be shown again.
        public void UpdateDevices(List<BatteryDevice> connectedDevices, bool readFailed)
        {
            deviceList.SetDevices(connectedDevices, host.Settings.Hidden, readFailed);
        }

        int Px(float logical)
        {
            return (int)Math.Round(logical * scale);
        }

        // Rows span the page; the scroll bar has its own column, so the layout never shifts when it appears.
        int RowWidth
        {
            get { return ClientSize.Width - deviceColumn.Width - scrollBar.Width - page.Padding.Horizontal; }
        }

        void UpdateScrollRange()
        {
            scrollBar.SetRange(page.Height, viewport.ClientSize.Height);
            page.Top = -scrollBar.Value;
        }

        // The mouse wheel scrolls the page wherever the pointer is over the window, whichever control has the focus.
        public bool PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg != WM_MOUSEWHEEL || !Visible)
                return false;

            long lParam = m.LParam.ToInt64();
            var pointer = new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
            if (!viewport.RectangleToScreen(viewport.ClientRectangle).Contains(pointer) &&
                !scrollBar.RectangleToScreen(scrollBar.ClientRectangle).Contains(pointer))
                return false;

            int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
            scrollBar.Value -= delta * Px(56) / 120;
            return true;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Application.AddMessageFilter(this);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Application.RemoveMessageFilter(this);
            base.OnFormClosed(e);
        }

        void Build()
        {
            Settings settings = host.Settings;
            page.SuspendLayout();
            page.Controls.Clear();

            // --- tray icon ---
            AddSection(Strings.Get(Strings.SectionTrayIcon));

            var style = new Segmented(scale, controlFont, new[]
            {
                Strings.Get(Strings.StyleBattery),
                Strings.Get(Strings.StylePercent),
                Strings.Get(Strings.StyleDeviceShort),
            }, (int)settings.IconStyle);
            style.SelectedIndexChanged += delegate
            {
                settings.IconStyle = (TrayIconStyle)style.SelectedIndex;
                settings.Save();
                host.ApplyIconSettings();
            };
            AddRow(Strings.Get(Strings.StyleTitle), Strings.Get(Strings.StyleDescription), style);

            var perDevice = new ToggleSwitch(scale, settings.PerDeviceIcons);
            perDevice.CheckedChanged += delegate
            {
                settings.PerDeviceIcons = perDevice.Checked;
                settings.Save();
                host.ApplyIconSettings();
                shownDevice.Enabled = !perDevice.Checked;
            };
            AddRow(Strings.Get(Strings.OneIconPerDevice), Strings.Get(Strings.OneIconDescription), perDevice);

            var deviceOptions = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(Strings.Get(Strings.AutomaticLowest), null),
            };
            foreach (BatteryDevice device in host.ConnectedDevices.Where(d => !settings.Hidden.Contains(d.Address)))
                deviceOptions.Add(new KeyValuePair<string, string>(device.DisplayName, device.Address));
            int current = deviceOptions.FindIndex(o => o.Value != null &&
                string.Equals(o.Value, settings.TrayDevice, StringComparison.OrdinalIgnoreCase));
            shownDevice = new DropDownButton(scale, controlFont, deviceOptions, current < 0 ? 0 : current);
            shownDevice.Enabled = !settings.PerDeviceIcons;
            shownDevice.SelectionChanged += delegate
            {
                settings.TrayDevice = shownDevice.SelectedValue;
                settings.Save();
                host.ApplyIconSettings();
            };
            AddRow(Strings.Get(Strings.ShownDevice), Strings.Get(Strings.ShownDeviceDescription), shownDevice);

            // --- notifications ---
            AddSection(Strings.Get(Strings.Notifications));
            AddToggle(Strings.Get(Strings.NotifyLow), Strings.Get(Strings.NotifyLowDescription), settings.NotifyLow,
                delegate(bool on) { settings.NotifyLow = on; });
            AddToggle(Strings.Get(Strings.NotifyFull), Strings.Get(Strings.NotifyFullDescription), settings.NotifyFull,
                delegate(bool on) { settings.NotifyFull = on; });
            AddToggle(Strings.Get(Strings.NotifyConnect), null, settings.NotifyConnect,
                delegate(bool on) { settings.NotifyConnect = on; });
            AddToggle(Strings.Get(Strings.NotifyDisconnect), null, settings.NotifyDisconnect,
                delegate(bool on) { settings.NotifyDisconnect = on; });

            string[] thresholds = Settings.LowThresholdChoices.Select(v => Strings.Format(Strings.Percent, v)).ToArray();
            var threshold = new Segmented(scale, controlFont, thresholds,
                Array.IndexOf(Settings.LowThresholdChoices, settings.LowThreshold));
            threshold.SelectedIndexChanged += delegate
            {
                settings.LowThreshold = Settings.LowThresholdChoices[threshold.SelectedIndex];
                settings.Save();
                host.ApplyLowThreshold();
            };
            AddRow(Strings.Get(Strings.LowThreshold), Strings.Get(Strings.LowThresholdDescription), threshold);

            var test = new FlatButton(scale, controlFont, Strings.Get(Strings.Send));
            test.Click += delegate { host.SendTestNotification(); };
            AddRow(Strings.Get(Strings.TestNotification), Strings.Get(Strings.TestNotificationDescription), test);

            // --- general ---
            AddSection(Strings.Get(Strings.SectionGeneral));
            var autostart = new ToggleSwitch(scale, Autostart.IsEnabled);
            autostart.CheckedChanged += delegate { Autostart.Set(autostart.Checked); };
            AddRow(Strings.Get(Strings.StartWithWindows), Strings.Get(Strings.StartWithWindowsDescription), autostart);

            var log = new FlatButton(scale, controlFont, Strings.Get(Strings.Open));
            log.Click += delegate { Log.Open(); };
            AddRow(Strings.Get(Strings.DiagnosticLog), Strings.Get(Strings.DiagnosticLogDescription), log);

            page.ResumeLayout();
        }

        // Rebuilds the settings after a rename or a visibility change, so the "device shown" drop-down follows.
        void RefreshShownDeviceNames()
        {
            int scroll = scrollBar.Value;
            Build();
            UpdateScrollRange();
            scrollBar.Value = scroll;
        }

        void AddSection(string text)
        {
            page.Controls.Add(new Label
            {
                AutoSize = true,
                Text = text,
                Font = sectionFont,
                ForeColor = Palette.Current.Text,
                Margin = new Padding(Px(2), Px(page.Controls.Count == 0 ? 8 : 22), 0, Px(8)),
            });
        }

        SettingRow AddRow(string title, string description, params Control[] controls)
        {
            var row = new SettingRow(scale, titleFont, descriptionFont, title, description, controls);
            row.Width = RowWidth;
            page.Controls.Add(row);
            return row;
        }

        void AddToggle(string title, string description, bool initial, Action<bool> apply)
        {
            var toggle = new ToggleSwitch(scale, initial);
            toggle.CheckedChanged += delegate
            {
                apply(toggle.Checked);
                host.Settings.Save();
            };
            AddRow(title, description, toggle);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dark = Theme.IsLight ? 0 : 1;
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Icon icon = Icon;
                Icon = null;
                if (icon != null)
                    icon.Dispose();
                if (iconHandle != IntPtr.Zero)
                    Native.DestroyIcon(iconHandle);
                titleFont.Dispose();
                descriptionFont.Dispose();
                sectionFont.Dispose();
                controlFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
