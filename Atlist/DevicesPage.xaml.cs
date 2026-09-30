using Atlist.ViewModels;

namespace Atlist.Views;

public partial class DevicesPage : ContentPage
{
    private readonly DevicesViewModel _viewModel;

    public DevicesPage(DevicesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Check devices only while this page is on screen.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Stop();
    }
}
