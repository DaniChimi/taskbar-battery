using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace TaskbarBattery
{
    enum TrayIconStyle { Battery = 0, Percent = 1, Device = 2 }

    // Draws the notification-area icon in one of three styles:
    // Battery: the Windows 11 battery outline filled to the level; Percent: the number alone;
    // Device: the device glyph with a level bar underneath. The fill turns red when the battery is low.
    // While charging, Battery shows the Windows charging battery, Percent becomes the device glyph with a bolt
    // inside, and Device animates its bar (see AnimationFrames).
    // Returns an icon handle the caller must free with DestroyIcon.
    static class TrayIconRenderer
    {
        // Steps of the charging sweep on the Device style bar; the caller cycles the frame through them.
        public const int AnimationFrames = 6;

        const string BatteryOutlineGlyph = "";

        // MobBatteryCharging0..10 (EBAB-EBB5): the battery with a bolt, one glyph per 10% step.
        const int ChargingBatteryBase = 0xEBAB;

        static readonly Dictionary<int, Rectangle> InteriorCache = new Dictionary<int, Rectangle>();

        // Whether this combination changes over time, so the caller knows to keep redrawing it.
        public static bool IsAnimated(BatteryDevice device, TrayIconStyle style)
        {
            return device != null && device.IsCharging && (style == TrayIconStyle.Device || style == TrayIconStyle.Percent);
        }

        public static IntPtr Render(BatteryDevice device, TrayIconStyle style, int size, int frame)
        {
            bool light = Theme.IsLight;
            Color foreground = light ? Color.FromArgb(28, 28, 28) : Color.White;

            if (device != null && device.IsCharging && style == TrayIconStyle.Percent)
            {
                using (Bitmap filling = RenderFillingPercent(size, device.Percent, foreground, frame))
                {
                    return CreateAlphaIcon(filling);
                }
            }
            Color lowColor = light ? Color.FromArgb(209, 52, 56) : Color.FromArgb(255, 107, 107);
            Color dim = Color.FromArgb(128, foreground);

            using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    var box = new RectangleF(0, 0, size, size);

                    if (device == null)
                    {
                        // Nothing connected: an empty, dimmed symbol of the chosen style.
                        string glyph = style == TrayIconStyle.Device ? Theme.BluetoothGlyph : BatteryOutlineGlyph;
                        DrawGlyph(g, glyph, size, box, dim);
                    }
                    else
                    {
                        Color fill = device.IsLow ? lowColor : foreground;
                        switch (style)
                        {
                            case TrayIconStyle.Percent:
                                DrawPercent(g, size, device.Percent, fill);
                                break;
                            case TrayIconStyle.Device:
                                DrawDeviceWithBar(g, size, device, foreground, fill, frame);
                                break;
                            default:
                                if (device.IsCharging)
                                    DrawChargingBattery(g, size, device.Percent, foreground);
                                else
                                    DrawBattery(g, size, device.Percent, foreground, fill);
                                break;
                        }
                    }
                }
                return CreateAlphaIcon(bitmap);
            }
        }

        static void DrawBattery(Graphics g, int size, int percent, Color outline, Color fill)
        {
            DrawGlyph(g, BatteryOutlineGlyph, size, new RectangleF(0, 0, size, size), outline);
            Rectangle inside = Interior(size);
            if (inside.Width <= 0 || inside.Height <= 0)
                return;

            // A sliver stays visible even near 0%, as on the Windows battery icon.
            int width = Math.Max(1, (int)Math.Round(inside.Width * Math.Max(percent, 5) / 100.0));
            g.SmoothingMode = SmoothingMode.None;
            using (var brush = new SolidBrush(fill))
            {
                g.FillRectangle(brush, inside.X, inside.Y, width, inside.Height);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
        }

        // The Windows charging battery glyph closest to the level (the bolt is part of the glyph).
        static void DrawChargingBattery(Graphics g, int size, int percent, Color color)
        {
            int step = Math.Max(0, Math.Min(10, (int)Math.Round(percent / 10.0)));
            DrawGlyph(g, char.ConvertFromUtf32(ChargingBatteryBase + step), size, new RectangleF(0, 0, size, size), color);
        }

        static void DrawPercent(Graphics g, int size, int percent, Color color)
        {
            string text = percent.ToString();
            float pixels = size * (text.Length >= 3 ? 0.55f : 0.8f);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var font = new Font(Theme.DisplayFont, pixels, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (StringFormat format = CenteredFormat())
            {
                g.DrawString(text, font, brush, new RectangleF(0, size * 0.05f, size, size), format);
            }
        }

        // Charging with the Percent style: the number in a faint tint, filled with full colour from the bottom up,
        // one step per frame (empty on the first frame, full on the last), like liquid rising in the digits.
        static Bitmap RenderFillingPercent(int size, int percent, Color color, int frame)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                DrawPercent(g, size, percent, color);
            }

            // Rows that carry ink: the fill level is measured on the digits, not on the whole icon.
            int top = -1;
            int bottom = -1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (bitmap.GetPixel(x, y).A > 0)
                    {
                        if (top < 0)
                            top = y;
                        bottom = y;
                        break;
                    }
                }
            }
            if (top < 0)
                return bitmap;

            int step = ((frame % AnimationFrames) + AnimationFrames) % AnimationFrames;
            float level = (float)step / (AnimationFrames - 1);
            int inkHeight = bottom - top + 1;
            // Rows at or below this one are filled.
            int filledFrom = bottom + 1 - (int)Math.Round(inkHeight * level);

            for (int y = top; y <= bottom; y++)
            {
                if (y >= filledFrom)
                    continue;
                for (int x = 0; x < size; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.A > 0)
                        bitmap.SetPixel(x, y, Color.FromArgb(pixel.A * 35 / 100, pixel.R, pixel.G, pixel.B));
                }
            }
            return bitmap;
        }

        // While charging, a lighter section grows step by step from the current level to the end, then restarts.
        static void DrawDeviceWithBar(Graphics g, int size, BatteryDevice device, Color foreground, Color fill, int frame)
        {
            float barHeight = Math.Max(2f, (float)Math.Round(size / 8f));
            float glyphArea = size - barHeight - 1;
            DrawGlyph(g, Theme.Glyph(device.Kind), glyphArea - 1, new RectangleF(0, 0, size, glyphArea), foreground);

            float y = size - barHeight;
            float trackWidth = size - 2;
            float levelWidth = Math.Max(1f, trackWidth * device.Percent / 100f);
            g.SmoothingMode = SmoothingMode.None;
            using (var track = new SolidBrush(Color.FromArgb(80, foreground)))
            using (var level = new SolidBrush(fill))
            using (var sweep = new SolidBrush(Color.FromArgb(170, foreground)))
            {
                g.FillRectangle(track, 1, y, trackWidth, barHeight);
                if (device.IsCharging)
                {
                    int step = ((frame % AnimationFrames) + AnimationFrames) % AnimationFrames;
                    float sweepWidth = (trackWidth - levelWidth) * step / (AnimationFrames - 1);
                    if (sweepWidth >= 1f)
                        g.FillRectangle(sweep, 1 + levelWidth, y, sweepWidth, barHeight);
                }
                g.FillRectangle(level, 1, y, levelWidth, barHeight);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
        }

        static void DrawGlyph(Graphics g, string glyph, float pixels, RectangleF bounds, Color color)
        {
            using (var font = new Font(Theme.GlyphFont, pixels, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (StringFormat format = CenteredFormat())
            {
                g.DrawString(glyph, font, brush, bounds, format);
            }
        }

        // Typographic layout without LineLimit, which would drop a glyph taller than its rectangle.
        static StringFormat CenteredFormat()
        {
            var format = new StringFormat(StringFormat.GenericTypographic);
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.FormatFlags = (format.FormatFlags & ~StringFormatFlags.LineLimit) | StringFormatFlags.NoClip | StringFormatFlags.NoWrap;
            return format;
        }

        // Finds the empty space inside the battery outline at this size by scanning a rendered copy of it:
        // walk the middle row to the inner edges of the left and right strokes, and the middle column to the
        // inner edges of the top and bottom strokes. Leaves a one pixel gap (two at large sizes).
        static Rectangle Interior(int size)
        {
            Rectangle cached;
            if (InteriorCache.TryGetValue(size, out cached))
                return cached;

            Rectangle result = Rectangle.Empty;
            using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    DrawGlyph(g, BatteryOutlineGlyph, size, new RectangleF(0, 0, size, size), Color.White);
                }

                Func<int, int, bool> ink = (x, y) => bitmap.GetPixel(x, y).A >= 96;
                int midY = size / 2;

                int left = 0;
                while (left < size && !ink(left, midY)) left++;
                while (left < size && ink(left, midY)) left++;
                int right = left;
                while (right < size && !ink(right, midY)) right++;

                if (left < size && right < size && right > left)
                {
                    int midX = (left + right) / 2;
                    int top = midY;
                    while (top > 0 && !ink(midX, top - 1)) top--;
                    int bottom = midY;
                    while (bottom < size - 1 && !ink(midX, bottom + 1)) bottom++;

                    int gap = size >= 24 ? 2 : 1;
                    result = Rectangle.FromLTRB(left + gap, top + gap, right - gap, bottom + 1 - gap);
                }
            }
            InteriorCache[size] = result;
            return result;
        }

        // Bitmap.GetHicon loses smooth alpha edges; build a 32-bit icon from a DIB instead.
        static IntPtr CreateAlphaIcon(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            var header = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth = width,
                biHeight = -height, // top-down
                biPlanes = 1,
                biBitCount = 32,
            };

            IntPtr bits;
            IntPtr color = CreateDIBSection(IntPtr.Zero, ref header, 0, out bits, IntPtr.Zero, 0);
            IntPtr mask = CreateBitmap(width, height, 1, 1, IntPtr.Zero);
            try
            {
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var row = new byte[width * 4];
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                        Marshal.Copy(row, 0, bits + y * width * 4, row.Length);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                var info = new ICONINFO { fIcon = true, hbmMask = mask, hbmColor = color };
                return CreateIconIndirect(ref info);
            }
            finally
            {
                Native.DeleteObject(color);
                Native.DeleteObject(mask);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER header, uint usage, out IntPtr bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, IntPtr bits);

        [DllImport("user32.dll")]
        static extern IntPtr CreateIconIndirect(ref ICONINFO info);
    }

    // The five-step scale used by the flyout.
    static class Segments
    {
        public const int Count = 5;

        // One segment per started 20%: 1-20% lights one, 21-40% two, ... 81-100% all five; 0% lights none.
        public static int Lit(int percent)
        {
            if (percent <= 0)
                return 0;
            return Math.Min(Count, (int)Math.Ceiling(percent / (100.0 / Count)));
        }
    }
}
