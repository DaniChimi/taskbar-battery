using System.Collections.Generic;
using System.Globalization;

namespace TaskbarBattery
{
    // User-facing text, one dictionary per language. The UI language follows Windows; English is the fallback.
    static class Strings
    {
        // Flyout and tray.
        public const string Title = "title";
        public const string NoDevices = "noDevices";
        public const string NoDevicesHint = "noDevicesHint";
        public const string Percent = "percent";
        public const string ReportedToday = "reportedToday";
        public const string ReportedOn = "reportedOn";
        public const string UpdatedNow = "updatedNow";
        public const string UpdatedAt = "updatedAt";
        public const string TooltipLine = "tooltipLine";
        public const string TooltipCharging = "tooltipCharging";
        public const string ReadError = "readError";
        public const string Charging = "charging";
        public const string FullyCharged = "fullyCharged";
        public const string ConnectedFor = "connectedFor";
        public const string UnderAMinute = "underAMinute";
        public const string Minutes = "minutes";
        public const string Hours = "hours";
        public const string Days = "days";

        // Right-click menu.
        public const string SettingsMenu = "settingsMenu";
        public const string Refresh = "refresh";
        public const string Exit = "exit";

        // Settings window.
        public const string SettingsTitle = "settingsTitle";
        public const string SectionTrayIcon = "sectionTrayIcon";
        public const string StyleTitle = "styleTitle";
        public const string StyleDescription = "styleDescription";
        public const string StyleBattery = "styleBattery";
        public const string StylePercent = "stylePercent";
        public const string StyleDeviceShort = "styleDeviceShort";
        public const string OneIconPerDevice = "oneIconPerDevice";
        public const string OneIconDescription = "oneIconDescription";
        public const string ShownDevice = "shownDevice";
        public const string ShownDeviceDescription = "shownDeviceDescription";
        public const string AutomaticLowest = "automaticLowest";
        public const string Notifications = "notifications";
        public const string NotifyLow = "notifyLow";
        public const string NotifyLowDescription = "notifyLowDescription";
        public const string NotifyFull = "notifyFull";
        public const string NotifyFullDescription = "notifyFullDescription";
        public const string NotifyConnect = "notifyConnect";
        public const string NotifyDisconnect = "notifyDisconnect";
        public const string LowThreshold = "lowThreshold";
        public const string LowThresholdDescription = "lowThresholdDescription";
        public const string TestNotification = "testNotification";
        public const string TestNotificationDescription = "testNotificationDescription";
        public const string Send = "send";

        public const string RenameTip = "renameTip";
        public const string HideTip = "hideTip";
        public const string ShowTip = "showTip";
        public const string HiddenTag = "hiddenTag";

        public const string SectionGeneral = "sectionGeneral";
        public const string StartWithWindows = "startWithWindows";
        public const string StartWithWindowsDescription = "startWithWindowsDescription";
        public const string DiagnosticLog = "diagnosticLog";
        public const string DiagnosticLogDescription = "diagnosticLogDescription";
        public const string Open = "open";

        // Notifications.
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

        // Rename dialog.
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
                    { Title, "Devices" },
                    { NoDevices, "No device connected" },
                    { NoDevicesHint, "Turn on a Bluetooth device that reports its battery" },
                    { Percent, "{0}%" },
                    { ReportedToday, "{0:HH:mm}" },
                    { ReportedOn, "{0:d}" },
                    { UpdatedNow, "Updated just now" },
                    { UpdatedAt, "Updated at {0:HH:mm}" },
                    { TooltipLine, "{0}: {1}%" },
                    { TooltipCharging, "{0}: {1}% (charging)" },
                    { ReadError, "Could not read the devices" },
                    { Charging, "Charging" },
                    { FullyCharged, "Fully charged" },
                    { ConnectedFor, "connected for {0}" },
                    { UnderAMinute, "under a minute" },
                    { Minutes, "{0} min" },
                    { Hours, "{0} h" },
                    { Days, "{0} d" },

                    { SettingsMenu, "Settings…" },
                    { Refresh, "Refresh now" },
                    { Exit, "Exit" },

                    { SettingsTitle, "TaskbarBattery settings" },
                    { SectionTrayIcon, "Tray icon" },
                    { StyleTitle, "Icon style" },
                    { StyleDescription, "How the icon looks next to the clock" },
                    { StyleBattery, "Battery" },
                    { StylePercent, "Percentage" },
                    { StyleDeviceShort, "Device" },
                    { OneIconPerDevice, "One icon per device" },
                    { OneIconDescription, "A separate icon for each connected device" },
                    { ShownDevice, "Device shown" },
                    { ShownDeviceDescription, "The device the single icon follows" },
                    { AutomaticLowest, "Automatic (lowest battery)" },
                    { Notifications, "Notifications" },
                    { NotifyLow, "Battery low" },
                    { NotifyLowDescription, "Once per discharge, at the threshold below" },
                    { NotifyFull, "Fully charged" },
                    { NotifyFullDescription, "For devices that report charging (Logitech)" },
                    { NotifyConnect, "Device connected" },
                    { NotifyDisconnect, "Device disconnected" },
                    { LowThreshold, "Low battery threshold" },
                    { LowThresholdDescription, "At or below this level the battery turns red" },
                    { TestNotification, "Test notification" },
                    { TestNotificationDescription, "Check that Windows shows the notifications" },
                    { Send, "Send" },
                    { RenameTip, "Rename" },
                    { HideTip, "Hide from the icon and the panel" },
                    { ShowTip, "Show again" },
                    { HiddenTag, "hidden" },
                    { SectionGeneral, "General" },
                    { StartWithWindows, "Start with Windows" },
                    { StartWithWindowsDescription, "Open TaskbarBattery when you sign in" },
                    { DiagnosticLog, "Diagnostic log" },
                    { DiagnosticLogDescription, "Changes and errors, for troubleshooting" },
                    { Open, "Open" },

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
                    { Title, "Dispositivi" },
                    { NoDevices, "Nessun dispositivo connesso" },
                    { NoDevicesHint, "Accendi un dispositivo Bluetooth che comunica la batteria" },
                    { Percent, "{0}%" },
                    { ReportedToday, "{0:HH:mm}" },
                    { ReportedOn, "{0:d}" },
                    { UpdatedNow, "Aggiornato adesso" },
                    { UpdatedAt, "Aggiornato alle {0:HH:mm}" },
                    { TooltipLine, "{0}: {1}%" },
                    { TooltipCharging, "{0}: {1}% (in carica)" },
                    { ReadError, "Impossibile leggere i dispositivi" },
                    { Charging, "In carica" },
                    { FullyCharged, "Carica completa" },
                    { ConnectedFor, "connesso da {0}" },
                    { UnderAMinute, "meno di un minuto" },
                    { Minutes, "{0} min" },
                    { Hours, "{0} h" },
                    { Days, "{0} g" },

                    { SettingsMenu, "Impostazioni…" },
                    { Refresh, "Aggiorna ora" },
                    { Exit, "Esci" },

                    { SettingsTitle, "Impostazioni di TaskbarBattery" },
                    { SectionTrayIcon, "Icona nell'area di notifica" },
                    { StyleTitle, "Stile dell'icona" },
                    { StyleDescription, "Come appare l'icona accanto all'orologio" },
                    { StyleBattery, "Batteria" },
                    { StylePercent, "Percentuale" },
                    { StyleDeviceShort, "Dispositivo" },
                    { OneIconPerDevice, "Un'icona per dispositivo" },
                    { OneIconDescription, "Un'icona separata per ogni dispositivo connesso" },
                    { ShownDevice, "Dispositivo mostrato" },
                    { ShownDeviceDescription, "Il dispositivo che l'icona segue" },
                    { AutomaticLowest, "Automatico (batteria più bassa)" },
                    { Notifications, "Notifiche" },
                    { NotifyLow, "Batteria scarica" },
                    { NotifyLowDescription, "Una volta per ciclo di scarica, alla soglia qui sotto" },
                    { NotifyFull, "Carica completa" },
                    { NotifyFullDescription, "Per i dispositivi che comunicano la carica (Logitech)" },
                    { NotifyConnect, "Dispositivo connesso" },
                    { NotifyDisconnect, "Dispositivo disconnesso" },
                    { LowThreshold, "Soglia di batteria scarica" },
                    { LowThresholdDescription, "Da questo livello in giù la batteria diventa rossa" },
                    { TestNotification, "Notifica di prova" },
                    { TestNotificationDescription, "Controlla che Windows mostri le notifiche" },
                    { Send, "Invia" },
                    { RenameTip, "Rinomina" },
                    { HideTip, "Nascondi dall'icona e dal pannello" },
                    { ShowTip, "Mostra di nuovo" },
                    { HiddenTag, "nascosto" },
                    { SectionGeneral, "Generale" },
                    { StartWithWindows, "Avvia con Windows" },
                    { StartWithWindowsDescription, "Apre TaskbarBattery quando accedi" },
                    { DiagnosticLog, "Log diagnostico" },
                    { DiagnosticLogDescription, "Cambiamenti ed errori, per capire i problemi" },
                    { Open, "Apri" },

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
