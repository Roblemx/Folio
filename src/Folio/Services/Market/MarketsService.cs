using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Folio.Models;

namespace Folio.Services.Market;

public interface IMarketsService
{
    /// <summary>Top coins by market cap (USD), cached with a short TTL; last-known on failure.</summary>
    Task<IReadOnlyList<MarketEntry>> GetTopAsync(int count, CancellationToken ct = default);

    /// <summary>The Crypto Fear &amp; Greed index, cached; last-known on failure.</summary>
    Task<FearGreed?> GetFearGreedAsync(CancellationToken ct = default);
}

/// <summary>
/// Backs the Markets screen: top coins (via the market-data client) and the Fear &amp; Greed
/// index (from alternative.me). Both are disk-cached so the screen works offline and stays
/// within API rate limits. Prices are fetched in USD and converted for display via FX.
/// </summary>
public sealed class MarketsService : IMarketsService
{
    private const string TopCacheKey = "markets-top";
    private const string FngCacheKey = "fear-greed";
    private const string FngUrl = "https://api.alternative.me/fng/?limit=1";

    private static readonly TimeSpan TopTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FngTtl = TimeSpan.FromMinutes(10);

    private readonly IMarketDataClient _client;
    private readonly HttpClient _http;
    private readonly CacheStore _cache;

    public MarketsService(IMarketDataClient client, HttpClient http, CacheStore cache)
    {
        _client = client;
        _http = http;
        _cache = cache;
    }

    public async Task<IReadOnlyList<MarketEntry>> GetTopAsync(int count, CancellationToken ct = default)
    {
        var cached = _cache.Read<List<MarketEntry>>(TopCacheKey);
        if (cached != null && DateTimeOffset.UtcNow - cached.FetchedAt < TopTtl)
        {
            return cached.Value;
        }

        try
        {
            var top = await _client.GetTopMarketsAsync("usd", count, ct);
            _cache.Write(TopCacheKey, top.ToList());
            return top;
        }
        catch
        {
            return cached?.Value ?? (IReadOnlyList<MarketEntry>)Array.Empty<MarketEntry>();
        }
    }

    public async Task<FearGreed?> GetFearGreedAsync(CancellationToken ct = default)
    {
        var cached = _cache.Read<FearGreed>(FngCacheKey);
        if (cached != null && DateTimeOffset.UtcNow - cached.FetchedAt < FngTtl)
        {
            return cached.Value;
        }

        try
        {
            var json = await _http.GetStringAsync(FngUrl, ct);
            var fg = ParseFearGreed(json);
            if (fg != null)
            {
                _cache.Write(FngCacheKey, fg);
            }

            return fg ?? cached?.Value;
        }
        catch
        {
            return cached?.Value;
        }
    }

    public static FearGreed? ParseFearGreed(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
        {
            return null;
        }

        var entry = data[0];
        if (!entry.TryGetProperty("value", out var v) ||
            !int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        var label = entry.TryGetProperty("value_classification", out var c) ? c.GetString() ?? "" : "";
        var at = entry.TryGetProperty("timestamp", out var t) && long.TryParse(t.GetString(), out var unix)
            ? DateTimeOffset.FromUnixTimeSeconds(unix)
            : DateTimeOffset.UtcNow;

        return new FearGreed(value, label, at);
    }
}
