## v0.4.0

- Replace the device table with compact cards that automatically appear and disappear, device/connection icons and pages instead of scrolling.
- Use a standard Windows toolbar docked at the top of the window.
- Redesign Settings into aligned General, Tray icons and Notifications sections with dark dropdowns.
- Add Ukrainian/English switching and an About dialog with developer/contact links.
- Show up to four battery symbols and four status lines in the combined tray icon, or a separate icon for every connected device.
- Add generic Bluetooth Battery Service reading and notifications; correctly combine Classic/BLE connection states. Xbox Wireless Headset was verified live.
- Filter transient upward AJ139 charge estimates while discharging, preserving immediate decreases and explicit charging reports.
- Add an accessible disconnect icon on Bluetooth device cards. It requests a Windows disconnect while preserving pairing; drivers may reject it or the device may reconnect. Receiver connections are excluded.
- Add a release picker supporting updates and deliberate rollback to older compatible releases, retaining checksum/package validation and local settings.

Download **PeripheralBatteryMonitor-Setup-0.4.0.exe**, or extract the portable ZIP. Existing installations can use the update action in the tray menu. Older releases may lack newer settings and features.

Validation covers protocol parsing and battery stability, dual-mode Bluetooth aggregation, release filtering, dynamic cards, system toolbar configuration, both tray modes, settings layout, installation, upgrade and rollback. Physical disconnects were not performed on the user's active input devices during validation. AJ139 battery support remains limited to its verified 2.4 GHz protocol.
