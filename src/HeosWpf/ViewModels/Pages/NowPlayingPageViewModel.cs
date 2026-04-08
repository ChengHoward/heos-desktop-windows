using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeosWpf.Models.Heos;
using HeosWpf.Services.Heos;

namespace HeosWpf.ViewModels.Pages;

public partial class NowPlayingPageViewModel : ObservableObject
{
    private readonly IHeosClient _client;
    private readonly DispatcherTimer _pollTimer;
    private DispatcherTimer? _volumeDebounce;
    private bool _suppressVolumeEvents;
    private DateTime _lastVolumeUserChangeUtc = DateTime.MinValue;

    public NowPlayingPageViewModel(IHeosClient client)
    {
        _client = client;
        _client.ConnectionChanged += (_, _) =>
            Application.Current.Dispatcher.Invoke(RefreshAvailability);

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pollTimer.Tick += async (_, _) => await RefreshNowPlayingAsync();
        _pollTimer.Start();

        RefreshAvailability();
        _ = InitializeAsync();
    }

    [ObservableProperty]
    private string subtitle = "请先在「我的设备」页连接 HEOS。";

    [ObservableProperty]
    private string song = "—";

    [ObservableProperty]
    private string artist = "—";

    [ObservableProperty]
    private string album = "—";

    [ObservableProperty]
    private string? albumArtUrl;

    [ObservableProperty]
    private bool showAlbumPlaceholder = true;

    [ObservableProperty]
    private int volume;

    [ObservableProperty]
    private bool canControl;

    [ObservableProperty]
    private bool isAuxInPlaying;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private bool isDeviceSettingsOpen;

    [ObservableProperty]
    private bool isDeviceSettingsBusy;

    [ObservableProperty]
    private string deviceModel = "—";

    [ObservableProperty]
    private string deviceNetwork = "—";

    [ObservableProperty]
    private string deviceNameDisplay = "—";

    [ObservableProperty]
    private bool isPageLoading = true;

    [ObservableProperty]
    private bool isActionLoading;

    [ObservableProperty]
    private string loadingText = "正在加载播放信息…";

    partial void OnVolumeChanged(int value)
    {
        if (_suppressVolumeEvents || !CanControl || _client.ActivePlayerId is null)
            return;

        _lastVolumeUserChangeUtc = DateTime.UtcNow;
        _volumeDebounce?.Stop();
        _volumeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _volumeDebounce.Tick += async (_, _) =>
        {
            _volumeDebounce!.Stop();
            if (_client.ActivePlayerId is not int pid)
                return;
            try
            {
                await _client.SetVolumeAsync(pid, Volume);
            }
            catch
            {
                // ignored
            }
        };
        _volumeDebounce.Start();
    }

    partial void OnCanControlChanged(bool value)
    {
        TogglePlayCommand.NotifyCanExecuteChanged();
        NextTrackCommand.NotifyCanExecuteChanged();
        PrevTrackCommand.NotifyCanExecuteChanged();
        PullNowPlayingCommand.NotifyCanExecuteChanged();
        SwitchToAuxInCommand.NotifyCanExecuteChanged();
        VolumeUpCommand.NotifyCanExecuteChanged();
        VolumeDownCommand.NotifyCanExecuteChanged();
        OpenDeviceSettingsCommand.NotifyCanExecuteChanged();
        RestartDeviceCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAuxInPlayingChanged(bool value)
    {
        NextTrackCommand.NotifyCanExecuteChanged();
        PrevTrackCommand.NotifyCanExecuteChanged();
        SwitchToAuxInCommand.NotifyCanExecuteChanged();
    }

    private void RefreshAvailability()
    {
        CanControl = _client.IsConnected && _client.ActivePlayerId is not null;
        Subtitle = CanControl
            ? $"已连接 {GetConnectedDisplayName()}"
            : "请先在「我的设备」页添加设备，或在侧栏点击已添加的设备以连接。";

        if (!CanControl)
        {
            Song = Artist = Album = "—";
            AlbumArtUrl = null;
            ShowAlbumPlaceholder = true;
            IsAuxInPlaying = false;
            IsPlaying = false;
            Volume = 0;
            DeviceModel = DeviceNetwork = DeviceNameDisplay = "—";
        }
    }

    private async Task RefreshNowPlayingAsync()
    {
        if (!CanControl || _client.ActivePlayerId is not int pid)
            return;

        try
        {
            var np = await _client.GetNowPlayingAsync(pid);
            if (np is not null)
            {
                Song = string.IsNullOrWhiteSpace(np.Song) ? "—" : np.Song;
                Artist = string.IsNullOrWhiteSpace(np.Artist) ? "—" : np.Artist;
                Album = string.IsNullOrWhiteSpace(np.Album) ? "—" : np.Album;
                AlbumArtUrl = string.IsNullOrWhiteSpace(np.ArtUrl) ? null : np.ArtUrl;
                ShowAlbumPlaceholder = string.IsNullOrWhiteSpace(AlbumArtUrl);
                IsAuxInPlaying = DetectAuxIn(np);
                SwitchToAuxInCommand.NotifyCanExecuteChanged();
            }

            var vol = await _client.GetVolumeAsync(pid);
            if (vol is int v && ShouldApplyPolledVolume(v))
            {
                _suppressVolumeEvents = true;
                Volume = v;
                _suppressVolumeEvents = false;
            }

            var state = await _client.GetPlayStateAsync(pid);
            IsPlaying = string.Equals(state, "play", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // ignored
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            IsPageLoading = true;
            await RefreshNowPlayingAsync();
        }
        finally
        {
            IsPageLoading = false;
        }
    }

    private bool CanOperate() => CanControl;

    /// <summary>上一首 / 下一首：流媒体队列有意义；AUX IN 无“曲目”概念，禁用。</summary>
    private bool CanTransportOperate() => CanControl && !IsAuxInPlaying;

    /// <summary>播放 / 暂停：任意音源（含 AUX IN）都应可切换。</summary>
    private bool CanTogglePlay() => CanControl;

    private bool CanSwitchToAuxIn() => CanControl && !IsAuxInPlaying;

    private static bool DetectAuxIn(HeosNowPlaying np)
    {
        if (np.SourceId is 1027)
            return true;

        var source = np.Source ?? string.Empty;
        return source.Contains("aux", StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldApplyPolledVolume(int remoteVolume)
    {
        // 用户刚拖动或点击音量按钮后，短时间内忽略轮询值，避免 UI 回弹。
        if (DateTime.UtcNow - _lastVolumeUserChangeUtc < TimeSpan.FromMilliseconds(1200))
            return false;

        return remoteVolume != Volume;
    }

    private bool CanOpenDeviceSettings() => CanControl;

    private string GetConnectedDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(_client.ActivePlayerName))
            return _client.ActivePlayerName!;
        return string.IsNullOrWhiteSpace(_client.ConnectedHost) ? "HEOS 设备" : _client.ConnectedHost!;
    }

    private bool CanRestartDevice() => CanControl && !IsDeviceSettingsBusy;

    private async Task RunActionWithLoadingAsync(string text, Func<Task> action)
    {
        try
        {
            LoadingText = text;
            IsActionLoading = true;
            await action();
        }
        catch (Exception ex)
        {
            Subtitle = $"{text}失败";
            MessageBox.Show(
                $"操作失败：{ex.Message}",
                "HEOS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsActionLoading = false;
            LoadingText = "正在加载播放信息…";
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenDeviceSettings))]
    private async Task OpenDeviceSettingsAsync()
    {
        await LoadDeviceInfoAsync();
        IsDeviceSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseDeviceSettings()
    {
        IsDeviceSettingsOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanRestartDevice))]
    private async Task RestartDeviceAsync()
    {
        if (_client.ActivePlayerId is not int pid)
            return;

        try
        {
            IsDeviceSettingsBusy = true;
            RestartDeviceCommand.NotifyCanExecuteChanged();

            await _client.RestartPlayerAsync(pid);
            Subtitle = "已发送重启命令，请稍候设备重新上线。";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"重启设备失败：{ex.Message}", "设备设置", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsDeviceSettingsBusy = false;
            RestartDeviceCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task LoadDeviceInfoAsync()
    {
        if (_client.ActivePlayerId is not int pid)
            return;

        try
        {
            var players = await _client.GetPlayersAsync();
            var current = players.FirstOrDefault(x => x.Pid == pid);
            if (current is null)
                return;

            DeviceModel = string.IsNullOrWhiteSpace(current.Model) ? "—" : current.Model;
            DeviceNetwork = string.IsNullOrWhiteSpace(current.Ip) ? (_client.ConnectedHost ?? "—") : current.Ip;
            DeviceNameDisplay = string.IsNullOrWhiteSpace(current.Name) ? "—" : current.Name;
            RestartDeviceCommand.NotifyCanExecuteChanged();
        }
        catch
        {
            // keep previous values
        }
    }

    [RelayCommand(CanExecute = nameof(CanTogglePlay))]
    private async Task TogglePlayAsync()
    {
        if (_client.ActivePlayerId is not int pid)
            return;
        await RunActionWithLoadingAsync("正在切换播放状态…", async () =>
        {
            var st = await _client.GetPlayStateAsync(pid);
            var next = string.Equals(st, "play", StringComparison.OrdinalIgnoreCase) ? "pause" : "play";
            await _client.SetPlayStateAsync(pid, next);
            IsPlaying = string.Equals(next, "play", StringComparison.OrdinalIgnoreCase);
        });
    }

    [RelayCommand(CanExecute = nameof(CanTransportOperate))]
    private async Task NextTrackAsync()
    {
        if (_client.ActivePlayerId is not int pid)
            return;
        await RunActionWithLoadingAsync("正在切换到下一首…", () => _client.PlayNextAsync(pid));
    }

    [RelayCommand(CanExecute = nameof(CanTransportOperate))]
    private async Task PrevTrackAsync()
    {
        if (_client.ActivePlayerId is not int pid)
            return;
        await RunActionWithLoadingAsync("正在切换到上一首…", () => _client.PlayPreviousAsync(pid));
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task PullNowPlayingAsync()
    {
        await RunActionWithLoadingAsync("正在刷新播放信息…", RefreshNowPlayingAsync);
    }

    [RelayCommand(CanExecute = nameof(CanSwitchToAuxIn))]
    private async Task SwitchToAuxInAsync()
    {
        if (_client.ActivePlayerId is not int pid)
            return;

        await RunActionWithLoadingAsync("正在切换到 AUX IN…", async () =>
        {
            await _client.PlayAuxInAsync(pid);
            await RefreshNowPlayingAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task VolumeUpAsync()
    {
        await SetVolumeImmediateAsync(Volume + 5);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task VolumeDownAsync()
    {
        await SetVolumeImmediateAsync(Volume - 5);
    }

    private async Task SetVolumeImmediateAsync(int target)
    {
        if (_client.ActivePlayerId is not int pid)
            return;

        var level = Math.Clamp(target, 0, 100);
        _lastVolumeUserChangeUtc = DateTime.UtcNow;
        _volumeDebounce?.Stop();

        _suppressVolumeEvents = true;
        Volume = level;
        _suppressVolumeEvents = false;

        try
        {
            await _client.SetVolumeAsync(pid, level);
        }
        catch
        {
            // ignored
        }
    }
}
