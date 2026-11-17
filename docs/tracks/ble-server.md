# Track: BLE server (Python, UNO Q Linux side)

**Owner:** Kevin
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

### [x] 1. Advertise and expose the service
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

- **2026-11-17: Step 2 almost done.** `device/protocol.py` + `device/test_protocol.py` are committed, and **49 tests pass**. `on_write` in `ble_server.py` now parses every write before storing it.
  - **On the board with nRF Connect (iPhone):**
    - `Drink water|Glass by sink|1|0` printed `Message(line1='Drink water', line2='Glass by sink', beep=1, hold=0)`.
    - `a|b|9|0` was rejected (`beep out of range: 9`), and nothing was stored.
  - **How a rejection reaches BlueZ:** a plain exception from `on_write` doesn't work. bless doesn't catch it, so dbus-next replies to BlueZ with a generic `org.freedesktop.DBus.Python...` error name, and BlueZ seems to report that to the phone as a *successful* write. So `on_write` raises `dbus_next.errors.DBusError("org.bluez.Error.Failed", reason)`. A debug run confirmed that BlueZ receives that error reply for a write it expects a reply to (`no_reply_expected=False`).
  - **Not verified:** that the phone actually gets an ATT error. It should be 0x80. nRF Connect on iOS showed no alert, and its write result wasn't found in its logs. Check with `sudo btmon` in a terminal on the board (not Claude Code's `!` mode), or from the C# app later. Only tick step 2 once that's confirmed.
  - **Harmless noise:** each rejection also logs "got unexpected error processing a message" with a traceback. That's dbus-next 0.2.3 re-raising the exception after it has sent the error reply.
  - **Gotchas found:**
    - **Stale bond.** The iPhone was bonded to the board from an earlier session and cached an old GATT table, so its writes never reached `on_write`. Fix: `bluetoothctl remove <phone address>` on the board, plus **Forget This Device** on the iPhone.
    - **iOS pairing popup.** When the iPhone connects, BlueZ reads the iPhone's protected services (Battery, Current Time), so iOS asks to pair. Tap **Pair**: Cancel drops the connection.
    - **nRF Connect on iOS:** set the format to UTF8. Its write type is chosen automatically.
  - **Next:** confirm the ATT error with btmon (optional), tick step 2, then start step 3 (`bridge.py` with `FakeBridge`).

- **2026-11-13 (later): Step 1 done.** `device/ble_server.py` advertises as `Checklist-F279` with the service and all four characteristics.
  - **Checkpoint passed:** from nRF Connect, a read of `press` returned `0`, and writing `Drink water|Glass by sink|1|0` to `message` printed in the terminal.
  - **How the name works:** it comes from `bluetoothctl show` (the `Controller` line). bless sets the adapter alias to it.
  - **bless 0.3.0 signatures, checked against the installed source:** `write_request_func(characteristic, value)` and `read_request_func(characteristic)`, with no kwargs.
  - **Gotcha:** Bluetooth was soft-blocked (rfkill) after the smoke test, so `bluetoothctl power on` failed and advertising was rejected.
    - Check with `cat /sys/class/rfkill/rfkill0/soft`; `1` means blocked.
    - The `arduino` user is in `netdev`, so it can unblock through `/dev/rfkill` without sudo. Or turn Bluetooth on from the blueman tray icon, which also remembers it.
    - `sudo` doesn't work in Claude Code's `!` mode, because it can't prompt for a password.
  - **Still open from step 0:** the `device/` layout with Ethan, and sharing the Bridge notes.
  - **Next:** step 2, `device/protocol.py` + `device/test_protocol.py`: parsers, `ProtocolError` and pytest. No Bluetooth needed.

- **2026-11-13: Bridge Python API checked (step 0, third bullet).**
  - **Source:** the `arduino_app_bricks` **0.10.1** package inside App Lab's Python image, `ghcr.io/arduino/app-bricks/python-apps-base:0.10.1`, which is already on the board. The file is `/usr/local/lib/python3.13/site-packages/arduino/app_utils/bridge.py`, plus its `examples/1_bridge_call_notify.py` and `2_bridge_provide.py`. Re-check if App Lab updates the package.
  - **Import:** `from arduino.app_utils import App, Bridge`. The examples use `from arduino.app_utils import *`, which also gives the `call`, `notify` and `provide` decorators.
  - **Call a function the sketch provides:** `Bridge.call("show", line1, line2, beep, hold, timeout=10)`.
    - Returns the sketch's return value.
    - Raises `ValueError` if the sketch has no such function or returns an error, `TimeoutError` after `timeout` seconds (default 10), and `RuntimeError` if the send fails.
    - It **blocks** the calling thread. In bless's asyncio code, use `await asyncio.to_thread(Bridge.call, "show", ...)`.
  - **Fire-and-forget to the sketch:** `Bridge.notify("name", *args)`. We don't need it, since every Protocol.md call returns `bool`.
  - **Receive a notification from the sketch:** `Bridge.provide("pressed", on_pressed)`, with `def on_pressed(count): ...`.
    - The same `provide` handles both: the sketch's notifications (`pressed`, `pair_hold`) and its calls that expect a reply. For a notification, the return value is ignored.
    - Register before the sketch sends anything. `provide` registers the name with the router, using `$/register`.
    - Handlers run on the **`Bridge.read_loop` thread, not the asyncio loop**. To touch bless from one, use `loop.call_soon_threadsafe(...)`. A handler that raises is only logged.
    - Arguments are positional only: msgpack arrays, so ints stay ints.
  - **Decorator style (equivalent):** `@call("show")`, `@notify("x")` and `@provide("pressed")` on a function. `@call` and `@notify` functions have a body of `...`.
  - **Keep-alive:** `App.run(user_loop=None)` blocks until Ctrl+C or SIGTERM. We'll run our own `asyncio.run(main())` instead. Bridge needs no start call: it connects on first use.
  - **Transport:** MessagePack-RPC over the Unix socket `/var/run/arduino-router.sock`. That socket exists on the host and is world-writable, so code outside the container can reach it too. Override it with the env var `APP_SOCKET` (`unix:///path` or `tcp://host:port`). If the router isn't up, the first Bridge use blocks and retries every 3 seconds.
  - **Found along the way:**
    - `bless` is **not** installed in the App Lab Python image.
    - `arduino.app_utils` is **not** installed on the host Python.
    - So whichever place runs `ble_server.py` needs one of them added. Decide this together with the "how Python runs" bullet.
  - **Not checked:** the sketch side (C++ `Bridge.provide` / `Bridge.notify` names and includes). That belongs to the firmware track.
  - **BLE confirmed:** `device/ble_smoke.py` runs on the **host** Python, with `bless` 0.3.0 pip-installed and not in App Lab's container.
    - nRF Connect saw the device and read `0` from `press`. **Step 0 checkpoint passes.**
    - The controller is `14:B5:CD:F0:F2:79`, so step 1's name is **`Checklist-F279`**. The adapter alias is currently `AtlistGoon`.
    - If `server.start()` fails, check `bluetoothctl show` for `Powered: no` and run `bluetoothctl power on`.
  - **Half-done in step 0:**
    - The `device/` layout agreed with Ethan. Running on the host means App Lab only needs to run the sketch.
    - Sharing these Bridge notes with Ethan.
    - Installing `arduino.app_utils` on the host so the BLE server can use Bridge. How to install it there isn't checked yet. The package is `arduino_app_bricks` 0.10.1.
  - **Next:** share the Bridge notes with Ethan and agree the layout, then tick step 0 and start step 1 (`ble_server.py`).

- **2026-10-30:** Track reset. The code now runs on the UNO Q, so no laptop setup is needed. **Next:** step 0 on the UNO Q.
