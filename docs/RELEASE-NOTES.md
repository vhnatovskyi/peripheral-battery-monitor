## v0.5.1

- Show the application version in the window title next to the system window controls.
- Disable window maximization and reduce the default window and device-card sizes.
- Stop device icon flicker by retaining the existing native image source during charge/status updates.
- Retain the compact framework-dependent deployment from v0.5.0.

## v0.5.0 — Native MAUI desktop application

- Replace the PowerShell/WinForms dashboard with a native .NET MAUI Windows interface, including adaptive device cards and dedicated Settings, Updates and About pages.
- Preserve AJ139 receiver, Magic Keyboard and Windows device providers; read Bluetooth Battery Service data and notifications directly in the application process.
- Keep device colors, separate or segmented combined tray icons, four-device tooltips, low battery alerts and per-user startup settings.
- Disconnect supported Bluetooth devices directly from their card while retaining pairing.
- Select newer or older releases, verify SHA-256 checksums and install or roll back from the Updates page.
- Include a custom application icon, Ukrainian and English, and developer contact links.
- Use a compact package with shared .NET 10 and Windows App Runtime 1.8 components. Setup and portable startup detect missing components and offer a one-time Microsoft download. Subsequent app updates reuse them.

Download **PeripheralBatteryMonitor-Setup-0.5.0.exe** or the **win-x64.zip** asset. This is the separate MAUI release; settings from 0.4.x are retained. The compact app package is about 18 MiB; runtime downloads, if required, are additional.

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
