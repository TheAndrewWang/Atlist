using Atlist.ViewModels;

namespace Atlist.Views;

public partial class ComposePage : ContentPage
{
    private readonly ComposeViewModel _viewModel;

    public ComposePage(ComposeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Devices may have been paired, renamed or forgotten since this tab was last shown.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Load();
    }
}
