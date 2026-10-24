## v0.4.6

- Draw charge digits directly on the native notification icon pixel grid, avoiding clipped or rescaled font glyphs. Preserve device colors and the segmented combined ring.
- Smooth text in device cards instead of relying on host text rendering defaults.
- Add a custom application icon to the window, Start shortcut and setup executable.
- Keep the MAUI project and build output out of legacy release archives.
- Include the v0.4.5 updater compatibility fix. If the old updater fails, download **PeripheralBatteryMonitor-Setup-0.4.6.exe** directly.

The full MAUI redesign is still a separate release. A Windows notification icon is a small square: detailed status for every device remains available in its tooltip and the main window.

## v0.4.5

- Fix update verification failing when Get-FileHash is unavailable in the host environment. SHA-256 is calculated directly with .NET cryptography.
- Extract verified ZIPs with the .NET compression API instead of depending on Expand-Archive module discovery.
- Restore the Windows PowerShell modules path for updater processes launched from another PowerShell/runtime environment.
- Add a checksum regression test that deliberately makes Get-FileHash unavailable.

Download **PeripheralBatteryMonitor-Setup-0.4.5.exe** if your current updater shows this error. Device settings and the larger, colored charge indicators are preserved. The MAUI redesign remains a separate release.
