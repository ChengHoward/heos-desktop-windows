using HeosWpf.Services;
using HeosWpf.Services.Heos;
using HeosWpf.ViewModels.Pages;
using HeosWpf.ViewModels.Windows;
using HeosWpf.Views.Pages;
using HeosWpf.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace HeosWpf.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHeosWpf(this IServiceCollection services)
    {
        services.AddSingleton<INavigationViewPageProvider>(sp => new ServiceProviderPageProvider(sp));
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddTransient<DevicesPageViewModel>();
        services.AddTransient<DevicesPage>();
        services.AddTransient<NowPlayingPageViewModel>();
        services.AddTransient<NowPlayingPage>();
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<SettingsPage>();

        services.AddSingleton<ISavedHeosDeviceStore, JsonSavedHeosDeviceStore>();
        services.AddSingleton<IHeosAppSettings, JsonHeosAppSettings>();
        services.AddSingleton<IHeosClient, HeosTcpClient>();
        services.AddSingleton<HeosAutoReconnectService>();
        services.AddSingleton<IHeosDeviceDiscoveryService, HeosSsdpDiscoveryService>();

        return services;
    }
}
