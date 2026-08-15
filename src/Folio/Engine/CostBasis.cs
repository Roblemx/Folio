using System;
using System.Collections.Generic;
using System.Linq;
using Folio.Models;

namespace Folio.Engine;

/// <summary>The outcome of running one coin's transactions through a cost-basis method.</summary>
public readonly record struct CostBasisResult(decimal Amount, decimal CostBasis, decimal Realized)
{
    /// <summary>Average cost per remaining unit, or null when nothing is held.</summary>
    public decimal? AvgCost => Amount > 0 ? CostBasis / Amount : null;
}

/// <summary>
/// Cost-basis accounting for a single coin's ledger. Rules:
/// <list type="bullet">
/// <item>Inflows (Buy, TransferIn, Airdrop, SwapIn) add amount; cost += amount*price + fee.</item>
/// <item>Outflows reduce amount and remove cost (FIFO lots or running average).</item>
/// <item>Only disposals (Sell, SwapOut) realize a gain/loss: proceeds(amount*price - fee) - costRemoved.</item>
/// <item>TransferOut and Fee remove at cost without realizing (moving funds / spending in-kind).</item>
/// </list>
/// Selling more than held removes only what is available (robust to bad input).
/// </summary>
public static class CostBasis
{
    public static bool IsInflow(TransactionType type) =>
        type is TransactionType.Buy or TransactionType.TransferIn
             or TransactionType.Airdrop or TransactionType.SwapIn;

    public static bool IsDisposal(TransactionType type) =>
        type is TransactionType.Sell or TransactionType.SwapOut;

    public static CostBasisResult Process(IEnumerable<Transaction> coinTransactions, CostBasisMethod method)
    {
        var ordered = coinTransactions.OrderBy(t => t.Timestamp).ToList();
        return method == CostBasisMethod.Average ? Average(ordered) : Fifo(ordered);
    }

    private static CostBasisResult Average(List<Transaction> txns)
    {
        decimal amount = 0, cost = 0, realized = 0;

        foreach (var t in txns)
        {
            if (IsInflow(t.Type))
            {
                amount += t.Amount;
                cost += (t.Amount * t.PricePerCoin) + t.Fee;
            }
            else
            {
                if (amount <= 0)
                {
                    continue;
                }

                var qty = Math.Min(t.Amount, amount);
                var avg = cost / amount;
                var costRemoved = avg * qty;
                amount -= qty;
                cost -= costRemoved;

                if (IsDisposal(t.Type))
                {
                    var proceeds = (qty * t.PricePerCoin) - t.Fee;
                    realized += proceeds - costRemoved;
                }
            }
        }

        return new CostBasisResult(amount, amount > 0 ? cost : 0, realized);
    }

    private static CostBasisResult Fifo(List<Transaction> txns)
    {
        var lots = new List<Lot>();
        decimal realized = 0;

        foreach (var t in txns)
        {
            if (IsInflow(t.Type))
            {
                if (t.Amount <= 0)
                {
                    continue;
                }

                var lotCost = (t.Amount * t.PricePerCoin) + t.Fee;
                lots.Add(new Lot { Qty = t.Amount, CostPerUnit = lotCost / t.Amount });
            }
            else
            {
                var remaining = t.Amount;
                decimal costRemoved = 0, qtyRemoved = 0;

                while (remaining > 0 && lots.Count > 0)
                {
                    var lot = lots[0];
                    var take = Math.Min(remaining, lot.Qty);
                    costRemoved += take * lot.CostPerUnit;
                    qtyRemoved += take;
                    remaining -= take;
                    lot.Qty -= take;
                    if (lot.Qty <= 0)
                    {
                        lots.RemoveAt(0);
                    }
                }

                if (IsDisposal(t.Type))
                {
                    var proceeds = (qtyRemoved * t.PricePerCoin) - t.Fee;
                    realized += proceeds - costRemoved;
                }
            }
        }

        var amount = lots.Sum(l => l.Qty);
        var cost = lots.Sum(l => l.Qty * l.CostPerUnit);
        return new CostBasisResult(amount, cost, realized);
    }

    private sealed class Lot
    {
        public decimal Qty;
        public decimal CostPerUnit;
    }
}
