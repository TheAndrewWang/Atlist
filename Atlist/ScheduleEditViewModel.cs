using System.Collections.ObjectModel;
using Atlist.Models;
using Atlist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atlist.ViewModels;

/// <summary>
/// Add or change one daily scheduled message. Opened as "scheduleedit"
/// (new) or "scheduleedit?id=…" (existing).
/// </summary>
public partial class ScheduleEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly ScheduleStore _schedule;
    private readonly PairedDeviceStore _devices;
    private ScheduledMessage? _existing;

    public ScheduleEditViewModel(ScheduleStore schedule, PairedDeviceStore devices)
    {
        _schedule = schedule;
        _devices = devices;
    }

    public ObservableCollection<Choice> Targets { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle), nameof(CanDelete))]
    private bool isNew = true;

    [ObservableProperty]
    private TimeSpan time = new(8, 0, 0);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count1))]
    private string line1 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count2))]
    private string line2 = string.Empty;

    [ObservableProperty]
    private bool beep = true;

    public string PageTitle => IsNew ? "New scheduled message" : "Scheduled message";
    public bool CanDelete => !IsNew;
    public string Count1 => $"{Line1.Length} / {DeviceProtocol.LineLength}";
    public string Count2 => $"{Line2.Length} / {DeviceProtocol.LineLength}";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _existing = query.TryGetValue("id", out var id) && Guid.TryParse(id?.ToString(), out var guid)
            ? _schedule.Get(guid)
            : null;

        IsNew = _existing is null;
        Time = _existing?.Time ?? new TimeSpan(8, 0, 0);
        Line1 = _existing?.Line1 ?? string.Empty;
        Line2 = _existing?.Line2 ?? string.Empty;
        Beep = _existing?.Beep ?? true;

        Targets.Clear();
        var devices = _devices.GetAll();
        foreach (var d in devices)
            Targets.Add(new Choice(d.Title, d.Id));
        if (devices.Count > 1)
            Targets.Add(new Choice("All devices", null));

        var chosen = Targets.FirstOrDefault(t => Equals(t.Value, _existing?.DeviceId)) ?? Targets.FirstOrDefault();
        if (chosen is not null) chosen.IsSelected = true;
    }

    partial void OnLine1Changed(string value)
    {
        var clean = DeviceProtocol.CleanLine(value);
        if (clean != value) Line1 = clean;
    }

    partial void OnLine2Changed(string value)
    {
        var clean = DeviceProtocol.CleanLine(value);
        if (clean != value) Line2 = clean;
    }

    [RelayCommand]
    private void SelectTarget(Choice choice)
    {
        foreach (var t in Targets) t.IsSelected = t == choice;
    }

    [RelayCommand]
    private void ToggleBeep() => Beep = !Beep;

    [RelayCommand]
    private Task CancelAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Line1))
        {
            await Shell.Current.DisplayAlertAsync("Add a top line", "The message needs at least a top line.", "OK");
            return;
        }

        var target = Targets.FirstOrDefault(t => t.IsSelected);
        if (target is null)
        {
            await Shell.Current.DisplayAlertAsync("Pair a device first", "Scheduled messages are shown by a device.", "OK");
            return;
        }

        _schedule.Save(new ScheduledMessage(
            _existing?.Id ?? Guid.NewGuid(),
            new TimeSpan(Time.Hours, Time.Minutes, 0),
            Line1,
            Line2,
            target.Value as Guid?,
            Beep,
            _existing?.Enabled ?? true));

        await Shell.Current.GoToAsync(".."); // the Schedule page syncs when it reappears
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_existing is null) return;

        var ok = await Shell.Current.DisplayAlertAsync("Delete this message?",
            "The device will stop showing it every day.", "Delete", "Keep");
        if (!ok) return;

        _schedule.Remove(_existing.Id);
        await Shell.Current.GoToAsync("..");
    }
}
