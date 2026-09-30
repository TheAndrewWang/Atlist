using System.Collections.ObjectModel;
using Atlist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atlist.ViewModels;

/// <summary>One daily message on the Schedule page.</summary>
public partial class ScheduleRow : ObservableObject
{
    public ScheduleRow(ScheduledMessage item, string deviceName)
    {
        Item = item;
        DeviceName = deviceName;
        isEnabled = item.Enabled;
    }

    public ScheduledMessage Item { get; }
    public string DeviceName { get; }

    public string Clock => DateTime.Today.Add(Item.Time).ToString("h:mm");
    public string AmPm => DateTime.Today.Add(Item.Time).ToString("tt");
    public string Title => string.IsNullOrEmpty(Item.Line2) ? Item.Line1 : $"{Item.Line1} / {Item.Line2}";
    public string Subtitle => $"{DeviceName} · {(Item.Beep ? "beep on" : "silent")}";

    [ObservableProperty]
    private bool isEnabled;

    /// <summary>Set by the page's view model; saves the on/off switch.</summary>
    public Action<ScheduleRow>? EnabledChanged { get; set; }

    partial void OnIsEnabledChanged(bool value) => EnabledChanged?.Invoke(this);
}

/// <summary>One device's reset cycle in the "Reset cycles" list.</summary>
public record ResetRow(Guid DeviceId, string Name, string CycleText);

/// <summary>
/// Drives the Schedule page: the daily messages each device shows by itself,
/// and each device's reset cycle. Changes are copied onto the devices
/// automatically; pull down to copy again.
/// </summary>
public partial class ScheduleViewModel : ObservableObject
{
    private readonly ScheduleStore _schedule;
    private readonly PairedDeviceStore _devices;
    private readonly DeviceSyncService _sync;
    private bool _syncing;
    private bool _syncAgain;

    public ScheduleViewModel(ScheduleStore schedule, PairedDeviceStore devices, DeviceSyncService sync)
    {
        _schedule = schedule;
        _devices = devices;
        _sync = sync;
    }

    public ObservableCollection<ScheduleRow> Items { get; } = [];
    public ObservableCollection<ResetRow> ResetCycles { get; } = [];

    [ObservableProperty]
    private string syncText = string.Empty;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool hasItems;

    public void Load()
    {
        var devices = _devices.GetAll();
        string NameOf(Guid? id) => id is null
            ? "All devices"
            : devices.FirstOrDefault(d => d.Id == id)?.Title ?? "Forgotten device";

        Items.Clear();
        foreach (var item in _schedule.GetSorted())
            Items.Add(new ScheduleRow(item, NameOf(item.DeviceId)) { EnabledChanged = OnRowToggled });
        HasItems = Items.Count > 0;

        ResetCycles.Clear();
        foreach (var d in devices)
            ResetCycles.Add(new ResetRow(d.Id, d.Title, $"Every {d.ResetHours} hours  ›"));

        UpdateSyncText();

        if (_schedule.NeedsSync) _ = SyncAsync();
    }

    private void UpdateSyncText(IReadOnlyList<string>? missed = null)
    {
        if (_devices.GetAll().Count == 0) { SyncText = "Pair a device to use the schedule"; return; }
        if (_syncing) { SyncText = "Copying to devices…"; return; }
        if (missed is { Count: > 0 }) { SyncText = $"Not copied yet to {string.Join(", ", missed)} · pull down to retry"; return; }

        var last = _schedule.LastSync;
        SyncText = _schedule.NeedsSync || last is null
            ? "Not copied to devices yet · pull down to sync"
            : $"Saved on each device · synced {Ago(last.Value)}";
    }

    private static string Ago(DateTimeOffset when)
    {
        var ago = DateTimeOffset.Now - when;
        if (ago.TotalMinutes < 1) return "just now";
        if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes} min ago";
        if (ago.TotalHours < 24) return $"{(int)ago.TotalHours} h ago";
        return $"{(int)ago.TotalDays} days ago";
    }

    private void OnRowToggled(ScheduleRow row)
    {
        _schedule.Save(row.Item with { Enabled = row.IsEnabled });
        _ = SyncAsync();
    }

    /// <summary>Copies the schedule to every device. A change made mid-sync triggers one more pass.</summary>
    private async Task SyncAsync()
    {
        if (_syncing) { _syncAgain = true; return; }
        if (_devices.GetAll().Count == 0) { IsRefreshing = false; return; }

        _syncing = true;
        IReadOnlyList<string>? missed = null;
        try
        {
            do
            {
                _syncAgain = false;
                UpdateSyncText();
                missed = await _sync.SyncAllAsync();
            } while (_syncAgain);
        }
        finally
        {
            _syncing = false;
            IsRefreshing = false;
            UpdateSyncText(missed);
        }
    }

    // ---------- Commands ----------

    [RelayCommand]
    private Task RefreshAsync() => SyncAsync();

    [RelayCommand]
    private Task AddAsync() => Shell.Current.GoToAsync("scheduleedit");

    [RelayCommand]
    private Task EditAsync(ScheduleRow row) => Shell.Current.GoToAsync($"scheduleedit?id={row.Item.Id}");

    [RelayCommand]
    private Task OpenDeviceAsync(ResetRow row) => Shell.Current.GoToAsync($"settings?deviceId={row.DeviceId}");
}
