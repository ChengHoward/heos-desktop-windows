using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using HeosWpf.Services.Heos;
using HeosWpf.ViewModels.Navigation;
using HeosWpf.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace HeosWpf.ViewModels.Windows;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISavedHeosDeviceStore _store;
    private readonly IHeosClient _heos;
    private readonly INavigationService _navigation;
    private bool _suppressMenuSelection;

    public MainWindowViewModel(
        ISavedHeosDeviceStore store,
        IHeosClient heos,
        INavigationService navigation)
    {
        _store = store;
        _heos = heos;
        _navigation = navigation;

        _store.DevicesChanged += (_, _) =>
            Application.Current.Dispatcher.Invoke(RebuildMenuItems);

        FooterMenuItems.Add(
            new NavigationViewItem("我的设备", SymbolRegular.Speaker224, typeof(DevicesPage)));
        FooterMenuItems.Add(
            new NavigationViewItem("设置", SymbolRegular.Settings24, typeof(SettingsPage)));

        RebuildMenuItems();
    }

    [ObservableProperty]
    private string applicationTitle = "HEOS Desktop";

    public ObservableCollection<object> MenuItems { get; } = new();

    public ObservableCollection<object> FooterMenuItems { get; } = new();

    public void RebuildMenuItems()
    {
        _suppressMenuSelection = true;
        try
        {
            MenuItems.Clear();

            foreach (var d in _store.Devices)
            {
                var title = string.IsNullOrWhiteSpace(d.DisplayName)
                    ? d.Host
                    : d.DisplayName;
                var tag = new SavedDeviceNavTag(d.Id, d.Host);
                var item = new NavigationViewItem
                {
                    Content = title,
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Link24 },
                    Tag = tag,
                };
                // 使用隧道/冒泡的手势订阅：部分主题下 Click 不稳定；子元素命中时需 handledEventsToo。
                item.AddHandler(
                    UIElement.MouseLeftButtonUpEvent,
                    (MouseButtonEventHandler)((_, _) => _ = OnSavedDeviceItemClickAsync(tag)),
                    handledEventsToo: true);
                MenuItems.Add(item);
            }
        }
        finally
        {
            _suppressMenuSelection = false;
        }
    }

    private async Task OnSavedDeviceItemClickAsync(SavedDeviceNavTag tag)
    {
        if (_suppressMenuSelection)
            return;

        _store.SetLastActive(tag.Id);
        try
        {
            await _heos.ConnectAsync(tag.Host);
        }
        catch
        {
            // 设备页会显示连接状态；此处不弹窗以免与自动连接重复
        }

        // 若当前已在正在播放页，Navigate 会因导航栈相同直接失败，需替换内容以切到新连接的设备表现。
        if (!_navigation.Navigate(typeof(NowPlayingPage)))
            _navigation.GetNavigationControl()?.ReplaceContent(typeof(NowPlayingPage));
    }

}
