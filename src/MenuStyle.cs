using System.Drawing;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // Windows 11 look for every drop-down menu in the app: the tray icon's menu and the settings drop-downs.
    static class MenuStyle
    {
        // showChecks: reserve the column for check marks.
        public static void Apply(ToolStripDropDown dropDown, bool showChecks)
        {
            dropDown.Renderer = new MenuRenderer();
            dropDown.Font = new Font(Theme.TextFont, 9.5f);
            // No custom Padding: the menu recomputes it for the check column, and overriding it makes the items
            // wider than the menu (the hover highlight then spills over the edge and submenus open too far right).
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
                    menuItem.Padding = new Padding(0, 7, 0, 7);
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
