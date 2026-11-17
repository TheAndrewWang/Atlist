namespace Atlist.Models;

public enum HoldMode { UntilPressed = 0, OneHour = 1, NextReminder = 2 }
public enum DeliveryStatus { Waiting, Delivered, Failed }

public class Message {
    public string Line1 { get; set; } = "";
    public string Line2 { get; set; } = "";
    public bool Beep { get; set; }
    public HoldMode Hold { get; set; }
    public List<String> TargetIds { get; set; } = new();
    public DateTimeOffset SentAt { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }
}
