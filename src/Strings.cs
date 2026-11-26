using System.Collections.Generic;
using System.Globalization;

namespace TaskbarBattery
{
    // User-facing text, one dictionary per language. The UI language follows Windows; English is the fallback.
    static class Strings
    {
        public const string Title = "title";
        public const string Refresh = "refresh";
        public const string Exit = "exit";
        public const string NoDevices = "noDevices";
        public const string NoDevicesHint = "noDevicesHint";
        public const string Percent = "percent";
        public const string ReportedToday = "reportedToday";
        public const string ReportedOn = "reportedOn";
        public const string UpdatedNow = "updatedNow";
        public const string UpdatedAt = "updatedAt";
        public const string TooltipLine = "tooltipLine";
        public const string ReadError = "readError";
        public const string TrayIcon = "trayIcon";
        public const string StyleBattery = "styleBattery";
        public const string StylePercent = "stylePercent";
        public const string StyleDevice = "styleDevice";
        public const string ShownDevice = "shownDevice";
        public const string AutomaticLowest = "automaticLowest";
        public const string Charging = "charging";
        public const string FullyCharged = "fullyCharged";
        public const string TooltipCharging = "tooltipCharging";
        public const string ConnectedFor = "connectedFor";
        public const string UnderAMinute = "underAMinute";
        public const string Minutes = "minutes";
        public const string Hours = "hours";
        public const string Days = "days";

        static readonly Dictionary<string, Dictionary<string, string>> Table = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "en", new Dictionary<string, string>
                {
                    { Title, "Batteries" },
                    { Refresh, "Refresh now" },
                    { Exit, "Exit" },
                    { NoDevices, "No device connected" },
                    { NoDevicesHint, "Turn on a Bluetooth device that reports its battery" },
                    { Percent, "{0}%" },
                    { ReportedToday, "{0:HH:mm}" },
                    { ReportedOn, "{0:d}" },
                    { UpdatedNow, "Updated just now" },
                    { UpdatedAt, "Updated at {0:HH:mm}" },
                    { TooltipLine, "{0}: {1}%" },
                    { ReadError, "Could not read the devices" },
                    { TrayIcon, "Tray icon" },
                    { StyleBattery, "Battery" },
                    { StylePercent, "Percentage" },
                    { StyleDevice, "Device and bar" },
                    { ShownDevice, "Device shown" },
                    { AutomaticLowest, "Automatic (lowest battery)" },
                    { Charging, "Charging" },
                    { FullyCharged, "Fully charged" },
                    { TooltipCharging, "{0}: {1}% (charging)" },
                    { ConnectedFor, "connected for {0}" },
                    { UnderAMinute, "under a minute" },
                    { Minutes, "{0} min" },
                    { Hours, "{0} h" },
                    { Days, "{0} d" },
                }
            },
            {
                "it", new Dictionary<string, string>
                {
                    { Title, "Batterie" },
                    { Refresh, "Aggiorna ora" },
                    { Exit, "Esci" },
                    { NoDevices, "Nessun dispositivo connesso" },
                    { NoDevicesHint, "Accendi un dispositivo Bluetooth che comunica la batteria" },
                    { Percent, "{0}%" },
                    { ReportedToday, "{0:HH:mm}" },
                    { ReportedOn, "{0:d}" },
                    { UpdatedNow, "Aggiornato adesso" },
                    { UpdatedAt, "Aggiornato alle {0:HH:mm}" },
                    { TooltipLine, "{0}: {1}%" },
                    { ReadError, "Impossibile leggere i dispositivi" },
                    { TrayIcon, "Icona nella tray" },
                    { StyleBattery, "Batteria" },
                    { StylePercent, "Percentuale" },
                    { StyleDevice, "Dispositivo e barra" },
                    { ShownDevice, "Dispositivo mostrato" },
                    { AutomaticLowest, "Automatico (batteria più bassa)" },
                    { Charging, "In carica" },
                    { FullyCharged, "Carica completa" },
                    { TooltipCharging, "{0}: {1}% (in carica)" },
                    { ConnectedFor, "connesso da {0}" },
                    { UnderAMinute, "meno di un minuto" },
                    { Minutes, "{0} min" },
                    { Hours, "{0} h" },
                    { Days, "{0} g" },
                }
            },
        };

        static readonly string Language = Table.ContainsKey(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            : "en";

        public static string Get(string key)
        {
            string value;
            if (Table[Language].TryGetValue(key, out value))
                return value;
            return Table["en"][key];
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), args);
        }
    }
}
