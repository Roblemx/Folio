using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Folio.Models;

namespace Folio.Services.Market;

public interface IPriceService
{
    IReadOnlyDictionary<string, PriceSnapshot> Snapshots { get; }

    /// <summary>True until a successful refresh; set again if a refresh fails (last-known shown).</summary>
    bool IsStale { get; }

    DateTimeOffset? LastUpdated { get; }

    Task RefreshAsync(IReadOnlyCollection<string> ids, string vsCurrency, CancellationToken ct = default);

    event EventHandler? Updated;
}

/// <summary>
/// Holds the latest market snapshots. Successful refreshes update memory + disk cache and
/// clear the stale flag; failures keep the last-known data and mark it stale, so the app
/// keeps working offline.
/// </summary>
public sealed class PriceService : IPriceService
{
    private const string CacheKey = "prices";

    private readonly IMarketDataClient _client;
    private readonly CacheStore _cache;
    private Dictionary<string, PriceSnapshot> _snapshots = new();

    public PriceService(IMarketDataClient client, CacheStore cache)
    {
        _client = client;
        _cache = cache;

        var cached = _cache.Read<List<PriceSnapshot>>(CacheKey);
        if (cached != null)
        {
            _snapshots = cached.Value.ToDictionary(s => s.CoinId);
            LastUpdated = cached.FetchedAt;
        }
    }

    public IReadOnlyDictionary<string, PriceSnapshot> Snapshots => _snapshots;

    public bool IsStale { get; private set; } = true;

    public DateTimeOffset? LastUpdated { get; private set; }

    public event EventHandler? Updated;

    public async Task RefreshAsync(IReadOnlyCollection<string> ids, string vsCurrency, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return;
        }

        try
        {
            var entries = await _client.GetMarketsAsync(ids, vsCurrency, ct);
            var now = DateTimeOffset.UtcNow;
            var snaps = entries
                .Select(e => new PriceSnapshot(e.Id, e.Symbol, e.Name, e.ImageUrl, e.Price, e.Change24h, e.MarketCap, e.Sparkline7d, now))
                .ToList();

            _snapshots = snaps.ToDictionary(s => s.CoinId);
            LastUpdated = now;
            IsStale = false;
            _cache.Write(CacheKey, snaps);
        }
        catch
        {
            IsStale = true; // keep last-known snapshots
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }
}
