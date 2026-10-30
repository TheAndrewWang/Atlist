# Product development plan

This plan reflects the repository as it exists today. Dates from the original Fall 2026 schedule are omitted so the plan remains useful as priorities change.

## Current product shape

- The caregiver interface is a .NET MAUI app in `Atlist/`.
- The app talks directly to checklist devices over BLE using the text protocol in `BleProtocol.md`.
- Device settings and schedules are stored on the phone and copied to each device. The device is expected to retain its schedule locally.
- `backend/` is an independent Express health-check stub and `frontend/` is a Vite placeholder. Neither participates in the app's current flows.
- Firmware and hardware files are maintained outside this repository.

## Completed foundation

The repository contains app flows for pairing, device settings, sending messages, scheduled messages, and message delivery history. It also contains local JSON-backed stores, a shared BLE connection service, and a documented command protocol.

These code paths still depend on compatible external device firmware and need end-to-end validation on the target hardware. The presence of an app flow does not establish that the corresponding firmware behavior is implemented or tested.

## Priorities

### 1. Validate the device contract

- Confirm the firmware implements every command and reply in `BleProtocol.md`.
- Confirm behavior for repeated button presses, repeated messages, settings changes, and schedule replacement.
- Verify that time and reset behavior remain correct after reboot, timezone changes, daylight-saving transitions, and power loss.
- Record the board, BLE module, display, controls, pin mapping, and firmware build/flash steps.

### 2. Make synchronization recoverable

- Exercise full schedule sync with the device disconnected at each command boundary.
- Verify that retrying a sync converges to the phone's schedule without duplicate entries.
- Consider adding protocol support for staging and committing a schedule atomically if the firmware can be updated in lockstep.
- Surface actionable sync state to the caregiver, including the affected device and retry path.

### 3. Validate usability and reliability

- Test pairing and routine caregiver tasks with the physical device.
- Check readability, control-to-task mapping, pending/completed feedback, and recovery from mistaken input.
- Exercise Bluetooth loss, device power loss, app restart, and repeated delivery.
- Keep a short record of observed failures and fixes.

### 4. Decide whether cloud services are needed

The current product works as a local phone-to-device companion. If remote caregiver access, cross-phone backup, or server-side history becomes a requirement, define the data model, authentication, privacy boundaries, and offline conflict rules before extending the backend. Until then, the backend and Vite frontend should be treated as placeholders rather than dependencies of the current app.

## Release checklist

- Reproducible app build and device firmware flashing instructions.
- End-to-end pairing, settings, message, schedule, retry, and reboot checks on the target hardware.
- Documented limitations, local data behavior, and recovery steps.
- A known-good demo device and a caregiver setup walkthrough.
