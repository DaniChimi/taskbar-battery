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
        public const string OneIconPerDevice = "oneIconPerDevice";
        public const string Devices = "devices";
        public const string HiddenDevice = "hiddenDevice";
        public const string Rename = "rename";
        public const string Hide = "hide";
        public const string Notifications = "notifications";
        public const string NotifyLow = "notifyLow";
        public const string NotifyFull = "notifyFull";
        public const string NotifyConnect = "notifyConnect";
        public const string NotifyDisconnect = "notifyDisconnect";
        public const string LowThreshold = "lowThreshold";
        public const string SendTestNotification = "sendTestNotification";
        public const string StartWithWindows = "startWithWindows";
        public const string OpenLog = "openLog";
        public const string NoticeLowTitle = "noticeLowTitle";
        public const string NoticeLowText = "noticeLowText";
        public const string NoticeFullTitle = "noticeFullTitle";
        public const string NoticeFullText = "noticeFullText";
        public const string NoticeConnectedTitle = "noticeConnectedTitle";
        public const string NoticeConnectedText = "noticeConnectedText";
        public const string NoticeDisconnectedTitle = "noticeDisconnectedTitle";
        public const string NoticeDisconnectedText = "noticeDisconnectedText";
        public const string TestNoticeTitle = "testNoticeTitle";
        public const string TestNoticeText = "testNoticeText";
        public const string RenameTitle = "renameTitle";
        public const string RenamePrompt = "renamePrompt";
        public const string Save = "save";
        public const string Cancel = "cancel";
        public const string RestoreName = "restoreName";

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
                    { OneIconPerDevice, "One icon per device" },
                    { Devices, "Devices" },
                    { HiddenDevice, "{0} (hidden)" },
                    { Rename, "Rename…" },
                    { Hide, "Hide" },
                    { Notifications, "Notifications" },
                    { NotifyLow, "When the battery is low" },
                    { NotifyFull, "When fully charged" },
                    { NotifyConnect, "When a device connects" },
                    { NotifyDisconnect, "When a device disconnects" },
                    { LowThreshold, "Low battery level" },
                    { SendTestNotification, "Send a test notification" },
                    { StartWithWindows, "Start with Windows" },
                    { OpenLog, "Open diagnostic log" },
                    { NoticeLowTitle, "{0}: battery low" },
                    { NoticeLowText, "{0}% left. Time to charge it." },
                    { NoticeFullTitle, "{0} is fully charged" },
                    { NoticeFullText, "You can unplug the cable." },
                    { NoticeConnectedTitle, "{0} connected" },
                    { NoticeConnectedText, "Battery at {0}%." },
                    { NoticeDisconnectedTitle, "{0} disconnected" },
                    { NoticeDisconnectedText, "Last battery level: {0}%." },
                    { TestNoticeTitle, "TaskbarBattery" },
                    { TestNoticeText, "Notifications are working." },
                    { RenameTitle, "Rename device" },
                    { RenamePrompt, "New name for {0}:" },
                    { Save, "Save" },
                    { Cancel, "Cancel" },
                    { RestoreName, "Restore original" },
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
                    { OneIconPerDevice, "Un'icona per dispositivo" },
                    { Devices, "Dispositivi" },
                    { HiddenDevice, "{0} (nascosto)" },
                    { Rename, "Rinomina…" },
                    { Hide, "Nascondi" },
                    { Notifications, "Notifiche" },
                    { NotifyLow, "Batteria scarica" },
                    { NotifyFull, "Carica completa" },
                    { NotifyConnect, "Dispositivo connesso" },
                    { NotifyDisconnect, "Dispositivo disconnesso" },
                    { LowThreshold, "Soglia batteria scarica" },
                    { SendTestNotification, "Invia una notifica di prova" },
                    { StartWithWindows, "Avvia con Windows" },
                    { OpenLog, "Apri il log diagnostico" },
                    { NoticeLowTitle, "{0}: batteria scarica" },
                    { NoticeLowText, "Rimane il {0}%. È ora di caricarlo." },
                    { NoticeFullTitle, "{0} è carico" },
                    { NoticeFullText, "Puoi scollegare il cavo." },
                    { NoticeConnectedTitle, "{0} connesso" },
                    { NoticeConnectedText, "Batteria al {0}%." },
                    { NoticeDisconnectedTitle, "{0} disconnesso" },
                    { NoticeDisconnectedText, "Ultimo livello di batteria: {0}%." },
                    { TestNoticeTitle, "TaskbarBattery" },
                    { TestNoticeText, "Le notifiche funzionano." },
                    { RenameTitle, "Rinomina dispositivo" },
                    { RenamePrompt, "Nuovo nome per {0}:" },
                    { Save, "Salva" },
                    { Cancel, "Annulla" },
                    { RestoreName, "Ripristina originale" },
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
