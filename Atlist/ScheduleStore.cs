namespace Atlist.Services;

/// <summary>
/// A message a device shows by itself every day at <see cref="Time"/>.
/// DeviceId null means "all devices".
/// </summary>
public record ScheduledMessage(
    Guid Id,
    TimeSpan Time,
    string Line1,
    string Line2,
    Guid? DeviceId,
    bool Beep,
    bool Enabled = true);

/// <summary>
/// The daily schedule. The phone is the source of truth; each device keeps
/// its own copy so reminders still appear when the phone is away.
/// </summary>
public class ScheduleStore : JsonListStore<ScheduledMessage>
{
    private const string LastSyncKey = "schedule_last_sync";
    private const string DirtyKey = "schedule_dirty";

    public ScheduleStore() : base("schedule") { }

    public IReadOnlyList<ScheduledMessage> GetSorted() => GetAll().OrderBy(s => s.Time).ToList();

    public ScheduledMessage? Get(Guid id) => GetAll().FirstOrDefault(s => s.Id == id);

    /// <summary>Adds or replaces an entry, and marks the schedule as needing a sync.</summary>
    public void Save(ScheduledMessage item)
    {
        var all = GetAll();
        var index = all.FindIndex(s => s.Id == item.Id);
        if (index >= 0) all[index] = item;
        else all.Add(item);
        SaveAll(all);
        NeedsSync = true;
    }

    public void Remove(Guid id)
    {
        SaveAll(GetAll().Where(s => s.Id != id));
        NeedsSync = true;
    }

    /// <summary>Drops entries meant only for a device that was forgotten.</summary>
    public void RemoveForDevice(Guid deviceId)
    {
        SaveAll(GetAll().Where(s => s.DeviceId != deviceId));
        NeedsSync = true;
    }

    /// <summary>Enabled entries this device should show (its own plus "all devices" ones).</summary>
    public IReadOnlyList<ScheduledMessage> ForDevice(Guid deviceId) =>
        GetSorted().Where(s => s.Enabled && (s.DeviceId is null || s.DeviceId == deviceId)).ToList();

    /// <summary>The next time today (or tomorrow) this device has something scheduled, if anything.</summary>
    public TimeSpan? NextTimeFor(Guid deviceId, DateTime now)
    {
        var times = ForDevice(deviceId).Select(s => s.Time).ToList();
        if (times.Count == 0) return null;
        return times.Where(t => t > now.TimeOfDay).DefaultIfEmpty(times.Min()).Min();
    }

    /// <summary>True after a change that hasn't reached the devices yet.</summary>
    public bool NeedsSync
    {
        get => Preferences.Default.Get(DirtyKey, false);
        set => Preferences.Default.Set(DirtyKey, value);
    }

    public DateTimeOffset? LastSync
    {
        get
        {
            var ticks = Preferences.Default.Get(LastSyncKey, 0L);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero).ToLocalTime();
        }
        set => Preferences.Default.Set(LastSyncKey, value?.UtcTicks ?? 0L);
    }
}
