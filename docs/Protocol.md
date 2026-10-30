# Atlist BLE Protocol

How the caregiver app (C#), the BLE server (Python, UNO Q Linux side) and the sketch (C++, UNO Q MCU) talk to each other. All three must follow this document. If you change it, tell the team.

## Device name

Each device advertises as `Checklist-XXXX`, where `XXXX` is the last 4 hex digits of its own Bluetooth address, in uppercase. Example: address `AA:BB:CC:DD:3F:2A` → `Checklist-3F2A`.

The device makes this name itself. It has to advertise before the app has ever seen it, so the app can't assign it. The app shows this name until the caregiver gives the device a friendlier one.

## Service

UUID: `101ef69f-fe23-4d35-8a19-caa905720000`

## Characteristics

| Name    | UUID                                 | Direction      | Payload                |
|---------|--------------------------------------|----------------|------------------------|
| message | 101ef69f-fe23-4d35-8a19-caa905720001 | phone → device | `line1\|line2\|beep\|hold` |
| press   | 101ef69f-fe23-4d35-8a19-caa905720002 | device → phone | `count` |
| clock   | 101ef69f-fe23-4d35-8a19-caa905720003 | phone → device | `epoch\|offset` |
| config  | 101ef69f-fe23-4d35-8a19-caa905720004 | phone → device | `name\|rest1\|rest2\|resetHours\|highPriority` |

## Payloads

All payloads are plain ASCII text, with fields separated by `|`. Numbers are written as decimal digits, for example `12`, not raw bytes. Text is easier to debug: you can read it in a BLE scanner app.

### message (write)

Shows a reminder on the LCD.

| Field | Meaning | Values |
|-------|---------|--------|
| `line1` | Top LCD row | 0–16 characters |
| `line2` | Bottom LCD row | 0–16 characters |
| `beep` | Gentle beep when it arrives | `0` = off, `1` = on |
| `hold` | Keep on screen | `0` = until pressed, `1` = 1 hour, `2` = until next reminder |

Example: `Take meds|With breakfast|1|0`

### press (read + notify)

The device sends this when the button is pressed to check off the current task.

| Field | Meaning | Values |
|-------|---------|--------|
| `count` | Total presses since the device booted | `0` and up |

Example: `7`

The app notices a new press when the number goes up. A counter, not just "pressed", lets the app see how many presses it missed while it was disconnected.

### clock (write)

Sets the device's clock. The app sends it right after connecting, and when the caregiver taps **Sync clock**.

| Field | Meaning | Values |
|-------|---------|--------|
| `epoch` | Current time as Unix seconds (UTC) | e.g. `1793122440` |
| `offset` | Phone's time zone offset from UTC, in minutes | e.g. `-240` for EDT |

Example: `1793122440|-240`

The device needs `offset` to show local times like "Due 8:00 AM" on the resting screen.

### config (write)

Sends the device's settings from the Settings screen.

| Field | Meaning | Values |
|-------|---------|--------|
| `name` | Task name, e.g. "Take Meds" | 1–16 characters |
| `rest1` | Resting screen top row | 0–16 characters |
| `rest2` | Resting screen bottom row | 0–16 characters |
| `resetHours` | Reset cycle: check-off clears after this many hours | `1`–`168` |
| `highPriority` | Beep when a reminder comes due | `0` = off, `1` = on |

Example: `Take Meds|TAKE MEDS|Due 8:00 AM|12|1`

## Rules

- **Max text length:** 16 characters per text field, the width of the LCD.
- **Allowed characters:** printable ASCII only (codes 32–126: letters, digits, space and punctuation), **except `|`**, because it separates fields.
- **Both sides check:**
  - **The app** blocks bad input before sending (`MaxLength="16"`, and it refuses `|` and non-ASCII characters such as emoji), so the caregiver sees what's wrong.
  - **The device** checks every write again, because it can't trust what it receives. If a payload has the wrong number of fields, a text field over 16 characters, a disallowed character, or a number out of range, the device **rejects the whole write** and changes nothing. The BLE write fails, so the app gets an error and can tell the caregiver.

## Bridge (Linux ↔ MCU)

On the UNO Q, the Python BLE server runs on the **Linux** side and the C++ sketch runs on the **MCU**. They talk through Arduino **Bridge** RPC. This section is the contract between the Python track and the firmware track.

> The exact Bridge library calls (`Bridge.provide`, `Bridge.notify`, `Bridge.call`, include and import names) must be **checked against the Arduino UNO Q / App Lab Bridge docs and examples**. That's step 1 of both device tracks. The names and arguments below are the team's agreement and don't depend on the library.

### Python → sketch (the sketch provides these)

| Name | Args | Returns | What the sketch does |
|------|------|---------|----------------------|
| `show` | `line1: str, line2: str, beep: int, hold: int` | `bool` (accepted) | Shows the message. Beeps once if `beep == 1`. Handles `hold` (see below). |
| `rest` | `line1: str, line2: str` | `bool` | Sets the resting screen text. Shows it right away if no message is on screen. |
| `pair_show` | `passkey: str` | `bool` | Shows `PAIR MODE` on line 1 and the 6-digit passkey on line 2. |
| `pair_end` | none | `bool` | Leaves pair mode and goes back to the message or resting screen. |
| `clear_done` | none | `bool` | Clears the "done" check-off when a reset cycle ends. |

### Sketch → Python (the sketch sends these as notifications)

| Name | Args | When |
|------|------|------|
| `pressed` | `count: int` | Debounced short press. `count` is the total since boot, the same number as the BLE `press` payload. |
| `pair_hold` | none | The button was held for 3 seconds. Python then enters pair mode. |

### Who handles what

- **Validation:** Python checks every BLE payload against the Rules above **before** calling the sketch. The sketch still cuts anything over 16 characters so the LCD never wraps, but it doesn't reject.
- **`hold` timing:** the sketch owns it.
  - `0` (until pressed): keep the message until the button is pressed, then show the resting screen.
  - `1` (1 hour): keep it until pressed or 60 minutes pass, whichever comes first. `millis()` is accurate enough for this.
  - `2` (next reminder): keep it until the next `show` call.
- **Time, schedule and reset cycles:** Python owns them, because the Linux side has the real clock and storage. The sketch only reacts to `show`, `rest` and `clear_done`.
- **Press count:** the sketch counts and Python forwards it. Both reset to 0 on reboot.

## Pairing

Pairing uses standard BLE **passkey pairing**, done by BlueZ on the UNO Q and by the phone's own system dialog. The link is encrypted after pairing.

1. The caregiver holds the device's button for 3 seconds.
2. The device enters pair mode: it starts advertising and lets new phones pair.
3. The app scans and lists every `Checklist-XXXX` device in pair mode.
4. The caregiver taps **Pair**. BlueZ generates a random **6-digit** passkey, for example `481732`, and the device shows `PAIR MODE` and the passkey on its LCD.
5. The phone shows its system pairing dialog, and the caregiver types in the passkey from the LCD. A wrong passkey fails the pairing.
6. After pairing, the caregiver names the device in the app, and the app sends `config`.

Rules:
- All four characteristics require an **encrypted (paired) connection**. An unpaired phone can see the device but can't read or write anything.
- Pair mode ends after a successful pairing or after **2 minutes**, whichever comes first.
- The passkey has 6 digits because that's what the BLE standard uses. Design.pdf shows 4, so the Pair screen and LCD mockups need updating.

## Future contract changes

Agree these with all three tracks before anyone builds them:
- **Schedule:** a characteristic to send the reminder list (time, lines, beep, hold) to the device. Design.pdf says the schedule is "Saved on each device".
- **Status:** a way for the app to read whether the current task is done, and when the device last reset.
