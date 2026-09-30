namespace Atlist.Services;

public enum KeepOnScreen { UntilPressed, OneHour, NextReminder }

public enum DeliveryStatus { Delivered, Waiting }

/// <summary>Whether one device has received a message yet.</summary>
public record Delivery(Guid DeviceId, string DeviceName, DeliveryStatus Status, DateTimeOffset? DeliveredAt = null);

/// <summary>
/// One message the caregiver sent from the Send page. A message to
/// "all devices" is one entry with a Delivery per device.
/// </summary>
public record SentMessage(
    Guid Id,
    string Line1,
    string Line2,
    KeepOnScreen KeepOnScreen,
    bool Beep,
    bool ToAllDevices,
    DateTimeOffset SentAt,
    List<Delivery> Deliveries);

/// <summary>Messages sent from the phone, newest first. Keeps the last 200.</summary>
public class HistoryStore : JsonListStore<SentMessage>
{
    private const int MaxEntries = 200;

    public HistoryStore() : base("message_history") { }

    public IReadOnlyList<SentMessage> GetNewestFirst() => GetAll().OrderByDescending(m => m.SentAt).ToList();

    public void Save(SentMessage message)
    {
        var all = GetAll();
        var index = all.FindIndex(m => m.Id == message.Id);
        if (index >= 0) all[index] = message;
        else all.Add(message);
        SaveAll(all.OrderByDescending(m => m.SentAt).Take(MaxEntries));
    }

    /// <summary>Messages still waiting for this device, oldest first so they arrive in order.</summary>
    public IReadOnlyList<SentMessage> WaitingFor(Guid deviceId) =>
        GetAll()
            .Where(m => m.Deliveries.Any(d => d.DeviceId == deviceId && d.Status == DeliveryStatus.Waiting))
            .OrderBy(m => m.SentAt)
            .ToList();
}
