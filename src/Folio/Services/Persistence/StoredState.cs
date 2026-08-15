using System;
using System.Collections.Generic;

namespace Folio.Services.Persistence;

// Storage DTOs are intentionally separate from the domain model so the on-disk shape can
// evolve via migrations without coupling to engine/domain refactors. Enums are stored as
// strings for stability across reordering.

public sealed class StoredState
{
    public int SchemaVersion { get; set; } = StorageMigrator.CurrentVersion;
    public List<StoredPortfolio> Portfolios { get; set; } = new();
}

public sealed class StoredPortfolio
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Mode { get; set; } = "Manual";
    public string CostBasis { get; set; } = "Average";
    public DateTimeOffset CreatedAt { get; set; }
    public List<StoredHolding> Holdings { get; set; } = new();
    public List<StoredTransaction> Transactions { get; set; } = new();
    public Dictionary<string, decimal> Targets { get; set; } = new();
}

public sealed class StoredHolding
{
    public string CoinId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? ManualAvgPrice { get; set; }
}

public sealed class StoredTransaction
{
    public string Id { get; set; } = string.Empty;
    public string CoinId { get; set; } = string.Empty;
    public string Type { get; set; } = "Buy";
    public decimal Amount { get; set; }
    public decimal PricePerCoin { get; set; }
    public decimal Fee { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Note { get; set; }
}
