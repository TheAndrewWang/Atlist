using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlist.Services;

/// <summary>
/// What the phone remembers about each paired device.
/// Everything after DisplayName has a default value so devices saved by
/// earlier versions of this file still load without errors.
/// </summary>
public record PairedDeviceInfo(
    Guid Id,
    string AdvertisedName,
    string DisplayName,
    string Location = "",
    DateTimeOffset? LastSeen = null,
    string Color = "blue",       // backlight color name, see LcdColors
    int ResetHours = 12,         // check-off clears after this long: 6, 12 or 24
    bool HighPriority = true)    // beep when a reminder comes due
{
    [JsonIgnore]
    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? AdvertisedName : DisplayName;
}

/// <summary>
/// Remembers which devices the caregiver has paired, so other pages
/// can reconnect to them later. Stored on the phone with MAUI Preferences.
/// </summary>
public class PairedDeviceStore
{
    private const string Key = "paired_devices";

    public IReadOnlyList<PairedDeviceInfo> GetAll()
    {
        var json = Preferences.Default.Get(Key, string.Empty);
        if (string.IsNullOrEmpty(json)) return [];

        try { return JsonSerializer.Deserialize<List<PairedDeviceInfo>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public PairedDeviceInfo? Get(Guid id) => GetAll().FirstOrDefault(d => d.Id == id);

    public bool IsPaired(Guid id) => Get(id) is not null;

    /// <summary>Adds the device, or replaces the saved entry with the same Id (keeping its place in the list).</summary>
    public void Save(PairedDeviceInfo device)
    {
        var all = GetAll().ToList();
        var index = all.FindIndex(d => d.Id == device.Id);
        if (index >= 0) all[index] = device;
        else all.Add(device);
        Preferences.Default.Set(Key, JsonSerializer.Serialize(all));
    }

    public void UpdateLastSeen(Guid id, DateTimeOffset when)
    {
        var existing = Get(id);
        if (existing is not null)
            Save(existing with { LastSeen = when });
    }

    public void Remove(Guid id)
    {
        var all = GetAll().Where(d => d.Id != id).ToList();
        Preferences.Default.Set(Key, JsonSerializer.Serialize(all));
    }
}
