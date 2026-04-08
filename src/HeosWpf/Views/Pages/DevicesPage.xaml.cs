using HeosWpf.ViewModels.Pages;
using System.Windows.Controls;

namespace HeosWpf.Views.Pages;

public partial class DevicesPage : Page
{
    public DevicesPage(DevicesPageViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public DevicesPageViewModel ViewModel { get; }
}
