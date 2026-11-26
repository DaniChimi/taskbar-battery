using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // Draws one device row exactly as the flyout shows it: glyph, name and charging state, the five-segment meter with
    // the percentage and the reading time, then maker and connection time. Shared by the flyout and the settings window.
    sealed class DeviceRowPainter : IDisposable
    {
        // Layout in logical pixels (96 DPI).
        public const float RowHeight = 80f;
        public const float SidePadding = 20f;
        const float GlyphColumn = 36f;
        const float SegmentWidth = 22f;
        const float SegmentHeight = 6f;
        const float SegmentGap = 4f;

        readonly float scale;
        readonly Font nameFont;
        readonly Font percentFont;
        readonly Font secondaryFont;
        readonly Font glyphFont;
        readonly Font boltFont;

        public DeviceRowPainter(float scale)
        {
            this.scale = scale;
            nameFont = new Font(Theme.TextFont, 14f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            percentFont = new Font(Theme.TextFont, 13f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            secondaryFont = new Font(Theme.TextFont, 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            glyphFont = new Font(Theme.GlyphFont, 20f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            boltFont = new Font(Theme.GlyphFont, 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public int Px(float logical)
        {
            return (int)Math.Round(logical * scale);
        }

        // A thin line between rows, starting under the text column.
        public void DrawDivider(Graphics g, Palette p, Rectangle row)
        {
            using (var pen = new Pen(p.Divider))
            {
                g.DrawLine(pen, row.Left + Px(SidePadding + GlyphColumn), row.Top, row.Right - Px(SidePadding), row.Top);
            }
        }

        // Edit actions drawn on a row in the settings window (never in the flyout): a pencil after the name to rename,
        // an eye on the right to show or hide. The painter fills in where it drew them, for hit testing.
        public sealed class RowActions
        {
            public bool Hidden;
            public bool PencilHover;
            public bool EyeHover;
            public Rectangle Pencil;
            public Rectangle Eye;
        }

        const string PencilGlyph = "";
        const string EyeGlyph = "";
        const string EyeOffGlyph = "";
        const float ActionSize = 28f;

        public void Draw(Graphics g, Palette p, Rectangle row, BatteryDevice device)
        {
            Draw(g, p, row, device, null);
        }

        public void Draw(Graphics g, Palette p, Rectangle row, BatteryDevice device, RowActions actions)
        {
            int left = row.Left + Px(SidePadding);
            int right = row.Right - Px(SidePadding);
            bool hidden = actions != null && actions.Hidden;

            // Edit mode: the eye takes a column on the right, vertically centred.
            if (actions != null)
            {
                int size = Px(ActionSize);
                actions.Eye = new Rectangle(right - size, row.Top + (row.Height - size) / 2, size, size);
                DrawAction(g, p, actions.Eye, actions.EyeHover, hidden ? EyeOffGlyph : EyeGlyph);
                right = actions.Eye.Left - Px(8);
            }

            int textLeft = left + Px(GlyphColumn);
            Color text = hidden ? p.Secondary : p.Text;

            DrawText(g, Theme.Glyph(device.Kind), glyphFont, text,
                new RectangleF(left, row.Top, Px(GlyphColumn) - Px(8), row.Height), StringAlignment.Center);

            // First line: name on the left (then the pencil in edit mode), charging state on the right.
            var firstLine = new RectangleF(textLeft, row.Top + Px(10), right - textLeft, Px(20));
            float chargeWidth = hidden ? 0 : DrawChargeState(g, p, device, firstLine);
            float nameRoom = firstLine.Width - chargeWidth - Px(8) - (actions != null ? Px(ActionSize) : 0);
            float nameWidth = Math.Min(nameRoom, g.MeasureString(device.DisplayName, nameFont).Width);
            DrawText(g, device.DisplayName, nameFont, text,
                new RectangleF(textLeft, firstLine.Y, nameRoom, firstLine.Height), StringAlignment.Near);
            if (actions != null)
            {
                int size = Px(ActionSize - 4);
                actions.Pencil = new Rectangle((int)(textLeft + nameWidth) + Px(2),
                    (int)(firstLine.Y + (firstLine.Height - size) / 2), size, size);
                DrawAction(g, p, actions.Pencil, actions.PencilHover, PencilGlyph);
            }

            // Second line: the five segments and the percentage; time of the reading on the right.
            Color on = hidden ? p.Secondary : device.IsLow ? p.Low : p.Bar;
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
            DrawText(g, Strings.Format(Strings.Percent, device.Percent), percentFont, hidden ? text : device.IsLow ? p.Low : p.Text,
                new RectangleF(x + Px(6), segmentTop - Px(7), Px(60), Px(20)), StringAlignment.Near);

            // Third line: maker and how long the device has been connected (and "hidden" when it is).
            var details = new List<string>();
            if (hidden)
                details.Add(Strings.Get(Strings.HiddenTag));
            if (device.Manufacturer != null)
                details.Add(device.Manufacturer);
            if (device.ConnectedSince.HasValue)
                details.Add(Strings.Format(Strings.ConnectedFor, DescribeDuration(DateTime.Now - device.ConnectedSince.Value)));
            if (details.Count > 0)
                DrawText(g, string.Join(" · ", details.ToArray()), secondaryFont, p.Secondary,
                    new RectangleF(textLeft, row.Top + Px(52), right - textLeft, Px(18)), StringAlignment.Near);
        }

        // A small glyph button: secondary colour at rest, a rounded highlight and full colour on hover.
        void DrawAction(Graphics g, Palette p, Rectangle bounds, bool hover, string glyph)
        {
            if (hover)
            {
                using (GraphicsPath path = Theme.RoundedRect(bounds, Px(4)))
                using (var fill = new SolidBrush(p.Hover))
                    g.FillPath(fill, path);
            }
            using (var font = new Font(Theme.GlyphFont, 14f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
                DrawText(g, glyph, font, hover ? p.Text : p.Secondary, bounds, StringAlignment.Center);
        }

        // The row shown when no device is connected (or the read failed).
        public void DrawEmpty(Graphics g, Palette p, Rectangle row, bool readFailed)
        {
            int left = row.Left + Px(SidePadding);
            int width = row.Width - 2 * Px(SidePadding);
            string title = readFailed ? Strings.Get(Strings.ReadError) : Strings.Get(Strings.NoDevices);
            DrawText(g, title, nameFont, p.Text, new RectangleF(left, row.Top + Px(10), width, Px(20)), StringAlignment.Near);
            DrawText(g, Strings.Get(Strings.NoDevicesHint), secondaryFont, p.Secondary,
                new RectangleF(left, row.Top + Px(32), width, Px(18)), StringAlignment.Near);
        }

        // Draws "bolt + Charging" or "Fully charged" right-aligned in the line; returns the width used.
        float DrawChargeState(Graphics g, Palette p, BatteryDevice device, RectangleF line)
        {
            string label;
            if (device.Charge == ChargeState.Charging)
                label = Strings.Get(Strings.Charging);
            else if (device.Charge == ChargeState.Full)
                label = Strings.Get(Strings.FullyCharged);
            else
                return 0;

            float labelWidth = g.MeasureString(label, secondaryFont).Width;
            DrawText(g, label, secondaryFont, p.Bar, line, StringAlignment.Far);
            if (device.Charge != ChargeState.Charging)
                return labelWidth;

            float boltWidth = Px(14);
            DrawText(g, "", boltFont, p.Bar,
                new RectangleF(line.Right - labelWidth - boltWidth, line.Y, boltWidth, line.Height), StringAlignment.Center);
            return labelWidth + boltWidth;
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

        static string DescribeReport(BatteryDevice device)
        {
            if (!device.LastUpdated.HasValue)
                return string.Empty;
            DateTime updated = device.LastUpdated.Value;
            return updated.Date == DateTime.Today
                ? Strings.Format(Strings.ReportedToday, updated)
                : Strings.Format(Strings.ReportedOn, updated);
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, RectangleF bounds, StringAlignment alignment)
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

        public void Dispose()
        {
            nameFont.Dispose();
            percentFont.Dispose();
            secondaryFont.Dispose();
            glyphFont.Dispose();
            boltFont.Dispose();
        }
    }

    // The device list of the settings window: the same rows as the flyout, on a card, plus the edit actions
    // (pencil to rename, eye to show or hide). Hidden devices stay listed, dimmed, so they can be shown again.
    sealed class DeviceListView : Control
    {
        readonly DeviceRowPainter painter;
        readonly ToolTip tooltip = new ToolTip();
        List<BatteryDevice> devices = new List<BatteryDevice>();
        HashSet<string> hiddenAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DeviceRowPainter.RowActions[] actions = new DeviceRowPainter.RowActions[0];
        bool readFailed;
        string tooltipText;

        public event Action<BatteryDevice> RenameRequested;
        public event Action<BatteryDevice, bool> VisibilityChanged;

        public DeviceListView(float scale)
        {
            painter = new DeviceRowPainter(scale);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            UpdateHeight();
        }

        // All connected devices, hidden ones included; 'hidden' holds the addresses to show dimmed.
        public void SetDevices(List<BatteryDevice> connectedDevices, HashSet<string> hidden, bool failed)
        {
            devices = connectedDevices;
            hiddenAddresses = new HashSet<string>(hidden, StringComparer.OrdinalIgnoreCase);
            readFailed = failed;

            var next = new DeviceRowPainter.RowActions[devices.Count];
            for (int i = 0; i < next.Length; i++)
                next[i] = new DeviceRowPainter.RowActions { Hidden = hiddenAddresses.Contains(devices[i].Address) };
            actions = next;

            UpdateHeight();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool changed = false;
            bool onAction = false;
            string tip = null;
            foreach (DeviceRowPainter.RowActions action in actions)
            {
                bool pencil = action.Pencil.Contains(e.Location);
                bool eye = action.Eye.Contains(e.Location);
                changed |= pencil != action.PencilHover || eye != action.EyeHover;
                action.PencilHover = pencil;
                action.EyeHover = eye;
                if (pencil)
                    tip = Strings.Get(Strings.RenameTip);
                else if (eye)
                    tip = Strings.Get(action.Hidden ? Strings.ShowTip : Strings.HideTip);
                onAction |= pencil || eye;
            }
            Cursor = onAction ? Cursors.Hand : Cursors.Default;
            if (tip != tooltipText)
            {
                tooltipText = tip;
                tooltip.SetToolTip(this, tip);
            }
            if (changed)
                Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            foreach (DeviceRowPainter.RowActions action in actions)
            {
                action.PencilHover = false;
                action.EyeHover = false;
            }
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left)
                return;
            for (int i = 0; i < actions.Length && i < devices.Count; i++)
            {
                if (actions[i].Pencil.Contains(e.Location) && RenameRequested != null)
                {
                    RenameRequested(devices[i]);
                    return;
                }
                if (actions[i].Eye.Contains(e.Location) && VisibilityChanged != null)
                {
                    actions[i].Hidden = !actions[i].Hidden;
                    Invalidate();
                    VisibilityChanged(devices[i], !actions[i].Hidden);
                    return;
                }
            }
        }

        void UpdateHeight()
        {
            Height = painter.Px(DeviceRowPainter.RowHeight * Math.Max(1, devices.Count));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Palette p = Palette.Current;
            g.Clear(p.Background);

            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Theme.RoundedRect(card, painter.Px(6)))
            using (var fill = new SolidBrush(p.Card))
            using (var border = new Pen(p.CardBorder == p.Card ? p.Divider : p.CardBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int rowHeight = painter.Px(DeviceRowPainter.RowHeight);
            if (devices.Count == 0)
            {
                painter.DrawEmpty(g, p, new Rectangle(0, 0, Width, rowHeight), readFailed);
                return;
            }
            for (int i = 0; i < devices.Count; i++)
            {
                var row = new Rectangle(0, i * rowHeight, Width, rowHeight);
                if (i > 0)
                    painter.DrawDivider(g, p, row);
                painter.Draw(g, p, row, devices[i], i < actions.Length ? actions[i] : null);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                painter.Dispose();
                tooltip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
