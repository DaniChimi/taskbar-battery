using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TaskbarBattery
{
    // Start with Windows through the per-user Run key (no administrator rights needed).
    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "TaskbarBattery";

        static string Command
        {
            get { return "\"" + Application.ExecutablePath + "\""; }
        }

        public static bool IsEnabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
        }

        public static void Set(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled)
                        key.SetValue(ValueName, Command);
                    else if (key.GetValue(ValueName) != null)
                        key.DeleteValue(ValueName);
                }
                Log.Write("Start with Windows " + (enabled ? "enabled" : "disabled") + ".");
            }
            catch (Exception ex)
            {
                Log.Write("Could not change start with Windows: " + ex.Message);
            }
        }

        // If the program was moved since autostart was enabled, point the entry at the current location.
        public static void RefreshPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    object value = key == null ? null : key.GetValue(ValueName);
                    if (value != null && !string.Equals((string)value, Command, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(ValueName, Command);
                        Log.Write("Start with Windows now points to " + Command + ".");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Could not check start with Windows: " + ex.Message);
            }
        }
    }
}
