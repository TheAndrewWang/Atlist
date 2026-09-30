using System.Collections.ObjectModel;
using Atlist.Models;
using Atlist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atlist.ViewModels;

/// <summary>
/// Drives the "Send a message" page: pick a device (or all), type two
/// 16-character lines, see them on a mock LCD, send.
/// Other pages can open it with ?deviceId=… (preselect a device) or pass
/// line1/line2 back from Quick messages.
/// </summary>
public partial class ComposeViewModel : ObservableObject, IQueryAttributable
{
    private readonly PairedDeviceStore _devices;
    private readonly MessageService _messages;
    private Guid? _preselectDeviceId;

    public ComposeViewModel(PairedDeviceStore devices, MessageService messages)
    {
        _devices = devices;
        _messages = messages;

        for (int row = 0; row < 2; row++)
            for (int col = 0; col < DeviceProtocol.LineLength; col++)
                PreviewCells.Add(new LcdCell(row, col));

        Durations.Add(new Choice("Until pressed", KeepOnScreen.UntilPressed, 0) { IsSelected = true });
        Durations.Add(new Choice("1 hour", KeepOnScreen.OneHour, 1));
        Durations.Add(new Choice("Next reminder", KeepOnScreen.NextReminder, 2));
    }

    public ObservableCollection<Choice> Targets { get; } = [];
    public ObservableCollection<Choice> Durations { get; } = [];
    public ObservableCollection<LcdCell> PreviewCells { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count1))]
    private string line1 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count2))]
    private string line2 = string.Empty;

    [ObservableProperty]
    private bool beep = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SendLabel))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool isSending;

    [ObservableProperty]
    private string statusText = string.Empty;

    /// <summary>Green when every device got the message, amber when some are waiting.</summary>
    [ObservableProperty]
    private Color statusColor = Color.FromArgb("#145C4A");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool hasDevices;

    /// <summary>LCD text color: the chosen device's backlight, or blue for "all devices".</summary>
    [ObservableProperty]
    private Color previewColor = LcdColors.Default;

    public string Count1 => $"{Line1.Length} / {DeviceProtocol.LineLength}";
    public string Count2 => $"{Line2.Length} / {DeviceProtocol.LineLength}";

    public string SendLabel => IsSending ? "Sending…" : $"Send to {SelectedTargetName}";

    private Choice? SelectedTarget => Targets.FirstOrDefault(t => t.IsSelected);

    private string SelectedTargetName => SelectedTarget switch
    {
        null => "…",
        { Value: null } => "all devices",
        var t => t.Label
    };

    // ---------- Lifecycle ----------

    /// <summary>Rebuilds the "Send to" chips from the paired devices, keeping the current choice.</summary>
    public void Load()
    {
        var saved = _devices.GetAll();
        var keep = _preselectDeviceId ?? SelectedTarget?.Value as Guid?;
        var keepAll = _preselectDeviceId is null && SelectedTarget is { Value: null };
        _preselectDeviceId = null;

        Targets.Clear();
        foreach (var d in saved)
            Targets.Add(new Choice(d.Title, d.Id));
        if (saved.Count > 1)
            Targets.Add(new Choice("All devices", null));

        var chosen = keepAll
            ? Targets.FirstOrDefault(t => t.Value is null)
            : Targets.FirstOrDefault(t => keep is not null && t.Value is Guid id && id == keep);
        chosen ??= Targets.FirstOrDefault();
        if (chosen is not null) chosen.IsSelected = true;

        HasDevices = saved.Count > 0;
        UpdatePreviewColor();
        OnPropertyChanged(nameof(SendLabel));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("deviceId", out var id) && Guid.TryParse(id?.ToString(), out var deviceId))
        {
            _preselectDeviceId = deviceId;
            if (Targets.Count > 0) Load(); // already on screen: apply now
        }

        if (query.TryGetValue("line1", out var l1)) Line1 = l1?.ToString() ?? string.Empty;
        if (query.TryGetValue("line2", out var l2)) Line2 = l2?.ToString() ?? string.Empty;
    }

    // ---------- Typing ----------

    partial void OnLine1Changed(string value)
    {
        var clean = DeviceProtocol.CleanLine(value);
        if (clean != value) { Line1 = clean; return; } // re-enters with the clean text
        ShowOnPreview(0, clean);
        StatusText = string.Empty;
    }

    partial void OnLine2Changed(string value)
    {
        var clean = DeviceProtocol.CleanLine(value);
        if (clean != value) { Line2 = clean; return; }
        ShowOnPreview(1, clean);
        StatusText = string.Empty;
    }

    private void ShowOnPreview(int row, string text)
    {
        var padded = text.PadRight(DeviceProtocol.LineLength);
        foreach (var cell in PreviewCells.Where(c => c.Row == row))
            cell.Character = padded[cell.Column].ToString();
    }

    private void UpdatePreviewColor()
    {
        var device = SelectedTarget?.Value is Guid id ? _devices.Get(id) : null;
        PreviewColor = device is null ? LcdColors.Default : LcdColors.FromName(device.Color);
    }

    // ---------- Commands ----------

    [RelayCommand]
    private void SelectTarget(Choice choice)
    {
        foreach (var t in Targets) t.IsSelected = t == choice;
        StatusText = string.Empty;
        UpdatePreviewColor();
        OnPropertyChanged(nameof(SendLabel));
    }

    [RelayCommand]
    private void SelectDuration(Choice choice)
    {
        foreach (var d in Durations) d.IsSelected = d == choice;
    }

    [RelayCommand]
    private void ToggleBeep() => Beep = !Beep;

    [RelayCommand]
    private Task OpenQuickMessagesAsync() => Shell.Current.GoToAsync("quickmessages");

    private bool CanSend() => HasDevices && !IsSending;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(Line1) && string.IsNullOrWhiteSpace(Line2))
        {
            StatusColor = Color.FromArgb("#8A4414");
            StatusText = "Type a message first.";
            return;
        }

        var target = SelectedTarget;
        if (target is null) return;

        var toAll = target.Value is null;
        var devices = toAll
            ? _devices.GetAll()
            : _devices.GetAll().Where(d => target.Value is Guid id && d.Id == id).ToList();

        var keep = (KeepOnScreen)(Durations.First(d => d.IsSelected).Value ?? KeepOnScreen.UntilPressed);

        IsSending = true;
        StatusText = string.Empty;
        try
        {
            var sent = await _messages.SendAsync(Line1, Line2, keep, Beep, devices, toAll);
            ShowResult(sent);
        }
        finally
        {
            IsSending = false;
        }
    }

    private void ShowResult(SentMessage sent)
    {
        var delivered = sent.Deliveries.Count(d => d.Status == DeliveryStatus.Delivered);
        var total = sent.Deliveries.Count;
        var allOk = delivered == total;

        StatusColor = Color.FromArgb(allOk ? "#145C4A" : "#8A4414");
        StatusText = (sent.ToAllDevices, allOk) switch
        {
            (false, true) => $"Delivered to {sent.Deliveries[0].DeviceName} just now",
            (false, false) => $"{sent.Deliveries[0].DeviceName} is out of range. It will get the message when it's back in range.",
            (true, true) => $"Delivered to all {total} devices just now",
            (true, false) => $"Delivered to {delivered} of {total}. The others will get it when they're back in range."
        };
    }
}
