using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // The right-click menu, rebuilt each time it opens so it always reflects the current devices and settings.
    sealed partial class TrayApp
    {
        // "Device shown", disabled while one icon per device is on; kept to update it while the menu is open.
        ToolStripMenuItem shownDeviceMenuItem;

        void RebuildMenu()
        {
            menu.Items.Clear();

            menu.Items.Add(BuildIconMenu());
            menu.Items.Add(BuildShownDeviceMenu());
            menu.Items.Add(BuildDevicesMenu());
            menu.Items.Add(BuildNotificationsMenu());

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(Check(Strings.Get(Strings.StartWithWindows), Autostart.IsEnabled, delegate(bool on)
            {
                Autostart.Set(on);
            }));
            menu.Items.Add(MenuAction(Strings.Get(Strings.Refresh), Scan));
            menu.Items.Add(MenuAction(Strings.Get(Strings.OpenLog), Log.Open));

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(MenuAction(Strings.Get(Strings.Exit), ExitThread));

            SpaceItems(menu.Items);
        }

        ToolStripMenuItem BuildIconMenu()
        {
            ToolStripMenuItem iconMenu = Submenu(Strings.Get(Strings.TrayIcon));
            iconMenu.DropDownItems.Add(StyleChoice(Strings.StyleBattery, TrayIconStyle.Battery));
            iconMenu.DropDownItems.Add(StyleChoice(Strings.StylePercent, TrayIconStyle.Percent));
            iconMenu.DropDownItems.Add(StyleChoice(Strings.StyleDevice, TrayIconStyle.Device));
            iconMenu.DropDownItems.Add(new ToolStripSeparator());
            iconMenu.DropDownItems.Add(Check(Strings.Get(Strings.OneIconPerDevice), settings.PerDeviceIcons, delegate(bool on)
            {
                settings.PerDeviceIcons = on;
                settings.Save();
                UpdateIcons(true);
                // The menu stays open: keep the dependent item in step.
                if (shownDeviceMenuItem != null)
                    shownDeviceMenuItem.Enabled = !on;
            }));
            return iconMenu;
        }

        ToolStripMenuItem BuildShownDeviceMenu()
        {
            ToolStripMenuItem shownMenu = Submenu(Strings.Get(Strings.ShownDevice));
            // With one icon per device every device has its own icon: there is nothing to choose.
            shownMenu.Enabled = !settings.PerDeviceIcons;
            shownDeviceMenuItem = shownMenu;
            shownMenu.DropDownItems.Add(TrayDeviceChoice(Strings.Get(Strings.AutomaticLowest), null));
            List<BatteryDevice> visible = Visible;
            if (visible.Count > 0)
                shownMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (BatteryDevice device in visible)
                shownMenu.DropDownItems.Add(TrayDeviceChoice(device.DisplayName, device.Address));
            return shownMenu;
        }

        // Rename or hide each connected device; hidden ones stay listed so they can be shown again.
        ToolStripMenuItem BuildDevicesMenu()
        {
            ToolStripMenuItem devicesMenu = Submenu(Strings.Get(Strings.Devices));
            if (connected.Count == 0)
            {
                devicesMenu.DropDownItems.Add(new ToolStripMenuItem(Strings.Get(Strings.NoDevices)) { Enabled = false });
                return devicesMenu;
            }

            foreach (BatteryDevice device in connected)
            {
                BatteryDevice target = device;
                bool hidden = settings.Hidden.Contains(device.Address);
                string label = hidden
                    ? Strings.Format(Strings.HiddenDevice, device.DisplayName)
                    : device.DisplayName;

                ToolStripMenuItem deviceMenu = Submenu(label);
                deviceMenu.DropDownItems.Add(MenuAction(Strings.Get(Strings.Rename), delegate { RenameDevice(target); }));
                deviceMenu.DropDownItems.Add(Check(Strings.Get(Strings.Hide), hidden, delegate(bool on)
                {
                    if (on)
                        settings.Hidden.Add(target.Address);
                    else
                        settings.Hidden.Remove(target.Address);
                    settings.Save();
                    Log.Write((on ? "Hidden: " : "Shown again: ") + target.DisplayName);
                    // The menu stays open: relabel the device entry.
                    deviceMenu.Text = on ? Strings.Format(Strings.HiddenDevice, target.DisplayName) : target.DisplayName;
                    Refresh();
                }));
                devicesMenu.DropDownItems.Add(deviceMenu);
            }
            return devicesMenu;
        }

        ToolStripMenuItem BuildNotificationsMenu()
        {
            ToolStripMenuItem notifyMenu = Submenu(Strings.Get(Strings.Notifications));
            notifyMenu.DropDownItems.Add(Check(Strings.Get(Strings.NotifyLow), settings.NotifyLow, delegate(bool on)
            {
                settings.NotifyLow = on;
                settings.Save();
            }));
            notifyMenu.DropDownItems.Add(Check(Strings.Get(Strings.NotifyFull), settings.NotifyFull, delegate(bool on)
            {
                settings.NotifyFull = on;
                settings.Save();
            }));
            notifyMenu.DropDownItems.Add(Check(Strings.Get(Strings.NotifyConnect), settings.NotifyConnect, delegate(bool on)
            {
                settings.NotifyConnect = on;
                settings.Save();
            }));
            notifyMenu.DropDownItems.Add(Check(Strings.Get(Strings.NotifyDisconnect), settings.NotifyDisconnect, delegate(bool on)
            {
                settings.NotifyDisconnect = on;
                settings.Save();
            }));

            notifyMenu.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem thresholdMenu = Submenu(Strings.Get(Strings.LowThreshold));
            foreach (int value in Settings.LowThresholdChoices)
            {
                int threshold = value;
                thresholdMenu.DropDownItems.Add(Choice(Strings.Format(Strings.Percent, threshold), settings.LowThreshold == threshold, delegate
                {
                    settings.LowThreshold = threshold;
                    BatteryDevice.LowThreshold = threshold;
                    settings.Save();
                    Refresh();
                }));
            }
            notifyMenu.DropDownItems.Add(thresholdMenu);

            notifyMenu.DropDownItems.Add(new ToolStripSeparator());

            notifyMenu.DropDownItems.Add(MenuAction(Strings.Get(Strings.SendTestNotification), delegate
            {
                ShowNotice(new Notice
                {
                    Title = Strings.Get(Strings.TestNoticeTitle),
                    Text = Strings.Get(Strings.TestNoticeText),
                    Icon = ToolTipIcon.Info,
                });
            }));
            return notifyMenu;
        }

        void RenameDevice(BatteryDevice device)
        {
            using (var dialog = new RenameDialog(device))
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                if (dialog.CustomName == null)
                    settings.Names.Remove(device.Address);
                else
                    settings.Names[device.Address] = dialog.CustomName;
                settings.Save();
                Log.Write("Renamed " + device.Name + " to " + (dialog.CustomName ?? "its original name") + ".");

                foreach (BatteryDevice same in connected.Where(d => d.Address == device.Address))
                    same.CustomName = dialog.CustomName;
                Refresh();
            }
        }

        ToolStripMenuItem StyleChoice(string textKey, TrayIconStyle style)
        {
            return Choice(Strings.Get(textKey), settings.IconStyle == style, delegate
            {
                settings.IconStyle = style;
                settings.Save();
                UpdateIcons(true);
            });
        }

        ToolStripMenuItem TrayDeviceChoice(string text, string address)
        {
            bool isChecked = address == null
                ? settings.TrayDevice == null
                : string.Equals(settings.TrayDevice, address, StringComparison.OrdinalIgnoreCase);
            return Choice(text, isChecked, delegate
            {
                settings.TrayDevice = address;
                settings.Save();
                UpdateIcons(true);
            });
        }

        // --- building blocks ---

        // Items that change a setting in place leave the menu open when clicked, as Windows menus do for toggles and
        // options; actions (refresh, rename, exit...) still close it. The tag tells the two kinds apart.
        static readonly object ToggleTag = new object();
        static readonly object ChoiceTag = new object();

        // Set by the click being handled; read by every open menu level as it is asked to close.
        static bool keepMenuOpen;

        static bool KeepsMenuOpen(ToolStripItem item)
        {
            return item.Tag == ToggleTag || item.Tag == ChoiceTag;
        }

        static ToolStripMenuItem MenuAction(string text, Action onClick)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += delegate { onClick(); };
            return item;
        }

        // A check item that flips its own state and reports the new value.
        static ToolStripMenuItem Check(string text, bool isChecked, Action<bool> onChange)
        {
            var item = new ToolStripMenuItem(text) { Checked = isChecked, Tag = ToggleTag };
            item.Click += delegate
            {
                item.Checked = !item.Checked;
                onChange(item.Checked);
            };
            return item;
        }

        // One option of a group (the other Choice items of the same submenu): selecting it moves the check mark here.
        static ToolStripMenuItem Choice(string text, bool isChecked, Action onSelect)
        {
            var item = new ToolStripMenuItem(text) { Checked = isChecked, Tag = ChoiceTag };
            item.Click += delegate
            {
                if (item.Owner != null)
                {
                    foreach (ToolStripItem sibling in item.Owner.Items)
                    {
                        var option = sibling as ToolStripMenuItem;
                        if (option != null && option.Tag == ChoiceTag)
                            option.Checked = option == item;
                    }
                }
                onSelect();
            };
            return item;
        }

        static ToolStripMenuItem Submenu(string text)
        {
            var item = new ToolStripMenuItem(text);
            StyleDropDown(item.DropDown, true);
            return item;
        }

        // Windows 11 menu metrics: about 32 px per item and a little air around separators (at 100% scale).
        static void SpaceItems(ToolStripItemCollection items)
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

        // showChecks: reserve the column for check marks.
        static void StyleDropDown(ToolStripDropDown dropDown, bool showChecks)
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

            // A click on a toggle or an option must not close this level or any level above it: each level is asked
            // to close with reason ItemClicked, and each one cancels while the flag is set.
            dropDown.ItemClicked += delegate(object sender, ToolStripItemClickedEventArgs e)
            {
                keepMenuOpen = KeepsMenuOpen(e.ClickedItem);
            };
            dropDown.Closing += delegate(object sender, ToolStripDropDownClosingEventArgs e)
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && keepMenuOpen)
                    e.Cancel = true;
            };
            dropDown.Closed += delegate
            {
                keepMenuOpen = false;
            };
        }
    }
}
