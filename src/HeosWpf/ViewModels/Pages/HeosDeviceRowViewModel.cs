using CommunityToolkit.Mvvm.ComponentModel;
using HeosWpf.Models.Heos;

namespace HeosWpf.ViewModels.Pages;

public partial class HeosDeviceRowViewModel : ObservableObject
{
    public HeosDeviceRowViewModel(DiscoveredHeosDevice device)
    {
        IpAddress = device.IpAddress;
        Location = device.Location;
        SearchTarget = device.SearchTarget ?? string.Empty;
        Server = device.Server ?? string.Empty;

        DisplayName = string.IsNullOrWhiteSpace(device.FriendlyName)
            ? $"HEOS / Denon（{device.IpAddress}）"
            : device.FriendlyName.Trim();

        Detail = string.IsNullOrWhiteSpace(device.SearchTarget)
            ? device.IpAddress
            : $"{device.IpAddress} · {device.SearchTarget}";
    }

    public string DisplayName { get; }

    public string Detail { get; }

    public string IpAddress { get; }

    public string Location { get; }

    public string SearchTarget { get; }

    public string Server { get; }
}
