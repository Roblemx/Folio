using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Folio.Models;

namespace Folio.Services.Market;

public interface IChainBalanceClient
{
    /// <summary>Reads an address's native-asset balance from a public explorer. Null on failure.</summary>
    Task<decimal?> GetBalanceAsync(WatchChain chain, string address, CancellationToken ct = default);
}

/// <summary>
/// Read-only balance lookups from public block explorers: BTC via mempool.space, ETH via
/// Blockscout. No API keys, no private data — only a public address is sent.
/// </summary>
public sealed class ChainBalanceClient : IChainBalanceClient
{
    private readonly HttpClient _http;

    public ChainBalanceClient(HttpClient http) => _http = http;

    public async Task<decimal?> GetBalanceAsync(WatchChain chain, string address, CancellationToken ct = default)
    {
        try
        {
            if (chain == WatchChain.Bitcoin)
            {
                var json = await _http.GetStringAsync($"https://mempool.space/api/address/{address}", ct);
                return ParseBtcBalance(json);
            }

            var ethJson = await _http.GetStringAsync(
                $"https://eth.blockscout.com/api?module=account&action=balance&address={address}&tag=latest", ct);
            return ParseEthBalance(ethJson);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>mempool.space address stats → confirmed + unconfirmed BTC balance. Public for tests.</summary>
    public static decimal? ParseBtcBalance(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        long sats = 0;

        foreach (var section in new[] { "chain_stats", "mempool_stats" })
        {
            if (root.TryGetProperty(section, out var s) &&
                s.TryGetProperty("funded_txo_sum", out var funded) &&
                s.TryGetProperty("spent_txo_sum", out var spent))
            {
                sats += funded.GetInt64() - spent.GetInt64();
            }
        }

        return sats / 100_000_000m;
    }

    /// <summary>Blockscout account balance (wei string) → ETH. Public for tests.</summary>
    public static decimal? ParseEthBalance(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("result", out var r))
        {
            return null;
        }

        var raw = r.ValueKind == JsonValueKind.String ? r.GetString() : r.GetRawText();
        if (!decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wei))
        {
            return null;
        }

        return wei / 1_000_000_000_000_000_000m;
    }
}
