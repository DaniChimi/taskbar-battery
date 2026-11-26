using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // The panel that opens above the taskbar: a compact list, one row per connected device with its name,
    // the five-segment meter and the percentage, plus a footer with the last scan time and a refresh button.
    // Closes when it loses focus or on Esc.
    sealed class Flyout : Form
    {
        // Layout in logical pixels (96 DPI); scaled at paint time.
        const float PanelWidth = 340f;
        const float ScreenMargin = 12f;
        const float HeaderHeight = 48f;
        const float RowHeight = DeviceRowPainter.RowHeight;
        const float SidePadding = DeviceRowPainter.SidePadding;
        const float ListBottomPadding = 8f;
        const float FooterHeight = 48f;
        const float ButtonSize = 32f;

        List<BatteryDevice> devices = new List<BatteryDevice>();
        DateTime lastScan = DateTime.Now;
        bool readFailed;
        float scale = 1f;
        Rectangle refreshBounds;
        bool refreshHover;
        Rectangle settingsBounds;
        bool settingsHover;

        public event EventHandler RefreshRequested;
        public event EventHandler SettingsRequested;

        public Flyout()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            Text = "TaskbarBattery";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        // When the flyout last closed; lets a click on the tray icon that caused the close not reopen it.
        public DateTime HiddenAt { get; private set; }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
                cp.ClassStyle |= Native.CS_DROPSHADOW;
                return cp;
            }
        }

        public void ShowNear(Rectangle anchor, List<BatteryDevice> connectedDevices, DateTime scanTime, bool failed)
        {
            SetData(connectedDevices, scanTime, failed);
            if (!IsHandleCreated)
                CreateHandle();

            Screen screen = anchor.IsEmpty ? Screen.PrimaryScreen : Screen.FromRectangle(anchor);
            UpdateScale();
            Rectangle work = screen.WorkingArea;
            var size = new Size(Px(PanelWidth), Px(TotalHeight()));
            int margin = Px(ScreenMargin);
            Bounds = new Rectangle(work.Right - margin - size.Width, work.Bottom - margin - size.Height, size.Width, size.Height);

            ApplyFrame();
            Show();
            Activate();
            Invalidate();
        }

        // Refreshes the contents of an open flyout, growing or shrinking upwards from its bottom edge.
        public void SetData(List<BatteryDevice> connectedDevices, DateTime scanTime, bool failed)
        {
            devices = connectedDevices;
            lastScan = scanTime;
            readFailed = failed;
            if (Visible)
            {
                int bottom = Bottom;
                Height = Px(TotalHeight());
                Top = bottom - Height;
                Invalidate();
            }
        }

        float TotalHeight()
        {
            float rows = devices.Count == 0 ? RowHeight : 0;
            foreach (BatteryDevice device in devices)
                rows += DeviceRowPainter.HeightOf(device);
            return HeaderHeight + rows + ListBottomPadding + FooterHeight;
        }

        void UpdateScale()
        {
            uint dpi = IsHandleCreated ? Native.GetDpiForWindow(Handle) : 96;
            scale = (dpi == 0 ? 96 : dpi) / 96f;
        }

        int Px(float logical)
        {
            return (int)Math.Round(logical * scale);
        }

        void ApplyFrame()
        {
            Palette palette = Palette.Current;
            int corner = Native.DWMWCP_ROUND;
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            int dark = Theme.IsLight ? 0 : 1;
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            Color b = palette.WindowBorder;
            int colorRef = b.R | (b.G << 8) | (b.B << 16);
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Palette p = Palette.Current;
            g.Clear(p.Background);

            using (var painter = new DeviceRowPainter(scale))
            using (var title = new Font(Theme.TextFont, 14f * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var secondary = new Font(Theme.TextFont, 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var smallGlyph = new Font(Theme.GlyphFont, 16f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                DrawText(g, Strings.Get(Strings.Title), title, p.Text,
                    new RectangleF(Px(SidePadding), 0, Width - 2 * Px(SidePadding), Px(HeaderHeight)), StringAlignment.Near);

                // Settings gear, top right, aligned with the refresh button below.
                int button = Px(ButtonSize);
                settingsBounds = new Rectangle(Width - Px(12) - button, (Px(HeaderHeight) - button) / 2, button, button);
                DrawIconButton(g, p, settingsBounds, settingsHover, "", smallGlyph);

                if (devices.Count == 0)
                    painter.DrawEmpty(g, p, RowRect(0), readFailed);
                for (int i = 0; i < devices.Count; i++)
                {
                    if (i > 0)
                        painter.DrawDivider(g, p, RowRect(i));
                    painter.Draw(g, p, RowRect(i), devices[i]);
                }

                DrawFooter(g, p, secondary, smallGlyph);
            }
        }

        Rectangle RowRect(int index)
        {
            float top = HeaderHeight;
            for (int i = 0; i < index && i < devices.Count; i++)
                top += DeviceRowPainter.HeightOf(devices[i]);
            float height = index < devices.Count ? DeviceRowPainter.HeightOf(devices[index]) : RowHeight;
            return new Rectangle(0, Px(top), Width, Px(top + height) - Px(top));
        }

        void DrawFooter(Graphics g, Palette p, Font secondaryFont, Font glyphFont)
        {
            int top = Height - Px(FooterHeight);
            using (var fill = new SolidBrush(p.Footer))
            using (var divider = new Pen(p.Divider))
            {
                g.FillRectangle(fill, 0, top, Width, Height - top);
                g.DrawLine(divider, 0, top, Width, top);
            }

            string updated = DateTime.Now - lastScan < TimeSpan.FromMinutes(1)
                ? Strings.Get(Strings.UpdatedNow)
                : Strings.Format(Strings.UpdatedAt, lastScan);
            DrawText(g, updated, secondaryFont, p.Secondary,
                new RectangleF(Px(SidePadding), top, Width - Px(80), Px(FooterHeight)), StringAlignment.Near);

            int button = Px(ButtonSize);
            refreshBounds = new Rectangle(Width - Px(12) - button, top + (Px(FooterHeight) - button) / 2, button, button);
            DrawIconButton(g, p, refreshBounds, refreshHover, Theme.RefreshGlyph, glyphFont);
        }

        void DrawIconButton(Graphics g, Palette p, Rectangle bounds, bool hover, string glyph, Font glyphFont)
        {
            if (hover)
            {
                using (GraphicsPath path = Theme.RoundedRect(bounds, Px(4)))
                using (var fill = new SolidBrush(p.Hover))
                {
                    g.FillPath(fill, path);
                }
            }
            DrawText(g, glyph, glyphFont, p.Text, bounds, StringAlignment.Center);
        }

        static void DrawText(Graphics g, string text, Font font, Color color, RectangleF bounds, StringAlignment alignment)
        {
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat())
            {
                format.Alignment = alignment;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, font, brush, bounds, format);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hover = refreshBounds.Contains(e.Location);
            if (hover != refreshHover)
            {
                refreshHover = hover;
                Invalidate(refreshBounds);
            }
            hover = settingsBounds.Contains(e.Location);
            if (hover != settingsHover)
            {
                settingsHover = hover;
                Invalidate(settingsBounds);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (refreshHover || settingsHover)
            {
                refreshHover = false;
                settingsHover = false;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left)
                return;
            if (refreshBounds.Contains(e.Location) && RefreshRequested != null)
                RefreshRequested(this, EventArgs.Empty);
            else if (settingsBounds.Contains(e.Location) && SettingsRequested != null)
                SettingsRequested(this, EventArgs.Empty);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
                Hide();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Hide();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible)
            {
                HiddenAt = DateTime.Now;
                refreshHover = false;
                settingsHover = false;
            }
        }
    }
}
