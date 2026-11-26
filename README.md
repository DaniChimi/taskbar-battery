# TaskbarBattery

Shows the battery level of your wireless devices in the Windows notification area, whatever the brand.

[Italiano](README.it.md)

## How it works

- **Any Bluetooth device.** Windows already reads the battery of many Bluetooth devices (the standard GATT Battery
  Service) and keeps the last value as a device property. TaskbarBattery reads that property, so mice, keyboards,
  controllers and headsets that report their battery over Bluetooth work without vendor software.
- **Logitech devices, more detail.** Over Logitech's HID++ protocol the app also reads the charging state and a fresher
  level. It keeps the device's HID++ channel open and waits for the device's own battery notifications, so plugging or
  unplugging the cable shows up immediately, with no polling.
- Only connected devices are shown.

## The tray icon

One icon, for the device you pick or, automatically, the connected device with the lowest battery. Three styles,
from the right-click menu:

- **Battery**: the Windows battery outline, filled to the level; the battery with a bolt while charging.
- **Percentage**: the number; while charging, the digits fill up from the bottom, step by step.
- **Device and bar**: the device glyph with a level bar; while charging, the bar animates towards full.

With **One icon per device**, every connected device gets its own icon. The level turns red at the low-battery
threshold (20% by default). Click the icon for the panel with every connected device: a five-segment meter, the
percentage, the charging state (Logitech), the maker and how long it has been connected.

## Menu

- **Notifications**: low battery (once per discharge), fully charged (Logitech), device connected or disconnected,
  each on or off, with a low-battery threshold from 10% to 30% and a test notification.
- **Devices**: rename a device or hide it from the icon, the panel and the notifications.
- **Start with Windows**: adds the app to the per-user startup list.
- **Open diagnostic log**: `%LOCALAPPDATA%\TaskbarBattery\log.txt`, which records changes and errors only.

Tested with: Logitech MX Master 4 (Bluetooth), Xbox Wireless Controller (Bluetooth).

## Requirements

Windows 10 or 11. .NET Framework 4.x is preinstalled.

## Build

```
build.bat
```

It uses the C# compiler that ships with Windows; no SDK or IDE is needed.

## Troubleshooting

```
TaskbarBattery.exe --probe | Out-String
```

prints every device that reports a battery level, the Logitech HID collections, and what HID++ answers.
Settings are stored in `%APPDATA%\TaskbarBattery\settings.txt`.

## License

MIT
