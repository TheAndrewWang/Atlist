# Track: Caregiver app (C# / .NET MAUI)

**Owner:** Nicholas
**Branch:** `app`
**Code lives in:** `Atlist/`
**Builds against:** `docs/Protocol.md` (BLE sections) and `docs/Design.pdf` (7 screens)
**Tests alone with:** `FakeDeviceService`, an in-memory pretend device, until M2

## Setup (done once)

- **Windows:** Visual Studio 2026 with the **.NET MAUI** workload. It installs the Android SDK and emulator for you.
- **Linux:** the .NET 10 SDK, then `dotnet workload install maui-android`. Then install the Android SDK with:
  ```
  dotnet build Atlist/Atlist.csproj -t:InstallAndroidDependencies -f net10.0-android -p:AndroidSdkDirectory=$HOME/Android/Sdk -p:JavaSdkDirectory=/usr/lib/jvm/java-21-openjdk-amd64 -p:AcceptAndroidSDKLicenses=True
  ```
  Then add `export ANDROID_HOME=$HOME/Android/Sdk` to `~/.bashrc`.
- Build check, on either OS: `dotnet build Atlist/Atlist.csproj -f net10.0-android`
- For step 6 you need a **real Android phone**. The emulator has no Bluetooth.
- **Android only for now.** Follow the "iOS later" rules in [README.md](README.md) so iOS stays cheap to add.

## Design palette (from Design.pdf)

| Key | Hex | Used for |
|-----|-----|----------|
| `Primary` | `#1E6F69` | teal buttons, the selected tab |
| `PageBackground` | `#F3F0E9` | cream page background |
| `CardBackground` | `#FFFFFF` | device cards |
| `CardBorder` | `#EAE5DC` | card outlines |
| `Ink` | `#0E191F` | headings, the dark LCD panel |

---

## Steps

### [x] 0. Toolchain and repo cleanup
Done during the repo reset:
- `bin/`, `obj/` and the `.user` files are untracked and listed in `.gitignore`.
- The project targets Android only. The Windows and MacCatalyst platform folders are gone; `Platforms/iOS` stays for later.

### [x] 1. Protocol contract
`docs/Protocol.md`, written with the team.

### [ ] 2. App skeleton: tabs and colors
- **Concepts:**
  - XAML + code-behind: two files, one `partial` class.
  - Shell `<TabBar>` navigation. `ContentTemplate="{DataTemplate ...}"` creates a page the first time its tab is tapped.
  - `ResourceDictionary`: named colors used with `{StaticResource Key}`.
- **Build:**
  - `Views/DevicesPage`, `SendPage`, `SchedulePage` and `HistoryPage` (`.xaml` + `.xaml.cs`, namespace `Atlist.Views`). Each is a `ContentPage` with a `Title` and one placeholder `Label`.
  - `AppShell.xaml`: a `<TabBar>` with the four pages. Add `xmlns:views="clr-namespace:Atlist.Views"`.
  - `Resources/Styles/Colors.xaml`: change `Primary` and add the new palette keys. **Don't delete the template's keys**: `Styles.xaml` uses them, and the app crashes at startup if one is missing.
  - `Styles.xaml`: in the `ContentPage` style, set `BackgroundColor` to `{StaticResource PageBackground}`.
  - Delete `MainPage.xaml(.cs)` and `Resources/Images/dotnet_bot.png`.
- **Checkpoint:** the build has no errors. On an emulator, there are four tabs, they switch, and the background is cream.

### [ ] 3. MVVM and a fake device service
- **Concepts:**
  - MVVM with CommunityToolkit.Mvvm: `[ObservableProperty]`, `[RelayCommand]`, `ObservableCollection<T>`.
  - Dependency injection: register services and pages in `MauiProgram.cs`.
  - `BindingContext` and compiled bindings (`x:DataType`).
- **Build:**
  - `Models/Device.cs`: `Id`, `Name`, `Location`, `Status` (Connected / OutOfRange), `RestLine1`, `RestLine2`, `ResetHours`, `HighPriority`.
  - `Models/Message.cs`: `Line1`, `Line2`, `Beep`, `Hold` (enum: UntilPressed=0, OneHour=1, NextReminder=2; same numbers as Protocol.md), `TargetIds`, `SentAt`, `DeliveryStatus`.
  - `Services/IDeviceService.cs`:
    - `Task<IReadOnlyList<Device>> GetDevicesAsync()`
    - `Task SendAsync(Message message, Device device)`
    - `event Action<string, int> Pressed` (device id, press count)
    - `IAsyncEnumerable<Device> ScanAsync(CancellationToken ct)`
    - `Task PairAsync(Device device)` (the phone's system dialog asks for the passkey; see Protocol.md Pairing)
  - `Services/FakeDeviceService.cs`: an in-memory version with three devices (one out of range).
  - `ViewModels/DevicesViewModel.cs`, and the Devices cards in `Views/DevicesPage.xaml`: name, location, status chip, a dark LCD preview `Border`, and Send / Settings buttons.
- **Checkpoint:** Devices shows the three fake devices, matching Design.pdf page 2.

### [ ] 4. Send screen
- **Concepts:** two-way binding, computed properties (the `11 / 16` counter), `Entry.MaxLength`, and a reusable `ContentView`.
- **Build:**
  - `Controls/LcdPreview.xaml`: bindable `Line1` and `Line2` in a monospace font. It's reused on Devices, Quick messages and Settings.
  - `ViewModels/SendViewModel.cs`, and `Views/SendPage.xaml`: target chips, live preview, keep-on-screen options, beep checkbox and Send button.
  - Input rules from Protocol.md: at most 16 characters per line, printable ASCII only, **no `|`**. Show the caregiver why input is blocked.
- **Checkpoint:** typing updates the preview live. Send adds a record to the fake service.

### [ ] 5. Payload encoding (testable without hardware)
- **Concepts:** one place that turns models into Protocol.md strings, and unit tests with xUnit.
- **Build:**
  - `Services/ProtocolEncoder.cs`: `string EncodeMessage(Message m)`, `string EncodeClock(DateTimeOffset now)`, `string EncodeConfig(Device d)`, `int ParsePress(string payload)`.
  - A small test project, `Atlist.Tests`, that checks each one against the examples in Protocol.md.
- **Checkpoint:** `dotnet test` passes. `EncodeMessage` gives exactly `Take meds|With breakfast|1|0` for the Protocol.md example.

### [ ] 6. Real BLE (needs M1, so the device side must work first)
- **Concepts:**
  - Android runtime permissions: `BLUETOOTH_SCAN` and `BLUETOOTH_CONNECT` in `Platforms/Android/AndroidManifest.xml`, requested at runtime.
  - Plugin.BLE, which also covers iOS later (check first that it supports net10.0-android).
  - Swapping the DI registration from the fake service to the real one.
- **Build:**
  - `Services/BleDeviceService.cs`: implements `IDeviceService` with the UUIDs from Protocol.md and uses `ProtocolEncoder`.
  - `Views/PairPage.xaml` and `ViewModels/PairViewModel.cs`: the "Connect your checklist devices" screen. It scans for `Checklist-XXXX`, pairs, then names the device. Show **6** passkey digits, not 4.
- **Checkpoint (M2):** on a real phone, the app pairs, sends a message that shows on the LCD, and sees the button press.

### [ ] 7. Remaining screens and saving data
Do these one at a time, in this order:
1. **Saving data:** `sqlite-net-pcl` or `Preferences` for devices, quick messages and history.
2. **Quick messages:** a list with use counts. Tapping one loads it into Send, which teaches Shell navigation with parameters.
3. **Device settings:** name and location, resting screen, reset-cycle stepper, high priority, Test screen, Sync clock, Forget device. Sends `config` and `clock`.
4. **History:** All / Delivered / Waiting filters, and an offline queue with Retry and Cancel. Messages for out-of-range devices are sent automatically when the device reconnects.
5. **Schedule:** waits until the team adds the schedule section to Protocol.md.
- **Stretch:** backlight color.

---

## Progress log

Newest entry first. At the end of each session, write what you finished, what's half-done, and the next action.

- **2026-10-30 (repo reset):**
  - The tracks now live on `main`, and this track's branch is `app`.
  - The `Views/*.xaml` files from the previous entry were never committed. If you still have them locally, copy them onto `app`.
  - **Next:** step 2.
- **2026-10-30:** Steps 0–1 done. Protocol.md finished (passkey pairing, Bridge section added). Step 2 started: `Atlist/Views/` has four **empty** `.xaml` files and no `.xaml.cs` files yet. One is named `DevicePages.xaml`; rename it to `DevicesPage.xaml`. **Next:** fill in `DevicesPage.xaml(.cs)` and add the `TabBar`, build it, then do the other three pages.
