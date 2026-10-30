# Atlist build tracks

Three tracks, one per person. Each one builds and tests **on its own**, using fakes in place of the other two, and they meet at the milestones below.

```
[Caregiver app]  --BLE-->  [BLE server, Python]  --Bridge-->  [Sketch, C++]  -> LCD / button / beeper
   phone                     UNO Q Linux side                   UNO Q MCU
   app.md                    ble-server.md                      firmware.md
```

The UNO Q's Bluetooth is wired to its Linux side, not the MCU. That's why there's a Python program in the middle.

| Track | Language | Owner | Code | Branch |
|-------|----------|-------|------|--------|
| [Caregiver app](app.md) | C# / .NET MAUI | Nicholas | `Atlist/` | `app` |
| [BLE server](ble-server.md) | Python | _____ | `device/` | `ble-server` |
| [Firmware](firmware.md) | C++ / Arduino | _____ | `device/` | `firmware` |

## Setup by computer

| Track | Windows | Linux |
|-------|---------|-------|
| App | Visual Studio 2026 with the **.NET MAUI** workload, or the .NET 10 SDK + `dotnet workload install maui-android` | .NET 10 SDK + `dotnet workload install maui-android` (CLI only) |
| BLE server | Edit anywhere. **Code runs on the UNO Q** (it's a Linux computer), over SSH or App Lab | same |
| Firmware | Arduino App Lab | Arduino App Lab |

Nobody needs a Linux laptop. The BLE server and firmware tracks share the one UNO Q, so agree on times.

## Git workflow

1. Work on your track's branch, made from `main`.
2. Before each session, `git pull` then `git merge main`, so you stay close to everyone else.
3. When a step's checkpoint passes, open a small pull request into `main`.
4. Never commit build output (`bin/`, `obj/`, `.venv/`). `.gitignore` covers it.

## The contract: `docs/Protocol.md`

[`Protocol.md`](../Protocol.md) defines everything that crosses a boundary: BLE for app ↔ Python, and Bridge for Python ↔ C++.
- Build against the doc, not someone else's code.
- **Any change needs all three owners to agree** before it's merged.

## How a track file works

- **Numbered steps.** Each has a concept to learn, what to build, and a checkpoint that proves it works. Tick the box when it passes.
- **A progress log** at the bottom. At the end of every session, write what's done, what's half-done and the next action. That's how you, or a Claude session, pick up later.

You write the code. Claude explains, gives file names and signatures, and reviews (see [`CLAUDE.md`](../../CLAUDE.md)).

## iOS later

The app targets Android only for now. To keep iOS cheap to add later:
- All Bluetooth code goes through `IDeviceService`, using Plugin.BLE, which supports both Android and iOS.
- Platform-specific code goes only in `Atlist/Platforms/<platform>/`, and is only permissions.

Adding iOS then means:
- adding `net10.0-ios` to `TargetFrameworks`
- adding a Bluetooth usage string to `Platforms/iOS/Info.plist`
- building on a Mac

## Milestones

| Milestone | Who | Proves |
|-----------|-----|--------|
| **M1: device works over BLE** | BLE server + firmware | From the **nRF Connect** phone app, writing `Drink water\|Glass by sink\|1\|0` to `message` shows it on the LCD with a beep, and a button press sends a `press` notification. |
| **M2: end to end** | everyone | On a real Android phone, the app pairs, sends a message that shows on the LCD, and sees the button press. |
| **M3: full product** | everyone | Pair and send, and the LCD shows it. A press shows as done in History. Messages to an off device wait and are delivered when it comes back. A restart keeps the config. |

`docs/Phases.md` is the original course plan. These tracks replace it for day-to-day work.
