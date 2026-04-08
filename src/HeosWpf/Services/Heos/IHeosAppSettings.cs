namespace HeosWpf.Services.Heos;

public interface IHeosAppSettings
{
    /// <summary>意外断线后是否在后台自动重试连接（关闭时改为弹窗由用户选择）。</summary>
    bool AutoReconnectEnabled { get; set; }

    void Save();
}
