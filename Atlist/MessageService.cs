namespace Atlist.Services;

/// <summary>
/// Sends caregiver messages to devices and keeps the history up to date.
/// A device that can't be reached gets a "Waiting" delivery; it is retried
/// from the History page, or automatically when the Devices page next finds
/// the device in range.
/// </summary>
public class MessageService
{
    private readonly DeviceLink _link;
    private readonly HistoryStore _history;

    public MessageService(DeviceLink link, HistoryStore history)
    {
        _link = link;
        _history = history;
    }

    /// <summary>Sends to each device in turn, saves the history entry, and returns it.</summary>
    public async Task<SentMessage> SendAsync(
        string line1, string line2, KeepOnScreen keep, bool beep,
        IReadOnlyList<PairedDeviceInfo> devices, bool toAllDevices)
    {
        var message = new SentMessage(
            Guid.NewGuid(),
            DeviceProtocol.CleanLine(line1),
            DeviceProtocol.CleanLine(line2),
            keep, beep, toAllDevices,
            DateTimeOffset.Now,
            devices.Select(d => new Delivery(d.Id, d.Title, DeliveryStatus.Waiting)).ToList());

        _history.Save(message);
        return await DeliverWaitingAsync(message);
    }

    /// <summary>Tries again for every device that hasn't received the message yet.</summary>
    public async Task<SentMessage> DeliverWaitingAsync(SentMessage message)
    {
        var deliveries = message.Deliveries.ToList();

        for (int i = 0; i < deliveries.Count; i++)
        {
            if (deliveries[i].Status == DeliveryStatus.Delivered) continue;
            try
            {
                await _link.RunAsync(deliveries[i].DeviceId, link => link.SendAsync(Command(message)));
                deliveries[i] = deliveries[i] with { Status = DeliveryStatus.Delivered, DeliveredAt = DateTimeOffset.Now };
            }
            catch
            {
                // Out of range or no answer: stays Waiting.
            }
        }

        var updated = message with { Deliveries = deliveries };
        _history.Save(updated);
        return updated;
    }

    /// <summary>
    /// Delivers anything waiting for this device over a connection that's
    /// already open. Call only inside <see cref="DeviceLink.RunAsync"/>.
    /// </summary>
    public async Task DeliverWaitingOnOpenLinkAsync(DeviceLink link, Guid deviceId)
    {
        foreach (var message in _history.WaitingFor(deviceId))
        {
            await link.SendAsync(Command(message));

            var deliveries = message.Deliveries
                .Select(d => d.DeviceId == deviceId
                    ? d with { Status = DeliveryStatus.Delivered, DeliveredAt = DateTimeOffset.Now }
                    : d)
                .ToList();
            _history.Save(message with { Deliveries = deliveries });
        }
    }

    /// <summary>Stops trying to deliver: removes the waiting devices from the message.</summary>
    public void CancelWaiting(SentMessage message) =>
        _history.Save(message with
        {
            Deliveries = message.Deliveries.Where(d => d.Status == DeliveryStatus.Delivered).ToList()
        });

    private static string Command(SentMessage m) => DeviceProtocol.Message(m.Line1, m.Line2, m.KeepOnScreen, m.Beep);
}
