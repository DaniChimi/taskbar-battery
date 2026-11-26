using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace TaskbarBattery
{
    // The application's own icon, for window title bars: a white battery on a rounded square in the Windows accent
    // colour. Drawn at run time, so it follows the user's accent.
    static class AppIcon
    {
        // MobBattery10: the full battery glyph of Segoe Fluent Icons.
        const string BatteryGlyph = "";

        // The caller owns the returned handle and must free it with DestroyIcon.
        public static IntPtr Create(int size)
        {
            using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    var tile = new RectangleF(0.5f, 0.5f, size - 1f, size - 1f);
                    using (GraphicsPath path = Theme.RoundedRect(tile, size * 0.22f))
                    using (var fill = new SolidBrush(Theme.Accent))
                        g.FillPath(fill, path);

                    using (var font = new Font(Theme.GlyphFont, size * 0.72f, FontStyle.Regular, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.White))
                    using (var format = new StringFormat(StringFormat.GenericTypographic))
                    {
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Center;
                        format.FormatFlags = (format.FormatFlags & ~StringFormatFlags.LineLimit) | StringFormatFlags.NoClip;
                        g.DrawString(BatteryGlyph, font, brush, new RectangleF(0, 0, size, size), format);
                    }
                }
                return TrayIconRenderer.CreateAlphaIcon(bitmap);
            }
        }
    }
}
