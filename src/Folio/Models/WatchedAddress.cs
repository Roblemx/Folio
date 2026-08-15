using System;
using System.Text.Json.Serialization;

namespace Folio.Models;

/// <summary>A blockchain whose balances Folio can read from a public explorer (no keys).</summary>
public enum WatchChain
{
    Bitcoin,
    Ethereum
}

/// <summary>
/// A watch-only address: Folio reads its balance from a public explorer and shows it as a
/// read-only holding. No private keys, no signing — purely informational.
/// </summary>
public sealed record WatchedAddress(
    string Id,
    WatchChain Chain,
    string Address,
    string Label,
    DateTimeOffset AddedAt,
    decimal? LastBalance,
    DateTimeOffset? BalanceAt)
{
    /// <summary>The CoinGecko id used to price this chain's native asset.</summary>
    [JsonIgnore]
    public string CoinId => Chain == WatchChain.Bitcoin ? "bitcoin" : "ethereum";

    [JsonIgnore]
    public string Symbol => Chain == WatchChain.Bitcoin ? "BTC" : "ETH";
}
