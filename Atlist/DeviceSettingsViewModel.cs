using System.Collections.ObjectModel;
using Atlist.Models;
using Atlist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atlist.ViewModels;

/// <summary>One backlight color button.</summary>
public partial class ColorSwatch : ObservableObject
{
    public ColorSwatch(string name, int index)
    {
        Name = name;
        Index = index;
    }

    /// <summary>Lower-case name sent to the device, e.g. "teal".</summary>
    public string Name { get; }
    public int Index { get; }
    public string Label => char.ToUpperInvariant(Name[0]) + Name[1..];
    public Color Glow => LcdColors.FromName(Name);

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>
/// Drives the settings page for one device, opened as "settings?deviceId=…".
/// Changes are kept on the phone when you leave; "Save to device" also sends
/// them right away. Otherwise they reach the device on the next schedule sync.
/// </summary>
public partial class DeviceSettingsViewModel : ObservableObject, IQueryAttributable
{
    private static readonly int[] ResetChoices = [6, 12, 24];

    private readonly PairedDeviceStore _devices;
    private readonly ScheduleStore _schedule;
    private readonly DeviceLink _link;
    private readonly DeviceSyncService _sync;
    private PairedDeviceInfo? _device;
    private bool _forgotten;

    public DeviceSettingsViewModel(PairedDeviceStore devices, ScheduleStore schedule, DeviceLink link, DeviceSyncService sync)
    {
        _devices = devices;
        _schedule = schedule;
        _link = link;
        _sync = sync;

        string[] names = ["white", "red", "yellow", "green", "teal", "blue", "violet"];
        for (int i = 0; i < names.Length; i++)
            Swatches.Add(new ColorSwatch(names[i], i));
    }

    public ObservableCollection<ColorSwatch> Swatches { get; } = [];

    /// <summary>Reused from the Devices page for the name and the In range / Out of range pill.</summary>
    [ObservableProperty]
    private DeviceCard? card;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RestLine1), nameof(RestLine2))]
    private string name = string.Empty;

    [ObservableProperty]
    private string location = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glow), nameof(ColorLabel))]
    private string colorName = "blue";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResetText))]
    [NotifyCanExecuteChangedFor(nameof(ShorterCommand), nameof(LongerCommand))]
    private int resetHours = 12;

    [ObservableProperty]
    private bool highPriority = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveToDeviceCommand), nameof(TestScreenCommand), nameof(SyncClockCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    public Color Glow => LcdColors.FromName(ColorName);
    public string ColorLabel => char.ToUpperInvariant(ColorName[0]) + ColorName[1..];
    public string ResetText => $"{ResetHours} h";

    public string RestLine1 => Rest().Line1;
    public string RestLine2 => Rest().Line2;

    private (string Line1, string Line2) Rest() =>
        _device is null ? (string.Empty, string.Empty) : _sync.RestScreenFor(_device with { DisplayName = Name });

    // ---------- Lifecycle ----------

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("deviceId", out var id) || !Guid.TryParse(id?.ToString(), out var deviceId)) return;

        _device = _devices.Get(deviceId);
        if (_device is null) return;
        _forgotten = false;

        Card = new DeviceCard(_device.Id, _device.DisplayName, _device.AdvertisedName, _device.Location, _device.LastSeen);
        Name = _device.DisplayName;
        Location = _device.Location;
        ColorName = _device.Color;
        OnColorNameChanged(ColorName); // mark the swatch even when the color didn't change
        ResetHours = _device.ResetHours;
        HighPriority = _device.HighPriority;
        StatusText = string.Empty;

        _ = CheckReachableAsync(_device.Id);
    }

    /// <summary>Fills in the pill: can the phone reach this device right now?</summary>
    private async Task CheckReachableAsync(Guid deviceId)
    {
        if (Card is null) return;
        Card.IsChecking = true;
        try
        {
            await _link.RunAsync(deviceId, link => link.AskAsync("SCREEN?"));
            Card.Reachability = DeviceReachability.InRange;
            _devices.UpdateLastSeen(deviceId, DateTimeOffset.Now);
        }
        catch
        {
            Card.Reachability = DeviceReachability.OutOfRange;
        }
        finally
        {
            Card.IsChecking = false;
        }
    }

    /// <summary>Called when the page closes: keep the changes on the phone.</summary>
    public void SaveLocally()
    {
        if (_device is null || _forgotten) return;

        var updated = _device with
        {
            DisplayName = Name.Trim(),
            Location = Location.Trim(),
            Color = ColorName,
            ResetHours = ResetHours,
            HighPriority = HighPriority
        };
        if (updated == _device) return;

        _devices.Save(updated);
        _device = updated;
        _schedule.NeedsSync = true; // the Schedule page's next sync carries these to the device
    }

    partial void OnColorNameChanged(string value)
    {
        foreach (var s in Swatches) s.IsSelected = s.Name == value;
    }

    // ---------- Commands ----------

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void PickColor(ColorSwatch swatch) => ColorName = swatch.Name;

    private bool CanShorten() => ResetHours > ResetChoices[0];
    private bool CanLengthen() => ResetHours < ResetChoices[^1];

    [RelayCommand(CanExecute = nameof(CanShorten))]
    private void Shorter() => ResetHours = ResetChoices.Last(h => h < ResetHours);

    [RelayCommand(CanExecute = nameof(CanLengthen))]
    private void Longer() => ResetHours = ResetChoices.First(h => h > ResetHours);

    [RelayCommand]
    private void TogglePriority() => HighPriority = !HighPriority;

    private bool NotBusy() => !IsBusy;

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private Task SaveToDeviceAsync() => TalkAsync(
        "Saved and sent to the device.",
        async link =>
        {
            SaveLocally();
            await _sync.SyncOnOpenLinkAsync(link, _device!.Id);
        });

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private Task TestScreenAsync() => TalkAsync(
        "Test pattern sent. Check the device's screen.",
        link => link.SendAsync(DeviceProtocol.TestScreen));

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private Task SyncClockAsync() => TalkAsync(
        "The device's clock now matches the phone.",
        link => link.SendAsync(DeviceProtocol.Clock(DateTime.Now)));

    private async Task TalkAsync(string success, Func<DeviceLink, Task> work)
    {
        if (_device is null) return;

        IsBusy = true;
        StatusText = "Connecting…";
        try
        {
            await _link.RunAsync(_device.Id, work);
            StatusText = success;
            if (Card is not null) Card.Reachability = DeviceReachability.InRange;
        }
        catch (Exception ex)
        {
            StatusText = $"Couldn't reach the device: {ex.Message}";
            if (Card is not null) Card.Reachability = DeviceReachability.OutOfRange;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ForgetAsync()
    {
        if (_device is null) return;

        var ok = await Shell.Current.DisplayAlertAsync(
            $"Forget {_device.Title}?",
            "The app will stop checking it and its scheduled messages will be removed. You can pair it again later.",
            "Forget", "Keep");
        if (!ok) return;

        _devices.Remove(_device.Id);
        _schedule.RemoveForDevice(_device.Id);
        _forgotten = true;
        await Shell.Current.GoToAsync("..");
    }
}
