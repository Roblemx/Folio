namespace Folio.Models;

/// <summary>How a portfolio derives its holdings.</summary>
public enum PortfolioMode
{
    /// <summary>Amounts are entered directly (optionally with a manual average cost).</summary>
    Manual,

    /// <summary>Amounts and cost basis are derived from a transaction ledger.</summary>
    Transactions
}

/// <summary>Cost-basis accounting method for realized/unrealized gains.</summary>
public enum CostBasisMethod
{
    Average,
    Fifo
}

/// <summary>
/// A ledger entry's type. Inflows add to a position; outflows reduce it.
/// Only Sell and SwapOut realize a gain/loss (a disposal); TransferOut and Fee
/// reduce the position at cost without realizing (moving funds / spending in-kind).
/// </summary>
public enum TransactionType
{
    Buy,
    Sell,
    TransferIn,
    TransferOut,
    Airdrop,
    SwapIn,
    SwapOut,
    Fee
}
