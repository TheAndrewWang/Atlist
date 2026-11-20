namespace Atlist.Services;

using Atlist.Models;

public interface IDeviceService {
    Task<IReadOnlyList<Device>> GetDevicesAsync();
    Task SendAsync(Message message, Device device);
    event Action<string, int>? Pressed; // device id, press count
    IAsyncEnumerable<Device> ScanAsync(CancellationToken ct);
    Task PairAsync(Device device);
}
