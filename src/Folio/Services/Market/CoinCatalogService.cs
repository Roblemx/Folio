using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Folio.Models;

namespace Folio.Services.Market;

public interface ICoinCatalogService
{
    Task EnsureLoadedAsync(CancellationToken ct = default);
    IReadOnlyList<Coin> Search(string query, int limit = 20);
    Coin? Get(string id);
}

/// <summary>
/// The coin catalog used by the coin picker. The full list is fetched once and cached to
/// disk for a week; search ranks exact-symbol matches first, then prefix and substring
/// matches over symbol and name.
/// </summary>
public sealed class CoinCatalogService : ICoinCatalogService
{
    private const string CacheKey = "coin-list";
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    private readonly IMarketDataClient _client;
    private readonly CacheStore _cache;
    private IReadOnlyList<Coin> _coins = Array.Empty<Coin>();
    private Dictionary<string, Coin> _byId = new();

    public CoinCatalogService(IMarketDataClient client, CacheStore cache)
    {
        _client = client;
        _cache = cache;
    }

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_coins.Count > 0)
        {
            return;
        }

        var cached = _cache.Read<List<Coin>>(CacheKey);
        if (cached != null && DateTimeOffset.UtcNow - cached.FetchedAt < Ttl)
        {
            SetCoins(cached.Value);
            return;
        }

        try
        {
            var list = await _client.GetCoinListAsync(ct);
            SetCoins(list);
            _cache.Write(CacheKey, list.ToList());
        }
        catch
        {
            if (cached != null)
            {
                SetCoins(cached.Value); // offline → use stale cache if present
            }
        }
    }

    public IReadOnlyList<Coin> Search(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<Coin>();
        }

        var q = query.Trim();
        return _coins
            .Select(c => (Coin: c, Score: Score(c, q)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Coin.Name.Length)
            .ThenBy(x => x.Coin.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => x.Coin)
            .ToList();
    }

    public Coin? Get(string id) => _byId.TryGetValue(id, out var c) ? c : null;

    private void SetCoins(IReadOnlyList<Coin> coins)
    {
        _coins = coins;
        _byId = coins.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
    }

    private static int Score(Coin c, string q)
    {
        var sym = c.Symbol;
        var name = c.Name;

        // Exact id is the canonical coin (e.g. "bitcoin"), so it outranks meme coins that
        // merely use a popular ticker as their symbol or name.
        if (string.Equals(c.Id, q, StringComparison.OrdinalIgnoreCase))
        {
            return 120;
        }

        if (string.Equals(sym, q, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (string.Equals(name, q, StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        if (sym.StartsWith(q, StringComparison.OrdinalIgnoreCase))
        {
            return 70;
        }

        if (name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
        {
            return 60;
        }

        if (name.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 30;
        }

        if (sym.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        return 0;
    }
}
