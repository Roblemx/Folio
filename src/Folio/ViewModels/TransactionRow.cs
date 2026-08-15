using System.Globalization;
using System.Windows.Media;
using Folio.Helpers;
using Folio.Models;

namespace Folio.ViewModels;

/// <summary>A display-ready ledger row (already formatted in the active currency).</summary>
public sealed class TransactionRow
{
    public required Transaction Source { get; init; }
    public required string CoinId { get; init; }
    public required string CoinName { get; init; }
    public required string Symbol { get; init; }
    public required string Initial { get; init; }
    public required Brush Accent { get; init; }
    public string? ImageUrl { get; init; }

    public required string TypeLabel { get; init; }
    public required string DateText { get; init; }
    public required string AmountText { get; init; }
    public required string PriceText { get; init; }
    public required string ValueText { get; init; }
    public string? NoteText { get; init; }
    public bool IsInflow { get; init; }

    public static TransactionRow Build(Transaction t, PriceSnapshot? snap, string fxSymbol, decimal fxRate)
    {
        var symbol = snap?.Symbol?.ToUpperInvariant() ?? t.CoinId.ToUpperInvariant();
        var name = snap?.Name ?? t.CoinId;
        var inflow = IsInflowType(t.Type);
        var value = t.Amount * t.PricePerCoin;

        return new TransactionRow
        {
            Source = t,
            CoinId = t.CoinId,
            CoinName = name,
            Symbol = symbol,
            Initial = Format.Initial(symbol),
            Accent = Format.AccentFor(t.CoinId),
            ImageUrl = snap?.ImageUrl,
            TypeLabel = LabelFor(t.Type),
            DateText = t.Timestamp.ToLocalTime().ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            AmountText = (inflow ? "+" : "−") + Format.Amount(t.Amount) + " " + symbol,
            PriceText = Format.Price(t.PricePerCoin, fxSymbol, fxRate),
            ValueText = Format.Fiat(value, fxSymbol, fxRate),
            NoteText = string.IsNullOrWhiteSpace(t.Note) ? null : t.Note,
            IsInflow = inflow
        };
    }

    public static bool IsInflowType(TransactionType type) => type is
        TransactionType.Buy or TransactionType.TransferIn or
        TransactionType.Airdrop or TransactionType.SwapIn;

    public static string LabelFor(TransactionType type) => type switch
    {
        TransactionType.TransferIn => "Transfer in",
        TransactionType.TransferOut => "Transfer out",
        TransactionType.SwapIn => "Swap in",
        TransactionType.SwapOut => "Swap out",
        _ => type.ToString()
    };
}
