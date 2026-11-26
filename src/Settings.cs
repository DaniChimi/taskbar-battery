using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TaskbarBattery
{
    // User choices, saved as "key=value" lines in %APPDATA%\TaskbarBattery\settings.txt.
    // Custom device names are stored one per line as "Name.<address>=<escaped name>".
    sealed class Settings
    {
        static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskbarBattery", "settings.txt");

        const string NamePrefix = "Name.";

        public static readonly int[] LowThresholdChoices = { 10, 15, 20, 25, 30 };

        public TrayIconStyle IconStyle = TrayIconStyle.Battery;

        // Address of the device shown in the tray icon; null means automatic (the connected one with the least battery).
        public string TrayDevice;

        // One tray icon per connected device instead of a single one.
        public bool PerDeviceIcons;

        public bool NotifyLow = true;
        public bool NotifyFull = true;
        public bool NotifyConnect;
        public bool NotifyDisconnect;

        // Percentage at or below which a battery counts as low (red, and the low-battery notification).
        public int LowThreshold = 20;

        // Custom names by device address.
        public readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Addresses of devices hidden from the icon, the panel and the notifications.
        public readonly HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static Settings Load()
        {
            var settings = new Settings();
            try
            {
                if (!File.Exists(FilePath))
                    return settings;

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int equals = line.IndexOf('=');
                    if (equals <= 0)
                        continue;
                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    if (key.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string address = key.Substring(NamePrefix.Length);
                        string name = Uri.UnescapeDataString(value);
                        if (address.Length > 0 && name.Length > 0)
                            settings.Names[address] = name;
                    }
                    else
                    {
                        values[key] = value;
                    }
                }

                int style;
                if (int.TryParse(Get(values, "IconStyle"), out style) && Enum.IsDefined(typeof(TrayIconStyle), style))
                    settings.IconStyle = (TrayIconStyle)style;

                string trayDevice = Get(values, "TrayDevice");
                settings.TrayDevice = string.IsNullOrEmpty(trayDevice) ? null : trayDevice;

                settings.PerDeviceIcons = GetBool(values, "PerDeviceIcons", settings.PerDeviceIcons);
                settings.NotifyLow = GetBool(values, "NotifyLow", settings.NotifyLow);
                settings.NotifyFull = GetBool(values, "NotifyFull", settings.NotifyFull);
                settings.NotifyConnect = GetBool(values, "NotifyConnect", settings.NotifyConnect);
                settings.NotifyDisconnect = GetBool(values, "NotifyDisconnect", settings.NotifyDisconnect);

                int threshold;
                if (int.TryParse(Get(values, "LowThreshold"), out threshold) && LowThresholdChoices.Contains(threshold))
                    settings.LowThreshold = threshold;

                string hidden = Get(values, "Hidden");
                if (!string.IsNullOrEmpty(hidden))
                {
                    foreach (string address in hidden.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        settings.Hidden.Add(address.Trim());
                }
            }
            catch (Exception ex)
            {
                Log.Write("Settings load failed: " + ex.Message);
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    "IconStyle=" + (int)IconStyle,
                    "TrayDevice=" + (TrayDevice ?? string.Empty),
                    "PerDeviceIcons=" + PerDeviceIcons,
                    "NotifyLow=" + NotifyLow,
                    "NotifyFull=" + NotifyFull,
                    "NotifyConnect=" + NotifyConnect,
                    "NotifyDisconnect=" + NotifyDisconnect,
                    "LowThreshold=" + LowThreshold,
                    "Hidden=" + string.Join(";", Hidden.ToArray()),
                };
                foreach (KeyValuePair<string, string> name in Names)
                    lines.Add(NamePrefix + name.Key + "=" + Uri.EscapeDataString(name.Value));

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception ex)
            {
                Log.Write("Settings save failed: " + ex.Message);
            }
        }

        static string Get(Dictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : null;
        }

        static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(Get(values, key), out value) ? value : fallback;
        }
    }
}
