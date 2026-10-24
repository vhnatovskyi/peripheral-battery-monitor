# Peripheral Battery Monitor

A lightweight Windows app for wireless-device battery status. One tray icon, a device dashboard, local settings, and updates from GitHub Releases. No vendor application or third-party device driver is required.

## Install

Download the **Setup EXE** from [the latest release](https://github.com/vhnatovskyi/peripheral-battery-monitor/releases/latest) and run it. Installation is per-user and does not require administrator rights. You can uninstall it through Windows **Installed apps**.

For portable use, download and extract the `win-x64.zip` release asset and run **PeripheralBatteryMonitor.exe**. Keep the extracted files together. Binaries are unsigned, so Windows may show an unknown-publisher notice.

Requires Windows 10/11 x64 with Windows PowerShell 5.1 and the built-in .NET Framework. No separate runtime download is needed. A background HID listener runs while the app is open.

## Supported devices

- **AJAZZ AJ139 V2 MC**, over its 2.4 GHz receiver (`A8A5:2255`). Green status ring.
- **Apple Magic Keyboard with Touch ID and Numeric Keypad**, over Bluetooth (`004C:029F`). Blue status ring.

Other models are not assumed compatible. AJ139 Bluetooth battery reading remains unsupported; the Apple keyboard's Bluetooth implementation is separate.

## Use

Double-click the tray icon to open the dashboard. Hover over it for both percentages. The dashboard shows device cards, connection status and the last battery-report time. Closing the dashboard keeps monitoring active; choose **Exit** in the tray menu to stop the app.

**Settings** lets you change the tray display (battery rings or lowest percentage), low-battery notifications and threshold, startup at Windows sign-in, and release checking. Startup is disabled by default. Settings are kept in your local Windows profile, independently of application versions.

The app queries each device at startup/reconnect or manual **Refresh batteries**, then listens for battery-change reports. It does not poll battery percentages every five minutes. Firmware controls notification timing; if a change is not reported, use Refresh. Zero percent is a valid value; missing data is displayed as unavailable rather than zero.

## Updates

By default, the app checks the latest stable GitHub release at startup, at most once per day, and notifies you when a newer version exists. **Check for updates** in the window or tray menu downloads and installs it after you choose to proceed. This restart preserves settings.

The updater validates the release's SHA-256 checksum, expected repository/download paths, package version, archive paths and size. New version files are staged before the active-version pointer changes. Previous version directories are retained. Checksums are integrity checks, not a code-signing certificate. Portable copies are installed into the normal per-user location when you accept an update.

## Repository layout

```text
src/
  App/         Dashboard, tray, settings and release updater
  Core/        Shared state and Windows HID discovery
  Devices/     AJ139 and Magic Keyboard providers and parsers
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

Both connected devices returned **100%** in live initial/manual queries. Parser tests cover unsolicited battery packets, zero/invalid percentages, truncated reports and unrelated messages. Installation tests cover a clean install, version upgrade, retention of the previous version and rejection of an incomplete package. Update tests cover version comparison, checksum mismatch and archive traversal.

A physical percentage drop has not yet been observed during validation, so unsolicited notification timing is not confirmed. A missing disconnect notification may leave the last reported value until Refresh/reconnection. Charging flags are not interpreted as charging-state UI. Bluetooth sleep behavior may vary by firmware. Automatic post-launch health-check rollback is not implemented.

The keyboard provider opens only its battery vendor collection, not the keystroke collection. No typed keys, telemetry or cloud account are collected. Network access is used only for GitHub release checks/downloads; automatic checks can be disabled.

## Protocol references

AJ139 packet layouts were identified from the JavaScript served by its [official web panel](https://139v2mc.yjx2012.com/), linked in the [model manual](https://ae01.alicdn.com/kf/S2bc3c9a125f44cde8b8ef113942b4b16T.pdf), and checked against the receiver. The Apple report format was cross-checked against [MagicKeyBattery](https://github.com/fixtan/MagicKeyBattery) and verified against the connected keyboard. The implementations here were written independently. This project is not affiliated with AJAZZ or Apple.
