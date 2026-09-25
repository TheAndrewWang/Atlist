using System.Text;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace Atlist.Services;

public interface IChecklistBleService
{
    /// <summary>Raised (possibly on a background thread) whenever a checklist device is seen.</summary>
    event EventHandler<IDevice>? DeviceSeen;

    bool IsBluetoothOn { get; }
    bool IsScanning { get; }

    Task StartScanAsync(CancellationToken ct);
    Task StopScanAsync();

    Task ConnectAsync(IDevice device, CancellationToken ct = default);
    Task DisconnectAsync(IDevice device);

    /// <summary>Sends one line of text and waits for the device's one-line reply.</summary>
    Task<string> SendAndWaitForReplyAsync(string line, TimeSpan timeout);
}

/// <summary>
/// Talks to HM-10 style BLE serial modules. The HM-10 exposes a single
/// characteristic (FFE1 inside service FFE0) that works both ways:
/// the phone writes to it, and the Arduino's replies arrive as notifications.
///
/// Wire format (you define this on the Arduino side too):
///   - plain ASCII text, one message per line, ending in '\n'
///   - the phone sends at most 20 bytes per BLE write, so long lines are split
///     and the Arduino must collect bytes until it sees '\n'
/// </summary>
public class ChecklistBleService : IChecklistBleService
{
    // Standard HM-10 UUIDs. If you switch to an UNO R4 WiFi you choose your own
    // UUIDs in the Arduino sketch, and you change these two lines to match.
    public static readonly Guid ServiceUuid = Guid.Parse("0000FFE0-0000-1000-8000-00805F9B34FB");
    public static readonly Guid CharacteristicUuid = Guid.Parse("0000FFE1-0000-1000-8000-00805F9B34FB");

    /// <summary>Every device's Arduino sketch sets its BLE name to start with this.</summary>
    public const string NamePrefix = "Checklist-";

    private const int MaxChunkBytes = 20;

    private readonly IBluetoothLE _ble = CrossBluetoothLE.Current;
    private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;

    private ICharacteristic? _characteristic;
    private readonly StringBuilder _receiveBuffer = new();
    private TaskCompletionSource<string>? _pendingReply;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public event EventHandler<IDevice>? DeviceSeen;

    public ChecklistBleService()
    {
        // DeviceDiscovered fires the first time; DeviceAdvertised keeps firing,
        // which lets the signal-strength text stay up to date.
        _adapter.DeviceDiscovered += OnDeviceEvent;
        _adapter.DeviceAdvertised += OnDeviceEvent;
    }

    public bool IsBluetoothOn => _ble.State == BluetoothState.On;
    public bool IsScanning => _adapter.IsScanning;

    private void OnDeviceEvent(object? sender, DeviceEventArgs e) => DeviceSeen?.Invoke(this, e.Device);

    public async Task StartScanAsync(CancellationToken ct)
    {
        if (_adapter.IsScanning) return;

        _adapter.ScanMode = ScanMode.LowLatency;   // fastest discovery while the page is open
        _adapter.ScanTimeout = 60_000;             // the page restarts the scan if it times out

        await _adapter.StartScanningForDevicesAsync(
            deviceFilter: d => d.Name?.StartsWith(NamePrefix, StringComparison.Ordinal) == true,
            allowDuplicatesKey: true,
            cancellationToken: ct);
    }

    public Task StopScanAsync() =>
        _adapter.IsScanning ? _adapter.StopScanningForDevicesAsync() : Task.CompletedTask;

    public async Task ConnectAsync(IDevice device, CancellationToken ct = default)
    {
        await _adapter.ConnectToDeviceAsync(device, cancellationToken: ct);

        var service = await device.GetServiceAsync(ServiceUuid, ct)
            ?? throw new InvalidOperationException("This device doesn't have the checklist service. Is it an HM-10?");

        _characteristic = await service.GetCharacteristicAsync(CharacteristicUuid)
            ?? throw new InvalidOperationException("Checklist characteristic not found on the device.");

        _receiveBuffer.Clear();
        _characteristic.ValueUpdated += OnValueUpdated;
        await _characteristic.StartUpdatesAsync(ct);
    }

    public async Task DisconnectAsync(IDevice device)
    {
        if (_characteristic is not null)
        {
            _characteristic.ValueUpdated -= OnValueUpdated;
            try { await _characteristic.StopUpdatesAsync(); } catch { /* already gone */ }
            _characteristic = null;
        }

        _pendingReply?.TrySetCanceled();
        await _adapter.DisconnectDeviceAsync(device);
    }

    public async Task<string> SendAndWaitForReplyAsync(string line, TimeSpan timeout)
    {
        if (_characteristic is null)
            throw new InvalidOperationException("Not connected to a device.");

        await _sendLock.WaitAsync();
        try
        {
            _pendingReply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            // The LCD only draws basic ASCII, so anything else becomes '?'.
            var bytes = Encoding.ASCII.GetBytes(line.TrimEnd('\n') + "\n");

            for (int offset = 0; offset < bytes.Length; offset += MaxChunkBytes)
            {
                var chunk = bytes.AsSpan(offset, Math.Min(MaxChunkBytes, bytes.Length - offset)).ToArray();
                await _characteristic.WriteAsync(chunk);
                await Task.Delay(20); // HM-10 modules drop data if writes arrive back-to-back
            }

            var finished = await Task.WhenAny(_pendingReply.Task, Task.Delay(timeout));
            if (finished != _pendingReply.Task)
                throw new TimeoutException("The device didn't answer. Check that it's powered and in range.");

            return await _pendingReply.Task;
        }
        finally
        {
            _pendingReply = null;
            _sendLock.Release();
        }
    }

    /// <summary>Collects notification bytes until a full line has arrived.</summary>
    private void OnValueUpdated(object? sender, CharacteristicUpdatedEventArgs e)
    {
        var data = e.Characteristic.Value;
        if (data is null || data.Length == 0) return;

        _receiveBuffer.Append(Encoding.ASCII.GetString(data));

        var text = _receiveBuffer.ToString();
        int newline;
        while ((newline = text.IndexOf('\n')) >= 0)
        {
            var reply = text[..newline].Trim('\r', ' ');
            text = text[(newline + 1)..];
            _pendingReply?.TrySetResult(reply);
        }

        _receiveBuffer.Clear().Append(text);
    }
}
