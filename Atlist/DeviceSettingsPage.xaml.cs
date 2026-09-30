using Atlist.ViewModels;

namespace Atlist.Views;

public partial class DeviceSettingsPage : ContentPage
{
    private readonly DeviceSettingsViewModel _viewModel;

    public DeviceSettingsPage(DeviceSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Leaving the page (back link, system back, or Forget) keeps the edits on the phone.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.SaveLocally();
    }
}
