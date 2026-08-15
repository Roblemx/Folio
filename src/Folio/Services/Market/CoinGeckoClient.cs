using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Folio.Models;

namespace Folio.Services.Market;

/// <summary>Low-level access to the market-data API. The unit of mocking in tests.</summary>
public interface IMarketDataClient
{
    Task<IReadOnlyList<Coin>> GetCoinListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MarketEntry>> GetMarketsAsync(
        IReadOnlyCollection<string> ids, string vsCurrency, CancellationToken ct = default);

    /// <summary>Top coins by market cap (for the Markets screen).</summary>
    Task<IReadOnlyList<MarketEntry>> GetTopMarketsAsync(
        string vsCurrency, int count, CancellationToken ct = default);

    Task<IReadOnlyList<HistoryPoint>> GetMarketChartAsync(
        string id, string vsCurrency, string days, CancellationToken ct = default);

    /// <summary>USD→fiat rates (USD = 1), derived from BTC priced in each currency.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetUsdRatesAsync(
        IReadOnlyCollection<string> fiatCodes, CancellationToken ct = default);
}

/// <summary>CoinGecko public-API implementation with a small retry/backoff on 429/5xx.</summary>
public sealed class CoinGeckoClient : IMarketDataClient
{
    private const string Base = "https://api.coingecko.com/api/v3";
    private const int MaxRetries = 3;

    private readonly HttpClient _http;
    private readonly Func<int, TimeSpan> _backoff;

    public CoinGeckoClient(HttpClient http, Func<int, TimeSpan>? backoff = null)
    {
        _http = http;
        _backoff = backoff ?? (attempt => TimeSpan.FromMilliseconds(400 * Math.Pow(2, attempt)));
    }

    public async Task<IReadOnlyList<Coin>> GetCoinListAsync(CancellationToken ct = default)
    {
        var json = await GetStringAsync($"{Base}/coins/list", ct);
        using var doc = JsonDocument.Parse(json);
        var list = new List<Coin>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var id = Str(el, "id");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            list.Add(new Coin(id, Str(el, "symbol") ?? string.Empty, Str(el, "name") ?? id));
        }

        return list;
    }

    public async Task<IReadOnlyList<MarketEntry>> GetMarketsAsync(
        IReadOnlyCollection<string> ids, string vsCurrency, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<MarketEntry>();
        }

        var idsParam = string.Join(",", ids);
        var url = $"{Base}/coins/markets?vs_currency={vsCurrency}&ids={idsParam}" +
                  "&sparkline=true&price_change_percentage=24h&per_page=250&page=1";

        return ParseMarkets(await GetStringAsync(url, ct));
    }

    public async Task<IReadOnlyList<MarketEntry>> GetTopMarketsAsync(
        string vsCurrency, int count, CancellationToken ct = default)
    {
        var perPage = count <= 0 ? 50 : Math.Min(count, 250);
        var url = $"{Base}/coins/markets?vs_currency={vsCurrency}&order=market_cap_desc" +
                  $"&per_page={perPage}&page=1&sparkline=true&price_change_percentage=24h";

        return ParseMarkets(await GetStringAsync(url, ct));
    }

    private static IReadOnlyList<MarketEntry> ParseMarkets(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<MarketEntry>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var id = Str(el, "id");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var spark = Array.Empty<decimal>() as IReadOnlyList<decimal>;
            if (el.TryGetProperty("sparkline_in_7d", out var s) &&
                s.TryGetProperty("price", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                spark = arr.EnumerateArray()
                    .Where(p => p.ValueKind == JsonValueKind.Number)
                    .Select(p => p.GetDecimal())
                    .ToList();
            }

            result.Add(new MarketEntry(
                id,
                Str(el, "symbol") ?? string.Empty,
                Str(el, "name") ?? id,
                Str(el, "image"),
                Num(el, "current_price") ?? 0m,
                Num(el, "price_change_percentage_24h") ?? 0m,
                Num(el, "market_cap") ?? 0m,
                spark));
        }

        return result;
    }

    public async Task<IReadOnlyList<HistoryPoint>> GetMarketChartAsync(
        string id, string vsCurrency, string days, CancellationToken ct = default)
    {
        var url = $"{Base}/coins/{id}/market_chart?vs_currency={vsCurrency}&days={days}";
        var json = await GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("prices", out var prices) || prices.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<HistoryPoint>();
        }

        var result = new List<HistoryPoint>();
        foreach (var pair in prices.EnumerateArray())
        {
            if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() < 2)
            {
                continue;
            }

            var ms = pair[0].GetInt64();
            var price = pair[1].GetDecimal();
            result.Add(new HistoryPoint(DateTimeOffset.FromUnixTimeMilliseconds(ms), price));
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetUsdRatesAsync(
        IReadOnlyCollection<string> fiatCodes, CancellationToken ct = default)
    {
        var vs = string.Join(",", new[] { "usd" }.Concat(fiatCodes.Select(c => c.ToLowerInvariant())).Distinct());
        var url = $"{Base}/simple/price?ids=bitcoin&vs_currencies={vs}";
        var json = await GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["USD"] = 1m };
        if (!doc.RootElement.TryGetProperty("bitcoin", out var btc))
        {
            return rates;
        }

        var usd = Num(btc, "usd") ?? 0m;
        if (usd <= 0m)
        {
            return rates;
        }

        foreach (var code in fiatCodes)
        {
            var v = Num(btc, code.ToLowerInvariant());
            if (v is { } x && x > 0m)
            {
                rates[code.ToUpperInvariant()] = x / usd;
            }
        }

        return rates;
    }

    private async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var response = await _http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(ct);
            }

            var status = (int)response.StatusCode;
            if ((status == 429 || status >= 500) && attempt < MaxRetries)
            {
                await Task.Delay(_backoff(attempt), ct);
                continue;
            }

            response.EnsureSuccessStatusCode(); // throws
        }
    }

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static decimal? Num(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetDecimal() : null;
}
