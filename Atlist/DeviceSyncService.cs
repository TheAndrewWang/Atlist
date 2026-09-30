namespace Atlist.Services;

/// <summary>
/// Copies what the phone knows about a device onto the device itself:
/// its settings (color, reset cycle, priority, resting screen), the clock,
/// and its part of the daily schedule.
/// </summary>
public class DeviceSyncService
{
    private readonly DeviceLink _link;
    private readonly PairedDeviceStore _devices;
    private readonly ScheduleStore _schedule;

    public DeviceSyncService(DeviceLink link, PairedDeviceStore devices, ScheduleStore schedule)
    {
        _link = link;
        _devices = devices;
        _schedule = schedule;
    }

    /// <summary>
    /// The two lines shown between messages, e.g. "TAKE MEDS" / "Due 8:00 AM".
    /// </summary>
    public (string Line1, string Line2) RestScreenFor(PairedDeviceInfo device)
    {
        var line1 = DeviceProtocol.CleanLine(device.Title.ToUpperInvariant());
        var next = _schedule.NextTimeFor(device.Id, DateTime.Now);
        var line2 = next is null ? string.Empty : DeviceProtocol.CleanLine($"Due {DateTime.Today.Add(next.Value):h:mm tt}");
        return (line1, line2);
    }

    /// <summary>Sends settings, clock and schedule to one device. Throws if it can't be reached.</summary>
    public Task SyncDeviceAsync(Guid deviceId) =>
        _link.RunAsync(deviceId, link => SyncOnOpenLinkAsync(link, deviceId));

    /// <summary>Same as <see cref="SyncDeviceAsync"/> on a connection that's already open.</summary>
    public async Task SyncOnOpenLinkAsync(DeviceLink link, Guid deviceId)
    {
        var device = _devices.Get(deviceId) ?? throw new InvalidOperationException("This device isn't paired.");
        var (rest1, rest2) = RestScreenFor(device);

        await link.SendAsync(DeviceProtocol.Clock(DateTime.Now));
        await link.SendAsync(DeviceProtocol.Color(device.Color));
        await link.SendAsync(DeviceProtocol.ResetHours(device.ResetHours));
        await link.SendAsync(DeviceProtocol.Priority(device.HighPriority));
        await link.SendAsync(DeviceProtocol.RestScreen(rest1, rest2));

        await link.SendAsync(DeviceProtocol.ScheduleClear);
        foreach (var item in _schedule.ForDevice(deviceId))
            await link.SendAsync(DeviceProtocol.ScheduleAdd(item));
    }

    /// <summary>
    /// Syncs every paired device. Returns the names of the ones that couldn't be reached.
    /// The schedule counts as synced only when every device got it.
    /// </summary>
    public async Task<IReadOnlyList<string>> SyncAllAsync()
    {
        var missed = new List<string>();
        foreach (var device in _devices.GetAll())
        {
            try { await SyncDeviceAsync(device.Id); }
            catch { missed.Add(device.Title); }
        }

        if (missed.Count == 0)
        {
            _schedule.NeedsSync = false;
            _schedule.LastSync = DateTimeOffset.Now;
        }
        return missed;
    }
}
