using System.Text.Json;

namespace Atlist.Services;

/// <summary>
/// A list of records saved on the phone as JSON in MAUI Preferences.
/// Fine for the few dozen items this app keeps; not meant for large data.
/// </summary>
public abstract class JsonListStore<T>
{
    private readonly string _key;

    protected JsonListStore(string key) => _key = key;

    /// <summary>True when nothing has ever been saved under this key.</summary>
    protected bool IsEmptyOnDisk => !Preferences.Default.ContainsKey(_key);

    public List<T> GetAll()
    {
        var json = Preferences.Default.Get(_key, string.Empty);
        if (string.IsNullOrEmpty(json)) return [];

        try { return JsonSerializer.Deserialize<List<T>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public void SaveAll(IEnumerable<T> items) =>
        Preferences.Default.Set(_key, JsonSerializer.Serialize(items.ToList()));
}
