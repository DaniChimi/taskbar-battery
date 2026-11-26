using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // Base for the hand-drawn settings controls: double buffered, scaled for the window's DPI, themed.
    abstract class FluentControl : Control
    {
        protected readonly float UiScale;
        protected bool Hover;

        protected FluentControl(float scale)
        {
            UiScale = scale;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected int Px(float logical)
        {
            return (int)Math.Round(logical * UiScale);
        }

        protected static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        // Text colour that reads on the accent fill.
        protected static Color OnAccent
        {
            get { return Theme.IsLight ? Color.White : Color.Black; }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Hover = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    // On/off switch: a pill with a knob, filled with the accent colour when on.
    sealed class ToggleSwitch : FluentControl
    {
        bool isChecked;

        public event EventHandler CheckedChanged;

        public ToggleSwitch(float scale, bool initial) : base(scale)
        {
            isChecked = initial;
            Size = new Size(Px(40), Px(20));
        }

        public bool Checked
        {
            get { return isChecked; }
        }

        void Toggle()
        {
            isChecked = !isChecked;
            Invalidate();
            if (CheckedChanged != null)
                CheckedChanged(this, EventArgs.Empty);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Toggle();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space)
                Toggle();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Prepare(g);
            Palette p = Palette.Current;
            g.Clear(BackColor);

            var track = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float knob = Height - Px(Hover ? 8 : 10);
            float knobY = (Height - knob) / 2f;
            using (GraphicsPath path = Theme.RoundedRect(track, track.Height / 2f))
            {
                if (isChecked)
                {
                    using (var fill = new SolidBrush(Enabled ? p.Bar : p.Track))
                        g.FillPath(fill, path);
                    using (var dot = new SolidBrush(OnAccent))
                        g.FillEllipse(dot, Width - Px(5) - knob, knobY, knob, knob);
                }
                else
                {
                    using (var border = new Pen(p.Secondary, Math.Max(1f, UiScale)))
                        g.DrawPath(border, path);
                    using (var dot = new SolidBrush(p.Secondary))
                        g.FillEllipse(dot, Px(5), knobY, knob, knob);
                }
            }
        }
    }

    // A row of mutually exclusive options; the selected one is filled with the accent colour.
    sealed class Segmented : FluentControl
    {
        readonly string[] options;
        readonly int[] widths;
        int selected;
        int hoverIndex = -1;

        public event EventHandler SelectedIndexChanged;

        public Segmented(float scale, Font font, string[] options, int initial) : base(scale)
        {
            this.options = options;
            selected = initial;
            Font = font;
            widths = new int[options.Length];
            int total = 0;
            for (int i = 0; i < options.Length; i++)
            {
                widths[i] = TextRenderer.MeasureText(options[i], font).Width + Px(20);
                total += widths[i];
            }
            Size = new Size(total + Px(4), Px(30));
        }

        public int SelectedIndex
        {
            get { return selected; }
        }

        Rectangle SegmentBounds(int index)
        {
            int x = Px(2);
            for (int i = 0; i < index; i++)
                x += widths[i];
            return new Rectangle(x, Px(2), widths[index], Height - Px(4));
        }

        int IndexAt(Point point)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (SegmentBounds(i).Contains(point))
                    return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = IndexAt(e.Location);
            if (index != hoverIndex)
            {
                hoverIndex = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverIndex = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int index = IndexAt(e.Location);
            if (index >= 0)
                Select(index);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left && selected > 0)
                Select(selected - 1);
            else if (e.KeyCode == Keys.Right && selected < options.Length - 1)
                Select(selected + 1);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        void Select(int index)
        {
            if (index == selected)
                return;
            selected = index;
            Invalidate();
            if (SelectedIndexChanged != null)
                SelectedIndexChanged(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Prepare(g);
            Palette p = Palette.Current;
            g.Clear(BackColor);

            var outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Theme.RoundedRect(outer, Px(5)))
            using (var fill = new SolidBrush(p.Background))
            using (var border = new Pen(p.CardBorder == p.Card ? p.Divider : p.CardBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            for (int i = 0; i < options.Length; i++)
            {
                Rectangle bounds = SegmentBounds(i);
                Color text = p.Text;
                if (i == selected || i == hoverIndex)
                {
                    using (GraphicsPath path = Theme.RoundedRect(bounds, Px(4)))
                    using (var fill = new SolidBrush(i == selected ? p.Bar : p.Hover))
                        g.FillPath(fill, path);
                    if (i == selected)
                        text = OnAccent;
                }
                TextRenderer.DrawText(g, options[i], Font, bounds, text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }
    }

    // A flat rounded button, the shape Windows 11 uses for buttons. Primary buttons are filled with the accent colour.
    class FlatButton : FluentControl
    {
        public FlatButton(float scale, Font font, string text) : base(scale)
        {
            Font = font;
            Text = text;
            Size = new Size(Math.Max(Px(80), TextRenderer.MeasureText(text, font).Width + Px(28)), Px(30));
        }

        public bool Primary { get; set; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
                OnClick(EventArgs.Empty);
        }

        protected virtual Rectangle TextBounds
        {
            get { return ClientRectangle; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Prepare(g);
            Palette p = Palette.Current;
            g.Clear(BackColor);

            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color fillColor;
            Color borderColor;
            Color textColor;
            if (Primary && Enabled)
            {
                fillColor = Hover ? Theme.Mix(p.Bar, p.Background, 0.15f) : p.Bar;
                borderColor = fillColor;
                textColor = OnAccent;
            }
            else
            {
                fillColor = Hover && Enabled ? p.Hover : p.Tile;
                borderColor = p.CardBorder == p.Card ? p.Divider : p.CardBorder;
                textColor = Enabled ? p.Text : p.Secondary;
            }
            using (GraphicsPath path = Theme.RoundedRect(bounds, Px(4)))
            using (var fill = new SolidBrush(fillColor))
            using (var border = new Pen(borderColor))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            if (Focused && ShowFocusCues)
            {
                var focus = new RectangleF(2.5f, 2.5f, Width - 5.5f, Height - 5.5f);
                using (GraphicsPath path = Theme.RoundedRect(focus, Px(3)))
                using (var pen = new Pen(p.Text))
                    g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, Text, Font, TextBounds, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            PaintExtra(g, p);
        }

        protected virtual void PaintExtra(Graphics g, Palette p)
        {
        }
    }

    // A button showing the current choice; clicking it opens a menu of the options.
    sealed class DropDownButton : FlatButton
    {
        readonly List<KeyValuePair<string, string>> options;
        int selected;

        public event EventHandler SelectionChanged;

        // options: display text and value; the value may be null.
        public DropDownButton(float scale, Font font, List<KeyValuePair<string, string>> options, int initial)
            : base(scale, font, options[Math.Max(0, initial)].Key)
        {
            this.options = options;
            selected = Math.Max(0, initial);
            int widest = 0;
            foreach (KeyValuePair<string, string> option in options)
                widest = Math.Max(widest, TextRenderer.MeasureText(option.Key, font).Width);
            Size = new Size(widest + Px(52), Px(30));
        }

        public string SelectedValue
        {
            get { return options[selected].Value; }
        }

        protected override Rectangle TextBounds
        {
            get { return new Rectangle(Px(12), 0, Width - Px(40), Height); }
        }

        protected override void PaintExtra(Graphics g, Palette p)
        {
            using (var glyph = new Font(Theme.GlyphFont, 10f * UiScale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Enabled ? p.Text : p.Secondary))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("", glyph, brush, new RectangleF(Width - Px(30), 0, Px(20), Height), format);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var menu = new FluentMenu();
            MenuStyle.Apply(menu, true);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem(options[i].Key) { Checked = i == selected };
                item.Click += delegate
                {
                    selected = index;
                    Text = options[index].Key;
                    Invalidate();
                    if (SelectionChanged != null)
                        SelectionChanged(this, EventArgs.Empty);
                };
                menu.Items.Add(item);
            }
            MenuStyle.SpaceItems(menu.Items);
            menu.Closed += delegate { BeginInvoke(new MethodInvoker(menu.Dispose)); };
            menu.Show(this, new Point(0, Height + Px(2)));
        }
    }

    // Windows 11 scroll bar: a thin line at rest that widens on hover, with a faint track. Drag the thumb, or click
    // the track to move by a page. The owner sets the range and moves its content on ValueChanged.
    sealed class FluentScrollBar : FluentControl
    {
        int contentHeight;
        int viewportHeight;
        int value;
        bool dragging;
        int dragStartY;
        int dragStartValue;

        public event EventHandler ValueChanged;

        public FluentScrollBar(float scale) : base(scale)
        {
            Width = Px(14);
            TabStop = false;
            Cursor = Cursors.Default;
        }

        int MaxValue
        {
            get { return Math.Max(0, contentHeight - viewportHeight); }
        }

        public int Value
        {
            get { return value; }
            set
            {
                int clamped = Math.Max(0, Math.Min(MaxValue, value));
                if (clamped == this.value)
                    return;
                this.value = clamped;
                Invalidate();
                if (ValueChanged != null)
                    ValueChanged(this, EventArgs.Empty);
            }
        }

        public void SetRange(int content, int viewport)
        {
            contentHeight = content;
            viewportHeight = viewport;
            Value = value;
            Invalidate();
        }

        bool Needed
        {
            get { return contentHeight > viewportHeight && viewportHeight > 0; }
        }

        Rectangle Track
        {
            get { return new Rectangle(0, Px(4), Width, Height - Px(8)); }
        }

        Rectangle Thumb
        {
            get
            {
                Rectangle track = Track;
                int height = Math.Max(Px(28), (int)((long)track.Height * viewportHeight / Math.Max(1, contentHeight)));
                int y = track.Top + (MaxValue == 0 ? 0 : (int)((long)(track.Height - height) * value / MaxValue));
                bool wide = Hover || dragging;
                int width = Px(wide ? 6 : 2);
                return new Rectangle(Width - Px(4) - width, y, width, height);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Needed || e.Button != MouseButtons.Left)
                return;
            Rectangle thumb = Thumb;
            if (e.Y >= thumb.Top && e.Y <= thumb.Bottom)
            {
                dragging = true;
                dragStartY = e.Y;
                dragStartValue = value;
                Capture = true;
            }
            else
            {
                Value += e.Y < thumb.Top ? -viewportHeight : viewportHeight;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging)
                return;
            Rectangle track = Track;
            int travel = track.Height - Thumb.Height;
            if (travel > 0)
                Value = dragStartValue + (int)((long)(e.Y - dragStartY) * MaxValue / travel);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging)
            {
                dragging = false;
                Capture = false;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Prepare(g);
            Palette p = Palette.Current;
            g.Clear(BackColor);
            if (!Needed)
                return;

            Rectangle thumb = Thumb;
            if (Hover || dragging)
            {
                var track = new Rectangle(thumb.X, Track.Top, thumb.Width, Track.Height);
                using (GraphicsPath path = Theme.RoundedRect(track, thumb.Width / 2f))
                using (var fill = new SolidBrush(Color.FromArgb(40, p.Secondary)))
                    g.FillPath(fill, path);
            }
            using (GraphicsPath path = Theme.RoundedRect(thumb, thumb.Width / 2f))
            using (var fill = new SolidBrush(Color.FromArgb(Hover || dragging ? 220 : 150, p.Secondary)))
                g.FillPath(fill, path);
        }
    }

    // Windows 11 text box: rounded border, theme background and an accent line along the bottom while focused.
    // The editing itself is a borderless TextBox inside.
    sealed class FluentTextBox : FluentControl
    {
        readonly TextBox box;

        public FluentTextBox(float scale, Font font, string text, int width) : base(scale)
        {
            Cursor = Cursors.IBeam;
            TabStop = false;
            Palette p = Palette.Current;
            box = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = font,
                Text = text,
                BackColor = p.Tile,
                ForeColor = p.Text,
            };
            box.GotFocus += delegate { Invalidate(); };
            box.LostFocus += delegate { Invalidate(); };
            Controls.Add(box);
            Size = new Size(width, Px(34));
        }

        public TextBox Box
        {
            get { return box; }
        }

        public override string Text
        {
            get { return box == null ? base.Text : box.Text; }
            set { if (box == null) base.Text = value; else box.Text = value; }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (box == null)
                return;
            box.Width = Width - Px(24);
            box.Location = new Point(Px(12), (Height - box.Height) / 2);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            box.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Prepare(g);
            Palette p = Palette.Current;
            g.Clear(BackColor);

            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Theme.RoundedRect(bounds, Px(4)))
            using (var fill = new SolidBrush(p.Tile))
            using (var border = new Pen(p.CardBorder == p.Card ? p.Divider : p.CardBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            // Focused: a 2 px accent line at the bottom, inside the rounded corners.
            if (box.Focused)
            {
                using (var accent = new SolidBrush(p.Bar))
                    g.FillRectangle(accent, Px(3), Height - Px(2) - 1, Width - Px(6), Px(2));
            }
        }
    }

    // One setting: a card with a title, an optional description and its controls on the right.
    sealed class SettingRow : Panel
    {
        readonly float scale;
        readonly FlowLayoutPanel controls;
        readonly Font titleFont;
        readonly Font descriptionFont;
        string title;
        string description;

        public SettingRow(float scale, Font titleFont, Font descriptionFont, string title, string description, params Control[] items)
        {
            this.scale = scale;
            this.titleFont = titleFont;
            this.descriptionFont = descriptionFont;
            this.title = title;
            this.description = description;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Palette p = Palette.Current;
            BackColor = p.Background;
            Height = Px(description == null ? 52 : 66);
            Margin = new Padding(0, 0, 0, Px(4));

            controls = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = p.Card,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            foreach (Control item in items)
            {
                item.BackColor = p.Card;
                item.Margin = new Padding(Px(8), 0, 0, 0);
                controls.Controls.Add(item);
            }
            Controls.Add(controls);
        }

        int Px(float logical)
        {
            return (int)Math.Round(logical * scale);
        }

        public string Title
        {
            get { return title; }
            set { title = value; Invalidate(); }
        }

        public string Description
        {
            get { return description; }
            set { description = value; Invalidate(); }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (controls == null)
                return;
            Size size = controls.PreferredSize;
            controls.Size = size;
            controls.Location = new Point(Width - Px(16) - size.Width, (Height - size.Height) / 2);
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Palette p = Palette.Current;
            g.Clear(p.Background);

            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Theme.RoundedRect(card, Px(6)))
            using (var fill = new SolidBrush(p.Card))
            using (var border = new Pen(p.CardBorder == p.Card ? p.Divider : p.CardBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int textRight = (controls != null ? controls.Left : Width) - Px(12);
            int textWidth = Math.Max(0, textRight - Px(16));
            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

            if (description == null)
            {
                TextRenderer.DrawText(g, title, titleFont, new Rectangle(Px(16), 0, textWidth, Height), p.Text,
                    flags | TextFormatFlags.VerticalCenter);
                return;
            }
            TextRenderer.DrawText(g, title, titleFont, new Rectangle(Px(16), Px(12), textWidth, Px(22)), p.Text, flags);
            TextRenderer.DrawText(g, description, descriptionFont, new Rectangle(Px(16), Px(35), textWidth, Px(20)), p.Secondary, flags);
        }
    }
}
