using HeosWpf.ViewModels.Pages;
using System.Windows.Controls;

namespace HeosWpf.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsPageViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public SettingsPageViewModel ViewModel { get; }
}
