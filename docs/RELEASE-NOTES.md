## v0.2.0

- Automatically discover additional Bluetooth and USB/HID devices through Windows' local device/battery properties.
- Add an Other devices list, physical-container deduplication and an option to show devices without battery data.
- Distinguish cached/unconfirmed battery readings from confirmed connections; never use disconnected or unconfirmed data for generic low-battery alerts.
- Include confirmed additional devices in the combined tray indicator.
- Keep dedicated AJ139 and Apple Magic Keyboard providers for their verified protocols.
- Add discovery validation and a dashboard smoke test, including rendering many tray segments.
- Preserve the one-icon dashboard, settings, per-user installer and checksum-validated release updates.

Download **PeripheralBatteryMonitor-Setup-0.2.0.exe** to install, or extract the ZIP and run **PeripheralBatteryMonitor.exe**. Existing installations can use **Check for updates**.

Generic discovery refreshes the Windows cache every 15 seconds; it does not send periodic battery commands to devices. Not every receiver exposes battery data. Direct generic BLE GATT subscriptions are not implemented yet. AJ139 battery remains supported over 2.4 GHz only; an additional Bluetooth Input 0x90 test was rejected on all its HID collections.

The binaries are unsigned. No third-party driver or administrator rights are needed. Physical battery-change notification timing is not yet validated at a lower charge percentage. There is no key logging or telemetry.
