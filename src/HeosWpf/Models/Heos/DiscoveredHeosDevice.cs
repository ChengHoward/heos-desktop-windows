namespace HeosWpf.Models.Heos;

/// <summary>
/// HEOS / Denon / Marantz 类设备在 SSDP 中的发现结果（控制通道仍为 TCP 1255）。
/// </summary>
public sealed record DiscoveredHeosDevice(
    string IpAddress,
    string Location,
    string? SearchTarget,
    string? Server,
    string? FriendlyName);
