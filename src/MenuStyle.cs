using System.Drawing;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // A context menu that never gets a taskbar button. Shown without an owner control (as the tray menu is),
    // a ContextMenuStrip is a plain top-level window, and Windows gives it a taskbar button with a blank icon;
    // the tool-window style keeps it off the taskbar, like the shell's own menus.
    sealed class FluentMenu : ContextMenuStrip
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }
    }

    // Windows 11 look for every drop-down menu in the app: the tray icon's menu and the settings drop-downs.
    static class MenuStyle
    {
        // showChecks: reserve the column for check marks.
        public static void Apply(ToolStripDropDown dropDown, bool showChecks)
        {
            dropDown.Renderer = new MenuRenderer();
            // 14 px text, as in the Windows 11 shell menus.
            dropDown.Font = new Font(Theme.TextFont, 10.5f);
            // Without a check column WinForms puts the text 8 px from the edge, cramped against the hover highlight,
            // and recomputes any Padding set here: MenuRenderer indents the text instead (PlainTextIndent). The menu
            // keeps enough room on the right, where WinForms reserves a column for arrows and shortcuts.
            var menuDropDown = dropDown as ToolStripDropDownMenu;
            if (menuDropDown != null)
            {
                menuDropDown.ShowImageMargin = false;
                menuDropDown.ShowCheckMargin = showChecks;
                // The check column is sized from the image size: keep it narrow.
                menuDropDown.ImageScalingSize = new Size(12, 12);
            }
            dropDown.Opened += delegate
            {
                int corner = Native.DWMWCP_ROUND;
                Native.DwmSetWindowAttribute(dropDown.Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            };
        }

        // Air between the longest text and the menu's right edge, in logical pixels, as on the Windows 11 clock menu.
        const float TextRightMargin = 16f;

        // Menus without a check column draw their text further right than WinForms expects (see
        // MenuRenderer.TextIndent): size the menu to exactly the indent, the widest text and the right margin.
        // Call once the items are in place, before showing the menu.
        public static void FitWidth(ToolStripDropDownMenu menu)
        {
            if (menu.ShowCheckMargin || menu.ShowImageMargin)
                return;
            int widest = 0;
            foreach (ToolStripItem item in menu.Items)
            {
                // NoPadding: the renderer draws without it, so measure the same way.
                widest = System.Math.Max(widest,
                    TextRenderer.MeasureText(item.Text, menu.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width);
            }
            float scale = menu.DeviceDpi / 96f;
            int width = widest + (int)((MenuRenderer.TextIndent(menu) + TextRightMargin) * scale);

            // WinForms would size the menu wider on its own (it reserves room for arrows and shortcuts): pin the
            // width, keeping the height it computes for the items.
            menu.AutoSize = true;
            Size preferred = menu.GetPreferredSize(Size.Empty);
            menu.AutoSize = false;
            menu.Size = new Size(width, preferred.Height);

            // Items are laid out a few pixels narrower than the menu, and drawing is clipped to an item: stretch
            // them to the full width so the hover highlight keeps the same 4 px inset on both sides.
            foreach (ToolStripItem item in menu.Items)
            {
                var menuItem = item as ToolStripMenuItem;
                if (menuItem == null)
                    continue;
                int height = menuItem.Height;
                menuItem.AutoSize = false;
                menuItem.Size = new Size(width, height);
            }
        }

        // Windows 11 menu metrics: about 32 px per item and a little air around separators (at 100% scale).
        public static void SpaceItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                var menuItem = item as ToolStripMenuItem;
                if (menuItem != null)
                {
                    // Vertical only: horizontal padding makes items wider than the menu (WinForms sizes the menu
                    // without it), which pushes submenus away. The check column already indents the text.
                    menuItem.Padding = new Padding(0, 6, 0, 6);
                    if (menuItem.HasDropDownItems)
                        SpaceItems(menuItem.DropDownItems);
                }
                else if (item is ToolStripSeparator)
                {
                    item.AutoSize = false;
                    item.Height = 9;
                }
            }
        }
    }
}
