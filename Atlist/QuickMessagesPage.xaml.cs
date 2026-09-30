using Atlist.ViewModels;

namespace Atlist.Views;

public partial class QuickMessagesPage : ContentPage
{
    private readonly QuickMessagesViewModel _viewModel;

    public QuickMessagesPage(QuickMessagesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Load();
    }
}
