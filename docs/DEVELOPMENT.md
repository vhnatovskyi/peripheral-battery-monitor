# Development

Requires Windows PowerShell 5.1 and Windows' .NET Framework compiler; no SDK/package installation is needed.

```powershell
./scripts/Test.ps1
./scripts/Build.ps1
./Start.cmd
```

## Layout

`src/Core` contains shared device-state snapshots and Windows HID discovery. `src/Devices` contains isolated device providers and packet parsers. `src/App` contains the tray UI, settings and updater. `src/Launcher` contains the native Windows entry point. `installer` contains setup, per-user installation and uninstall logic. `tests` covers packet handling and release validation. `scripts` contains validation/build commands. `docs` contains protocol details and release notes. `.github/workflows` validates main/PRs and publishes tagged releases.

## New providers

Implement Start, Snapshot, Refresh and Dispose. Keep I/O in a background worker, publish locked snapshots, restrict discovery to verified IDs/collections, and parse only known battery reports. Add a device card and tray segment. Never read keyboard keystroke collections or send one vendor's commands to another device. Add malformed-report tests and live connection/refresh tests. Document which physical battery changes have actually been observed.

## Releases

1. Update `version.json` using a three-part numeric version.
2. Update `docs/RELEASE-NOTES.md` and run Test/Build.
3. Commit and push main, then create and push the matching `vX.Y.Z` tag.
4. The Release workflow validates, builds a portable ZIP and self-contained setup EXE, generates SHA256SUMS.txt, and publishes all three assets.

Application settings are stored in ``%LOCALAPPDATA%\PeripheralBatteryMonitor\settings.json``. Release-check results use ``release.json`` in the same directory. Neither file contains access tokens. Default settings disable sign-in startup, enable low-battery warnings at 20%, and allow one GitHub release check per day at startup.

The updater uses GitHub's latest stable release API, requires exactly named ZIP/checksum assets under this repository, verifies the ZIP hash and rejects path traversal or oversized archive entries. HTTPS and checksums protect transport/integrity; assets are not code-signed and the checksum is not an independent publisher signature.

Installed versions live under `%LOCALAPPDATA%\Programs\PeripheralBatteryMonitor\versions\X.Y.Z`. A validated current.txt pointer selects the active version. New files are staged before switching the pointer. Old version folders are retained, and settings live separately under `%LOCALAPPDATA%\PeripheralBatteryMonitor`. Installer restarts use a named exit event and the existing instance mutex rather than terminating arbitrary processes. Installer/update failures before activation leave the previous pointer intact; automatic health-check rollback is not implemented.
