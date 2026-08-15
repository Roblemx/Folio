using System;

namespace Folio.Models;

/// <summary>
/// A single ledger entry. <see cref="PricePerCoin"/> is the unit price in the portfolio's
/// base currency at the time; <see cref="Fee"/> is a flat fee in the base currency
/// (added to cost on inflows, subtracted from proceeds on disposals).
/// </summary>
public sealed record Transaction(
    string Id,
    string CoinId,
    TransactionType Type,
    decimal Amount,
    decimal PricePerCoin,
    decimal Fee,
    DateTimeOffset Timestamp,
    string? Note = null);
