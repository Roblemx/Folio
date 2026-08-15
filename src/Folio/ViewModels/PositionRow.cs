using System.Windows.Media;
using Folio.Helpers;
using Folio.Models;

namespace Folio.ViewModels;

/// <summary>A display-ready holdings/position row (already formatted in the active currency).</summary>
public sealed class PositionRow
{
    public required string CoinId { get; init; }
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public required string Initial { get; init; }
    public required Brush Accent { get; init; }
    public string? ImageUrl { get; init; }

    public decimal Amount { get; init; }
    public required string AmountText { get; init; }
    public required string PriceText { get; init; }
    public required string ValueText { get; init; }

    public decimal Change24h { get; init; }
    public required string ChangeText { get; init; }
    public bool IsUp { get; init; }

    public decimal AllocationPct { get; init; }
    public required string AllocationText { get; init; }

    public string? PnlText { get; init; }
    public bool PnlUp { get; init; }
    public bool HasPnl { get; init; }

    public static PositionRow Build(DerivedPosition pos, PriceSnapshot? snap, string fxSymbol, decimal fxRate)
    {
        var symbol = snap?.Symbol?.ToUpperInvariant() ?? pos.CoinId.ToUpperInvariant();
        var name = snap?.Name ?? pos.CoinId;

        return new PositionRow
        {
            CoinId = pos.CoinId,
            Symbol = symbol,
            Name = name,
            Initial = Format.Initial(symbol),
            Accent = Format.AccentFor(pos.CoinId),
            ImageUrl = snap?.ImageUrl,
            Amount = pos.Amount,
            AmountText = $"{Format.Amount(pos.Amount)} {symbol}",
            PriceText = Format.Price(pos.Price, fxSymbol, fxRate),
            ValueText = Format.Fiat(pos.Value, fxSymbol, fxRate),
            Change24h = pos.Change24h,
            ChangeText = Format.Percent(pos.Change24h),
            IsUp = pos.Change24h >= 0,
            AllocationPct = pos.AllocationPct,
            AllocationText = pos.AllocationPct.ToString("0.0") + "%",
            HasPnl = pos.Unrealized.HasValue,
            PnlText = pos.Unrealized.HasValue ? Format.Fiat(pos.Unrealized.Value, fxSymbol, fxRate) : null,
            PnlUp = (pos.Unrealized ?? 0m) >= 0m
        };
    }
}
