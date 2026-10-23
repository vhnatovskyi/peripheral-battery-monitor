# AJ139 V2 MC Battery Indicator

Windows tray battery percentage for the **AJAZZ AJ139 V2 MC**, using its 2.4 GHz USB receiver. No AJAZZ application, installation, third-party driver, or downloaded runtime is required. The indicator itself runs as a small background PowerShell process and compiles its included C# source with Windows' built-in .NET Framework.

## Run

1. Download or clone this repository and keep the files together.
2. Connect the USB receiver and switch the mouse to **2.4G**.
3. Double-click `Start-AJ139-Battery.cmd`.
4. Look for a green number near the clock. If Windows hides it, open **˄** and drag it onto the taskbar.

Right-click the icon for **Refresh battery** or **Exit**. Double-click the icon to refresh manually. A second launch does not create a second indicator. Startup at Windows sign-in is not enabled automatically.

Requires Windows 10/11, Windows PowerShell 5.1, and the built-in .NET Framework. Tested on Windows 11 with receiver `VID_A8A5 / PID_2255`. Other mouse models and firmware versions are not confirmed.

## Automatic updates

The indicator continuously listens to incoming HID reports. When the receiver sends a battery-change notification, the displayed value updates automatically. It **does not periodically query the battery**, including every five minutes.

An initial request obtains the current value at launch or after the USB receiver reconnects. Mouse-online notifications trigger a refresh when the mouse reconnects. Manual Refresh sends the same read-only requests. The UI checks the already received state every 100 ms; this timer sends no commands to the mouse and redraws only after a state change.

Updates depend on notifications emitted by the mouse/receiver firmware. If firmware fails to report a change, use Refresh. Live query and manual refresh were tested at 100%; parsing of unsolicited change notifications is covered by tests and the vendor web application's handler, but a physical battery percentage drop was not observed during validation.

## Display

- **0–100**, green: reported battery percentage above 20%.
- **0–20**, orange/red: reported battery percentage at or below 20%.
- **?**, gray: no confirmed current battery value, receiver missing, or mouse offline.

The charge-status byte is retained in the state but is not interpreted as a charging indicator because its complete semantics have not been validated.

## Bluetooth limitation

Bluetooth battery reading is **not supported** by this implementation. The tested mouse exposes no standard Bluetooth Battery Service (`0x180F`). Its Bluetooth HID collections expose no feature reports, and accessible GATT values did not provide a battery percentage. The USB command cannot simply be reused over Bluetooth. No firmware update or writes to the proprietary Bluetooth service are attempted.

When the USB receiver reports that the mouse is offline, its cached battery response is ignored instead of showing that old value as a Bluetooth reading. If firmware fails to notify a disconnect, the previous reported value may remain until a manual refresh or receiver removal.

## Files

- `AJ139-Battery.ps1`: tray UI, rendering, single-instance protection, and lifecycle.
- `Aj139Monitor.cs`: HID discovery, background listener, read-only query requests, and report parsing.
- `Start-AJ139-Battery.cmd`: launches Windows PowerShell with a hidden window. Its process-local execution-policy option does not change the computer's saved policy.
- `Test-Protocol.ps1`: parser regression tests; no mouse required.
- `PROTOCOL.md`: observed packet structure and design details.

Run tests:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-Protocol.ps1
```

## Sources

Packet layouts were identified by inspecting the JavaScript served by the [official AJ139 V2 MC web panel](https://139v2mc.yjx2012.com/), linked in the [model's manual](https://ae01.alicdn.com/kf/S2bc3c9a125f44cde8b8ef113942b4b16T.pdf), and then testing read-only requests against the physical receiver. This project is an independent implementation and is not affiliated with AJAZZ.
