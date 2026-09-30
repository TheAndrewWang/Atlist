namespace Atlist.Services;

/// <summary>
/// Runs a short conversation with one paired device: connect, send some
/// commands, disconnect. Only one conversation runs at a time, because the
/// Bluetooth service holds a single connection; others wait their turn.
/// </summary>
public class DeviceLink
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(4);

    private readonly IChecklistBleService _ble;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DeviceLink(IChecklistBleService ble) => _ble = ble;

    public bool IsBluetoothOn => _ble.IsBluetoothOn;

    /// <summary>
    /// Connects to the device, runs <paramref name="work"/>, and always disconnects.
    /// Throws if the device can't be reached (out of range, unplugged, Bluetooth off).
    /// </summary>
    public async Task<T> RunAsync<T>(Guid deviceId, Func<DeviceLink, Task<T>> work, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        Plugin.BLE.Abstractions.Contracts.IDevice? device = null;
        try
        {
            if (!_ble.IsBluetoothOn)
                throw new InvalidOperationException("Bluetooth is off.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ConnectTimeout);
            device = await _ble.ConnectKnownAsync(deviceId, timeout.Token);

            return await work(this);
        }
        finally
        {
            if (device is not null)
            {
                try { await _ble.DisconnectAsync(device); } catch { /* already gone */ }
            }
            _gate.Release();
        }
    }

    public Task RunAsync(Guid deviceId, Func<DeviceLink, Task> work, CancellationToken ct = default) =>
        RunAsync(deviceId, async link => { await work(link); return true; }, ct);

    /// <summary>Sends one command on the open connection and returns the reply. Only call inside RunAsync.</summary>
    public Task<string> AskAsync(string command) => _ble.SendAndWaitForReplyAsync(command, ReplyTimeout);

    /// <summary>Sends one command and throws unless the device answers "OK". Only call inside RunAsync.</summary>
    public async Task SendAsync(string command)
    {
        var reply = await AskAsync(command);
        if (reply != DeviceProtocol.Ok)
            throw new InvalidOperationException($"The device answered \"{reply}\" instead of OK.");
    }
}
