namespace Folio.Models;

/// <summary>
/// A manually-entered holding: an amount of a coin, optionally with a manual average
/// cost per unit (used to compute unrealized P&amp;L in manual-mode portfolios).
/// </summary>
public sealed record Holding(string CoinId, decimal Amount, decimal? ManualAvgPrice = null);
