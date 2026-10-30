# Atlist

Atlist is a caregiver companion app and a physical checklist device for people who benefit from reminders for daily activities. The device is designed around simple physical controls and visible feedback, with no login or typing required from the checklist user.

## Current implementation

- `Atlist/` is a .NET MAUI companion app. It pairs with nearby checklist devices over Bluetooth Low Energy, edits reminders and settings, sends messages, and keeps message delivery history on the phone.
- `docs/BleProtocol.md` documents the text protocol between the app and device firmware.
- `backend/` is an Express health-check stub. It is not connected to the app and does not currently store accounts, schedules, or completion history.
- `frontend/` is an unused Vite placeholder; the current app UI is the MAUI project.
- Device firmware and hardware design files are not included in this repository.

The phone is currently the source of truth for device settings and schedules. A device receives these over BLE and is expected to retain its schedule locally for reminders when the phone is away. App data is stored locally on the phone.

## Requirements

- .NET 10 SDK with the .NET MAUI workload installed
- Visual Studio 2026 with the .NET MAUI workload for Windows development, or a supported .NET MAUI environment for the desired target
- A supported device or emulator; Bluetooth pairing requires a physical device with Bluetooth LE

Install or verify the MAUI workload with:

```powershell
dotnet workload install maui
dotnet workload list
```

## Build and run

Open `Atlist.slnx` in Visual Studio and select the `Atlist` project and target device. Or build a target from the repository root:

```powershell
dotnet build Atlist/Atlist.csproj -f net10.0-windows10.0.19041.0
```

For Android, use `-f net10.0-android` with an Android SDK/emulator or connected device configured. iOS and Mac Catalyst builds require the corresponding Apple tooling. Bluetooth flows need a physical BLE device advertising a name beginning with `Checklist-` and implementing the service and characteristic described in `docs/BleProtocol.md`.

The optional Express health-check stub can be run separately:

```powershell
cd backend
npm install
npm run dev
```

It listens on port 3001 by default; set `PORT` to override it. The Vite frontend package has a development script but is not part of the current companion app.

## Repository map

| Path | Purpose |
| --- | --- |
| `Atlist/` | MAUI app, pages, view models, local stores, and BLE services |
| `docs/BleProtocol.md` | Phone-to-device wire protocol |
| `docs/Phases.md` | Product development plan and current implementation notes |
| `backend/` | Standalone Express health endpoint stub |
| `frontend/` | Vite placeholder, not currently used by the app |

## Known boundaries

- Pairing, reminders, and message delivery require compatible device firmware, which is maintained outside this repository.
- The BLE protocol sends a full schedule as a clear followed by individual entries. A failure partway through that sequence can leave the device with a partial schedule; retry a full sync after the device is reachable.
- There is no cloud account, remote sync, or server-side caregiver history in the current implementation.
- The app's date/time commands use the phone's local clock. The device firmware must interpret and maintain that local time consistently; timezone and daylight-saving behavior should be validated on the target firmware.
