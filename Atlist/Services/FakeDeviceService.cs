namespace Atlist.Services;

using System.Runtime.CompilerServices;
using Atlist.Models;

public class FakeDeviceService : IDeviceService {
    private readonly List<Device> _devices = new() {
        new Device { Id = "Checklist-3F2A", Name = "Take Meds", RestLine1 = "TAKE MEDS", RestLine2 = "Due 8:00 AM", Status = DeviceStatus.OutOfRange, Location = "Troy" },
        new Device { Id = "Checklist-6C5B", Name = "Brush Teeth", RestLine1 = "BRUSH TEETH", RestLine2 = "Due 9:00 AM", Status = DeviceStatus.Connected, Location = "Troy" },
        new Device { Id = "Checklist-8B3E", Name = "Grab Keys", RestLine1 = "GRAB CAR KEYS", RestLine2 = "Due 10:00 AM", Status = DeviceStatus.Connected, Location = "Troy" },
    };
    private readonly List<Message> _sent = new();

    private readonly List<Device> _nearby = new() {
        new Device { Id = "Checklist-5D8A", Name = "Finish HW", RestLine1 = "FINISH HW", RestLine2 = "Due 7:00 PM", Status = DeviceStatus.Connected, Location = "Troy" },
        new Device { Id = "Checklist-6C5B", Name = "Brush Teeth", RestLine1 = "BRUSH TEETH", RestLine2 = "Due 9:00 AM", Status = DeviceStatus.Connected, Location = "Troy" },
        new Device { Id = "Checklist-8B9E", Name = "Buy Food", RestLine1 = "BUY FOOD", RestLine2 = "Due 5:00 PM", Status = DeviceStatus.Connected, Location = "Troy" },
        new Device { Id = "Checklist-5C7A", Name = "Return Books", RestLine1 = "RETURN BOOKS", RestLine2 = "Due 3:00 PM", Status = DeviceStatus.Connected, Location = "Troy" },
    };

    public Task<IReadOnlyList<Device>> GetDevicesAsync() { return Task.FromResult<IReadOnlyList<Device>>(_devices.ToList()); }
    public Task SendAsync(Message message, Device device) {
        ArgumentNullException.ThrowIfNull(device); 
        ArgumentNullException.ThrowIfNull(message);

        message.DeliveryStatus = DeliveryStatus.Waiting;
        message.SentAt = DateTimeOffset.Now;

        if (device.Status == DeviceStatus.Connected) { 
            message.DeliveryStatus = DeliveryStatus.Delivered;
        }
        _sent.Add(message);
        return Task.CompletedTask;

    }
    public event Action<string, int>? Pressed;
    public async IAsyncEnumerable<Device> ScanAsync([EnumeratorCancellation] CancellationToken ct) {
        foreach(var device in _nearby) {
            await Task.Delay(1000, ct);
            if (_devices.Any(d => d.Id == device.Id)) {
                continue;
            }
            yield return device;
        }
    }
    public Task PairAsync(Device device) {
        if (!_devices.Any(d => d.Id == device.Id)) {
            _devices.Add(device);
        }
        return Task.CompletedTask;
    }

        
}



    
    
