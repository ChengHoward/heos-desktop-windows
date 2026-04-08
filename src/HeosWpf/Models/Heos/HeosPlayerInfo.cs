namespace HeosWpf.Models.Heos;

public sealed class HeosPlayerInfo
{
    public int Pid { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Ip { get; init; } = string.Empty;
}
