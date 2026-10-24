## v0.4.1

- Fix the release picker failing with "Cannot locate the history for command line Releases and rollback". Translation helpers no longer collide with PowerShell's built-in aliases.
- Add UI regression tests covering Ukrainian/English release dialogs, release notes and rollback selection while preserving the system history alias.
- Simplify the dashboard: keep the application name in the title bar, remove the duplicate heading/description/footer and show only Devices with a connection count above the cards.
- Use horizontal, monochrome battery tray symbols inspired by Windows 11, including the combined four-device layout.
- Show the disconnect action only for Bluetooth devices with a known target address. Hide it on receiver/unsupported cards, center a separated-plug symbol and preserve keyboard accessibility.
- Resolve disconnect addresses from background discovery data, avoiding additional device enumeration on the UI thread.
- Fix disposing a painted device card by releasing the button's image reference before disposing the image. Keep dashboard smoke tests independent of physical device I/O.

Download **PeripheralBatteryMonitor-Setup-0.4.1.exe**, or use the app's update action. The release selector supports deliberate rollback to older compatible releases with checksum verification.
