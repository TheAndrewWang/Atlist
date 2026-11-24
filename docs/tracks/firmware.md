# Track: Device firmware (C++ / Arduino, UNO Q MCU)

**Owner:** Ethan
**Branch:** `firmware`
**Code lives in:** `device/sketch/sketch.ino` (was `device/Atlist.ino`), the existing button + LCD + beeper test, which this track grows. `device/` is shared with the BLE server track (see the layout in `docs/tracks/README.md`). Test helpers: `device/ping/ping.ino` + `device/ping_test.py` check the Bridge link on their own; `device/lcd_test/` scans the I2C bus and checks the LCD on its own.
**Flashing:** the repo's `device/sketch/sketch.ino` is the source of truth. Edit it only in the repo, then copy it into the App Lab app and flash from the terminal: `cp device/sketch/sketch.ino ~/ArduinoApps/test1/sketch/sketch.ino` then `arduino-app-cli app restart ~/ArduinoApps/test1` (shows build errors; App Lab's Run button once failed silently). Never edit the App Lab copy. Also copy `device/sketch/sketch.yaml` over the app's `sketch/sketch.yaml`: App Lab only compiles with the libraries listed there (Bridge + `LiquidCrystal I2C (1.1.2)`).
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
| 16×2 LCD, I2C | address `0x27`; SDA/SCL on the **SDA/SCL pins past AREF** (D20/D21, the `Wire` bus), GND, **5V**. A4/A5 are a different bus (`Wire2`) on the UNO Q and won't work with this library |
| Button | pin 5 to GND, `INPUT_PULLUP` (internal pull-up, no resistor), reads LOW when pressed |
| Piezo speaker | pin 8, 1200 Hz, 150 ms beep |

## Setup

- **Arduino App Lab**, or `arduino-cli` with the UNO Q (Zephyr) core.
- The `LiquidCrystal_I2C` library.
- The UNO Q is shared with the Python track, so agree times.

---

## Steps

### [x] 0. Blink, then the existing sketch
- **Concepts:** the UNO Q has two processors. The **MCU** runs this sketch and talks to the pins; the **Linux** side has Bluetooth and storage.
- **Do:** flash Blink, then flash the current `Atlist.ino`.
- **Checkpoint:** the LCD shows `waiting` / `Presses: 0`, and each press beeps and counts up.

### [x] 1. Find the Bridge API (read before writing)
- **Do:** read the **Arduino UNO Q Bridge docs and examples**:
  - the include line for the sketch
  - how the sketch **provides** a function Linux can call (e.g. `Bridge.provide`)
  - how the sketch **sends** an event to Linux (e.g. `Bridge.notify`)
  - what argument types are allowed (`String`, `int`?)
  - how to run a tiny Python script on the Linux side that calls the sketch

  Write the exact calls into the progress log. The Python track needs them too, so share them.
  - With the BLE server owner, agree the `device/` folder layout App Lab needs for the sketch and the Python. Move `Atlist.ino` to match, and write the layout in the README's track table.
- **Checkpoint:** a sample provides `ping()`, and a Python one-liner on the Linux side calls it and prints the reply.

### [x] 2. Split the sketch into functions
- **Concepts:** turn the test sketch into small functions with one job each, so Bridge can call them.
- **Build:** in `device/sketch/sketch.ino`:
  - `void showLines(const String& l1, const String& l2)`: clears the LCD and prints both lines, **cut to 16 characters**.
  - `void beep()`
  - `void showRest()`: shows the saved resting lines (start with `"Atlist"` / `"Ready"`).
  - Keep the debounce code, but move the press handling into `void onPress()`.
- **Checkpoint:** behaves as in step 0, but the code is in functions. A 20-character test string is cut to 16 and doesn't wrap.

### [x] 3. Provide `show` and `rest`
- **Build:** following the Protocol.md Bridge table:
  - `show(line1, line2, beep, hold)`: shows the message and beeps if `beep == 1`. Store `hold` for step 5.
  - `rest(line1, line2)`: saves the resting lines and shows them right away if no message is on screen.
- **Checkpoint:** calling `show("Drink water", "Glass by sink", 1, 0)` from a Linux test script shows it and beeps.

### [x] 4. Send `pressed` and `pair_hold`
- **Concepts:** telling a short press from a long hold. Time the press from when it starts, and decide on release (or once 3 seconds pass).
- **Build:**
  - Short press: increase the count, call `notify("pressed", count)`.
  - Held 3 seconds: call `notify("pair_hold")` once, and don't count it as a press.
- **Checkpoint:** the Linux test script prints `pressed 1`, `pressed 2`… and `pair_hold` for a long hold.

### [x] 5. Keep-on-screen (`hold`) and the done state (M1, with the Python track)
- **Concepts:** a small **state machine**: the device is always in one state (Resting, ShowingMessage or PairMode), and events move it between them. Use `millis()` for timers, **never `delay()`**, which freezes the button.
- **Build:** per Protocol.md "Who handles what":
  - `hold 0`: a press goes back to the resting screen.
  - `hold 1`: a press **or** 60 minutes goes back.
  - `hold 2`: stay until the next `show`.
  - After a press, mark the task **done** (e.g. a ✓ or `*` on the resting screen). `clear_done()` clears it.
- **Checkpoint (M1):** with the Python server, writing a message from nRF Connect shows it on the LCD with a beep. The button press reaches the phone as a notification, and the screen behaves per `hold`.

### [x] 6. Pair mode screen
- **Build:** `pair_show(passkey)` switches to PairMode and shows `PAIR MODE` / the 6-digit passkey. `pair_end()` goes back to the message or resting screen. Ignore presses while in PairMode.
- **Checkpoint:** a Linux test call shows `PAIR MODE` / `481732`, and `pair_end()` brings back the resting screen.

### [x] 7. Feedback under 200 ms and edge cases
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

- **2026-11-24:** Step 7 **done**. Boot default (Ethan's choice): until the first `rest`, the screen shows `Atlist Ready` / `No task loaded` (`No task loaded...` is 17 chars, too long) and short presses are ignored with **no beep** (Ethan: silence is right when no task is loaded). Sketch: `taskLoaded` flag, set in `setRest()`; `onPress()` returns early while it is false. `pair_hold` and `show` still work before `rest`, so a new device can still be paired. All four checks passed (boot screen, silent tap, `rest`, tap gives the done screen). **Protocol.md changed** again ("Before the first `rest`" rule): Python must send `rest` when it starts, or the button never works; check this with Kevin. Both Protocol.md changes from today need **all three owners** to agree before merging into `main`. Still open: done is lost on reboot (Protocol change needed); the 60-minute hold 1 timeout is untested; pair mode is untested with real BLE pairing. **Next:** commit, get the team's OK on the Protocol.md changes, then open a PR into `main`. Step 0 ticked too: its hardware check (LCD, button, beep) is covered by the 11-20 and 11-24 tests, though its `waiting` / `Presses: 0` checkpoint describes the old sketch. After that, only the stretch goal (backlight colour) is left on this track.

- **2026-11-24:** Step 7 **half-done**: double-press and done-screen decisions made and tested. Ethan's design (**one device per task**, e.g. a TAKE MEDS device by the medicine): after a press the resting screen shows `✓ <rest line1>` / `Already done` (`DONE_LINE2`) until `clear_done` (the next reset cycle). Further presses beep and still send `pressed`, so the caregiver sees repeat presses. A `show` never clears done (a caregiver message must not make the meds look not taken), and messages never show the `✓`; `withDone()` was removed, and `drawScreen()` picks the done screen. All six checks passed on the board (`rest`, tap, tap again, `show` with no `✓`, tap back to the done screen, `clear_done`). **Protocol.md changed** (new "Done state" rule under "Who handles what"): **Kevin and the app owner must agree** before it is merged into `main`. Open issue: done lives only in MCU memory, so a reboot mid-day forgets it and shows TAKE MEDS again (risk of a second dose); fixing it needs Python to resend the done state, a Protocol change. Idea to raise: the `show` field `hold` is easy to confuse with the button hold (`pair_hold`). **Checkpoint passed:** with no Linux listener (sketch running, `listen_test.py` and the BLE server stopped), every press beeps and shows `Already done`. Note: stopping the app in App Lab stops the sketch too (no beep, LCD keeps its last text), so it is not a valid test. **Next:** commit; then the last step 7 item, a sensible default screen if `rest` hasn't arrived after a reboot.

- **2026-11-24:** Real **button and piezo wired and tested**. Button: option B, pin 5 to GND through a breadboard (button legs across the center gap in rows 10/12; D5 wire at 12h and GND at 10c reach diagonal legs, so the wires are never shorted by a breadboard strip). Sketch: `pinMode(BUTTON_PIN, INPUT_PULLUP)` (no resistor), so the logic is flipped: `stableState`/`lastReading` start `HIGH` (released) and `readButton()` treats `LOW` as pressed. `HAS_BUTTON = true`; hardware table updated. Piezo (passive) on pin 8 to GND sounds on every `beep()`, so `tone()` works on the UNO Q core. Checked with the serial monitor plus `listen_test.py`: taps gave `pressed 1..4` (one per tap, no bounce), each with a beep and the `✓`; a hold over 3 s gave only `pair_hold`, with no press on release. The 3 s hold timing is now tested on real hardware. Still untested: the 60-minute hold 1 timeout; pair mode with real BLE pairing. Open decision from step 5 still open (should a new `show` while done keep the `✓`?). **Next:** commit step 6 + button changes, then step 7 (feedback under 200 ms, double presses, default screen after reboot).

- **2026-11-22:** Step 6 **done**, checkpoint passed: `show_test.py pair_show 481732` shows `PAIR MODE` / `481732`, and `pair_end` brings back the resting screen. Sketch: `PAIR_MODE` added to `enum State`; `pairShow(key)` saves `stateBeforePair` (only when entering, so a second `pair_show` with a new key just updates `passkey`); `pairEnd()` restores it, so it returns to the message **or** the resting screen. `onPress()` returns early in pair mode (no count, beep, `pressed` or done); a hold still sends `pair_hold`. A `show` during pair mode is kept (`stateBeforePair = SHOWING_MESSAGE`) and appears after `pair_end`. `pair_end` outside pair mode is a harmless no-op. No pair-mode timeout in the sketch: Python owns the 2-minute limit and calls `pair_end`. `show_test.py` can call `pair_show KEY` / `pair_end`. Not yet tried with real BLE pairing (the BLE server does not use `pair_show` yet). **Next:** step 7, feedback under 200 ms and edge cases.
- **2026-11-22:** Step 5 **done**, **M1 checkpoint passed** with the BLE server (`ble_server.py --bridge real`, ble-server commit `ed60676`) and nRF Connect on an iPhone (device `Checklist-F279`): a `config` write changed the resting screen, a `message` write showed on the LCD with `*beep*`, and a typed `p` returned to `✓ TAKE MEDS` and updated the phone's `press` value. Sketch: state machine `enum State { RESTING, SHOWING_MESSAGE }` (PAIR_MODE comes in step 6); every event updates state, then `drawScreen()` draws from it. `done` is set by **any** short press (also from the resting screen) and cleared only by the new Bridge function `clear_done`. Done mark (Ethan's choice): a custom `✓` (LCD CGRAM slot 1, not 0, because char 0 ends a `String`) plus a space before line 1, on both the resting and message screens. Holds: a press leaves the message unless hold 2 (hold 2 stays and shows the `✓`); hold 1 also times out via `checkHoldTimeout()` (`millis()`, no `delay()`). `onPress()` no longer shows `Presses: N`. `show_test.py` can now call `clear_done`. All hold/done cases were checked over Bridge + serial `p`; the `✓` looks right on the real LCD. **Untested:** the 60-minute hold 1 timeout. Not a bug: a `Due 8;00 AM` semicolon came from the BLE server's saved `state.json` config (a typo in an earlier `config` write), fixed by writing the config again from nRF Connect. **Open decision:** a new `show` while done also shows the `✓` (e.g. `✓ Lunch`); decide whether a new message should start without it. **Next:** step 6, pair mode screen (`pair_show` / `pair_end`).
- **2026-11-22:** Step 4 **done** (serial stand-ins; real button not tested). `device/listen_test.py` printed `pressed 1`, `pressed 2`... for `p` and `pair_hold` for `h`, and the LCD showed `Atlist`/`Presses: N`. Sketch: `onPress()` now ends with `Bridge.notify("pressed", pressCount)`; new `onPairHold()` sends `Bridge.notify("pair_hold")` (screen unchanged; Linux answers with `pair_show`); `readButton()` times each press from button-down (`pressStart`) and decides on release, or sends `pair_hold` once after 3 s (`holdSent`), so a hold is never counted as a press. Serial `h` = 3 s hold. **Untested:** the 3 s timing in `readButton()`, until a button is wired (`HAS_BUTTON`). `listen_test.py` receives like the BLE server's `RealBridge`: `$/register` each name, then read `[2, method, args]`. The router gives each name to one program only (`[5, 'route already exists']`), so stop the BLE server, or other listeners, while it runs. Problem hit: App Lab's Run left the MCU with no working sketch (no reply to `show`, `p` ignored; the cached build was still the step 3 size). `arduino-app-cli app restart ~/ArduinoApps/test1` rebuilt and flashed it; flashing note above updated. Hardware on hand: LCD only, **no button and no speaker** (`beep()` prints `*beep*`). This also gives the BLE server's step 6 a sketch to test `RealBridge` against. **Next:** step 5, `hold` handling and the done state (with the Python track).
- **2026-11-22:** Step 3 **done**, checkpoint passed: `device/show_test.py` called `show("Drink water", "Glass by sink", 1, 0)`, the LCD showed it and printed `*beep*` (no speaker wired yet), reply `True`. `rest` while a message is up saves the text without changing the screen; it shows on the next `showRest()`. Sketch: globals `messageShowing` / `messageHold` (hold stored for step 5); `showRest()` clears `messageShowing`; Bridge names `show` -> `showMessage()`, `rest` -> `setRest()`, registered with `Bridge.provide_safe` (checked in library 0.4.4: safe calls run between `loop()` passes; string args are `String`). `show_test.py` reuses `call()` from `ping_test.py`. Flashing: `cp` the repo sketch to `~/ArduinoApps/test1/sketch/` then Run. Known gap: `onPress()` still writes the press count without clearing `messageShowing` (fixed when step 5 makes a press dismiss the message). From this session Claude writes the code and explains it (team is behind schedule). **Next:** step 4, send `pressed` and `pair_hold`.
- **2026-11-20:** Step 2 **done**, checkpoint passed on the real LCD: `Atlist`/`Ready` on boot, `t` shows only `1234567890123456`, `r` and `p` work, and the LCD matches the serial monitor. `HAS_LCD` is now `true` (`HAS_BUTTON` still `false`, no button yet). Problems hit: (1) App Lab's build cache (`~/ArduinoApps/<app>/.cache/sketch`) held 0-byte object files from the 11-17 failed build, giving `undefined reference` link errors. Deleting that folder fixed it. (2) The LCD stayed blank until SDA/SCL were wired to the SDA/SCL pins past AREF (D20/D21), not A4/A5. Added `device/lcd_test/` (I2C scan + LCD test), which found the LCD at `0x27`. Note: App Lab now strips the `Arduino_RouterBridge` lines from `sketch.yaml` (it's part of the platform now). The repo copy still lists them, so decide whether to update it. **Next:** step 3, provide `show` and `rest` over Bridge (read Protocol.md's Bridge table first).
- **2026-11-17:** Step 2 **written, compiles, not yet flashed**. `sketch.ino` now has `showLines` (cuts to 16 chars), `beep`, `showRest` (`restLine1`/`restLine2`, default `"Atlist"`/`"Ready"`) and `onPress`; debounce moved into `readButton()`. **No LCD or button on hand**, so the sketch uses the RouterBridge `Monitor` (App Lab serial monitor) as a stand-in: every screen update prints as `[LCD] ...`, and typed commands `p` (press), `t` (16-char cut test), `r` (rest screen) replace the button. `HAS_LCD` / `HAS_BUTTON` switches (both `false`) skip the hardware code; flip them when parts arrive. `Monitor` API checked against the library's `examples/monitor/monitor.ino` (0.4.4). **Next:** flash via App Lab, run the checkpoint in the serial monitor (`Atlist`/`Ready` on boot, `p` beeps and counts, `t` prints only `1234567890123456`), tick step 2, then step 3.

- **2026-11-13:** Step 1 **done**. Agreed Kevin's `device/` layout (written in `docs/tracks/README.md`) and moved `device/Atlist.ino` to `device/sketch/sketch.ino` with `git mv`; it compiles in place. Ping test files are kept as a Bridge health check. Flashing workflow: edit only the repo's `sketch.ino`, then paste it into App Lab to flash. Idea to raise with the team: have the real sketch also provide `ping`, so the health check works with the real firmware on (that adds a Bridge name, so it needs all three owners). **Next:** step 2, splitting the sketch into functions (`showLines`, `beep`, `showRest`, `onPress`).
- **2026-11-13:** Step 1 checkpoint **passed**. `device/ping/ping.ino` provides `String ping()` returning `"pong"` via `Bridge.provide_safe` (`#include <Arduino_RouterBridge.h>`, `Bridge.begin()` in `setup()`, library 0.4.4). `device/ping_test.py` (needs `msgpack` in `~/atlist-venv`) calls it over `/var/run/arduino-router.sock` and got `'pong'`, so **`String` return values work over Bridge**. Flashed through Arduino App Lab (paste into a new app's `sketch/sketch.ino`, then Run). `arduino-cli` network upload needs an interactive password prompt, and the board IP changes between sessions. Before flashing, the test returned `[2, 'method ping not available']`, which is the router's error for a name no sketch provides. **Still open in step 1:** agree the `device/` layout with Kevin (he proposed `device/sketch/sketch.ino` + `device/server/`) and move `Atlist.ino`. **Next:** that layout decision, then step 2.
- **2026-11-13:** Step 0 half-done. Blink flashed and works (`arduino-cli`, FQBN `arduino:zephyr:unoq`, network upload). `Atlist.ino` compiles (needs the `LiquidCrystal_I2C` library, and the sketch must sit in a folder with the same name), but it isn't flashed yet: no LCD or button on hand. **Next:** step 1 (Bridge `ping()`), which needs no extra hardware. Do the step 0 hardware check once the LCD and button arrive.

- **2026-10-30:** Track reset: the sketch moved to `device/Atlist.ino`, and the branch is `firmware`. **Next:** step 0.
- **2026-10-30:** Track created. `Atlist.ino` already reads the button with a 25 ms debounce, beeps and counts presses. **Next:** step 0.
