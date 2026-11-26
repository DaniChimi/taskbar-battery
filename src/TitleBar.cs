using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // A 48 px title bar drawn like the one of the Windows 11 Settings app: app icon at 16 px, title at 48 px,
    // and minimize/close caption buttons of 46 x 32 px. The owner window removes its system caption (see
    // CustomFrame) and lets the empty part of this bar act as the caption, so dragging moves the window.
    sealed class TitleBar : Control
    {
        public const float BarHeight = 48f;
        const float IconLeft = 16f;
        const float IconSize = 16f;
        const float TitleLeft = 48f;

        readonly float scale;
        readonly Icon icon;
        readonly CaptionButton minimize;
        readonly CaptionButton close;
        bool active = true;

        public TitleBar(float scale, Icon icon, string title)
        {
            this.scale = scale;
            this.icon = icon;
            Text = title;
            Font = new Font(Theme.TextFont, 9f);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = Px(BarHeight);
            BackColor = Palette.Current.Background;

            close = new CaptionButton(scale, "", true) { Dock = DockStyle.Right };
            minimize = new CaptionButton(scale, "", false) { Dock = DockStyle.Right };
            Controls.Add(minimize);
            Controls.Add(close);
        }

        public event EventHandler MinimizeClicked
        {
            add { minimize.Click += value; }
            remove { minimize.Click -= value; }
        }

        public event EventHandler CloseClicked
        {
            add { close.Click += value; }
            remove { close.Click -= value; }
        }

        // Windows greys the title and the caption glyphs of an inactive window.
        public bool Active
        {
            get { return active; }
            set
            {
                active = value;
                minimize.Active = value;
                close.Active = value;
                Invalidate();
            }
        }

        int Px(float logical)
        {
            return (int)Math.Round(logical * scale);
        }

        // The bar itself is transparent to hit testing, so the window sees the caption area and lets Windows move it.
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Palette p = Palette.Current;
            g.Clear(p.Background);

            int iconSize = Px(IconSize);
            if (icon != null)
                g.DrawIcon(icon, new Rectangle(Px(IconLeft), (Height - iconSize) / 2, iconSize, iconSize));

            int right = minimize.Left - Px(8);
            var titleBounds = new Rectangle(Px(TitleLeft), 0, Math.Max(0, right - Px(TitleLeft)), Height);
            TextRenderer.DrawText(g, Text, Font, titleBounds, active ? p.Text : p.Secondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Font.Dispose();
            base.Dispose(disposing);
        }

        // One caption button, 46 x 32 px at the top of the bar. Close turns red on hover, as in Windows.
        sealed class CaptionButton : Control
        {
            static readonly Color CloseHover = Color.FromArgb(196, 43, 28);
            static readonly Color ClosePressed = Color.FromArgb(200, 64, 52);

            readonly float scale;
            readonly string glyph;
            readonly bool isClose;
            bool hover;
            bool pressed;
            bool active = true;

            public CaptionButton(float scale, string glyph, bool isClose)
            {
                this.scale = scale;
                this.glyph = glyph;
                this.isClose = isClose;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                SetStyle(ControlStyles.Selectable, false);
                Width = (int)Math.Round(46 * scale);
            }

            public bool Active
            {
                set { active = value; Invalidate(); }
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; Invalidate(); }
            protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = true; Invalidate(); }
            protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Palette p = Palette.Current;
                g.Clear(p.Background);

                // The visible button is the top 32 px; the rest of the bar's height stays plain background.
                var button = new Rectangle(0, 0, Width, (int)Math.Round(32 * scale));
                Color glyphColor = active ? p.Text : p.Secondary;
                if (hover)
                {
                    Color fill = isClose ? (pressed ? ClosePressed : CloseHover) : (pressed ? p.Track : p.Hover);
                    using (var brush = new SolidBrush(fill))
                        g.FillRectangle(brush, button);
                    if (isClose)
                        glyphColor = Color.White;
                }

                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (var font = new Font(Theme.GlyphFont, 10f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(glyphColor))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(glyph, font, brush, button, format);
                }
            }
        }
    }

    // Removes the system caption of a window while keeping its frame behaviour (shadow, rounded corners, snap and
    // animations): the client area covers the whole window, and the empty part of the TitleBar reports itself as
    // the caption. Used by windows that draw their own TitleBar.
    static class CustomFrame
    {
        const int WM_NCCALCSIZE = 0x0083;
        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1;
        const int HTCAPTION = 2;

        // Call from the window's WndProc; returns true when the message was handled.
        public static bool Handle(Form window, TitleBar bar, ref Message m)
        {
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // Client area = whole window: no system caption or borders.
                m.Result = IntPtr.Zero;
                return true;
            }
            return false;
        }

        // Call after the default WM_NCHITTEST handling: turns the title bar's empty area into the caption.
        public static void HitTest(Form window, TitleBar bar, ref Message m)
        {
            if (m.Msg != WM_NCHITTEST || (int)m.Result != HTCLIENT || bar == null)
                return;
            long lParam = m.LParam.ToInt64();
            var screen = new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
            Point client = window.PointToClient(screen);
            if (client.Y >= 0 && client.Y < bar.Height)
                m.Result = (IntPtr)HTCAPTION;
        }

        // Makes Windows recompute the frame after the handle exists, so WM_NCCALCSIZE takes effect.
        public static void Apply(Form window)
        {
            Native.SetWindowPos(window.Handle, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
        }
    }
}
