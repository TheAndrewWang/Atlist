namespace Atlist.Models;

public enum DeviceStatus { Connected, OutOfRange }

public class Device {
    public string Id { get; set;} = "";
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public DeviceStatus Status { get; set; }
    public string RestLine1 { get; set; } = "";
    public string RestLine2 { get; set; } = "";
    public int ResetHours { get; set; }
    public bool HighPriority { get; set; }
}

