using Atlist.ViewModels;

namespace Atlist.Views;

public partial class ScheduleEditPage : ContentPage
{
    public ScheduleEditPage(ScheduleEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
