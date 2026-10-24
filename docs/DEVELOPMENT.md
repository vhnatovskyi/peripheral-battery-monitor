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

`src/Agent` owns the Win32 message loop, monitoring, tray, widget and persistence without MAUI/WinUI dependencies. `src/Desktop` is the on-demand MAUI application: Views contains native pages and Services contains the IPC client, settings and releases. `src/Shared` holds models, the bounded JSON pipe protocol, metadata and single-instance coordination. `src/Windows` contains native tray/widget rendering shared with UI smoke tests. `src/Core` and `src/Devices` hold device providers and packet parsers. `src/UI/TrayBatteryPainter.cs` draws notification icons at their native pixel size. `src/App` retains the legacy interface and compatibility entry script, which starts the agent in a complete package. `src/Launcher` is the small .NET Framework compatibility launcher. `installer` handles per-user setup, upgrade and rollback. `assets/branding` holds the original application icon. `.github/workflows` validates and publishes tagged releases.

## Memory verification

Run `dotnet run --project tests/DiscoveryMemory/DiscoveryMemory.csproj -c Release` to measure managed allocation per Windows inventory scan after warm-up. Counts depend on the local hardware; compare on the same machine with the same connected devices.

Use `./scripts/Measure-Memory.ps1 -ProcessId <agent-or-desktop-process-id> -Seconds 30` for external process measurements. Compare a fresh background launch after the same warm-up, with the same widget mode, visible devices and theme. Measure again after opening settings, switching language/theme and closing the window. Only the agent should remain; while the dashboard is open, account for both processes. Private working set, private committed bytes and allocation rate are different metrics; attaching diagnostic tools can itself increase process memory. A zero last-collection counter before the first GC is not evidence of zero heap usage.

Both processes use workstation GC with `System.GC.ConserveMemory=9`; see Microsoft's [GC configuration documentation](https://learn.microsoft.com/dotnet/core/runtime-config/garbage-collector#conserve-memory). Do not use forced periodic collections, working-set trimming or arbitrary heap limits to make Task Manager numbers smaller. Keep live device monitoring enabled during measurements. Live localization does not load two complete application interfaces; translations are small compared with the MAUI/WinUI process and cached pages. The on-demand process boundary releases those resources on window closure without removing language switching.

`Test-MauiPackage.ps1` also runs an isolated agent test without device access, startup registration or production tray icons. For the optional native MAUI integration test on a desktop with Windows App Runtime installed, set `PBM_AGENT_TEST_ROOT` to a scratch folder and `PBM_AGENT_TEST_UI=1`, then launch the packaged agent with `--agent-self-test`. It creates synthetic readings, launches an isolated dashboard, changes settings through IPC, closes through the real window handler, verifies the final disk value and leaves the test agent responsive. Test readings must not be presented as hardware results.

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

Installed versions live under `%LOCALAPPDATA%\Programs\PeripheralBatteryMonitor\versions\X.Y.Z`. A validated current.txt pointer selects the active version. New files are staged before switching the pointer. The current and previous version folders are retained; older owned versions are removed, and settings live separately under `%LOCALAPPDATA%\PeripheralBatteryMonitor`. Installer restarts use a named exit event and the existing instance mutex rather than terminating arbitrary processes. Installer/update failures before activation leave the previous pointer intact; automatic health-check rollback is not implemented.
