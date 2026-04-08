namespace HeosWpf.Models.Heos;

public sealed class HeosNowPlaying
{
    public string Song { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string Album { get; init; } = string.Empty;

    public string? ArtUrl { get; init; }

    public string? Source { get; init; }

    public int? SourceId { get; init; }
}
