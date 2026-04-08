using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeosWpf.Models.Heos;
using HeosWpf.Services.Heos;

namespace HeosWpf.ViewModels.Pages;

public partial class DevicesPageViewModel : ObservableObject
{
    private readonly IHeosDeviceDiscoveryService _discovery;
    private readonly IHeosClient _heos;
    private readonly ISavedHeosDeviceStore _store;

    public DevicesPageViewModel(
        IHeosDeviceDiscoveryService discovery,
        IHeosClient heos,
        ISavedHeosDeviceStore store)
    {
        _discovery = discovery;
        _heos = heos;
        _store = store;

        _store.DevicesChanged += (_, _) =>
            Application.Current.Dispatcher.Invoke(ReloadSavedDevices);

        _heos.ConnectionChanged += (_, _) =>
            Application.Current.Dispatcher.Invoke(RefreshConnectionStatus);

        ReloadSavedDevices();
        RefreshConnectionStatus();
    }

    public ObservableCollection<SavedHeosDeviceRowViewModel> SavedDevices { get; } = new();

    public ObservableCollection<HeosDeviceRowViewModel> DialogDiscoveredDevices { get; } = new();

    [ObservableProperty]
    private string subtitle =
        "下方为已添加的设备。点击「添加新设备」可自动扫描局域网或通过 IP 手动添加。";

    [ObservableProperty]
    private string connectionStatusText = "未连接";

    [ObservableProperty]
    private bool isAddDeviceDialogOpen;

    [ObservableProperty]
    private string dialogManualIp = string.Empty;

    [ObservableProperty]
    private string dialogScanSummary = "尚未扫描";

    [ObservableProperty]
    private bool dialogIsScanning;

    [ObservableProperty]
    private HeosDeviceRowViewModel? dialogSelectedDiscovery;

    [ObservableProperty]
    private SavedHeosDeviceRowViewModel? selectedSavedDevice;

    partial void OnDialogSelectedDiscoveryChanged(HeosDeviceRowViewModel? value)
    {
        if (value is not null)
            DialogManualIp = value.IpAddress;
    }

    partial void OnSelectedSavedDeviceChanged(SavedHeosDeviceRowViewModel? value)
    {
        if (value is null)
            return;

        _ = ConnectToSavedAsync(value);
    }

    private void ReloadSavedDevices()
    {
        SavedDevices.Clear();
        foreach (var d in _store.Devices)
        {
            SavedDevices.Add(
                new SavedHeosDeviceRowViewModel(d, _store.LastActiveDeviceId == d.Id));
        }

        RefreshSavedCurrentFlags();
        RefreshConnectionStatus();
    }

    [RelayCommand]
    private void OpenAddDeviceDialog()
    {
        IsAddDeviceDialogOpen = true;
        DialogManualIp = string.Empty;
        DialogSelectedDiscovery = null;
        DialogDiscoveredDevices.Clear();
        DialogScanSummary = "正在自动扫描…";
        _ = DialogScanAsync();
    }

    [RelayCommand]
    private void CloseAddDeviceDialog()
    {
        IsAddDeviceDialogOpen = false;
        DialogIsScanning = false;
        DialogScanCommand.NotifyCanExecuteChanged();
    }

    private bool CanDialogScan() => !DialogIsScanning;

    [RelayCommand(CanExecute = nameof(CanDialogScan))]
    private async Task DialogScanAsync()
    {
        DialogIsScanning = true;
        DialogScanCommand.NotifyCanExecuteChanged();
        DialogScanSummary = "正在扫描…";

        try
        {
            var list = await _discovery.DiscoverAsync(TimeSpan.FromSeconds(4), resolveFriendlyNames: true);

            DialogSelectedDiscovery = null;
            DialogDiscoveredDevices.Clear();

            var notYetAdded = list
                .Where(d => !_store.ContainsHost(d.IpAddress))
                .ToList();

            foreach (var d in notYetAdded)
                DialogDiscoveredDevices.Add(new HeosDeviceRowViewModel(d));

            var skipped = list.Count - notYetAdded.Count;
            DialogScanSummary = skipped > 0
                ? $"{DateTime.Now:t} 扫描完成 · {notYetAdded.Count} 台可添加（已忽略 {skipped} 台已添加）"
                : $"{DateTime.Now:t} 扫描完成 · {notYetAdded.Count} 台可添加";
        }
        catch (Exception ex)
        {
            DialogDiscoveredDevices.Clear();
            DialogScanSummary = $"{DateTime.Now:t} 扫描失败";
            MessageBox.Show(
                $"SSDP 扫描失败：{ex.Message}",
                "添加新设备",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            DialogIsScanning = false;
            DialogScanCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task ConfirmAddFromDialogAsync()
    {
        var scanIp = DialogSelectedDiscovery?.IpAddress;
        var friendly = DialogSelectedDiscovery?.DisplayName;
        if (!TryResolveHost(scanIp, DialogManualIp, out var host))
        {
            MessageBox.Show(
                "请选中扫描结果中的一台设备，或在下方输入合法的 IPv4 地址。",
                "添加新设备",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_store.ContainsHost(host))
        {
            MessageBox.Show("该 IP 已在列表中。", "添加新设备", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var display = !string.IsNullOrWhiteSpace(friendly) ? friendly : null;
        var saved = _store.Add(host, display);
        _store.SetLastActive(saved.Id);
        CloseAddDeviceDialog();
        await TryConnectAsync(host);
    }

    private static bool TryResolveHost(string? scanIp, string manual, out string host)
    {
        host = string.Empty;
        var h = !string.IsNullOrWhiteSpace(scanIp) ? scanIp.Trim() : manual.Trim();
        if (string.IsNullOrWhiteSpace(h))
            return false;
        if (!IPAddress.TryParse(h, out _))
            return false;
        host = h;
        return true;
    }

    [RelayCommand]
    private async Task RemoveSavedDeviceAsync()
    {
        if (SelectedSavedDevice is not { } row)
            return;

        var host = row.Host;
        _store.Remove(row.Id);
        SelectedSavedDevice = null;

        if (string.Equals(_heos.ConnectedHost, host, StringComparison.OrdinalIgnoreCase))
            await _heos.DisconnectAsync();

        ReloadSavedDevices();
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _heos.DisconnectAsync();
        RefreshConnectionStatus();
    }

    private async Task ConnectToSavedAsync(SavedHeosDeviceRowViewModel row)
    {
        _store.SetLastActive(row.Id);
        RefreshSavedCurrentFlags();
        await TryConnectAsync(row.Host);
    }

    private async Task TryConnectAsync(string host)
    {
        try
        {
            await _heos.ConnectAsync(host);
            RefreshConnectionStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"连接 {host}:1255 失败：{ex.Message}",
                "HEOS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            RefreshConnectionStatus();
        }
    }

    private void RefreshSavedCurrentFlags()
    {
        var id = _store.LastActiveDeviceId;
        foreach (var r in SavedDevices)
            r.IsCurrent = r.Id == id;
    }

    private void RefreshConnectionStatus()
    {
        if (_heos.IsConnected)
        {
            var pid = _heos.ActivePlayerId?.ToString() ?? "?";
            ConnectionStatusText = $"已连接 {_heos.ConnectedHost} · 播放器 PID {pid}";
        }
        else
        {
            ConnectionStatusText = "未连接 — 在列表中选择设备将尝试连接 TCP 1255";
        }
    }
}
