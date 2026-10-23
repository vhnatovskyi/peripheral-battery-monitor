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
