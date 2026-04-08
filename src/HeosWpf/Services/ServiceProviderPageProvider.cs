using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Abstractions;

namespace HeosWpf.Services;

/// <summary>
/// Resolves navigation pages from the same <see cref="IServiceProvider"/> used by the app.
/// </summary>
public sealed class ServiceProviderPageProvider(IServiceProvider serviceProvider) : INavigationViewPageProvider
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public object? GetPage(Type pageType) =>
        ActivatorUtilities.CreateInstance(_serviceProvider, pageType);
}
