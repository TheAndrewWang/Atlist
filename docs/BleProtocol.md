# Phone ↔ device Bluetooth protocol

What the Atlist app sends to a checklist device, and what the Arduino sketch must answer.
The app side lives in `Atlist/DeviceProtocol.cs` (message builders) and `Atlist/ChecklistBleService.cs` (transport). Change both this file and the code together.

## Transport

- HM-10 style BLE serial module: service `FFE0`, characteristic `FFE1` (write + notify).
- The device advertises a name starting with `Checklist-`, e.g. `Checklist-3F2A`.
- Plain ASCII, **one command per line, ending in `\n`**. The phone splits long lines into 20-byte writes, so the sketch must buffer bytes until it sees `\n`.
- Fields are separated by `|`. The app never puts `|` inside a field and only sends printable ASCII.
- **Every command gets exactly one reply line.** Unless noted, the reply is `OK`. Anything else is treated as a failure, and the app shows it to the caregiver.
- The phone connects, sends a few commands, and disconnects. It does not stay connected.
- Screen lines are at most 16 characters (16×2 LCD).

## Pairing

| Phone sends | Device does | Reply |
|---|---|---|
| `PAIR` | Shows a random 4-digit code on the LCD (the device must already be in PAIR MODE: button held 3 s) | `READY` |
| `CODE\|1234` | Checks the code | `OK` if it matches, anything else if not |
| `CANCEL` | Leaves pairing, back to normal screen | `OK` |

## Status

| Phone sends | Reply |
|---|---|
| `SCREEN?` | `SCREEN\|<line 1>\|<line 2>\|<color>`, the text currently on the LCD, e.g. `SCREEN\|TAKE MEDS\|With breakfast\|blue`. Color is optional. |

The Devices page sends this every 60 s while it's open, one device at a time.

## Messages

| Phone sends | Meaning |
|---|---|
| `MSG\|<line 1>\|<line 2>\|<keep>\|<beep>` | Show a message now. `keep`: `PRESS` = until the button is pressed, `60` = for 60 minutes, `NEXT` = until the next scheduled reminder. `beep`: `1` gentle beep on arrival, `0` silent. |

A message the phone can't deliver stays "Waiting" in the app. It is re-sent the next time the Devices page finds the device in range, or when the caregiver taps Retry in History. Duplicates are possible if the device showed a message but its `OK` was lost, so showing the same text twice should be harmless.

## Settings

| Phone sends | Meaning |
|---|---|
| `COLOR\|<name>` | Backlight color: `white`, `red`, `yellow`, `green`, `teal`, `blue`, `violet` |
| `RESET\|<hours>` | Reset cycle: the check-off clears after `6`, `12` or `24` hours |
| `PRIORITY\|<0\|1>` | `1` = beep when a scheduled reminder comes due |
| `REST\|<line 1>\|<line 2>` | Resting screen shown between messages, e.g. `REST\|TAKE MEDS\|Due 8:00 AM` |
| `TIME\|yyyy-MM-ddTHH:mm:ss` | Set the device clock to the phone's local time |
| `TEST` | Show a test pattern briefly (all colors / all cells), then return to the normal screen |

## Daily schedule

The phone sends the whole schedule for one device at once. The device should keep it in EEPROM, so reminders survive a power cut and work without the phone.

| Phone sends | Meaning |
|---|---|
| `SCHED\|CLEAR` | Delete every scheduled message |
| `SCHED\|ADD\|HH:mm\|<line 1>\|<line 2>\|<beep>` | Add one: show it every day at `HH:mm` (24-hour). Sent once per entry, after `CLEAR`. |

A full sync, from Schedule (pull down, or automatic after a change) or "Save to device" in device settings, sends, in order: `TIME`, `COLOR`, `RESET`, `PRIORITY`, `REST`, `SCHED|CLEAR`, then one `SCHED|ADD` per entry.
