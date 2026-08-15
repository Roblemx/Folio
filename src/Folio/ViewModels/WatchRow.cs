using System.Windows.Media;
using Folio.Helpers;
using Folio.Models;

namespace Folio.ViewModels;

/// <summary>A display-ready watch-only address row (formatted in the active currency).</summary>
public sealed class WatchRow
{
    public required WatchedAddress Source { get; init; }
    public required string Id { get; init; }
    public required string CoinId { get; init; }
    public required string Label { get; init; }
    public required string ShortAddress { get; init; }
    public required string ChainSymbol { get; init; }
    public required Brush ChainColor { get; init; }
    public string? ImageUrl { get; init; }

    public required string AmountText { get; init; }
    public required string ValueText { get; init; }
    public required string StatusText { get; init; }
    public decimal ValueUsd { get; init; }

    public static WatchRow Build(WatchedAddress addr, PriceSnapshot? snap, string fxSymbol, decimal fxRate)
    {
        var balance = addr.LastBalance ?? 0m;
        var price = snap?.Price ?? 0m;
        var valueUsd = balance * price;

        return new WatchRow
        {
            Source = addr,
            Id = addr.Id,
            CoinId = addr.CoinId,
            Label = addr.Label,
            ShortAddress = Shorten(addr.Address),
            ChainSymbol = addr.Symbol,
            ChainColor = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(addr.Chain == WatchChain.Bitcoin ? "#F7931A" : "#627EEA")),
            ImageUrl = snap?.ImageUrl,
            AmountText = addr.LastBalance is { } b ? $"{Format.Amount(b)} {addr.Symbol}" : "—",
            ValueText = addr.LastBalance != null ? Format.Fiat(valueUsd, fxSymbol, fxRate) : "—",
            StatusText = addr.BalanceAt is { } t ? "Updated " + Format.Ago(t) : "Fetching…",
            ValueUsd = valueUsd
        };
    }

    private static string Shorten(string address) =>
        address.Length <= 16 ? address : $"{address[..8]}…{address[^6..]}";
}
