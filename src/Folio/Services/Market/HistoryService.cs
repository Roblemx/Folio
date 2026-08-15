using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Folio.Models;

namespace Folio.Services.Market;

public interface IHistoryService
{
    /// <summary>Range is one of 24H / 1W / 1M / 1Y / ALL.</summary>
    Task<IReadOnlyList<HistoryPoint>> GetAsync(string id, string vsCurrency, string range, CancellationToken ct = default);
}

/// <summary>Fetches historical price series per coin/range, cached with a short TTL and
/// downsampled for rendering. Falls back to the cached series on failure.</summary>
public sealed class HistoryService : IHistoryService
{
    private const int MaxPoints = 220;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private readonly IMarketDataClient _client;
    private readonly CacheStore _cache;

    public HistoryService(IMarketDataClient client, CacheStore cache)
    {
        _client = client;
        _cache = cache;
    }

    public async Task<IReadOnlyList<HistoryPoint>> GetAsync(string id, string vsCurrency, string range, CancellationToken ct = default)
    {
        var key = $"hist-{id}-{vsCurrency}-{range}";
        var cached = _cache.Read<List<HistoryPoint>>(key);
        if (cached != null && DateTimeOffset.UtcNow - cached.FetchedAt < Ttl)
        {
            return cached.Value;
        }

        try
        {
            var points = await _client.GetMarketChartAsync(id, vsCurrency, RangeToDays(range), ct);
            var sampled = Downsample(points, MaxPoints);
            _cache.Write(key, sampled);
            return sampled;
        }
        catch
        {
            return cached?.Value ?? (IReadOnlyList<HistoryPoint>)Array.Empty<HistoryPoint>();
        }
    }

    public static string RangeToDays(string range) => range switch
    {
        "24H" => "1",
        "1W" => "7",
        "1M" => "30",
        "1Y" => "365",
        "ALL" => "max",
        _ => "7"
    };

    public static List<HistoryPoint> Downsample(IReadOnlyList<HistoryPoint> src, int max)
    {
        if (src.Count <= max)
        {
            return src.ToList();
        }

        var result = new List<HistoryPoint>(max);
        var step = (double)(src.Count - 1) / (max - 1);
        for (var i = 0; i < max; i++)
        {
            result.Add(src[(int)Math.Round(i * step)]);
        }

        return result;
    }
}
