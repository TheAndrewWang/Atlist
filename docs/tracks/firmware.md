# Track: Device firmware (C++ / Arduino, UNO Q MCU)

**Owner:** _____
**Branch:** `firmware`
**Code lives in:** `device/Atlist.ino`, the existing button + LCD + beeper test, which this track grows. `device/` is the UNO Q app folder shared with the BLE server track.
**Builds against:** `docs/Protocol.md`: the **Bridge** section and the Rules
**Tests alone with:** a tiny Python test script on the UNO Q's Linux side (or Serial commands) in place of the BLE server

## What this program does

It drives the hardware. It has no Bluetooth and no real clock; it does what the Linux side tells it over Bridge, and reports the button:
- shows messages and the resting screen on the 16×2 LCD
- beeps
- reads the button and tells Python about presses and 3-second holds
- handles the "keep on screen" timing

## Hardware (from `Atlist.ino`)

| Part | Connection |
|------|-----------|
| 16×2 LCD, I2C | address `0x27` |
| Button | pin 5, `INPUT`, reads HIGH when pressed |
| Piezo speaker | pin 8, 1200 Hz, 150 ms beep |

## Setup

- **Arduino App Lab**, or `arduino-cli` with the UNO Q (Zephyr) core.
- The `LiquidCrystal_I2C` library.
- The UNO Q is shared with the Python track, so agree times.

---

## Steps

### [ ] 0. Blink, then the existing sketch
- **Concepts:** the UNO Q has two processors. The **MCU** runs this sketch and talks to the pins; the **Linux** side has Bluetooth and storage.
- **Do:** flash Blink, then flash the current `Atlist.ino`.
- **Checkpoint:** the LCD shows `waiting` / `Presses: 0`, and each press beeps and counts up.

### [ ] 1. Find the Bridge API (read before writing)
- **Do:** read the **Arduino UNO Q Bridge docs and examples**:
  - the include line for the sketch
  - how the sketch **provides** a function Linux can call (e.g. `Bridge.provide`)
  - how the sketch **sends** an event to Linux (e.g. `Bridge.notify`)
  - what argument types are allowed (`String`, `int`?)
  - how to run a tiny Python script on the Linux side that calls the sketch

  Write the exact calls into the progress log. The Python track needs them too, so share them.
  - With the BLE server owner, agree the `device/` folder layout App Lab needs for the sketch and the Python. Move `Atlist.ino` to match, and write the layout in the README's track table.
- **Checkpoint:** a sample provides `ping()`, and a Python one-liner on the Linux side calls it and prints the reply.

### [ ] 2. Split the sketch into functions
- **Concepts:** turn the test sketch into small functions with one job each, so Bridge can call them.
- **Build:** in `Atlist.ino`:
  - `void showLines(const String& l1, const String& l2)`: clears the LCD and prints both lines, **cut to 16 characters**.
  - `void beep()`
  - `void showRest()`: shows the saved resting lines (start with `"Atlist"` / `"Ready"`).
  - Keep the debounce code, but move the press handling into `void onPress()`.
- **Checkpoint:** behaves as in step 0, but the code is in functions. A 20-character test string is cut to 16 and doesn't wrap.

### [ ] 3. Provide `show` and `rest`
- **Build:** following the Protocol.md Bridge table:
  - `show(line1, line2, beep, hold)`: shows the message and beeps if `beep == 1`. Store `hold` for step 5.
  - `rest(line1, line2)`: saves the resting lines and shows them right away if no message is on screen.
- **Checkpoint:** calling `show("Drink water", "Glass by sink", 1, 0)` from a Linux test script shows it and beeps.

### [ ] 4. Send `pressed` and `pair_hold`
- **Concepts:** telling a short press from a long hold. Time the press from when it starts, and decide on release (or once 3 seconds pass).
- **Build:**
  - Short press: increase the count, call `notify("pressed", count)`.
  - Held 3 seconds: call `notify("pair_hold")` once, and don't count it as a press.
- **Checkpoint:** the Linux test script prints `pressed 1`, `pressed 2`… and `pair_hold` for a long hold.

### [ ] 5. Keep-on-screen (`hold`) and the done state (M1, with the Python track)
- **Concepts:** a small **state machine**: the device is always in one state (Resting, ShowingMessage or PairMode), and events move it between them. Use `millis()` for timers, **never `delay()`**, which freezes the button.
- **Build:** per Protocol.md "Who handles what":
  - `hold 0`: a press goes back to the resting screen.
  - `hold 1`: a press **or** 60 minutes goes back.
  - `hold 2`: stay until the next `show`.
  - After a press, mark the task **done** (e.g. a ✓ or `*` on the resting screen). `clear_done()` clears it.
- **Checkpoint (M1):** with the Python server, writing a message from nRF Connect shows it on the LCD with a beep. The button press reaches the phone as a notification, and the screen behaves per `hold`.

### [ ] 6. Pair mode screen
- **Build:** `pair_show(passkey)` switches to PairMode and shows `PAIR MODE` / the 6-digit passkey. `pair_end()` goes back to the message or resting screen. Ignore presses while in PairMode.
- **Checkpoint:** a Linux test call shows `PAIR MODE` / `481732`, and `pair_end()` brings back the resting screen.

### [ ] 7. Feedback under 200 ms and edge cases
- **Concepts:** physical feedback must come in under 200 ms. The user must never wait for Bluetooth to feel a press work.
- **Build:**
  - Beep (or change the screen) on the press **itself**, before or regardless of what Linux does.
  - Double presses: decide what a second press on an already-done task does, and write it in Protocol.md.
  - If Linux hasn't sent `rest` yet after a reboot, show a sensible default.
- **Checkpoint:** a press with the Linux side stopped still beeps and shows done.

### [ ] Stretch: backlight color (needs an RGB LCD or an RGB LED)

---

## Progress log

Newest entry first. At the end of each session, write what you finished, what's half-done, and the next action.

- **2026-10-30:** Track reset: the sketch moved to `device/Atlist.ino`, and the branch is `firmware`. **Next:** step 0.
- **2026-10-30:** Track created. `Atlist.ino` already reads the button with a 25 ms debounce, beeps and counts presses. **Next:** step 0.
