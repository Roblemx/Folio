using System.Collections.Generic;

namespace Folio.Models;

/// <summary>A portfolio together with its editable holdings and transactions.</summary>
public sealed class PortfolioData
{
    public PortfolioData(Portfolio portfolio) => Portfolio = portfolio;

    public Portfolio Portfolio { get; set; }

    public List<Holding> Holdings { get; } = new();

    public List<Transaction> Transactions { get; } = new();

    /// <summary>Target allocation weights (percent) per coin id, for rebalancing.</summary>
    public Dictionary<string, decimal> Targets { get; } = new();
}

/// <summary>The full set of user data the store loads and saves.</summary>
public sealed class Workspace
{
    public List<PortfolioData> Portfolios { get; } = new();
}
