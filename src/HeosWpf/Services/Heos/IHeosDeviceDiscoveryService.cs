using HeosWpf.Models.Heos;

namespace HeosWpf.Services.Heos;

public interface IHeosDeviceDiscoveryService
{
    /// <summary>
    /// 通过 SSDP 在局域网发现 HEOS 相关设备。
    /// </summary>
    /// <param name="listenDuration">发送 M-SEARCH 后等待响应的时长。</param>
    /// <param name="resolveFriendlyNames">是否请求设备描述 XML 解析 friendlyName（可能略慢）。</param>
    Task<IReadOnlyList<DiscoveredHeosDevice>> DiscoverAsync(
        TimeSpan listenDuration,
        bool resolveFriendlyNames,
        CancellationToken cancellationToken = default);
}
