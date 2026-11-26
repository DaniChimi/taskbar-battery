using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TaskbarBattery
{
    // User choices, saved as "key=value" lines in %APPDATA%\TaskbarBattery\settings.txt.
    sealed class Settings
    {
        static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskbarBattery", "settings.txt");

        public TrayIconStyle IconStyle = TrayIconStyle.Battery;

        // Address of the device shown in the tray icon; null means automatic (the connected one with the least battery).
        public string TrayDevice;

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
                    if (equals > 0)
                        values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }

                string value;
                int style;
                if (values.TryGetValue("IconStyle", out value) && int.TryParse(value, out style) &&
                    Enum.IsDefined(typeof(TrayIconStyle), style))
                    settings.IconStyle = (TrayIconStyle)style;
                if (values.TryGetValue("TrayDevice", out value) && value.Length > 0)
                    settings.TrayDevice = value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Settings load failed: " + ex);
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, new[]
                {
                    "IconStyle=" + (int)IconStyle,
                    "TrayDevice=" + (TrayDevice ?? string.Empty),
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Settings save failed: " + ex);
            }
        }
    }
}
