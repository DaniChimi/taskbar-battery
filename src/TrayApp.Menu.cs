using System.Windows.Forms;

namespace TaskbarBattery
{
    // The right-click menu: quick actions only. Every option lives in the settings window.
    sealed partial class TrayApp
    {
        void RebuildMenu()
        {
            menu.Items.Clear();
            menu.Items.Add(MenuAction(Strings.Get(Strings.SettingsMenu), OpenSettings));
            menu.Items.Add(MenuAction(Strings.Get(Strings.Refresh), Scan));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuAction(Strings.Get(Strings.Exit), ExitThread));
            MenuStyle.SpaceItems(menu.Items);
        }

        static ToolStripMenuItem MenuAction(string text, System.Action onClick)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += delegate { onClick(); };
            return item;
        }
    }
}
