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

        static Color MenuBackground(Palette p)
        {
            return p.Background;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var fill = new SolidBrush(MenuBackground(Palette.Current)))
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

        // Drawn here rather than by the base renderer: with Segoe UI Variable, GDI's font metrics push the
        // automatically placed text above the middle of the item. Centring on the full item height fixes it.
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            Palette p = Palette.Current;
            var bounds = new Rectangle(e.TextRectangle.X, 0, e.Item.Width - e.TextRectangle.X, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, e.Item.Enabled ? p.Text : p.Secondary,
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
            int y = e.Item.Height / 2;
            using (var pen = new Pen(Palette.Current.Divider))
            {
                e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
            }
        }
    }
}
