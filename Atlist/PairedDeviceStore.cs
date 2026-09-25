using System.Text.Json;

namespace Atlist.Services;

public record PairedDeviceInfo(Guid Id, string AdvertisedName, string DisplayName);

/// <summary>
/// Remembers which devices the caregiver has paired, so the Devices page
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

    public void Save(PairedDeviceInfo device)
    {
        var all = GetAll().Where(d => d.Id != device.Id).ToList();
        all.Add(device);
        Preferences.Default.Set(Key, JsonSerializer.Serialize(all));
    }

    public bool IsPaired(Guid id) => GetAll().Any(d => d.Id == id);
}
