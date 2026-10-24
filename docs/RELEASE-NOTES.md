## v0.5.10

- Activate the running application when launching again. Check the shared instance before loading MAUI or PowerShell; keep one monitor across installation folders and versions, and allow restart after an unexpected exit.
- Apply all settings immediately, including device visibility, tray mode, theme, language and widget transparency. Remove the Save button and confirmation dialog; rename the positioning action to Move widget.
- Allow background transparency from 0% to 100%; at 100% the widget background disappears while text and battery bars remain visible.
- Coalesce automatic settings writes for 250 ms while sliders move, flush pending settings on exit, and show persistence errors in Settings. Avoid rebuilding Settings for each switch or slider change.
- Create Settings, Updates and About pages only when opened; skip hidden-window UI refreshes while keeping device monitoring, tray and widget live.

## v0.5.9

- Add independent per-device visibility controls for the widget and tray in Settings. Keep hidden devices on the main Devices page and remember choices across restarts and reconnections. Newly discovered devices are visible by default.
- Keep a neutral application entry in the tray when all battery indicators are hidden, so the app can still be opened.
- Embed the application icon directly in the installer window and executable.
- Include developer name, contact, version, copyright and project/profile URLs in application, launcher and installer file metadata. Windows displays URLs as text in file comments; clickable links remain available in About.

## v0.5.8

- Ask where to install the application before extracting or installing it. Include an editable destination, Browse, Cancel and installation progress, with Ukrainian and English labels.
- Prefill an existing installation folder for reinstallations and retain the selected location for updates, shortcuts, enabled startup and uninstall.
- Accept custom folder names, spaces and Ukrainian characters. Reject drive roots, unrelated nonempty directories, nested installations and directory links.
- Validate ownership and registration before uninstalling a custom location. Keep settings in the existing user profile.
- Releases before v0.5.8 do not support custom installation folders; rollback to those releases is blocked when using a custom location.

## v0.5.7

- Smooth widget text and charge numbers with four-times-density text rendering and alpha-safe reduction to the actual screen pixel size.
- Retain desktop background transparency, fully opaque text interiors, rounded corners and the frame-free window from v0.5.6.

## v0.5.6

- Fix the widget appearing black instead of transparent on the desktop by presenting per-pixel alpha through a dedicated native layered popup.
- Remove the native window frame and panel outline, including the white border.
- Keep names, percentages and battery bars opaque; preserve positioning, click-through protection and device colors.
- Test real desktop composition over a separate two-color window at three opacity levels in both themes, rather than relying only on offscreen rendering.

## v0.5.5

- Apply widget transparency to the background only; keep names, percentages and battery bars fully opaque.
- Remove the widget heading and explanatory caption to reduce height. After explicitly enabling positioning, drag anywhere on the widget.
- Preserve click-through protection, automatic locking when switching applications, and saved position.
- Add a Ukrainian user guide in the repository Wiki.

## v0.5.4

- Add an optional always-on-top battery widget with live device levels, device colors, light/dark themes, and remembered screen position.
- Enable or disable it in Settings. Use **Save and move widget** to drag its header; switching to any other application locks it again.
- Adjust widget transparency from 0% to 80% in Settings; preserve the value across restarts.
- Keep the widget click-through by default, including over windowed games. It does not take focus or add a taskbar button. Exclusive fullscreen applications may cover desktop overlays.
- Retain compact shared-runtime packaging; existing installations reuse their runtime components.

## v0.5.3

- Place the About headline and version next to the application icon in a compact, vertically centered header.
- Match native window caption button contrast to the selected application theme.
- Place charge and update time beside device names in shorter cards; fit the window height to the current page and device count.
- Remove unused icon spacing from flyout navigation and align labels to the left.

## v0.5.2

- Give the combined indicator an opaque dark center and stable colored device sections; remove digit outlines that obscured the ring.
- Remove dark pixel halos from separate battery captions and strengthen the colored battery outline.
- Normalize transparent application-icon margins and embed the larger mark in the native executable as well as the launcher.
- Refresh launcher icon resources during installation and upgrade.
- Add system, light and dark appearance modes, including appropriate contrast for notification icons on a light taskbar.
- Replace refresh text with a toolbar icon and tooltip, add disconnect hover feedback, and further compact the device dashboard.

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
