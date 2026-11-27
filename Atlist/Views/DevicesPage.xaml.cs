using Atlist.ViewModels;

namespace Atlist.Views
{
    public partial class DevicesPage : ContentPage
    {
        private readonly DevicesViewModel _viewModel;

        public DevicesPage(DevicesViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            _viewModel = viewModel;
        }

        protected override void OnAppearing() {
            base.OnAppearing();
            _viewModel.LoadDevicesCommand.Execute(null);
        }
    }
}
