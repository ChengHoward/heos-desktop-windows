using System.Windows;

namespace HeosWpf.Services.Heos;

/// <summary>
/// 意外断线后：开启「自动重新连接」时按退避后台重试；关闭时弹窗由用户选择重连或放弃（并清除重连目标）。
/// </summary>
public sealed class HeosAutoReconnectService : IDisposable
{
    private readonly IHeosClient _client;
    private readonly IHeosAppSettings _settings;
    private int _reconnectLoopId;
    private volatile bool _disposed;

    public HeosAutoReconnectService(IHeosClient client, IHeosAppSettings settings)
    {
        _client = client;
        _settings = settings;
        _client.ConnectionChanged += OnConnectionChanged;
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        if (_disposed)
            return;

        if (_client.IsConnected)
        {
            Interlocked.Increment(ref _reconnectLoopId);
            return;
        }

        if (string.IsNullOrWhiteSpace(_client.ReconnectHost))
            return;

        var loopId = Interlocked.Increment(ref _reconnectLoopId);

        if (_settings.AutoReconnectEnabled)
            _ = ReconnectLoopAsync(loopId);
        else
            _ = PromptDisconnectedAndMaybeReconnectAsync(loopId);
    }

    private async Task ReconnectLoopAsync(int loopId)
    {
        var delay = TimeSpan.FromSeconds(2);
        const double maxMs = 30_000;
        var failurePromptShown = false;

        try
        {
            while (!_disposed)
            {
                await Task.Delay(delay).ConfigureAwait(false);
                if (Volatile.Read(ref _reconnectLoopId) != loopId)
                    return;
                if (_disposed || _client.IsConnected)
                    return;

                var host = _client.ReconnectHost;
                if (string.IsNullOrWhiteSpace(host))
                    return;

                try
                {
                    await _client.ConnectAsync(host!).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    if (!failurePromptShown)
                    {
                        failurePromptShown = true;
                        await ShowAutoReconnectFailedOnUiAsync(ex).ConfigureAwait(false);
                    }

                    delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, maxMs));
                }
            }
        }
        catch
        {
            // 忽略取消等
        }
    }

    private static async Task ShowAutoReconnectFailedOnUiAsync(Exception ex)
    {
        var app = Application.Current;
        if (app?.Dispatcher is null)
            return;

        await app.Dispatcher.InvokeAsync(() =>
            MessageBox.Show(
                $"自动重新连接失败：{ex.Message}\n\n将在后台继续重试。",
                "HEOS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning));
    }

    private async Task PromptDisconnectedAndMaybeReconnectAsync(int loopId)
    {
        var app = Application.Current;
        if (app?.Dispatcher is null)
            return;

        await Task.Yield();

        var outcome = DisconnectPromptOutcome.Skipped;
        string? hostForReconnect = null;

        app.Dispatcher.Invoke(() =>
        {
            if (_disposed || Volatile.Read(ref _reconnectLoopId) != loopId)
            {
                outcome = DisconnectPromptOutcome.Skipped;
                return;
            }

            if (_client.IsConnected)
            {
                outcome = DisconnectPromptOutcome.Skipped;
                return;
            }

            var host = _client.ReconnectHost;
            if (string.IsNullOrWhiteSpace(host))
            {
                outcome = DisconnectPromptOutcome.Skipped;
                return;
            }

            var r = MessageBox.Show(
                $"与 HEOS 设备（{host}）的连接已断开。\n\n是否重新连接？",
                "连接已断开",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (_disposed || Volatile.Read(ref _reconnectLoopId) != loopId)
            {
                outcome = DisconnectPromptOutcome.Skipped;
                return;
            }

            if (r == MessageBoxResult.Yes)
            {
                outcome = DisconnectPromptOutcome.Reconnect;
                hostForReconnect = host;
            }
            else
                outcome = DisconnectPromptOutcome.Abandon;
        });

        if (outcome == DisconnectPromptOutcome.Skipped)
            return;

        if (outcome == DisconnectPromptOutcome.Abandon)
        {
            await _client.ClearReconnectTargetAsync().ConfigureAwait(false);
            return;
        }

        try
        {
            await _client.ConnectAsync(hostForReconnect!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await app.Dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    $"重新连接失败：{ex.Message}",
                    "HEOS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning));
        }
    }

    private enum DisconnectPromptOutcome
    {
        Skipped,
        Reconnect,
        Abandon,
    }

    public void Dispose()
    {
        _disposed = true;
        Interlocked.Increment(ref _reconnectLoopId);
        _client.ConnectionChanged -= OnConnectionChanged;
    }
}
