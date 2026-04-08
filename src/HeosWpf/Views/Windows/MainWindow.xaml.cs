using HeosWpf.ViewModels.Windows;
using HeosWpf.Views.Pages;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace HeosWpf.Views.Windows;

public partial class MainWindow
{
    private readonly INavigationService _navigationService;

    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationViewPageProvider pageProvider,
        INavigationService navigationService)
    {
        SystemThemeWatcher.Watch(this);

        ViewModel = viewModel;
        DataContext = this;
        _navigationService = navigationService;

        InitializeComponent();

        NavigationView.SetPageProviderService(pageProvider);
        navigationService.SetNavigationControl(NavigationView);
        Loaded += OnLoaded;
    }

    public MainWindowViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _navigationService.Navigate(typeof(DevicesPage));
    }
}
