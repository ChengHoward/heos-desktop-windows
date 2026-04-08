using CommunityToolkit.Mvvm.ComponentModel;
using HeosWpf.Services.Heos;

namespace HeosWpf.ViewModels.Pages;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly IHeosAppSettings _settings;
    private bool _suppressSave;

    public SettingsPageViewModel(IHeosAppSettings settings)
    {
        _settings = settings;
        _suppressSave = true;
        AutoReconnectEnabled = settings.AutoReconnectEnabled;
        _suppressSave = false;
    }

    [ObservableProperty]
    private string subtitle = "主题、网络与调试选项将在此呈现。";

    [ObservableProperty]
    private bool autoReconnectEnabled;

    partial void OnAutoReconnectEnabledChanged(bool value)
    {
        if (_suppressSave)
            return;
        _settings.AutoReconnectEnabled = value;
        _settings.Save();
    }
}
