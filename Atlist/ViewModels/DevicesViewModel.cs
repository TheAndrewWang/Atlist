namespace Atlist.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Atlist.Models;
using Atlist.Services;

public partial class DevicesViewModel : ObservableObject {
	private readonly IDeviceService _deviceService;

	public ObservableCollection<Device> Devices { get; } = new();

	[ObservableProperty] private bool _isBusy;

	public DevicesViewModel(IDeviceService deviceService) {
		_deviceService = deviceService;
	}
	
	[RelayCommand] private async Task LoadDevicesAsync() {
		if (IsBusy) return;
		try {
			IsBusy = true;
			IReadOnlyList<Device> newDevices = await _deviceService.GetDevicesAsync();
			Devices.Clear();
			foreach (var device in newDevices) {
				Devices.Add(device);
			}
		}
		finally {
			IsBusy = false;
		}
	}
}
