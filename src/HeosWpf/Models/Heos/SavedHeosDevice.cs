namespace HeosWpf.Models.Heos;

public sealed class SavedHeosDevice
{
    public Guid Id { get; set; }

    public string Host { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
}
