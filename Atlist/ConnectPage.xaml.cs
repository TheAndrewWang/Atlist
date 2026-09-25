using Atlist.ViewModels;

namespace Atlist.Views;

public partial class ConnectPage : ContentPage
{
    private readonly ConnectViewModel _viewModel;

    public ConnectPage(ConnectViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Scan only while this page is on screen: scanning drains the battery.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.StartAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _viewModel.StopAsync();
    }
}
