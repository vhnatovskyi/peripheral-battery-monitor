# Receiver protocol and implementation

## Interface discovery

Enumerate present HID interfaces with Windows SetupAPI. Restrict candidates to USB VID `0xA8A5`, PID `0x2255`, HID usage page `0xFF01`, usage `0x0010`, and input/output report sizes of 65 bytes. The final size includes the report-ID byte. Mouse and keyboard input collections are not used.

The listener and writer have separate overlapped handles. This prevents a pending `FileStream.ReadAsync` from blocking a write on the same stream. The background read waits for receiver input rather than repeatedly querying the battery. Cancellation stops the read on Exit. UI access is isolated through locked snapshots.

## Read-only requests

All packets are 65 bytes, padded with zeros. Hex prefixes below include report ID `00`.

Check whether the mouse is online:

```text
00 55 ED 00 01 2E 00 00 00 ...
```

Read battery:

```text
00 55 30 A5 0B 2E 01 01 00 00 00 ...
```

No DPI, lighting, key assignment, pairing, macro, or firmware commands are sent.

## Incoming reports

Offsets are zero-based and include report ID at byte 0. The web application's DataView omits that byte, so its offsets are one lower.

- Online reply: byte 1 = `AA`, byte 2 = `ED`; byte 9 = `02` means online.
- Battery query reply: byte 1 = `AA`, byte 2 = `30`; percentage at byte 9, charge-status flag at byte 10.
- Unsolicited battery event: byte 1 = `AA`, byte 2 = `FA`, byte 9 = `D0`; percentage at byte 10, charge-status flag at byte 11.
- Other `AA FA` notifications can represent DPI/report-rate changes. They must not be parsed as battery data.

Values above 100 and truncated reports are rejected. Zero percent is valid. A query response is accepted only after the receiver has confirmed the mouse is online. A matching unsolicited battery event confirms a live mouse itself.

Example observed query reply at 100%, charge flag 0:

```text
00 AA 30 A5 0B 0A 01 01 00 64 00 ...
```

## State and refresh behavior

The worker starts with an unknown value. It listens for input immediately and sends an online/battery query pair once. Later battery notifications update the snapshot. A mouse-online transition requests current data again; USB removal clears state and retries local interface discovery every three seconds. Discovery sends no battery commands. Manual Refresh is serialized with other request pairs and uses a 2.5 second write timeout.

There is no periodic battery query timer. The UI's 100 ms timer reads the in-memory snapshot and redraws only when its version changes. All C# awaits avoid capturing the WinForms synchronization context.

## Validation and limitations

Tested: receiver discovery, live online status, a 100% battery reply, manual refresh, parser handling of battery notifications versus unrelated reports, malformed data, and zero/invalid percentages. A physical unsolicited change at a different charge level has not yet been observed. Firmware may vary; loss of a notification can leave the last reported value until a manual refresh. Bluetooth battery retrieval remains unsupported.

## Apple Magic Keyboard provider

Supported Bluetooth identity: VID/company 004C, PID 029F. Select vendor collection FF00 / usage 0014, with input size 3 and output size 0. Query report 0x90 using HidD_GetInputReport on a query-only handle. Observed packet: `90 00 64`, meaning 100% (byte 2, decimal range 0â€“100). The middle byte is not interpreted. The passive listener accepts only three-byte reports whose first byte is 90 and whose percentage is at most 100. It opens this battery vendor collection, not the separate keyboard input collection.

The keyboard provider has its own locked state and cancellation token. It queries once when a newly discovered interface is opened, and on manual Refresh; subsequent battery reports update the state without polling. Physical disconnection clears the percentage and restarts local interface discovery. Startup/manual queries were validated at 100%, and malformed/zero/unrelated reports are covered by parser tests. A real battery drop was not observed yet. If the keyboard does not push changes or signal a disconnect, the last reported value remains until manual refresh/reconnection.

## Windows fallback provider

`WindowsDeviceMonitor.cs` groups present Bluetooth/USB/HID nodes by DEVPKEY_Device_ContainerId, reads the documented System.Devices.BatteryLife property (49CD1F76-5626-4B17-A4E8-18B4AA1A2213 / 10), and checks the Windows Bluetooth battery-cache property (104EA319-6EE2-4701-BD47-8DDBF425BBE5 / 2). It accepts typed byte/DWORD values from 0 to 100, rejects unknown status 101 and missing properties, and excludes containers handled by specialized providers.

The worker refreshes Windows device properties and Classic/BLE association endpoints every three seconds. Container IDs combine both Bluetooth transports; any active endpoint confirms connection. Unknown/disconnected devices do not contribute current readings to the dashboard or tray.

The BLE worker reads the standard Battery Service (180F), Battery Level (2A19) at startup/reconnection/manual Refresh, then registers a ValueChanged handler and enables the Notify descriptor when available. This descriptor configures a standard battery subscription; no unknown vendor commands are sent. Notification handlers run in managed code because Windows PowerShell cannot directly subscribe to WinRT events. GATT services, event tokens and device objects are released at disconnect/exit.

AJ139 upward estimates while discharging require three consistent reports spanning at least 20 seconds. Downward values and charging-flag reports apply immediately; reconnection resets the baseline. This avoids temporary 97-to-100-to-97 fluctuations without permanently clamping the reported charge.