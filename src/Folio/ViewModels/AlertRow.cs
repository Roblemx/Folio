using System.Windows.Media;
using Folio.Helpers;
using Folio.Models;

namespace Folio.ViewModels;

/// <summary>A display-ready price-alert row (already formatted in the active currency).</summary>
public sealed class AlertRow
{
    public required Alert Source { get; init; }
    public required string AlertId { get; init; }
    public required string CoinId { get; init; }
    public required string Name { get; init; }
    public required string Symbol { get; init; }
    public required string Initial { get; init; }
    public required Brush Accent { get; init; }
    public string? ImageUrl { get; init; }

    public required string ConditionText { get; init; }
    public bool IsAbove { get; init; }
    public required string TargetText { get; init; }
    public required string CurrentText { get; init; }
    public required string StatusText { get; init; }
    public bool Enabled { get; init; }
    public bool HasTriggered { get; init; }

    public static AlertRow Build(Alert alert, PriceSnapshot? snap, string fxSymbol, decimal fxRate)
    {
        var symbol = snap?.Symbol?.ToUpperInvariant() ?? alert.CoinId.ToUpperInvariant();
        var name = snap?.Name ?? alert.CoinId;
        var above = alert.Direction == AlertDirection.Above;

        return new AlertRow
        {
            Source = alert,
            AlertId = alert.Id,
            CoinId = alert.CoinId,
            Name = name,
            Symbol = symbol,
            Initial = Format.Initial(symbol),
            Accent = Format.AccentFor(alert.CoinId),
            ImageUrl = snap?.ImageUrl,
            ConditionText = above ? "Rises above" : "Falls below",
            IsAbove = above,
            TargetText = Format.Price(alert.TargetPrice, fxSymbol, fxRate),
            CurrentText = snap != null ? Format.Price(snap.Price, fxSymbol, fxRate) : "—",
            StatusText = alert.LastTriggeredAt is { } t ? "Triggered " + Format.Ago(t) : "Waiting",
            Enabled = alert.Enabled,
            HasTriggered = alert.LastTriggeredAt != null
        };
    }
}
