using HeosWpf.Models.Heos;

namespace HeosWpf.Services.Heos;

public interface ISavedHeosDeviceStore
{
    event EventHandler? DevicesChanged;

    IReadOnlyList<SavedHeosDevice> Devices { get; }

    Guid? LastActiveDeviceId { get; }

    SavedHeosDevice Add(string host, string? displayName);

    void Remove(Guid id);

    void SetLastActive(Guid? id);

    SavedHeosDevice? Find(Guid id);

    bool ContainsHost(string host);
}
