using HeosWpf.Models.Heos;

namespace HeosWpf.Services.Heos;

public interface IHeosClient : IDisposable
{
    bool IsConnected { get; }

    string? ConnectedHost { get; }

    /// <summary>上次成功连接的主机；仅用户主动断开时清除。意外断线后供自动重连使用。</summary>
    string? ReconnectHost { get; }

    int? ActivePlayerId { get; }

    string? ActivePlayerName { get; }

    event EventHandler? ConnectionChanged;

    Task ConnectAsync(string host, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>清除自动重连目标（用户选择不再重连时调用）。</summary>
    Task ClearReconnectTargetAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeosPlayerInfo>> GetPlayersAsync(CancellationToken cancellationToken = default);

    Task<string?> GetPlayStateAsync(int playerId, CancellationToken cancellationToken = default);

    Task SetPlayStateAsync(int playerId, string state, CancellationToken cancellationToken = default);

    Task<int?> GetVolumeAsync(int playerId, CancellationToken cancellationToken = default);

    Task SetVolumeAsync(int playerId, int level, CancellationToken cancellationToken = default);

    Task PlayNextAsync(int playerId, CancellationToken cancellationToken = default);

    Task PlayPreviousAsync(int playerId, CancellationToken cancellationToken = default);

    Task<HeosNowPlaying?> GetNowPlayingAsync(int playerId, CancellationToken cancellationToken = default);

    Task PlayAuxInAsync(int playerId, CancellationToken cancellationToken = default);

    Task RestartPlayerAsync(int playerId, CancellationToken cancellationToken = default);
}
