using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeosWpf.Services.Heos;

public sealed class JsonHeosAppSettings : IHeosAppSettings
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public JsonHeosAppSettings()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeosWpf");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        Load();
    }

    public bool AutoReconnectEnabled { get; set; } = true;

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return;
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<SettingsDto>(json, _json);
            if (dto?.AutoReconnectEnabled is bool b)
                AutoReconnectEnabled = b;
        }
        catch
        {
            // 使用默认值
        }
    }

    public void Save()
    {
        try
        {
            var dto = new SettingsDto { AutoReconnectEnabled = AutoReconnectEnabled };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(dto, _json));
        }
        catch
        {
            // 忽略写入失败
        }
    }

    private sealed class SettingsDto
    {
        public bool? AutoReconnectEnabled { get; set; }
    }
}
