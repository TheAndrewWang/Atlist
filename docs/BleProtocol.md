# Phone-to-device Bluetooth protocol

This document describes the text protocol used by the MAUI app and the checklist device firmware. App-side command builders are in `Atlist/DeviceProtocol.cs`; BLE transport is in `Atlist/ChecklistBleService.cs`. Update this document and the firmware together when changing the wire format.

## Transport

- HM-10-style BLE serial module: service `FFE0`, characteristic `FFE1` (write and notify).
- The device advertises a name beginning with `Checklist-`, such as `Checklist-3F2A`.
- Commands are printable ASCII, one command per line, terminated by `\n`. The phone splits writes into 20-byte chunks; firmware must buffer chunks until the newline.
- Fields are separated by `|`. App-generated display fields are cleaned to printable ASCII, exclude `|`, and are limited to 16 characters.
- The app expects one reply line per command. Most commands expect `OK`; a non-`OK` reply is reported as a failure. `PAIR` and `SCREEN?` have their own reply formats below.
- The app connects for a short operation and disconnects afterward; it does not keep a persistent connection.

## Pairing

| Phone sends | Device action | Reply |
| --- | --- | --- |
| `PAIR` | Show a random four-digit code. Device must already be in pair mode (button held for 3 seconds). | `READY` |
| `CODE|1234` | Validate the displayed code. | `OK` if it matches; otherwise an error reply. |
| `CANCEL` | Exit pairing and return to the normal screen. | `OK` |

## Status

| Phone sends | Reply |
| --- | --- |
| `SCREEN?` | `SCREEN|<line 1>|<line 2>|<color>` describing the current LCD text. Color may be omitted. |

The Devices page requests status every 60 seconds while open, one device at a time.

## Messages

`MSG|<line 1>|<line 2>|<keep>|<beep>` displays a caregiver message.

- `keep`: `PRESS` means display until the button is pressed; `60` means display for 60 minutes; `NEXT` means display until the next scheduled reminder.
- `beep`: `1` requests a gentle beep on arrival; `0` is silent.

If delivery fails, the app keeps the message in a Waiting state and retries when the device is found or when the caregiver taps Retry in History. A reply may be lost after the device displays the message, so duplicate delivery is possible; firmware should make displaying the same text twice harmless.

## Settings

| Command | Meaning |
| --- | --- |
| `COLOR|<name>` | Backlight color: `white`, `red`, `yellow`, `green`, `teal`, `blue`, or `violet`. |
| `RESET|<hours>` | Clear the check-off after `6`, `12`, or `24` hours. |
| `PRIORITY|<0|1>` | `1` enables a beep when a scheduled reminder is due. |
| `REST|<line 1>|<line 2>` | Set the resting screen shown between messages. |
| `TIME|yyyy-MM-ddTHH:mm:ss` | Set the device clock from the phone's local time. No timezone offset is sent. |
| `TEST` | Briefly show a test pattern, then return to the normal screen. |

## Daily schedule

The phone sends the complete schedule for one device. Firmware should store it in non-volatile memory so reminders continue when the phone is away.

| Command | Meaning |
| --- | --- |
| `SCHED|CLEAR` | Delete all scheduled messages. |
| `SCHED|ADD|HH:mm|<line 1>|<line 2>|<beep>` | Add a daily message at a 24-hour local time, such as `08:05`. |

A full sync sends `TIME`, `COLOR`, `RESET`, `PRIORITY`, `REST`, `SCHED|CLEAR`, then one `SCHED|ADD` for each enabled entry assigned to that device or to all devices. Since clearing and adding are separate commands, an interrupted sync can leave a partial schedule; the app can retry the full sync when the device is reachable again.

## Firmware behavior to agree on

- Whether reset intervals are rolling durations or fixed clock boundaries.
- How local time behaves after timezone changes, daylight-saving transitions, and loss of power.
- Whether duplicate commands are safe to repeat, especially schedule additions and message display.
- How unsupported commands and malformed fields are reported in the reply line.
