using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using HeosWpf.Models.Heos;

namespace HeosWpf.Services.Heos;

public sealed class HeosSsdpDiscoveryService : IHeosDeviceDiscoveryService
{
    private const string SsdpMulticast = "239.255.255.250";
    private const int SsdpPort = 1900;

    private static readonly string[] SearchTargets =
    {
        "urn:schemas-denon-com:device:ACT-Denon:1",
        "urn:schemas-denon-com:device:AiosDevice:1",
        "urn:schemas-marantz-com:device:ACT-Marantz:1",
    };

    public async Task<IReadOnlyList<DiscoveredHeosDevice>> DiscoverAsync(
        TimeSpan listenDuration,
        bool resolveFriendlyNames,
        CancellationToken cancellationToken = default)
    {
        if (listenDuration <= TimeSpan.Zero)
            return Array.Empty<DiscoveredHeosDevice>();

        var localIp = GetOutboundIPv4();
        if (localIp is null)
            throw new InvalidOperationException("未找到可用的 IPv4 网卡地址，无法发送 SSDP 发现。");

        var multicast = IPAddress.Parse(SsdpMulticast);
        var rawByIp = new ConcurrentDictionary<string, RawSsdpDevice>(StringComparer.OrdinalIgnoreCase);

        using var udp = new UdpClient(new IPEndPoint(localIp, 0));
        udp.JoinMulticastGroup(multicast, localIp);

        try
        {
            foreach (var st in SearchTargets)
            {
                var payload = BuildMSearch(st);
                await udp.SendAsync(payload, new IPEndPoint(multicast, SsdpPort), cancellationToken)
                    .ConfigureAwait(false);
            }

            var receiveTask = ReceiveLoopAsync(udp, rawByIp, listenDuration, cancellationToken);
            await receiveTask.ConfigureAwait(false);
        }
        finally
        {
            try
            {
                udp.DropMulticastGroup(multicast);
            }
            catch
            {
                // ignored
            }
        }

        var merged = MergeByBestLocation(rawByIp.Values);
        if (!resolveFriendlyNames)
            return merged;

        var bag = new ConcurrentBag<DiscoveredHeosDevice>();
        await Parallel.ForEachAsync(
                merged,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
                async (r, ct) =>
                {
                    var name = await TryGetFriendlyNameAsync(r.Location, ct).ConfigureAwait(false);
                    bag.Add(new DiscoveredHeosDevice(r.IpAddress, r.Location, r.SearchTarget, r.Server, name));
                })
            .ConfigureAwait(false);

        return bag.OrderBy(d => d.IpAddress, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task ReceiveLoopAsync(
        UdpClient udp,
        ConcurrentDictionary<string, RawSsdpDevice> sink,
        TimeSpan listenDuration,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + listenDuration;

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            try
            {
                using var slice = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                slice.CancelAfter(remaining);

                var result = await udp.ReceiveAsync(slice.Token).ConfigureAwait(false);
                var text = Encoding.UTF8.GetString(result.Buffer);
                if (!SsdpResponseParser.TryParse(text, out var location, out var st, out var server))
                    continue;

                if (!IsLikelyHeos(st, server, text))
                    continue;

                if (!Uri.TryCreate(location, UriKind.Absolute, out var uri))
                    continue;

                var ip = uri.Host;
                if (string.IsNullOrWhiteSpace(ip))
                    continue;

                sink.AddOrUpdate(
                    ip,
                    _ => new RawSsdpDevice(ip, location, st, server),
                    (_, existing) => PreferDevice(existing, new RawSsdpDevice(ip, location, st, server)));
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static RawSsdpDevice PreferDevice(RawSsdpDevice a, RawSsdpDevice b)
    {
        var scoreA = ScoreLocation(a.Location);
        var scoreB = ScoreLocation(b.Location);
        return scoreB > scoreA ? b : a;
    }

    private static int ScoreLocation(string location)
    {
        if (location.Contains("60006", StringComparison.Ordinal))
            return 2;
        return 1;
    }

    private static List<DiscoveredHeosDevice> MergeByBestLocation(IEnumerable<RawSsdpDevice> raw)
    {
        return raw
            .GroupBy(r => r.Ip, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => ScoreLocation(x.Location)).First())
            .Select(r => new DiscoveredHeosDevice(r.Ip, r.Location, r.St, r.Server, null))
            .OrderBy(d => d.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsLikelyHeos(string? st, string? server, string raw)
    {
        var hay = $"{st} {server} {raw}";
        return hay.Contains("denon", StringComparison.OrdinalIgnoreCase)
               || hay.Contains("marantz", StringComparison.OrdinalIgnoreCase)
               || hay.Contains("heos", StringComparison.OrdinalIgnoreCase)
               || hay.Contains("AiosDevice", StringComparison.Ordinal)
               || hay.Contains("ACT-Denon", StringComparison.Ordinal)
               || hay.Contains("ACT-Marantz", StringComparison.Ordinal);
    }

    private static byte[] BuildMSearch(string searchTarget)
    {
        var msg =
            "M-SEARCH * HTTP/1.1\r\n" +
            $"HOST: {SsdpMulticast}:{SsdpPort}\r\n" +
            "MAN: \"ssdp:discover\"\r\n" +
            $"ST: {searchTarget}\r\n" +
            "MX: 3\r\n" +
            "\r\n";
        return Encoding.UTF8.GetBytes(msg);
    }

    private static IPAddress? GetOutboundIPv4()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect(IPAddress.Parse("8.8.8.8"), 65530);
            if (s.LocalEndPoint is IPEndPoint ep)
                return ep.Address;
        }
        catch
        {
            // fallback below
        }

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback)
                continue;

            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (IPAddress.IsLoopback(ua.Address))
                    continue;
                if (ua.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                    continue;
                return ua.Address;
            }
        }

        return null;
    }

    private static async Task<string?> TryGetFriendlyNameAsync(string location, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var xml = await http.GetStringAsync(new Uri(location), cancellationToken).ConfigureAwait(false);
            var m = Regex.Match(xml, @"<friendlyName>(?<n>[^<]+)</friendlyName>", RegexOptions.IgnoreCase);
            if (!m.Success)
                return null;
            return WebUtility.HtmlDecode(m.Groups["n"].Value.Trim());
        }
        catch
        {
            return null;
        }
    }

    private sealed record RawSsdpDevice(string Ip, string Location, string? St, string? Server);
}

internal static class SsdpResponseParser
{
    public static bool TryParse(string raw, out string location, out string? st, out string? server)
    {
        location = string.Empty;
        st = null;
        server = null;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        using var reader = new StringReader(raw);
        var first = reader.ReadLine();
        if (first is null || !first.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
            return false;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                break;

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();

            if (name.Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
                location = value;
            else if (name.Equals("ST", StringComparison.OrdinalIgnoreCase))
                st = value;
            else if (name.Equals("SERVER", StringComparison.OrdinalIgnoreCase))
                server = value;
        }

        return !string.IsNullOrWhiteSpace(location);
    }
}
