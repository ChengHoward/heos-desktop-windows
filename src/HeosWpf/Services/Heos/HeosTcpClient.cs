using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HeosWpf.Models.Heos;

namespace HeosWpf.Services.Heos;

/// <summary>
/// HEOS CLI over TCP :1255 — 发送 <c>heos://...</c> 文本行，读取 JSON 行应答。
/// </summary>
public sealed class HeosTcpClient : IHeosClient
{
    private const int HeosPort = 1255;

    private readonly SemaphoreSlim _io = new(1, 1);
    private int _cid;

    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public bool IsConnected => _tcp?.Connected == true && _reader is not null && _writer is not null;

    public string? ConnectedHost { get; private set; }

    public string? ReconnectHost { get; private set; }

    public int? ActivePlayerId { get; private set; }

    public string? ActivePlayerName { get; private set; }

    public event EventHandler? ConnectionChanged;

    public async Task ConnectAsync(string host, CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TeardownUnsafe();

            var tcp = new TcpClient();
            await tcp.ConnectAsync(host.Trim(), HeosPort, cancellationToken).ConfigureAwait(false);
            var stream = tcp.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
            _writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: false)
            {
                NewLine = "\r\n",
            };
            _tcp = tcp;
            ConnectedHost = host.Trim();

            var players = await GetPlayersUnsafeAsync(cancellationToken).ConfigureAwait(false);
            ActivePlayerId = players.Count > 0 ? players[0].Pid : null;
            ActivePlayerName = players.Count > 0 ? players[0].Name : null;

            ReconnectHost = ConnectedHost;
            RaiseConnectionChanged();
        }
        catch
        {
            TeardownUnsafe();
            throw;
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ReconnectHost = null;
            TeardownUnsafe();
            RaiseConnectionChanged();
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task ClearReconnectTargetAsync(CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ReconnectHost = null;
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task<IReadOnlyList<HeosPlayerInfo>> GetPlayersAsync(CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();
            return await GetPlayersUnsafeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task<string?> GetPlayStateAsync(int playerId, CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();
            using var doc = await SendCommandUnsafeAsync(
                    $"player/get_play_state?pid={playerId}",
                    "player/get_play_state",
                    cancellationToken)
                .ConfigureAwait(false);
            var msg = doc.RootElement.GetProperty("heos").GetProperty("message").GetString();
            return ParseMessageValue(msg, "state");
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task SetPlayStateAsync(int playerId, string state, CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();
            using var doc = await SendCommandUnsafeAsync(
                    $"player/set_play_state?pid={playerId}&state={Uri.EscapeDataString(state)}",
                    "player/set_play_state",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task<int?> GetVolumeAsync(int playerId, CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();
            using var doc = await SendCommandUnsafeAsync(
                    $"player/get_volume?pid={playerId}",
                    "player/get_volume",
                    cancellationToken)
                .ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("payload", out var payload))
            {
                if (payload.TryGetProperty("level", out var level) && level.ValueKind == JsonValueKind.Number)
                    return level.GetInt32();
            }

            var msg = doc.RootElement.GetProperty("heos").TryGetProperty("message", out var m)
                ? m.GetString()
                : null;
            var s = ParseMessageValue(msg, "level");
            return int.TryParse(s, out var v) ? v : null;
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task SetVolumeAsync(int playerId, int level, CancellationToken cancellationToken = default)
    {
        level = Math.Clamp(level, 0, 100);
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();
            using var doc = await SendCommandUnsafeAsync(
                    $"player/set_volume?pid={playerId}&level={level}",
                    "player/set_volume",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task PlayNextAsync(int playerId, CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();
            using var doc = await SendCommandUnsafeAsync(
                    $"player/play_next?pid={playerId}",
                    "player/play_next",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task PlayPreviousAsync(int playerId, CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();
            try
            {
                using var doc = await SendCommandUnsafeAsync(
                        $"player/play_previous?pid={playerId}",
                        "player/play_previous",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException)
            {
                using var doc = await SendCommandUnsafeAsync(
                        $"player/play_prev?pid={playerId}",
                        "player/play_prev",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task<HeosNowPlaying?> GetNowPlayingAsync(int playerId, CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();
            using var doc = await SendCommandUnsafeAsync(
                    $"player/get_now_playing_media?pid={playerId}",
                    "player/get_now_playing_media",
                    cancellationToken)
                .ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("payload", out var p) || p.ValueKind != JsonValueKind.Object)
                return null;

            return new HeosNowPlaying
            {
                Song = HeosJson.GetString(p, "song", "Song") ?? string.Empty,
                Artist = HeosJson.GetString(p, "artist", "Artist") ?? string.Empty,
                Album = HeosJson.GetString(p, "album", "Album") ?? string.Empty,
                ArtUrl = HeosJson.GetString(p, "art_url", "image_url", "ImageUrl"),
                Source = HeosJson.GetString(p, "source", "station", "name", "source_name", "mid"),
                SourceId = HeosJson.GetInt32(p, "sid", "source_id"),
            };
        }
        finally
        {
            _io.Release();
        }
    }

    /// <summary>
    /// 切换到 AUX 模拟输入。实现依据 Denon / Sound United
    /// <see href="https://rn.dmglobal.com/usmodel/HEOS_CLI_ProtocolSpecification-Version-1.17.pdf">HEOS CLI Protocol Specification v1.17</see>
    /// §4.4.9 Play Input source、§4.4.3 Browse Source（sid=1027「HEOS aux inputs」）、§4.4.7 play_stream 示例。
    /// </summary>
    public async Task PlayAuxInAsync(int playerId, CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();

            Exception? last = null;

            foreach (var input in HeosAuxPlayInputNames)
            {
                try
                {
                    using var doc = await SendCommandUnsafeAsync(
                            $"browse/play_input?pid={playerId}&input={Uri.EscapeDataString(input)}",
                            "browse/play_input",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (IsAuxSwitchRetryable(ex))
                {
                    last = ex;
                }
            }

            // 文档示例：heos://browse/play_stream?pid=1&sid=1441320818&mid=inputs/aux_in_1 — sid 来自 get_music_sources，未必是 1027。
            try
            {
                if (await TryPlayAuxDirectPlayStreamUnsafeAsync(playerId, cancellationToken).ConfigureAwait(false))
                    return;
            }
            catch (Exception ex) when (IsAuxSwitchRetryable(ex))
            {
                last = ex;
            }

            try
            {
                await TryPlayAuxBrowseAllSourceIdsUnsafeAsync(playerId, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsAuxSwitchRetryable(ex))
            {
                last = ex;
            }

            var detail = last?.Message ?? string.Empty;
            throw new IOException(
                string.IsNullOrWhiteSpace(detail)
                    ? "切换 AUX IN 失败（browse/play_input、browse/play_stream、browse 取 mid 均未成功）。"
                    + " 若官方 App 可用：设备可能暂不允许切换外接输入，或需先在 App 中结束当前外接输入（见协议 §4.4.9 多机限制）。"
                    : $"切换 AUX IN 失败：{detail}");
        }
        finally
        {
            _io.Release();
        }
    }

    /// <summary>HEOS CLI v1.17 §4.4.9 列出的与 AUX 相关的 <c>input</c> 名称（节选）。</summary>
    private static readonly string[] HeosAuxPlayInputNames =
    {
        "inputs/aux_in_1",
        "inputs/aux_single",
        "inputs/aux1",
        "inputs/aux_in_2",
        "inputs/aux2",
        "inputs/aux_in_3",
        "inputs/aux3",
        "inputs/aux_in_4",
        "inputs/aux4",
        "inputs/aux5",
        "inputs/aux6",
        "inputs/aux7",
        "inputs/aux_8k",
    };

    private static bool IsAuxSwitchRetryable(Exception ex) =>
        ex is IOException
        or TimeoutException
        or SocketException
        or ObjectDisposedException;

    /// <summary>
    /// 从 <c>browse/get_music_sources</c> 收集用于尝试 AUX 的 <c>sid</c>：优先 1027 与名称含 “aux” 的源，再包含全部 <c>heos_service</c>
    /// （部分固件把外接输入列在其它服务源下，名称未必含 aux）。
    /// </summary>
    private async Task<List<int>> GetAuxRelatedSourceIdsUnsafeAsync(CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        var seen = new HashSet<int>();

        void Add(int sid)
        {
            if (seen.Add(sid))
                ids.Add(sid);
        }

        var auxNamed = new List<int>();
        var heosServiceOther = new List<int>();

        using var doc = await SendCommandUnsafeAsync("browse/get_music_sources", "browse/get_music_sources", cancellationToken)
            .ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in payload.EnumerateArray())
            {
                if (HeosJson.GetInt32(el, "sid", "Sid") is not int sid)
                    continue;
                var type = HeosJson.GetString(el, "type", "Type") ?? string.Empty;
                if (!string.Equals(type, "heos_service", StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = HeosJson.GetString(el, "name", "Name") ?? string.Empty;
                if (name.Contains("aux", StringComparison.OrdinalIgnoreCase))
                    auxNamed.Add(sid);
                else
                    heosServiceOther.Add(sid);
            }
        }

        auxNamed.Sort();
        heosServiceOther.Sort();

        Add(1027);
        foreach (var sid in auxNamed)
            Add(sid);
        foreach (var sid in heosServiceOther)
            Add(sid);

        return ids;
    }

    /// <summary>
    /// 按 v1.17 示例：不经过 browse 即尝试 <c>browse/play_stream?pid&amp;sid&amp;mid=inputs/…</c>。
    /// </summary>
    private async Task<bool> TryPlayAuxDirectPlayStreamUnsafeAsync(int playerId, CancellationToken cancellationToken)
    {
        var sids = await GetAuxRelatedSourceIdsUnsafeAsync(cancellationToken).ConfigureAwait(false);
        foreach (var sid in sids)
        {
            foreach (var mid in HeosAuxPlayInputNames)
            {
                try
                {
                    await SendCommandUnsafeAsync(
                            $"browse/play_stream?pid={playerId}&sid={sid}&mid={Uri.EscapeDataString(mid)}",
                            "browse/play_stream",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }
                catch (IOException)
                {
                    // 下一组合
                }
            }
        }

        return false;
    }

    private async Task TryPlayAuxBrowseAllSourceIdsUnsafeAsync(int playerId, CancellationToken cancellationToken)
    {
        var sids = await GetAuxRelatedSourceIdsUnsafeAsync(cancellationToken).ConfigureAwait(false);
        IOException? last = null;

        foreach (var auxSid in sids)
        {
            try
            {
                await TryPlayAuxBrowseSingleSidUnsafeAsync(playerId, auxSid, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (IOException ex)
            {
                last = ex;
            }
        }

        throw last ?? new IOException("browse/browse + browse/play_stream 未找到可用的 AUX 条目。");
    }

    private async Task TryPlayAuxBrowseSingleSidUnsafeAsync(int playerId, int auxSid, CancellationToken cancellationToken)
    {
        using var browseDoc = await BrowseBrowseWithRangeRetryUnsafeAsync(auxSid, cid: null, cancellationToken)
            .ConfigureAwait(false);

        if (!TryGetBrowsePayloadArray(browseDoc.RootElement, out var payload) || payload.ValueKind != JsonValueKind.Array)
            throw new IOException($"浏览源 sid={auxSid} 无列表（browse/browse）。");

        var midsOrdered = CollectAuxMediaIdsFromBrowsePayload(payload);
        if (midsOrdered.Count == 0)
            midsOrdered = await TryBrowseAuxChildContainersUnsafeAsync(auxSid, payload, cancellationToken).ConfigureAwait(false);

        if (midsOrdered.Count == 0)
            throw new IOException($"AUX 源 sid={auxSid} 下列表为空或缺少 mid。");

        foreach (var mid in midsOrdered)
        {
            try
            {
                await SendCommandUnsafeAsync(
                        $"browse/play_stream?pid={playerId}&sid={auxSid}&mid={Uri.EscapeDataString(mid)}",
                        "browse/play_stream",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            catch (IOException)
            {
                // 尝试下一个 mid
            }
        }

        throw new IOException($"sid={auxSid} 下 browse/play_stream 均未成功。");
    }

    private async Task<List<string>> TryBrowseAuxChildContainersUnsafeAsync(
        int auxSid,
        JsonElement rootPayload,
        CancellationToken cancellationToken)
    {
        foreach (var el in rootPayload.EnumerateArray())
        {
            if (!IsBrowseContainerItemType(HeosJson.GetString(el, "type", "Type")))
                continue;
            var cid = HeosJson.GetString(el, "cid", "Cid")
                ?? HeosJson.GetString(el, "id", "Id");
            if (string.IsNullOrWhiteSpace(cid))
                continue;

            using var childDoc = await BrowseBrowseWithRangeRetryUnsafeAsync(auxSid, cid, cancellationToken)
                .ConfigureAwait(false);
            if (!TryGetBrowsePayloadArray(childDoc.RootElement, out var childPayload)
                || childPayload.ValueKind != JsonValueKind.Array)
                continue;

            var mids = CollectAuxMediaIdsFromBrowsePayload(childPayload);
            if (mids.Count > 0)
                return mids;
        }

        return new List<string>();
    }

    private async Task<JsonDocument> BrowseBrowseRawUnsafeAsync(
        int sid,
        string? cid,
        string? rangeSuffix,
        CancellationToken cancellationToken)
    {
        var q = $"browse/browse?sid={sid}";
        if (!string.IsNullOrEmpty(cid))
            q += $"&cid={Uri.EscapeDataString(cid)}";
        if (!string.IsNullOrEmpty(rangeSuffix))
            q += rangeSuffix;
        return await SendCommandUnsafeAsync(q, "browse/browse", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// v1.17：首包可能返回空 <c>payload</c> 但 <c>message</c> 含 <c>count</c>；用 <c>range=0,end</c> 再取一页。
    /// </summary>
    private async Task<JsonDocument> BrowseBrowseWithRangeRetryUnsafeAsync(
        int sid,
        string? cid,
        CancellationToken cancellationToken)
    {
        var doc = await BrowseBrowseRawUnsafeAsync(sid, cid, null, cancellationToken).ConfigureAwait(false);
        if (!TryGetBrowsePayloadArray(doc.RootElement, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return doc;

        if (arr.GetArrayLength() > 0)
            return doc;

        var total = TryParseBrowseTotalCount(doc.RootElement);
        doc.Dispose();

        if (total is int t && t > 0)
        {
            var end = Math.Min(Math.Max(t - 1, 0), 99);
            return await BrowseBrowseRawUnsafeAsync(sid, cid, $"&range=0,{end}", cancellationToken).ConfigureAwait(false);
        }

        return await BrowseBrowseRawUnsafeAsync(sid, cid, "&range=0,99", cancellationToken).ConfigureAwait(false);
    }

    private static bool TryGetBrowsePayloadArray(JsonElement root, out JsonElement arrayPayload)
    {
        arrayPayload = default;
        if (!root.TryGetProperty("payload", out var payload))
            return false;

        if (payload.ValueKind == JsonValueKind.Array)
        {
            arrayPayload = payload;
            return true;
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "items", "Items", "browse", "Browse", "payload" })
            {
                if (payload.TryGetProperty(key, out var inner) && inner.ValueKind == JsonValueKind.Array)
                {
                    arrayPayload = inner;
                    return true;
                }
            }

            foreach (var prop in payload.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    arrayPayload = prop.Value;
                    return true;
                }
            }
        }

        return false;
    }

    private static int? TryParseBrowseTotalCount(JsonElement root)
    {
        if (!root.TryGetProperty("heos", out var heos))
            return null;
        if (!heos.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.String)
            return null;
        var msg = m.GetString();
        var c = ParseMessageValue(msg, "count");
        return int.TryParse(c, out var n) && n > 0 ? n : null;
    }

    private static bool IsBrowseContainerItemType(string? type) =>
        type is not null
        && (string.Equals(type, "container", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "folder", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "directory", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> EnumerateMediaIdCandidatesFromBrowseItem(JsonElement el)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in new[]
                 {
                     "mid", "Mid", "id", "Id", "media_id", "mediaId", "MediaId", "path", "Path", "hmb_id", "HmbId",
                 })
        {
            if (!el.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.String)
                continue;
            var s = p.GetString();
            if (string.IsNullOrWhiteSpace(s))
                continue;
            var t = s.Trim();
            if (seen.Add(t))
                yield return t;
        }

        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
                continue;
            var s = prop.Value.GetString();
            if (string.IsNullOrWhiteSpace(s))
                continue;
            var t = s.Trim();
            if (!t.StartsWith("inputs/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (seen.Add(t))
                yield return t;
        }
    }

    private static List<string> CollectAuxMediaIdsFromBrowsePayload(JsonElement payload)
    {
        var preferred = new List<string>();
        var fallback = new List<string>();

        foreach (var el in payload.EnumerateArray())
        {
            foreach (var mid in EnumerateMediaIdCandidatesFromBrowseItem(el))
            {
                if (string.IsNullOrWhiteSpace(mid))
                    continue;

                var name = HeosJson.GetString(el, "name", "Name") ?? string.Empty;
                var type = HeosJson.GetString(el, "type", "Type") ?? string.Empty;

                var looksAux = name.Contains("aux", StringComparison.OrdinalIgnoreCase)
                    || mid.Contains("aux", StringComparison.OrdinalIgnoreCase);

                if (looksAux)
                    preferred.Add(mid);
                else if (string.Equals(type, "station", StringComparison.OrdinalIgnoreCase)
                         || mid.StartsWith("inputs/", StringComparison.OrdinalIgnoreCase))
                    fallback.Add(mid);
            }
        }

        static IEnumerable<string> DistinctPreserveOrder(IEnumerable<string> source)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in source)
            {
                if (seen.Add(s))
                    yield return s;
            }
        }

        var combined = DistinctPreserveOrder(preferred).Concat(DistinctPreserveOrder(fallback)).ToList();
        return combined;
    }

    public async Task RestartPlayerAsync(int playerId, CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectedUnsafe();
            using var doc = await SendCommandUnsafeAsync(
                    $"player/reboot?pid={playerId}",
                    "player/reboot",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _io.Release();
        }
    }

    public void Dispose()
    {
        _io.Wait();
        try
        {
            ReconnectHost = null;
            TeardownUnsafe();
        }
        finally
        {
            _io.Release();
            _io.Dispose();
        }
    }

    private void EnsureConnectedUnsafe()
    {
        if (!IsConnected)
            throw new InvalidOperationException("尚未连接 HEOS 设备。");
    }

    private void TeardownUnsafe()
    {
        ConnectedHost = null;
        ActivePlayerId = null;
        ActivePlayerName = null;

        try
        {
            _reader?.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            _writer?.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            _tcp?.Dispose();
        }
        catch
        {
            // ignored
        }

        _reader = null;
        _writer = null;
        _tcp = null;
    }

    private void HandleConnectionLostUnsafe()
    {
        if (_tcp is null && _reader is null)
            return;
        TeardownUnsafe();
        RaiseConnectionChanged();
    }

    private void RaiseConnectionChanged() =>
        ConnectionChanged?.Invoke(this, EventArgs.Empty);

    private async Task<IReadOnlyList<HeosPlayerInfo>> GetPlayersUnsafeAsync(CancellationToken cancellationToken)
    {
        using var doc = await SendCommandUnsafeAsync("player/get_players", "player/get_players", cancellationToken)
            .ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Array)
            return Array.Empty<HeosPlayerInfo>();

        var list = new List<HeosPlayerInfo>();
        foreach (var el in payload.EnumerateArray())
        {
            var pid = HeosJson.ReadPid(el);
            if (pid == 0)
                continue;
            list.Add(
                new HeosPlayerInfo
                {
                    Pid = pid,
                    Name = HeosJson.GetString(el, "name", "Name") ?? string.Empty,
                    Model = HeosJson.GetString(el, "model", "Model") ?? string.Empty,
                    Ip = HeosJson.GetString(el, "ip", "Ip") ?? string.Empty,
                });
        }

        return list;
    }

    private async Task<JsonDocument> SendCommandUnsafeAsync(
        string pathAndQuery,
        string expectedCommand,
        CancellationToken cancellationToken)
    {
        if (_writer is null || _reader is null)
            throw new InvalidOperationException("连接未就绪。");

        var cid = Interlocked.Increment(ref _cid);
        var withCid = pathAndQuery.Contains('?', StringComparison.Ordinal)
            ? $"{pathAndQuery}&cid={cid}"
            : $"{pathAndQuery}?cid={cid}";

        try
        {
            await _writer.WriteLineAsync($"heos://{withCid}").ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            HandleConnectionLostUnsafe();
            throw;
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        for (var n = 0; n < 200 && DateTime.UtcNow < deadline; n++)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            using var slice = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            slice.CancelAfter(remaining);

            string? line;
            try
            {
                line = await _reader.ReadLineAsync(slice.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                HandleConnectionLostUnsafe();
                throw;
            }

            if (line is null)
            {
                HandleConnectionLostUnsafe();
                throw new IOException("HEOS 连接已关闭。");
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            line = line.Trim();
            if (line.Length == 0 || line[0] != '{')
                continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("heos", out var heos))
                    continue;

                var cmd = heos.GetProperty("command").GetString();
                if (!string.Equals(cmd, expectedCommand, StringComparison.OrdinalIgnoreCase))
                    continue;

                var result = heos.GetProperty("result").GetString();
                if (!string.Equals(result, "success", StringComparison.OrdinalIgnoreCase))
                {
                    var msg = heos.TryGetProperty("message", out var m) ? m.GetString() : null;
                    throw new IOException($"HEOS 返回失败：{result} {msg}");
                }

                return JsonDocument.Parse(line);
            }
        }

        throw new TimeoutException($"等待 HEOS 应答超时：{expectedCommand}");
    }

    private static string? ParseMessageValue(string? message, string key)
    {
        if (string.IsNullOrEmpty(message))
            return null;

        foreach (var part in message.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            var k = part[..eq];
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.UnescapeDataString(part[(eq + 1)..]);
        }

        return null;
    }
}

internal static class HeosJson
{
    public static int ReadPid(JsonElement el)
    {
        if (!el.TryGetProperty("pid", out var p))
            return 0;
        return p.ValueKind switch
        {
            JsonValueKind.Number => p.TryGetInt32(out var i) ? i : 0,
            JsonValueKind.String => int.TryParse(p.GetString(), out var j) ? j : 0,
            _ => 0,
        };
    }

    public static string? GetString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var p))
                continue;
            if (p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }

        return null;
    }

    public static int? GetInt32(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var p))
                continue;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
                return i;
            if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var j))
                return j;
        }

        return null;
    }
}
