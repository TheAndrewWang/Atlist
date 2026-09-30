using Atlist.ViewModels;

namespace Atlist.Views;

public partial class SchedulePage : ContentPage
{
    private readonly ScheduleViewModel _viewModel;

    public SchedulePage(ScheduleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Reload after coming back from the editor or device settings.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Load();
    }
}
