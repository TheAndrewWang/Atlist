namespace Atlist.ViewModels;

using System.Collections.ObjectModel;
using Atlist.Models;
using Atlist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class DevicesViewModel : ObservableObject
{
    private readonly IDeviceService _deviceService;
    private CancellationTokenSource? _scanCts;

    public ObservableCollection<Device> Devices { get; } = new();
    public ObservableCollection<Device> NearbyDevices { get; } = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isScanning;

    public DevicesViewModel(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    [RelayCommand]
    private async Task LoadDevicesAsync()
    {
        if (IsBusy)
            return;
        try
        {
            IsBusy = true;
            IReadOnlyList<Device> newDevices = await _deviceService.GetDevicesAsync();
            Devices.Clear();
            foreach (var device in newDevices)
            {
                Devices.Add(device);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ScanDevicesAsync()
    {
        if (IsScanning)
            return;
        try
        {
            _scanCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            IsScanning = true;
            NearbyDevices.Clear();
            await foreach (var device in _deviceService.ScanAsync(_scanCts.Token))
            {
                NearbyDevices.Add(device);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
        }
    }

    [RelayCommand]
    private async Task PairDeviceAsync(Device device)
    {
        await _deviceService.PairAsync(device);
        NearbyDevices.Remove(device);
        await LoadDevicesAsync();
    }
}
