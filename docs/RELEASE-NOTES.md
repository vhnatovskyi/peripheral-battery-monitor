## v0.4.5

- Fix update verification failing when Get-FileHash is unavailable in the host environment. SHA-256 is calculated directly with .NET cryptography.
- Extract verified ZIPs with the .NET compression API instead of depending on Expand-Archive module discovery.
- Restore the Windows PowerShell modules path for updater processes launched from another PowerShell/runtime environment.
- Add a checksum regression test that deliberately makes Get-FileHash unavailable.

Download **PeripheralBatteryMonitor-Setup-0.4.5.exe** if your current updater shows this error. Device settings and the larger, colored charge indicators are preserved. The MAUI redesign remains a separate release.
