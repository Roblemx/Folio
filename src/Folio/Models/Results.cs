using System;
using System.Collections.Generic;

namespace Folio.Models;

/// <summary>
/// A computed position for one coin. Cost-related fields are null when no cost basis is
/// known (e.g. a manual holding without a manual average price).
/// </summary>
public sealed record DerivedPosition(
    string CoinId,
    decimal Amount,
    decimal Price,
    decimal Value,
    decimal? AvgCost,
    decimal? Invested,
    decimal? Unrealized,
    decimal Realized,
    decimal Change24h,
    decimal AllocationPct);

/// <summary>Rolled-up portfolio totals plus the per-coin positions.</summary>
public sealed record PortfolioSummary(
    decimal TotalValue,
    decimal TotalInvested,
    decimal TotalUnrealized,
    decimal TotalRealized,
    decimal TotalReturn,
    decimal TotalReturnPct,
    decimal Change24hAbs,
    decimal Change24hPct,
    IReadOnlyList<DerivedPosition> Positions)
{
    public static readonly PortfolioSummary Empty =
        new(0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<DerivedPosition>());
}

/// <summary>A point on the portfolio value-over-time series.</summary>
public sealed record ValuePoint(DateTimeOffset Date, decimal Value);
