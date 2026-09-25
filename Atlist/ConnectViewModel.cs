using System.Collections.ObjectModel;
using Atlist.Models;
using Atlist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.BLE.Abstractions.Contracts;

namespace Atlist.ViewModels;

/// <summary>
/// Drives the "Connect your checklist devices" page:
///   1. scan continuously for devices named "Checklist-XXXX"
///   2. Pair: connect → ask the device to show a 4-digit code on its LCD →
///      caregiver types the code → device confirms
///   3. caregiver names the paired device (e.g. "Take Meds")
///   4. Continue to the Devices page
/// </summary>
public partial class ConnectViewModel : ObservableObject
{
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(5);

    private readonly IChecklistBleService _ble;
    private readonly PairedDeviceStore _store;
    private CancellationTokenSource? _scanCts;
    private bool _pausedForPairing;

    public ConnectViewModel(IChecklistBleService ble, PairedDeviceStore store)
    {
        _ble = ble;
        _store = store;
        _ble.DeviceSeen += OnDeviceSeen;
        Devices.CollectionChanged += (_, _) => OnPropertyChanged(nameof(FoundText));
    }

    public ObservableCollection<ChecklistDevice> Devices { get; } = [];

    [ObservableProperty]
    private string scanStatus = "Scanning nearby…";

    [ObservableProperty]
    private bool isScanning;

    /// <summary>The device waiting for a name. Null hides the naming box.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNamingDevice))]
    private ChecklistDevice? deviceToName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveNameCommand))]
    private string nameEntry = string.Empty;

    public bool IsNamingDevice => DeviceToName is not null;

    public string FoundText => Devices.Count == 1 ? "1 found" : $"{Devices.Count} found";

    // ---------- Lifecycle (called from the page) ----------

    public async Task StartAsync()
    {
        if (!await EnsurePermissionsAsync())
        {
            ScanStatus = "Bluetooth permission is off. Turn it on in Settings.";
            return;
        }

        if (!_ble.IsBluetoothOn)
        {
            ScanStatus = "Bluetooth is off. Turn it on to find devices.";
            return;
        }

        _scanCts = new CancellationTokenSource();
        _ = ScanLoopAsync(_scanCts.Token);
    }

    public async Task StopAsync()
    {
        _scanCts?.Cancel();
        await _ble.StopScanAsync();
        IsScanning = false;
    }

    /// <summary>Plugin.BLE scans stop after ScanTimeout, so keep restarting while the page is open.</summary>
    private async Task ScanLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_pausedForPairing)
                {
                    await Task.Delay(500, ct);
                    continue;
                }

                IsScanning = true;
                ScanStatus = "Scanning nearby…";
                await _ble.StartScanAsync(ct);
            }
        }
        catch (OperationCanceledException) { /* page closed */ }
        catch (Exception ex)
        {
            ScanStatus = $"Scanning stopped: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void OnDeviceSeen(object? sender, IDevice device)
    {
        // Bluetooth events can arrive on a background thread; UI collections must change on the main thread.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = Devices.FirstOrDefault(d => d.Id == device.Id);
            if (existing is not null)
            {
                existing.Device = device;
                existing.Rssi = device.Rssi;
                return;
            }

            var item = new ChecklistDevice(device);
            var saved = _store.GetAll().FirstOrDefault(p => p.Id == device.Id);
            if (saved is not null)
            {
                item.IsPaired = true;
                item.DisplayName = saved.DisplayName;
            }
            Devices.Add(item);
        });
    }

    // ---------- Commands ----------

    [RelayCommand]
    private async Task PairAsync(ChecklistDevice device)
    {
        if (device.IsBusy || device.IsPaired) return;

        device.IsBusy = true;
        _pausedForPairing = true;
        await _ble.StopScanAsync(); // many Android phones connect unreliably while scanning

        try
        {
            await _ble.ConnectAsync(device.Device);

            // Ask the Arduino to show its code on the LCD.
            var ready = await _ble.SendAndWaitForReplyAsync("PAIR", ReplyTimeout);
            if (ready != "READY")
                throw new InvalidOperationException($"Unexpected reply from device: {ready}");

            var code = await Shell.Current.DisplayPromptAsync(
                title: $"Pair {device.AdvertisedName}",
                message: "Enter the 4-digit code shown on the device's screen.",
                accept: "Pair",
                cancel: "Cancel",
                maxLength: 4,
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(code))
            {
                await _ble.SendAndWaitForReplyAsync("CANCEL", ReplyTimeout);
                return;
            }

            var result = await _ble.SendAndWaitForReplyAsync($"CODE|{code.Trim()}", ReplyTimeout);
            if (result != "OK")
            {
                await Shell.Current.DisplayAlert("Code didn't match",
                    "Check the number on the device's screen and try Pair again.", "OK");
                return;
            }

            device.IsPaired = true;
            _store.Save(new PairedDeviceInfo(device.Id, device.AdvertisedName, string.Empty));

            NameEntry = string.Empty;
            DeviceToName = device;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Couldn't pair",
                $"{ex.Message}\n\nMake sure the device shows PAIR MODE and is close to the phone.", "OK");
        }
        finally
        {
            try { await _ble.DisconnectAsync(device.Device); } catch { /* already disconnected */ }
            device.IsBusy = false;
            _pausedForPairing = false; // scan loop resumes by itself
        }
    }

    private bool CanSaveName() => !string.IsNullOrWhiteSpace(NameEntry);

    [RelayCommand(CanExecute = nameof(CanSaveName))]
    private void SaveName()
    {
        if (DeviceToName is null) return;

        DeviceToName.DisplayName = NameEntry.Trim();
        _store.Save(new PairedDeviceInfo(DeviceToName.Id, DeviceToName.AdvertisedName, DeviceToName.DisplayName));

        DeviceToName = null;
        NameEntry = string.Empty;
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        if (DeviceToName is not null)
        {
            await Shell.Current.DisplayAlert("Name your device",
                "Give the device you just paired a name before continuing.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("//devices");
    }

    // ---------- Permissions ----------

    private static async Task<bool> EnsurePermissionsAsync()
    {
        // iOS asks for Bluetooth permission automatically the first time we scan
        // (the message comes from Info.plist), so only Android needs this.
        if (DeviceInfo.Platform != DevicePlatform.Android) return true;

        var status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Bluetooth>();

        return status == PermissionStatus.Granted;
    }
}
