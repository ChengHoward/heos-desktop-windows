using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeosWpf.Models.Heos;

namespace HeosWpf.Services.Heos;

public sealed class JsonSavedHeosDeviceStore : ISavedHeosDeviceStore
{
    private readonly string _filePath;
    private readonly ObservableCollection<SavedHeosDevice> _devices = new();
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public event EventHandler? DevicesChanged;

    public JsonSavedHeosDeviceStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeosWpf");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "devices.json");
        Load();
    }

    public IReadOnlyList<SavedHeosDevice> Devices => _devices;

    public Guid? LastActiveDeviceId { get; private set; }

    public SavedHeosDevice Add(string host, string? displayName)
    {
        var h = host.Trim();
        var d = new SavedHeosDevice
        {
            Id = Guid.NewGuid(),
            Host = h,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
        };
        _devices.Add(d);
        Save();
        RaiseDevicesChanged();
        return d;
    }

    public void Remove(Guid id)
    {
        for (var i = _devices.Count - 1; i >= 0; i--)
        {
            if (_devices[i].Id == id)
                _devices.RemoveAt(i);
        }

        if (LastActiveDeviceId == id)
            LastActiveDeviceId = null;

        Save();
        RaiseDevicesChanged();
    }

    public void SetLastActive(Guid? id)
    {
        LastActiveDeviceId = id;
        Save();
    }

    public SavedHeosDevice? Find(Guid id)
    {
        foreach (var d in _devices)
        {
            if (d.Id == id)
                return d;
        }

        return null;
    }

    public bool ContainsHost(string host)
    {
        var h = host.Trim();
        foreach (var d in _devices)
        {
            if (string.Equals(d.Host, h, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void Load()
    {
        _devices.Clear();
        LastActiveDeviceId = null;
        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<DeviceFileDto>(json, _json);
            if (dto?.Devices is null)
                return;

            LastActiveDeviceId = dto.LastActiveDeviceId;
            foreach (var d in dto.Devices)
                _devices.Add(d);
        }
        catch
        {
            // ignore corrupt file
        }
    }

    private void Save()
    {
        var dto = new DeviceFileDto
        {
            LastActiveDeviceId = LastActiveDeviceId,
            Devices = _devices.ToList(),
        };
        var json = JsonSerializer.Serialize(dto, _json);
        File.WriteAllText(_filePath, json);
    }

    private void RaiseDevicesChanged() =>
        DevicesChanged?.Invoke(this, EventArgs.Empty);

    private sealed class DeviceFileDto
    {
        public List<SavedHeosDevice> Devices { get; set; } = new();

        public Guid? LastActiveDeviceId { get; set; }
    }
}
