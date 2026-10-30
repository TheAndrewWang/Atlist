# Track: BLE server (Python, UNO Q Linux side)

**Owner:** _____
**Branch:** `ble-server`
**Code lives in:** `device/`, the UNO Q app folder shared with the firmware track. Step 0 confirms the exact layout App Lab wants.
**Builds against:** `docs/Protocol.md`: all of it, because this track sits in the middle
**Tests alone with:** **nRF Connect** (a free phone app that can read and write any BLE characteristic) in place of the C# app, and a `FakeBridge` in place of the sketch

## What this program does

It's the device's Bluetooth side. The UNO Q's Bluetooth is on its Linux side, not the MCU where the sketch runs. So this program:
1. **Advertises** as `Checklist-XXXX`, so phones can find it.
2. **Accepts writes** to `message`, `clock` and `config`, **checks them** against the Protocol.md rules, and passes them to the sketch over Bridge.
3. **Sends notifications** on `press` when the sketch reports a button press.
4. **Runs pairing:** the passkey goes to the LCD.
5. **Owns time:** the clock, reset cycles and, later, the schedule.

## Setup

- **The code runs on the UNO Q**, which is a small Linux computer with BlueZ (Linux's Bluetooth stack). Edit on your own Windows or Linux machine. Run on the board, through App Lab or over SSH.
- `bless` is a Python BLE *server* library built on BlueZ. Install it on the UNO Q in step 0. If it can't do what's needed, `dbus-next` with raw BlueZ D-Bus is the fallback.
- `pytest` for step 2. Those tests are plain Python, so they run on any machine.
- nRF Connect on your phone.
- The UNO Q is shared with the firmware track, so agree on times.

---

## Steps

### [ ] 0. Get Python and BLE running on the UNO Q (do this first)
- **Why first:** the whole design assumes the UNO Q's Linux side can run a BLE server. If it can't, the team needs to know this week.
- **Do:**
  - Find out how Python runs on the UNO Q: through App Lab (possibly in a container) or directly over SSH. Check that it can reach BlueZ: `bluetoothctl show`.
  - Run the smallest `bless` example on the board: one service, one readable characteristic.
  - Read the **Arduino UNO Q Bridge docs and examples** for Python: the import, how to call a function the sketch provides, and how to receive a notification from the sketch. Write the exact calls into the progress log, and share them with the firmware track.
  - With the firmware owner, agree the `device/` folder layout App Lab needs for the Python and the sketch. Write it in the README's track table.
- **Checkpoint:** nRF Connect sees the UNO Q advertising and reads a value. If not, write down what blocked it and tell the team.

### [ ] 1. Advertise and expose the service
- **Concepts:**
  - GATT: a **service** is a group of **characteristics**, the "mailboxes" from Protocol.md.
  - Each characteristic has properties: read, write, notify.
  - Advertising is how a device announces "I exist" before anyone connects.
- **Build:** `ble_server.py`:
  - Advertise as `Checklist-XXXX` (last 4 hex digits of the Bluetooth address, uppercase).
  - Add the service and the four characteristics with the UUIDs from Protocol.md: `message`, `clock` and `config` as write, and `press` as read + notify, starting at `b"0"`.
  - `print()` every write.
- **Checkpoint:** nRF Connect lists `Checklist-XXXX`, shows the four characteristics, and a write shows up in your terminal.

### [ ] 2. Parse and validate payloads (no Bluetooth needed)
- **Concepts:** keep the rules apart from the Bluetooth code so you can test them with pytest on any machine. Raise an exception for bad input, then turn it into a BLE write error.
- **Build:** `protocol.py`:
  - `parse_message(data: bytes) -> Message` (`line1`, `line2`, `beep`, `hold`)
  - `parse_clock(data: bytes) -> Clock` (`epoch`, `offset_minutes`)
  - `parse_config(data: bytes) -> Config` (`name`, `rest1`, `rest2`, `reset_hours`, `high_priority`)
  - `encode_press(count: int) -> bytes`
  - Each parser raises `ProtocolError` for any of these:
    - the wrong field count
    - a text field over 16 characters
    - a non-ASCII character or `|`
    - a number out of range

    Use dataclasses for the results.
- **Build:** `test_protocol.py`: the Protocol.md examples, plus one test per rejection rule.
- **Checkpoint:** `pytest` passes. A bad write from nRF Connect is rejected (nRF Connect shows an error), and a good one prints the parsed object.

### [ ] 3. The Bridge interface and a fake
- **Concepts:** program against an interface, so you can swap the real thing for a fake and test without the sketch.
- **Build:** `bridge.py`:
  - A class with the Protocol.md Bridge calls: `show(line1, line2, beep, hold)`, `rest(line1, line2)`, `pair_show(passkey)`, `pair_end()`, `clear_done()`, plus a way to register handlers for `pressed(count)` and `pair_hold()`.
  - `FakeBridge`: prints each call. Typing `p` + Enter pretends to be a button press, and `h` a 3-second hold.
- **Checkpoint:** writing a message in nRF Connect prints `show('Drink water', 'Glass by sink', 1, 0)`.

### [ ] 4. Press notifications
- **Concepts:** notify: the server pushes a new value to subscribed phones, so they don't have to keep asking.
- **Build:** when the Bridge reports `pressed(count)`, set `press` to `encode_press(count)` and notify.
- **Checkpoint:** in nRF Connect, subscribe to `press`, type `p`, and see `1`, `2`, `3`…

### [ ] 5. Clock and config
- **Build:**
  - `clock` writes set an offset between the phone's time and the system time, used for "local now". Don't change the system clock.
  - `config` writes call `bridge.rest(rest1, rest2)` and save the config to `state.json`. Write a temporary file, then rename it over the old one, so a power cut can't leave a half-written file.
  - Load `state.json` at startup and send the resting screen.
- **Checkpoint:** restart the server and the resting screen comes back from `state.json`.

### [ ] 6. Real Bridge (M1, with the firmware track)
- **Build:** `RealBridge`, using the Bridge calls you found in step 0. Choose `FakeBridge` or `RealBridge` with a command-line flag.
- **Checkpoint (M1):** writing `Drink water|Glass by sink|1|0` from nRF Connect shows it on the LCD with a beep. Pressing the real button sends a `press` notification.

### [ ] 7. Passkey pairing
- **Concepts:** BLE pairing and encryption. A BlueZ **agent** is the D-Bus object BlueZ asks for passkeys. With a "display only" agent, BlueZ generates the passkey and tells your code.
- **Build:**
  - Make the characteristics require an encrypted connection (Protocol.md Pairing rules).
  - On `pair_hold()`, become pairable for 2 minutes.
  - When BlueZ hands the agent a passkey, call `bridge.pair_show(passkey)`.
  - When pairing succeeds or the 2 minutes run out, call `bridge.pair_end()`.
  - `bless` may not do agents. If so, use `dbus-next` for the agent alone.
- **Checkpoint:** the phone shows its pairing dialog. The passkey from the LCD works and a wrong one fails. An unpaired phone can't write `message`.

### [ ] 8. Reset cycles
- **Build:** every `reset_hours` from the last reset, call `bridge.clear_done()`. Save the last reset time in `state.json` so a reboot doesn't lose it.
- **Checkpoint:** with `reset_hours` set to 1, `clear_done()` fires on time, and again after a restart.

### [ ] 9. Run on boot
- **Build:** start the server on power-up and restart it if it crashes, using App Lab's own mechanism or a systemd service.
- **Checkpoint:** unplug the UNO Q and plug it back in. It advertises without anyone logging in.

### [ ] 10. Schedule (waits until the team adds the schedule section to Protocol.md)

---

## Progress log

Newest entry first. At the end of each session, write what you finished, what's half-done, and the next action.

- **2026-10-30:** Track reset. The code now runs on the UNO Q, so no laptop setup is needed. **Next:** step 0 on the UNO Q.
