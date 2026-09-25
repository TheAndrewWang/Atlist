using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.BLE.Abstractions.Contracts;

namespace Atlist.Models;

/// <summary>
/// One checklist device found during a scan. Wraps the raw Bluetooth device
/// and adds the things the Connect page shows: signal strength, pairing state,
/// and the friendly name the caregiver gives it.
/// </summary>
public partial class ChecklistDevice : ObservableObject
{
    public ChecklistDevice(IDevice device)
    {
        Device = device;
        Id = device.Id;
        AdvertisedName = device.Name ?? "Unknown device";
        rssi = device.Rssi;
    }

    /// <summary>The underlying Plugin.BLE device. Replaced if the device is re-discovered.</summary>
    public IDevice Device { get; set; }

    /// <summary>Stable ID the phone uses to reconnect later.</summary>
    public Guid Id { get; }

    /// <summary>Name the Arduino advertises, e.g. "Checklist-3F2A".</summary>
    public string AdvertisedName { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignalText), nameof(IsWeakSignal), nameof(StatusText))]
    private int rssi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotPaired), nameof(StatusText))]
    private bool isPaired;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPairButton))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string displayName = string.Empty;

    public bool IsNotPaired => !IsPaired;

    /// <summary>The Pair button hides while pairing is in progress (a spinner shows instead).</summary>
    public bool ShowPairButton => !IsBusy;

    // RSSI is in dBm: closer to 0 = stronger. These thresholds are a starting point;
    // tune them against your real devices and walls.
    public bool IsWeakSignal => Rssi < -85;

    public string SignalText => Rssi switch
    {
        >= -65 => "Strong signal",
        >= -85 => "Good signal",
        _ => "Weak signal — move closer"
    };

    public string StatusText => IsPaired
        ? (string.IsNullOrWhiteSpace(DisplayName) ? "Paired · needs a name" : $"Paired as \"{DisplayName}\"")
        : SignalText;
}
