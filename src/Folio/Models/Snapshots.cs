using System;
using System.Collections.Generic;

namespace Folio.Models;

/// <summary>A row parsed from the markets endpoint (price + identity + sparkline).</summary>
public sealed record MarketEntry(
    string Id,
    string Symbol,
    string Name,
    string? ImageUrl,
    decimal Price,
    decimal Change24h,
    decimal MarketCap,
    IReadOnlyList<decimal> Sparkline7d);

/// <summary>A cached snapshot of a coin's market data.</summary>
public sealed record PriceSnapshot(
    string CoinId,
    string Symbol,
    string Name,
    string? ImageUrl,
    decimal Price,
    decimal Change24h,
    decimal MarketCap,
    IReadOnlyList<decimal> Sparkline7d,
    DateTimeOffset FetchedAt)
{
    /// <summary>Convenience projection to the engine's current-price input.</summary>
    public PricePoint ToPricePoint() => new(Price, Change24h);
}
