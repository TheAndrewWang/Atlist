using CommunityToolkit.Mvvm.ComponentModel;

namespace Atlist.Models;

public enum DeviceReachability
{
    Unknown,    // not checked yet since the app opened
    InRange,    // answered on the last check
    OutOfRange  // didn't answer on the last check
}

/// <summary>
/// One card on the "My devices" page: a paired device, whether the phone
/// can reach it, and a mirror of what its LCD is currently showing.
/// </summary>
public partial class DeviceCard : ObservableObject
{
    public DeviceCard(Guid id, string displayName, string advertisedName, string location, DateTimeOffset? lastSeen)
    {
        Id = id;
        this.displayName = displayName;
        AdvertisedName = advertisedName;
        this.location = location;
        this.lastSeen = lastSeen;
    }

    public Guid Id { get; }
    public string AdvertisedName { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string displayName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private string location;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle), nameof(StatusDetail))]
    private DateTimeOffset? lastSeen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsInRange), nameof(IsOutOfRange), nameof(ShowScreen), nameof(Subtitle), nameof(StatusDetail),
                              nameof(PillBackground), nameof(PillText))]
    private DeviceReachability reachability = DeviceReachability.Unknown;

    /// <summary>True while the app is talking to this device (shows a small spinner).</summary>
    [ObservableProperty]
    private bool isChecking;

    [ObservableProperty]
    private string screenLine1 = string.Empty;

    [ObservableProperty]
    private string screenLine2 = string.Empty;

    /// <summary>Text color of the LCD mirror, matching the device's backlight.</summary>
    [ObservableProperty]
    private Color screenColor = LcdColors.Default;

    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? AdvertisedName : DisplayName;

    public bool IsInRange => Reachability == DeviceReachability.InRange;
    public bool IsOutOfRange => Reachability == DeviceReachability.OutOfRange;

    /// <summary>The LCD mirror and buttons show unless the device is out of range.</summary>
    public bool ShowScreen => !IsOutOfRange;

    public string StatusText => Reachability switch
    {
        DeviceReachability.InRange => "In range",
        DeviceReachability.OutOfRange => "Out of range",
        _ => "Checking…"
    };

    public string StatusDetail => Reachability switch
    {
        DeviceReachability.InRange => "Connected now. The screen below shows the latest reply.",
        DeviceReachability.OutOfRange => $"Couldn't reach this device. Last connected {FormatAgo(LastSeen)}. Messages wait until it is nearby.",
        _ => "Checking connection. Pull down to check again."
    };

    // Pill colors from the mockup
    public Color PillBackground => Reachability switch
    {
        DeviceReachability.InRange => Color.FromArgb("#DDEFE9"),
        DeviceReachability.OutOfRange => Color.FromArgb("#F3E3D3"),
        _ => Color.FromArgb("#ECE7DC")
    };

    public Color PillText => Reachability switch
    {
        DeviceReachability.InRange => Color.FromArgb("#145C4A"),
        DeviceReachability.OutOfRange => Color.FromArgb("#8A4414"),
        _ => Color.FromArgb("#56616A")
    };

    /// <summary>"Kitchen counter" normally; adds "last seen …" when the device is out of range.</summary>
    public string Subtitle
    {
        get
        {
            var place = string.IsNullOrWhiteSpace(Location) ? AdvertisedName : Location;
            return IsOutOfRange ? $"{place} · last seen {FormatAgo(LastSeen)}" : place;
        }
    }

    private static string FormatAgo(DateTimeOffset? when)
    {
        if (when is null) return "never";

        var ago = DateTimeOffset.Now - when.Value;
        if (ago.TotalMinutes < 1) return "just now";
        if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes} min ago";
        if (ago.TotalHours < 24) return $"{(int)ago.TotalHours} h ago";
        return $"{(int)ago.TotalDays} days ago";
    }
}

/// <summary>The seven backlight colors the RGB LCD shield can show, as on-screen text colors.</summary>
public static class LcdColors
{
    public static readonly Color Default = Color.FromArgb("#8FE3FF");

    public static Color FromName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "white" => Color.FromArgb("#F2F5F7"),
        "red" => Color.FromArgb("#FF7A7A"),
        "yellow" => Color.FromArgb("#FFE08A"),
        "green" => Color.FromArgb("#B8F28C"),
        "teal" => Color.FromArgb("#7FF0D8"),
        "violet" => Color.FromArgb("#D2A6FF"),
        _ => Default // "blue" or anything unrecognized
    };
}
