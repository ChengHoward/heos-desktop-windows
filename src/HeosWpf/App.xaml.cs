using HeosWpf.Composition;
using HeosWpf.Services.Heos;
using HeosWpf.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace HeosWpf;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddHeosWpf();
        _serviceProvider = services.BuildServiceProvider();

        _ = _serviceProvider.GetRequiredService<HeosAutoReconnectService>();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        _ = TryAutoConnectLastDeviceAsync();
    }

    private async Task TryAutoConnectLastDeviceAsync()
    {
        try
        {
            await Task.Delay(400).ConfigureAwait(false);
            var sp = _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized.");
            var store = sp.GetRequiredService<ISavedHeosDeviceStore>();
            var client = sp.GetRequiredService<IHeosClient>();
            var id = store.LastActiveDeviceId;
            if (id is null)
                return;
            var d = store.Find(id.Value);
            if (d is null)
                return;
            await client.ConnectAsync(d.Host).ConfigureAwait(false);
        }
        catch
        {
            // 启动时自动连接失败则忽略，用户可在设备页重选
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
