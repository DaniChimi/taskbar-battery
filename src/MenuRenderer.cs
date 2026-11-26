using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // Draws the right-click menu in the Windows 11 style: flat background, rounded hover highlight, thin separators.
    // The rounded window corners and the border come from DWM (see TrayApp.ShowMenu).
    sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer()
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var fill = new SolidBrush(Palette.Current.Menu))
            {
                e.Graphics.FillRectangle(fill, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // DWM draws the border.
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // No image column.
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
                return;

            Palette p = Palette.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // Inset 4 px from the menu's own edges (not the item's, which can be wider than the menu), as on the
            // Windows 11 menus. Item coordinates: the menu's left edge is at -Item.Bounds.X.
            int menuWidth = e.ToolStrip != null ? e.ToolStrip.ClientSize.Width : e.Item.Width;
            var bounds = new Rectangle(4 - e.Item.Bounds.X, 1, menuWidth - 8, e.Item.Height - 2);
            using (GraphicsPath path = Theme.RoundedRect(bounds, 4))
            using (var fill = new SolidBrush(p.Hover))
            {
                e.Graphics.FillPath(fill, path);
            }
        }

        // Menus without a check column, distances from the menu's left edge in logical pixels, measured on the
        // Windows 11 clock menu: the glyph column starts at 15 px and is 16 px wide, the text starts at 44 px.
        // Without glyphs the text starts at 22 px, where it sits in menus that have a check column.
        public const float GlyphLeft = 15f;
        public const float GlyphSize = 16f;
        public const float GlyphTextIndent = 44f;
        public const float PlainTextIndent = 22f;

        // Text start for a menu without a check column, in logical pixels.
        public static float TextIndent(ToolStrip menu)
        {
            foreach (ToolStripItem item in menu.Items)
            {
                if (item is GlyphMenuItem)
                    return GlyphTextIndent;
            }
            return PlainTextIndent;
        }

        // Drawn here rather than by the base renderer: with Segoe UI Variable, GDI's font metrics push the
        // automatically placed text above the middle of the item. Centring on the full item height fixes it.
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            Palette p = Palette.Current;
            Color color = e.Item.Enabled ? p.Text : p.Secondary;
            int x = e.TextRectangle.X;
            var menu = e.ToolStrip as ToolStripDropDownMenu;
            if (menu != null && !menu.ShowCheckMargin && !menu.ShowImageMargin)
            {
                // Item coordinates: the menu's left edge is at -Item.Bounds.X.
                float scale = menu.DeviceDpi / 96f;
                int edge = -e.Item.Bounds.X;
                x = Math.Max(x, edge + (int)(TextIndent(menu) * scale));

                var glyphItem = e.Item as GlyphMenuItem;
                if (glyphItem != null && glyphItem.Glyph != null)
                {
                    var column = new RectangleF(edge + GlyphLeft * scale, 0, GlyphSize * scale, e.Item.Height);
                    DrawGlyph(e.Graphics, glyphItem.Glyph, GlyphSize * scale, column, color);
                }
            }
            var bounds = new Rectangle(x, 0, e.Item.Width - x, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        // Fluent check mark, centred on the whole check column and the item's height.
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var column = new RectangleF(e.ImageRectangle.X, 0, e.ImageRectangle.Width + 8, e.Item.Height);
            DrawGlyph(e.Graphics, "", 14f, column, Palette.Current.Text);
        }

        // Fluent chevron instead of the classic triangle.
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            var bounds = new RectangleF(e.ArrowRectangle.X - 4, 0, e.ArrowRectangle.Width + 4, e.Item.Height);
            DrawGlyph(e.Graphics, "", 10f, bounds, Palette.Current.Text);
        }

        static void DrawGlyph(Graphics g, string glyph, float pixels, RectangleF bounds, Color color)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var font = new Font(Theme.GlyphFont, pixels, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(glyph, font, brush, bounds, format);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            // Full width minus the same 4 px inset as the hover highlight, from the menu's own edges.
            int y = e.Item.Height / 2;
            int menuWidth = e.ToolStrip != null ? e.ToolStrip.ClientSize.Width : e.Item.Width;
            int left = 4 - e.Item.Bounds.X;
            using (var pen = new Pen(Palette.Current.Divider))
            {
                e.Graphics.DrawLine(pen, left + 4, y, left + menuWidth - 12, y);
            }
        }
    }

    // A menu item with a Fluent glyph drawn in front of its text, like the items of the Windows 11 shell menus.
    sealed class GlyphMenuItem : ToolStripMenuItem
    {
        public GlyphMenuItem(string text, string glyph) : base(text)
        {
            Glyph = glyph;
        }

        public string Glyph { get; private set; }
    }
}
