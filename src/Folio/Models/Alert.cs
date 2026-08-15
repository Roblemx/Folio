using System;

namespace Folio.Models;

/// <summary>Which side of the target price triggers the alert.</summary>
public enum AlertDirection
{
    Above,
    Below
}

/// <summary>
/// A price alert for one coin. <see cref="TargetPrice"/> is in the base currency (USD).
/// Alerts are edge-triggered: they fire once when the price crosses the target, then re-arm
/// when it crosses back, so a held condition does not spam notifications.
/// </summary>
public sealed record Alert(
    string Id,
    string CoinId,
    AlertDirection Direction,
    decimal TargetPrice,
    bool Enabled,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastTriggeredAt);
