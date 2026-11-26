using System;
using System.Drawing;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // One icon in the notification area: owns the NotifyIcon and the icon handle it shows, and redraws only
    // when what it shows changes.
    sealed class IconSlot : IDisposable
    {
        const int MaxTooltipLength = 63;

        readonly NotifyIcon notifyIcon;
        Icon current;
        IntPtr currentHandle = IntPtr.Zero;
        string renderKey;

        public IconSlot(ContextMenuStrip menu, Action leftClick)
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    leftClick();
            };
        }

        public void Update(BatteryDevice device, TrayIconStyle style, int frame, string tooltip, bool force)
        {
            int size = SystemInformation.SmallIconSize.Width;
            bool animated = TrayIconRenderer.IsAnimated(device, style);
            string key = string.Join("|", size, style, Theme.IsLight, animated ? frame : -1,
                device == null ? "-" : device.Address + ":" + device.Percent + ":" + device.Kind + ":" + device.Charge + ":" + device.IsLow);

            if (force || key != renderKey)
            {
                IntPtr handle = TrayIconRenderer.Render(device, style, size, frame);
                Icon icon = Icon.FromHandle(handle);
                notifyIcon.Icon = icon;
                Release();
                current = icon;
                currentHandle = handle;
                renderKey = key;
            }

            notifyIcon.Text = tooltip.Length > MaxTooltipLength ? tooltip.Substring(0, MaxTooltipLength) : tooltip;
            notifyIcon.Visible = true;
        }

        public void Hide()
        {
            notifyIcon.Visible = false;
        }

        public bool Visible
        {
            get { return notifyIcon.Visible; }
        }

        // Windows shows it as a toast from this icon.
        public void Show(Notice notice)
        {
            notifyIcon.ShowBalloonTip(5000, notice.Title, notice.Text, notice.Icon);
        }

        void Release()
        {
            if (current != null)
                current.Dispose();
            if (currentHandle != IntPtr.Zero)
                Native.DestroyIcon(currentHandle);
            current = null;
            currentHandle = IntPtr.Zero;
        }

        public void Dispose()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Release();
        }
    }
}
