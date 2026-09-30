using System.Text;

namespace Atlist.Services;

/// <summary>
/// Builds the one-line text commands the phone sends to a device.
/// The full list, with the replies the Arduino must send back, is in docs/BleProtocol.md.
/// Keep the two in step when adding a command.
/// </summary>
public static class DeviceProtocol
{
    /// <summary>The LCD is 16 characters wide.</summary>
    public const int LineLength = 16;

    public const string Ok = "OK";

    /// <summary>
    /// Makes text safe for the LCD and the wire format: printable ASCII only,
    /// no '|' (the field separator), at most 16 characters.
    /// </summary>
    public static string CleanLine(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(LineLength);
        foreach (var ch in text)
        {
            if (sb.Length == LineLength) break;
            sb.Append(ch is >= ' ' and <= '~' && ch != '|' ? ch : '?');
        }
        return sb.ToString();
    }

    public static string Message(string line1, string line2, KeepOnScreen keep, bool beep) =>
        $"MSG|{CleanLine(line1)}|{CleanLine(line2)}|{KeepCode(keep)}|{(beep ? 1 : 0)}";

    public static string Color(string colorName) => $"COLOR|{colorName.ToLowerInvariant()}";

    public static string ResetHours(int hours) => $"RESET|{hours}";

    public static string Priority(bool high) => $"PRIORITY|{(high ? 1 : 0)}";

    public static string RestScreen(string line1, string line2) => $"REST|{CleanLine(line1)}|{CleanLine(line2)}";

    public static string Clock(DateTime local) => $"TIME|{local:yyyy-MM-ddTHH:mm:ss}";

    public const string TestScreen = "TEST";

    public const string ScheduleClear = "SCHED|CLEAR";

    public static string ScheduleAdd(ScheduledMessage item) =>
        $"SCHED|ADD|{item.Time:hh\\:mm}|{CleanLine(item.Line1)}|{CleanLine(item.Line2)}|{(item.Beep ? 1 : 0)}";

    private static string KeepCode(KeepOnScreen keep) => keep switch
    {
        KeepOnScreen.OneHour => "60",
        KeepOnScreen.NextReminder => "NEXT",
        _ => "PRESS"
    };
}
