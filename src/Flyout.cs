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
        const float RowHeight = 80f;
        const float SidePadding = 20f;
        const float GlyphColumn = 36f;
        const float SegmentWidth = 22f;
        const float SegmentHeight = 6f;
        const float SegmentGap = 4f;
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
            return HeaderHeight + Math.Max(1, devices.Count) * RowHeight + ListBottomPadding + FooterHeight;
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

            using (var title = new Font(Theme.TextFont, 14f * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var name = new Font(Theme.TextFont, 14f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var percent = new Font(Theme.TextFont, 13f * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var secondary = new Font(Theme.TextFont, 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var glyph = new Font(Theme.GlyphFont, 20f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var smallGlyph = new Font(Theme.GlyphFont, 16f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                DrawText(g, Strings.Get(Strings.Title), title, p.Text,
                    new RectangleF(Px(SidePadding), 0, Width - 2 * Px(SidePadding), Px(HeaderHeight)), StringAlignment.Near);

                // Settings gear, top right, aligned with the refresh button below.
                int button = Px(ButtonSize);
                settingsBounds = new Rectangle(Width - Px(12) - button, (Px(HeaderHeight) - button) / 2, button, button);
                DrawIconButton(g, p, settingsBounds, settingsHover, "", smallGlyph);

                if (devices.Count == 0)
                {
                    DrawEmptyRow(g, p, RowRect(0), name, secondary);
                }
                for (int i = 0; i < devices.Count; i++)
                {
                    if (i > 0)
                        DrawRowDivider(g, p, RowRect(i));
                    DrawRow(g, p, RowRect(i), devices[i], name, percent, secondary, glyph);
                }

                DrawFooter(g, p, secondary, smallGlyph);
            }
        }

        Rectangle RowRect(int index)
        {
            return new Rectangle(0, Px(HeaderHeight + index * RowHeight), Width, Px(RowHeight));
        }

        void DrawRowDivider(Graphics g, Palette p, Rectangle row)
        {
            using (var pen = new Pen(p.Divider))
            {
                g.DrawLine(pen, Px(SidePadding + GlyphColumn), row.Top, Width - Px(SidePadding), row.Top);
            }
        }

        void DrawRow(Graphics g, Palette p, Rectangle row, BatteryDevice device, Font nameFont, Font percentFont,
            Font secondaryFont, Font glyphFont)
        {
            int left = Px(SidePadding);
            int right = Width - Px(SidePadding);
            int textLeft = left + Px(GlyphColumn);

            DrawText(g, Theme.Glyph(device.Kind), glyphFont, p.Text,
                new RectangleF(left, row.Top, Px(GlyphColumn) - Px(8), row.Height), StringAlignment.Center);

            // First line: name on the left, charging state on the right (only when the device reports it).
            var firstLine = new RectangleF(textLeft, row.Top + Px(10), right - textLeft, Px(20));
            float chargeWidth = DrawChargeState(g, p, device, secondaryFont, firstLine);
            DrawText(g, device.DisplayName, nameFont, p.Text,
                new RectangleF(textLeft, firstLine.Y, firstLine.Width - chargeWidth - Px(8), firstLine.Height), StringAlignment.Near);

            // Second line: the five segments and the percentage; time of the reading on the right.
            Color on = device.IsLow ? p.Low : p.Bar;
            int lit = Segments.Lit(device.Percent);
            float segmentTop = row.Top + Px(38);
            DrawText(g, DescribeReport(device), secondaryFont, p.Secondary,
                new RectangleF(textLeft, segmentTop - Px(7), right - textLeft, Px(20)), StringAlignment.Far);
            float x = textLeft;
            for (int i = 0; i < Segments.Count; i++)
            {
                var segment = new RectangleF(x, segmentTop, Px(SegmentWidth), Px(SegmentHeight));
                using (GraphicsPath path = Theme.RoundedRect(segment, Px(SegmentHeight) / 2f))
                using (var fill = new SolidBrush(i < lit ? on : p.Track))
                {
                    g.FillPath(fill, path);
                }
                x += Px(SegmentWidth) + Px(SegmentGap);
            }
            DrawText(g, Strings.Format(Strings.Percent, device.Percent), percentFont, device.IsLow ? p.Low : p.Text,
                new RectangleF(x + Px(6), segmentTop - Px(7), Px(60), Px(20)), StringAlignment.Near);

            // Third line: maker and how long the device has been connected.
            var details = new List<string>();
            if (device.Manufacturer != null)
                details.Add(device.Manufacturer);
            if (device.ConnectedSince.HasValue)
                details.Add(Strings.Format(Strings.ConnectedFor, DescribeDuration(DateTime.Now - device.ConnectedSince.Value)));
            if (details.Count > 0)
                DrawText(g, string.Join(" · ", details.ToArray()), secondaryFont, p.Secondary,
                    new RectangleF(textLeft, row.Top + Px(52), right - textLeft, Px(18)), StringAlignment.Near);
        }

        // Draws "bolt + Charging" or "Fully charged" right-aligned in the line; returns the width used.
        float DrawChargeState(Graphics g, Palette p, BatteryDevice device, Font font, RectangleF line)
        {
            string label;
            if (device.Charge == ChargeState.Charging)
                label = Strings.Get(Strings.Charging);
            else if (device.Charge == ChargeState.Full)
                label = Strings.Get(Strings.FullyCharged);
            else
                return 0;

            float labelWidth = g.MeasureString(label, font).Width;
            DrawText(g, label, font, p.Bar, line, StringAlignment.Far);
            if (device.Charge != ChargeState.Charging)
                return labelWidth;

            using (var bolt = new Font(Theme.GlyphFont, 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                float boltWidth = Px(14);
                DrawText(g, "", bolt, p.Bar,
                    new RectangleF(line.Right - labelWidth - boltWidth, line.Y, boltWidth, line.Height), StringAlignment.Center);
                return labelWidth + boltWidth;
            }
        }

        static string DescribeDuration(TimeSpan span)
        {
            if (span.TotalMinutes < 1)
                return Strings.Get(Strings.UnderAMinute);
            if (span.TotalHours < 1)
                return Strings.Format(Strings.Minutes, (int)span.TotalMinutes);
            if (span.TotalDays < 1)
                return Strings.Format(Strings.Hours, (int)span.TotalHours);
            return Strings.Format(Strings.Days, (int)span.TotalDays);
        }

        void DrawEmptyRow(Graphics g, Palette p, Rectangle row, Font nameFont, Font secondaryFont)
        {
            int left = Px(SidePadding);
            string title = readFailed ? Strings.Get(Strings.ReadError) : Strings.Get(Strings.NoDevices);
            DrawText(g, title, nameFont, p.Text, new RectangleF(left, row.Top + Px(10), Width - 2 * left, Px(20)), StringAlignment.Near);
            DrawText(g, Strings.Get(Strings.NoDevicesHint), secondaryFont, p.Secondary,
                new RectangleF(left, row.Top + Px(32), Width - 2 * left, Px(18)), StringAlignment.Near);
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

        static string DescribeReport(BatteryDevice device)
        {
            if (!device.LastUpdated.HasValue)
                return string.Empty;
            DateTime updated = device.LastUpdated.Value;
            return updated.Date == DateTime.Today
                ? Strings.Format(Strings.ReportedToday, updated)
                : Strings.Format(Strings.ReportedOn, updated);
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
