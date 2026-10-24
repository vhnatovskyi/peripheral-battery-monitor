# Development

Requires Windows, .NET SDK 10, the `maui-windows` workload and Windows PowerShell 5.1 for installer compatibility tests.

```powershell
./scripts/Test.ps1
dotnet workload install maui-windows
dotnet build ./src/Desktop/PeripheralBatteryMonitor.Desktop.csproj
./scripts/Build-Maui.ps1
./tests/Test-MauiPackage.ps1 -OutputDirectory dist-maui
```

## Layout

`src/Desktop` is the MAUI application: Views contains native pages, Services handles device state/settings/releases, and Platforms/Windows contains native notification icons. `src/Core` and `src/Devices` hold shared device providers and packet parsers. `src/UI/TrayBatteryPainter.cs` draws notification icons at their native pixel size. `src/App` retains the 0.4.x interface and the compatibility entry script, which starts the MAUI executable in a complete package. `src/Launcher` is the small .NET Framework compatibility launcher. `installer` handles per-user setup, upgrade and rollback. `assets/branding` holds the original application icon. `.github/workflows` validates and publishes tagged releases.

## New providers

Implement Start, Snapshot, Refresh and Dispose. Keep I/O in a background worker, publish locked snapshots, restrict discovery to verified IDs/collections, and parse only known battery reports. Add a device card and tray segment. Never read keyboard keystroke collections or send one vendor's commands to another device. Add malformed-report tests and live connection/refresh tests. Document which physical battery changes have actually been observed.

## Releases

1. Update `version.json` using a three-part numeric version.
2. Update `docs/RELEASE-NOTES.md` and run Test/Build.
3. Commit and push main, then create and push the matching `vX.Y.Z` tag.
4. The Release workflow validates and publishes the compact portable ZIP, setup EXE and SHA256SUMS.txt. Package checks retain the legacy updater's 20 MiB download and 100 MiB extracted limits.

Publishing uses shared .NET 10 and Windows App Runtime 1.8, JSON source generation, no ReadyToRun duplication, and English/Ukrainian managed satellite resources. The installer detects prerequisites before changing the active version. Missing components are downloaded once from Microsoft: the .NET runtime ZIP is SHA-512 verified and the Windows App Runtime installer must have a valid Microsoft Authenticode signature. The .NET fallback is shared per user across application versions. Windows App SDK 1.8.260921001 requires Windows App Runtime 8000.994.2142.0 or later in the 1.8 family.

For native UI verification, set `PBM_UI_SMOKE_ROOT` to an absolute scratch folder and launch the built desktop EXE with `--ui-smoke`. It renders each native page with clearly synthetic test readings, writes PNGs and a result file, and exits. This mode never starts device I/O or modifies settings. These readings must never be presented as a live hardware test.

Application settings are stored in ``%LOCALAPPDATA%\PeripheralBatteryMonitor\settings.json``. Release-check results use ``release.json`` in the same directory. Neither file contains access tokens. Default settings disable sign-in startup, enable low-battery warnings at 20%, and allow one GitHub release check per day at startup.

Startup checks use GitHub's latest stable release API. Manual update/rollback selection lists up to 100 releases, filters draft/prerelease/incomplete packages, requires exactly named ZIP/checksum assets under this repository, verifies the ZIP hash and rejects path traversal or oversized archive entries. HTTPS and checksums protect transport/integrity; assets are not code-signed and the checksum is not an independent publisher signature.

Installed versions live under `%LOCALAPPDATA%\Programs\PeripheralBatteryMonitor\versions\X.Y.Z`. A validated current.txt pointer selects the active version. New files are staged before switching the pointer. Old version folders are retained, and settings live separately under `%LOCALAPPDATA%\PeripheralBatteryMonitor`. Installer restarts use a named exit event and the existing instance mutex rather than terminating arbitrary processes. Installer/update failures before activation leave the previous pointer intact; automatic health-check rollback is not implemented.
