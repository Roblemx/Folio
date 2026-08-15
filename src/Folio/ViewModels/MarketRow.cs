using System.Collections;
using System.Windows.Media;
using Folio.Helpers;
using Folio.Models;

namespace Folio.ViewModels;

/// <summary>A display-ready Markets row (already formatted in the active currency).</summary>
public sealed class MarketRow
{
    public required string CoinId { get; init; }
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public required string Initial { get; init; }
    public required Brush Accent { get; init; }
    public string? ImageUrl { get; init; }
    public int Rank { get; init; }
    public required string RankText { get; init; }
    public required string PriceText { get; init; }
    public required string ChangeText { get; init; }
    public bool IsUp { get; init; }
    public required string MarketCapText { get; init; }
    public IEnumerable? Spark { get; init; }

    public static MarketRow Build(MarketEntry e, int rank, string fxSymbol, decimal fxRate) => new()
    {
        CoinId = e.Id,
        Symbol = e.Symbol.ToUpperInvariant(),
        Name = e.Name,
        Initial = Format.Initial(e.Symbol),
        Accent = Format.AccentFor(e.Id),
        ImageUrl = e.ImageUrl,
        Rank = rank,
        RankText = rank > 0 ? rank.ToString() : string.Empty,
        PriceText = Format.Price(e.Price, fxSymbol, fxRate),
        ChangeText = Format.Percent(e.Change24h),
        IsUp = e.Change24h >= 0,
        MarketCapText = e.MarketCap > 0 ? Format.MarketCap(e.MarketCap, fxSymbol, fxRate) : "—",
        Spark = e.Sparkline7d as IEnumerable
    };
}
