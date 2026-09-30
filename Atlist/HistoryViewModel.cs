using System.Collections.ObjectModel;
using Atlist.Models;
using Atlist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atlist.ViewModels;

/// <summary>One sent message on the History page.</summary>
public partial class HistoryRow : ObservableObject
{
    public HistoryRow(SentMessage message) => Message = message;

    public SentMessage Message { get; }

    private int Delivered => Message.Deliveries.Count(d => d.Status == DeliveryStatus.Delivered);
    private int Total => Message.Deliveries.Count;
    private IEnumerable<string> WaitingNames =>
        Message.Deliveries.Where(d => d.Status == DeliveryStatus.Waiting).Select(d => d.DeviceName);

    public string Title => string.IsNullOrEmpty(Message.Line2) ? Message.Line1 : $"{Message.Line1} / {Message.Line2}";
    public string TimeText => Message.SentAt.ToLocalTime().ToString("h:mm tt");

    public string ToText => Message.ToAllDevices
        ? "To all devices"
        : $"To {Message.Deliveries.FirstOrDefault()?.DeviceName ?? "a forgotten device"}";

    public bool HasDelivered => Delivered > 0;
    public bool HasWaiting => Delivered < Total;

    public string StatusText => (Total, Delivered) switch
    {
        (0, _) => "Cancelled",
        var (t, d) when d == t && !Message.ToAllDevices => "✓ Delivered",
        var (t, d) when d == t => $"✓ Delivered to all {t}",
        (_, 0) => "Not delivered yet",
        var (t, d) => $"✓ {d} of {t} delivered"
    };

    public string WaitingText => $"Waiting for {string.Join(", ", WaitingNames)} — out of range";

    [ObservableProperty]
    private bool isRetrying;
}

public class HistoryGroup : List<HistoryRow>
{
    public HistoryGroup(string title, IEnumerable<HistoryRow> rows) : base(rows) => Title = title;
    public string Title { get; }
}

/// <summary>
/// Drives the Message history page: everything sent from the Send page,
/// grouped by day, with retry for messages still waiting on a device.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly HistoryStore _history;
    private readonly MessageService _messages;

    public HistoryViewModel(HistoryStore history, MessageService messages)
    {
        _history = history;
        _messages = messages;

        Filters.Add(new Choice("All", "all") { IsSelected = true });
        Filters.Add(new Choice("Delivered", "delivered"));
        Filters.Add(new Choice("Waiting", "waiting"));
    }

    public ObservableCollection<Choice> Filters { get; } = [];
    public ObservableCollection<HistoryGroup> Groups { get; } = [];

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private string emptyText = string.Empty;

    public void Load()
    {
        var filter = Filters.First(f => f.IsSelected).Value as string;

        var rows = _history.GetNewestFirst()
            .Select(m => new HistoryRow(m))
            .Where(r => filter switch
            {
                "delivered" => r.HasDelivered,
                "waiting" => r.HasWaiting,
                _ => true
            });

        Groups.Clear();
        foreach (var day in rows.GroupBy(r => r.Message.SentAt.ToLocalTime().Date))
            Groups.Add(new HistoryGroup(DayTitle(day.Key), day));

        IsEmpty = Groups.Count == 0;
        EmptyText = filter switch
        {
            "waiting" => "Nothing waiting. Every message has reached its device.",
            "delivered" => "No delivered messages yet.",
            _ => "Messages you send from the Send tab will show up here."
        };
    }

    private static string DayTitle(DateTime date)
    {
        if (date == DateTime.Today) return "TODAY";
        if (date == DateTime.Today.AddDays(-1)) return "YESTERDAY";
        return date.ToString("dddd, MMM d").ToUpperInvariant();
    }

    [RelayCommand]
    private void SelectFilter(Choice choice)
    {
        foreach (var f in Filters) f.IsSelected = f == choice;
        Load();
    }

    [RelayCommand]
    private async Task RetryAsync(HistoryRow row)
    {
        if (row.IsRetrying) return;
        row.IsRetrying = true;
        try
        {
            var updated = await _messages.DeliverWaitingAsync(row.Message);
            if (updated.Deliveries.Any(d => d.Status == DeliveryStatus.Waiting))
                await Shell.Current.DisplayAlertAsync("Still out of range",
                    "The phone couldn't reach the device. Move closer to it and try again.", "OK");
        }
        finally
        {
            row.IsRetrying = false;
            Load();
        }
    }

    [RelayCommand]
    private async Task CancelAsync(HistoryRow row)
    {
        var ok = await Shell.Current.DisplayAlertAsync("Stop trying to deliver?",
            "The waiting devices won't get this message.", "Stop", "Keep waiting");
        if (!ok) return;

        _messages.CancelWaiting(row.Message);
        Load();
    }
}
