# Peripheral Battery Monitor

A Windows app built with **.NET MAUI** for wireless-device battery status. Native device cards, combined or separate tray indicators, local settings, and verified releases from GitHub. No vendor application or third-party device driver is required.

## Install

Download the **Setup EXE** from [the latest release](https://github.com/vhnatovskyi/peripheral-battery-monitor/releases/latest) and run it. Installation is per-user and does not require administrator rights. You can uninstall it through Windows **Installed apps**.

For portable use, download and extract the **`win-x64.zip`** release asset and run **PeripheralBatteryMonitor.exe**. Keep the extracted files together. Binaries are unsigned, so Windows may show an unknown-publisher notice.

Requires Windows 10 (build 19041+) or Windows 11 x64. The compact package uses shared **.NET 10 Runtime x64** and **Windows App Runtime 1.8 x64**. Setup and the portable launcher detect these components and offer to install missing ones once, directly from Microsoft. Existing components are reused. The .NET fallback is installed per user. Windows PowerShell 5.1 and the built-in .NET Framework support the compatibility launcher and installer; device monitoring runs in the native application process.

## Supported devices

- **AJAZZ AJ139 V2 MC**, over its 2.4 GHz receiver (`A8A5:2255`). Green battery icon.
- **Apple Magic Keyboard with Touch ID and Numeric Keypad**, over Bluetooth (`004C:029F`). Blue battery icon.

Other Bluetooth and USB/HID devices are discovered automatically. Their percentages appear when Windows exposes usable battery data; proprietary protocols still require a device provider. The app cannot infer a receiver's radio frequency from a generic USB interface. AJ139 Bluetooth battery reading remains unsupported; the Apple keyboard's Bluetooth implementation is separate.

## Automatic device discovery

All connected devices use the same compact cards, with device-type icons and Bluetooth/receiver/USB connection symbols. Cards appear and disappear automatically. The window fits the current number of cards; additional pages replace scrolling when the display is full.

Appearance follows the Windows theme by default. Settings also offers explicit light and dark themes. Notification icon captions adapt to the taskbar background independently of the application's selected theme.

Windows device nodes and Classic/BLE association endpoints are grouped by their physical container ID. A device is connected when any of its Bluetooth endpoints is active, so an inactive Classic endpoint cannot hide an active BLE connection. Connection information is checked every three seconds. Disconnected or unconfirmed devices are excluded from the dashboard, tray minimum and alerts.

Generic BLE devices exposing the standard Battery Service (180F) and Battery Level characteristic (2A19) are read at connection and manual Refresh. The app subscribes to battery notifications when supported; it does not periodically query the battery. Devices without notifications retain their last report until Refresh/reconnection. Other devices use available Windows battery properties. Proprietary receiver protocols still need dedicated providers.

Live validation confirmed an Xbox Wireless Headset over BLE, including a successful battery read and notification registration. An inactive paired Xbox controller is correctly excluded. This does not imply universal compatibility with every device.

## Use

Double-click the tray icon to open the dashboard. Separate tray icons use device-colored horizontal batteries with a percentage below each battery, inspired by Windows 11. The combined icon is a full-size circle with up to four device-colored battery sections and the lowest known charge number in its center. Its tooltip lists all four devices with battery levels and connection types. The dashboard shows device cards, connection status and the last battery-report time. **About** includes the app version, developer, contact email, LinkedIn and repository links. Closing the dashboard keeps monitoring active; choose **Exit** in the tray menu to stop the app.

**Settings** groups language (Ukrainian/English), combined/separate tray mode, battery symbols or lowest percentage, notifications, Windows startup and release checking. The window uses a standard Windows toolbar. Startup is disabled by default. Settings are kept in your local Windows profile, independently of application versions.

Bluetooth cards with a known target address show a centered unplug icon that requests Windows to disconnect that remote address while keeping its pairing. Unsupported/receiver cards hide this action. A driver may reject the request or reconnect automatically; the app offers Windows Bluetooth settings on failure. It does not disable the Bluetooth adapter or forget devices.

Enable the **Always-on-top widget** in Settings and save to show device battery levels above desktop windows. Adjust **Transparency** from 0% to 80% and save to apply it. The widget follows the application theme, updates as devices report data, and remembers its position separately from other settings. To reposition it, choose **Save and move widget** and drag its header. Switching to any other application, including a windowed game, automatically locks movement and restores click-through; returning to the app does not unlock it. Disable the toggle and save to remove the widget. Desktop overlays may be covered by games using exclusive fullscreen mode.

The app queries each device at startup/reconnect or manual **Refresh batteries**, then listens for battery-change reports. It does not poll battery percentages every five minutes. Firmware controls notification timing; if a change is not reported, use Refresh. Zero percent is a valid value; missing data is displayed as unavailable rather than zero.

## Updates

By default, the app checks the latest stable GitHub release at startup, at most once per day, and notifies you when a newer version exists. **Updates** opens a release picker. Select a newer version, reinstall the current one or roll back to an older compatible release. Downloads are verified before installation and the app restarts with settings retained. Older versions may not support newer settings or features.

The updater validates the release's SHA-256 checksum, expected repository/download paths, package version, archive paths and size. New version files are staged before the active-version pointer changes. Previous version directories are retained. Checksums are integrity checks, not a code-signing certificate. Portable copies are installed into the normal per-user location when you accept an update.

## Repository layout

```text
src/
  App/         Dashboard, tray, settings and release updater
  Core/        Shared state and Windows HID discovery
  Devices/     Dedicated HID providers, Windows discovery and BLE worker
  UI/          Drawn device cards and connection/device icons
  Launcher/    Native Windows entry point
installer/     Self-contained setup and install/uninstall scripts
scripts/       Build and validation commands
tests/         Protocol, updater and installation regression tests
docs/          Protocol, development guide and release notes
.github/       Validation and release workflows
version.json   Release version and repository metadata
```

## Build and develop

```powershell
./scripts/Test.ps1
./scripts/Build.ps1
./Start.cmd
```

Build output goes into `dist/`: portable ZIP, setup EXE and SHA256SUMS.txt. A matching `vX.Y.Z` tag triggers a tested GitHub release. See [development and release instructions](docs/DEVELOPMENT.md) and [device protocol details](docs/PROTOCOL.md).

## Validation and limits

Both specialized devices returned **100%** in live initial/manual queries. Parser tests cover unsolicited battery packets, zero/invalid percentages, truncated reports and unrelated messages. Installation tests cover a clean install, version upgrade, retention of the previous version and rejection of an incomplete package. Update tests cover version comparison, checksum mismatch and archive traversal.

Device firmware controls notification timing. The AJ139 listener has received unsolicited battery reports in live validation. Transient upward battery estimates while discharging are filtered: an increase must persist for at least three reports over 20 seconds, while decreases and explicitly flagged charging reports apply immediately. A missing disconnect notification may leave the last reported value until Refresh/reconnection. Charging flags are not interpreted as charging-state UI. Bluetooth sleep behavior may vary by firmware. Automatic post-launch health-check rollback is not implemented.

The keyboard provider opens only its battery vendor collection, not the keystroke collection. No typed keys, telemetry or cloud account are collected. Network access is used only for GitHub release checks/downloads; automatic checks can be disabled.

## Protocol references

AJ139 packet layouts were identified from the JavaScript served by its [official web panel](https://139v2mc.yjx2012.com/), linked in the [model manual](https://ae01.alicdn.com/kf/S2bc3c9a125f44cde8b8ef113942b4b16T.pdf), and checked against the receiver. The Apple report format was cross-checked against [MagicKeyBattery](https://github.com/fixtan/MagicKeyBattery) and verified against the connected keyboard. The implementations here were written independently. This project is not affiliated with AJAZZ or Apple.

## Developer

Developed by **Vladyslav Hnatovskyi** ([GitHub](https://github.com/vhnatovskyi), [LinkedIn](https://www.linkedin.com/in/vladyslav-hnatovskyi/)). Contact: [vhnatovskyi@hotmail.com](mailto:vhnatovskyi@hotmail.com).
