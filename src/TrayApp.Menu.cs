using System;
using System.Drawing;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // The right-click menu: quick actions only, every option lives in the settings window. It looks and opens like
    // the Windows 11 taskbar menus: a glyph per item, centred on the click and resting just above the taskbar.
    sealed partial class TrayApp
    {
        // Gap between the menu and the taskbar or the screen edges, in logical pixels (as the clock menu).
        const float MenuScreenMargin = 12f;

        void RebuildMenu()
        {
            menu.Items.Clear();
            menu.Items.Add(MenuAction(Strings.Get(Strings.SettingsMenu), "", OpenSettings));
            menu.Items.Add(MenuAction(Strings.Get(Strings.Refresh), Theme.RefreshGlyph, RefreshNow));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuAction(Strings.Get(Strings.Exit), "", ExitThread));
            MenuStyle.SpaceItems(menu.Items);
            MenuStyle.FitWidth(menu);
        }

        // Opens the menu centred on the pointer and resting above the taskbar, kept inside the screen.
        void ShowTrayMenu()
        {
            if (flyout.Visible)
                flyout.Hide();
            RebuildMenu();

            Point pointer = Cursor.Position;
            Rectangle work = Screen.FromPoint(pointer).WorkingArea;
            // RebuildMenu sized it (MenuStyle.FitWidth).
            Size size = menu.Size;
            int margin = (int)(MenuScreenMargin * menu.DeviceDpi / 96f);

            int x = pointer.X - size.Width / 2;
            x = Math.Max(work.Left + margin, Math.Min(work.Right - margin - size.Width, x));
            // Above the taskbar when it is at the bottom; otherwise just inside the work area near the pointer.
            int y = pointer.Y >= work.Bottom
                ? work.Bottom - margin - size.Height
                : Math.Max(work.Top + margin, Math.Min(work.Bottom - margin - size.Height, pointer.Y));

            menu.Show(new Point(x, y));
            // A menu opened from the notification area must own the foreground, or a click elsewhere leaves it open.
            Native.SetForegroundWindow(menu.Handle);
        }

        static ToolStripMenuItem MenuAction(string text, string glyph, Action onClick)
        {
            var item = new GlyphMenuItem(text, glyph);
            item.Click += delegate { onClick(); };
            return item;
        }
    }
}
