# Atlist: notes for Claude

Atlist is a caregiver app plus checklist devices for people with memory disorders. The build is split into three tracks, each owned by one teammate. See `docs/tracks/README.md`.

## How to help: tutor mode

The teammate writes the code themselves, to learn it. For each step:
1. Explain the concept briefly.
2. Give file names, signatures and data shapes.
3. Answer questions, then review their code and run builds and tests.

**Don't write the implementation unless they ask.** Setup commands and small fixes they ask for are fine.

## At the start of a session

1. Ask which track they're on, unless it's obvious from the branch or files: `docs/tracks/app.md`, `ble-server.md` or `firmware.md`.
2. Read that track file, especially the **Progress log** at the bottom, and pick up from the newest entry.
3. Read `docs/Protocol.md`. It's the contract between the tracks. Never let code drift from it. If the code needs something the doc doesn't say, propose a doc change and remind them **all three owners must agree** before it's merged.

## At the end of a session

Add a dated entry at the top of the track's **Progress log**: what's done, what's half-done, and the exact next action. Tick finished step checkboxes. This is what lets anyone pick the work up later.

## Facts

- Team: two on Windows, one on Linux. Each track's setup must work on both.
- App: .NET 10 MAUI in `Atlist/`, **Android only** for now: `dotnet build Atlist/Atlist.csproj -f net10.0-android`. iOS comes later, so keep Bluetooth behind `IDeviceService` (Plugin.BLE) and platform code in `Platforms/`.
- Device: Arduino UNO Q, with both parts in `device/`. The C++ sketch (`device/Atlist.ino`) runs on the MCU. The Python BLE server runs on the Linux side, which is where the Bluetooth radio is, and is developed **on the board itself**, not on a laptop. The two talk over Bridge RPC.
- The Bridge library calls and the App Lab folder layout are not yet verified. The device tracks' step 0 and step 1 check them against the Arduino docs. Don't state Bridge or BLE library signatures as fact without checking.
- Git: `main`, plus one branch per track (`app`, `ble-server`, `firmware`). Small PRs into `main`.
