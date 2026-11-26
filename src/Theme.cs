using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace TaskbarBattery
{
    // Fonts, glyphs and the light/dark palette shared by the taskbar widget, the flyout and the menu.
    static class Theme
    {
        public static readonly string GlyphFont = FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        public static readonly string TextFont = FontExists("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
        public static readonly string DisplayFont = FontExists("Segoe UI Variable Display") ? "Segoe UI Variable Display" : "Segoe UI";

        public const string RefreshGlyph = "";
        public const string BluetoothGlyph = "";

        // Follows the taskbar and system flyouts (not the app theme), like the Windows quick settings.
        public static bool IsLight
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("SystemUsesLightTheme");
                    return value is int && (int)value == 1;
                }
            }
        }

        // The Windows accent colour (stored as 0xAABBGGRR), or a neutral blue when it is not set.
        public static Color Accent
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    object value = key == null ? null : key.GetValue("AccentColor");
                    if (!(value is int))
                        return Color.FromArgb(0, 103, 192);
                    int abgr = (int)value;
                    return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                }
            }
        }

        public static string Glyph(DeviceKind kind)
        {
            switch (kind)
            {
                case DeviceKind.Mouse: return "";
                case DeviceKind.Keyboard: return "";
                case DeviceKind.Gamepad: return "";
                case DeviceKind.Headphones: return "";
                case DeviceKind.Phone: return "";
                default: return BluetoothGlyph;
            }
        }

        public static Color Mix(Color a, Color b, float amountOfB)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amountOfB),
                (int)(a.G + (b.G - a.G) * amountOfB),
                (int)(a.B + (b.B - a.B) * amountOfB));
        }

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float rad = Math.Min(radius, Math.Min(r.Width, r.Height) / 2f);
            if (rad <= 0.5f)
            {
                path.AddRectangle(r);
                return path;
            }
            float d = rad * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        static bool FontExists(string name)
        {
            using (var font = new Font(name, 10f))
            {
                return string.Equals(font.Name, name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // Colours for one theme (light or dark), close to the Windows 11 system flyouts.
    sealed class Palette
    {
        public Color Background, Footer, Card, CardBorder, Tile, Text, Secondary, Divider, Track, Bar, Low, Hover, WindowBorder;

        public static Palette Current
        {
            get { return Theme.IsLight ? Light() : Dark(); }
        }

        static Palette Dark()
        {
            Color accent = Theme.Accent;
            return new Palette
            {
                Background = Color.FromArgb(32, 32, 32),
                Footer = Color.FromArgb(28, 28, 28),
                Card = Color.FromArgb(43, 43, 43),
                CardBorder = Color.FromArgb(43, 43, 43),
                Tile = Color.FromArgb(55, 55, 55),
                Text = Color.FromArgb(255, 255, 255),
                Secondary = Color.FromArgb(197, 197, 197),
                Divider = Color.FromArgb(60, 60, 60),
                Track = Color.FromArgb(70, 70, 70),
                Bar = Theme.Mix(accent, Color.White, 0.45f),
                Low = Color.FromArgb(255, 107, 107),
                Hover = Color.FromArgb(55, 55, 55),
                WindowBorder = Color.FromArgb(70, 70, 70),
            };
        }

        static Palette Light()
        {
            Color accent = Theme.Accent;
            return new Palette
            {
                Background = Color.FromArgb(243, 243, 243),
                Footer = Color.FromArgb(238, 238, 238),
                Card = Color.FromArgb(251, 251, 251),
                CardBorder = Color.FromArgb(229, 229, 229),
                Tile = Color.FromArgb(240, 240, 240),
                Text = Color.FromArgb(26, 26, 26),
                Secondary = Color.FromArgb(96, 96, 96),
                Divider = Color.FromArgb(222, 222, 222),
                Track = Color.FromArgb(222, 222, 222),
                Bar = Theme.Mix(accent, Color.Black, 0.15f),
                Low = Color.FromArgb(196, 43, 28),
                Hover = Color.FromArgb(230, 230, 230),
                WindowBorder = Color.FromArgb(210, 210, 210),
            };
        }
    }
}
