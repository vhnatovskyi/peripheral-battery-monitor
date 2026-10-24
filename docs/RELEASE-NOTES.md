## v0.1.1

- Fix checksum-manifest downloads when GitHub serves the asset as binary content.
- One combined tray icon with separate green/blue battery rings, plus a lowest-percentage option.
- Device window with mouse and keyboard cards, connection status, last-report time and manual refresh.
- Settings for sign-in startup, low-battery notifications, threshold and update checks.
- AJAZZ AJ139 V2 MC battery via its 2.4 GHz receiver.
- Apple Magic Keyboard with Touch ID and Numeric Keypad (029F) battery via Bluetooth.
- Passive battery-change listeners; no periodic battery queries.
- Per-user Windows installer, portable ZIP and in-app release updates with SHA-256 validation.

Download **PeripheralBatteryMonitor-Setup-0.1.1.exe** to install, or extract the ZIP and run **PeripheralBatteryMonitor.exe**.

The binaries are unsigned. Windows may show an unknown-publisher notice. No third-party drivers or administrator rights are required. Settings and startup options can be changed from the dashboard.

Initial and manual battery reads were tested on both connected devices at 100%. Actual charge-change notifications have not yet been observed at a lower percentage, so firmware notification timing remains unconfirmed. AJ139 Bluetooth battery reading is unsupported. This initial version has no cloud account, telemetry, key logging or charging-state interpretation.
