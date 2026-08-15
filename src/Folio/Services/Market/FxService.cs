using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Folio.Services.Market;

public interface IFxService
{
    /// <summary>Supported display currencies (USD first).</summary>
    IReadOnlyList<string> Currencies { get; }

    string Currency { get; set; }

    /// <summary>Symbol for the active currency ($, €, …).</summary>
    string Symbol { get; }

    /// <summary>USD→<see cref="Currency"/> conversion rate (1 for USD).</summary>
    decimal Rate { get; }

    decimal RateFor(string code);

    Task RefreshAsync(CancellationToken ct = default);

    event EventHandler? Changed;
}

/// <summary>
/// Holds live USD→fiat rates and the active display currency. All stored values are USD;
/// the UI multiplies by <see cref="Rate"/>. Rates are cached so currency switching works
/// offline with the last-known values (demo defaults until the first refresh).
/// </summary>
public sealed class FxService : IFxService
{
    private const string CacheKey = "fx-rates";

    private static readonly string[] SupportedFiats = { "EUR", "GBP", "JPY", "TRY" };

    private static readonly Dictionary<string, string> SymbolMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "$", ["EUR"] = "€", ["GBP"] = "£", ["JPY"] = "¥", ["TRY"] = "₺"
    };

    private readonly IMarketDataClient _client;
    private readonly CacheStore _cache;

    private readonly Dictionary<string, decimal> _rates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 1m,
        ["EUR"] = 0.92m,
        ["GBP"] = 0.79m,
        ["JPY"] = 156m,
        ["TRY"] = 32.5m
    };

    private string _currency = "USD";

    public FxService(IMarketDataClient client, CacheStore cache)
    {
        _client = client;
        _cache = cache;

        var cached = _cache.Read<Dictionary<string, decimal>>(CacheKey);
        if (cached != null)
        {
            Merge(cached.Value);
        }
    }

    public IReadOnlyList<string> Currencies { get; } =
        new[] { "USD" }.Concat(SupportedFiats).ToList();

    public string Currency
    {
        get => _currency;
        set
        {
            if (_rates.ContainsKey(value) && !string.Equals(_currency, value, StringComparison.OrdinalIgnoreCase))
            {
                _currency = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Symbol => SymbolMap.TryGetValue(_currency, out var s) ? s : "$";

    public decimal Rate => RateFor(_currency);

    public decimal RateFor(string code) => _rates.TryGetValue(code, out var r) ? r : 1m;

    public event EventHandler? Changed;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var rates = await _client.GetUsdRatesAsync(SupportedFiats, ct);
            Merge(rates);
            _cache.Write(CacheKey, _rates);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // keep last-known/demo rates
        }
    }

    private void Merge(IReadOnlyDictionary<string, decimal> rates)
    {
        foreach (var (code, rate) in rates)
        {
            if (rate > 0m)
            {
                _rates[code] = rate;
            }
        }
    }
}
