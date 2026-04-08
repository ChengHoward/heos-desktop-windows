using CommunityToolkit.Mvvm.ComponentModel;
using HeosWpf.Models.Heos;

namespace HeosWpf.ViewModels.Pages;

public partial class SavedHeosDeviceRowViewModel : ObservableObject
{
    public SavedHeosDeviceRowViewModel(SavedHeosDevice device, bool isCurrent)
    {
        Id = device.Id;
        Host = device.Host;
        Title = string.IsNullOrWhiteSpace(device.DisplayName)
            ? device.Host
            : $"{device.DisplayName}  ({device.Host})";
        IsCurrent = isCurrent;
    }

    public Guid Id { get; }

    public string Host { get; }

    public string Title { get; }

    [ObservableProperty]
    private bool isCurrent;
}
