using System.Collections.Generic;
using System.Linq;
using Folio.Models;

namespace Folio.Engine;

/// <summary>
/// Pure, deterministic portfolio math: turns holdings/transactions + current prices into
/// per-coin positions and rolled-up totals. No UI, no IO — fully unit-testable.
/// </summary>
public static class PortfolioEngine
{
    /// <summary>Manual mode: amounts are given directly (cost basis optional per holding).</summary>
    public static PortfolioSummary ComputeManual(
        IReadOnlyList<Holding> holdings,
        IReadOnlyDictionary<string, PricePoint> prices)
    {
        var rows = new List<Row>(holdings.Count);
        foreach (var h in holdings)
        {
            var p = prices.TryGetValue(h.CoinId, out var pp) ? pp : new PricePoint(0m, 0m);
            decimal? invested = h.ManualAvgPrice.HasValue ? h.Amount * h.ManualAvgPrice.Value : null;
            rows.Add(new Row(h.CoinId, h.Amount, p.Price, p.Change24h, h.ManualAvgPrice, invested, Realized: 0m));
        }

        return Build(rows);
    }

    /// <summary>Transaction mode: amounts and cost basis derived from the ledger.</summary>
    public static PortfolioSummary ComputeFromTransactions(
        IReadOnlyList<Transaction> transactions,
        CostBasisMethod method,
        IReadOnlyDictionary<string, PricePoint> prices)
    {
        var rows = new List<Row>();
        foreach (var group in transactions.GroupBy(t => t.CoinId))
        {
            var cb = CostBasis.Process(group, method);

            // Skip coins that are fully closed with no realized result (nothing to show).
            if (cb.Amount <= 0 && cb.Realized == 0m)
            {
                continue;
            }

            var p = prices.TryGetValue(group.Key, out var pp) ? pp : new PricePoint(0m, 0m);
            decimal? invested = cb.Amount > 0 ? cb.CostBasis : null;
            rows.Add(new Row(group.Key, cb.Amount, p.Price, p.Change24h, cb.AvgCost, invested, cb.Realized));
        }

        return Build(rows);
    }

    /// <summary>
    /// Merges several portfolio summaries into one (the "All portfolios" view): positions for
    /// the same coin are pooled (amounts, cost basis and realized summed) and totals rolled up.
    /// </summary>
    public static PortfolioSummary Combine(IEnumerable<PortfolioSummary> summaries)
    {
        var merged = new Dictionary<string, Row>();
        foreach (var s in summaries)
        {
            foreach (var p in s.Positions)
            {
                if (merged.TryGetValue(p.CoinId, out var ex))
                {
                    decimal? invested = ex.Invested.HasValue || p.Invested.HasValue
                        ? (ex.Invested ?? 0m) + (p.Invested ?? 0m)
                        : null;
                    merged[p.CoinId] = ex with
                    {
                        Amount = ex.Amount + p.Amount,
                        Invested = invested,
                        Realized = ex.Realized + p.Realized
                    };
                }
                else
                {
                    merged[p.CoinId] = new Row(p.CoinId, p.Amount, p.Price, p.Change24h, p.AvgCost, p.Invested, p.Realized);
                }
            }
        }

        var rows = merged.Values
            .Select(r => r with { AvgCost = r.Invested.HasValue && r.Amount > 0 ? r.Invested.Value / r.Amount : r.AvgCost })
            .ToList();

        return Build(rows);
    }

    private readonly record struct Row(
        string CoinId, decimal Amount, decimal Price, decimal Change24h,
        decimal? AvgCost, decimal? Invested, decimal Realized);

    private static PortfolioSummary Build(List<Row> rows)
    {
        decimal totalValue = 0, valueYesterday = 0, totalInvested = 0, totalUnrealized = 0, totalRealized = 0;

        foreach (var r in rows)
        {
            var value = r.Amount * r.Price;
            totalValue += value;

            var factor = 1m + (r.Change24h / 100m);
            var priceYesterday = factor > 0m ? r.Price / factor : r.Price;
            valueYesterday += r.Amount * priceYesterday;

            if (r.Invested.HasValue)
            {
                totalInvested += r.Invested.Value;
                totalUnrealized += value - r.Invested.Value;
            }

            totalRealized += r.Realized;
        }

        var positions = rows
            .Select(r =>
            {
                var value = r.Amount * r.Price;
                decimal? unrealized = r.Invested.HasValue ? value - r.Invested.Value : null;
                var alloc = totalValue > 0m ? value / totalValue * 100m : 0m;
                return new DerivedPosition(r.CoinId, r.Amount, r.Price, value,
                    r.AvgCost, r.Invested, unrealized, r.Realized, r.Change24h, alloc);
            })
            .OrderByDescending(p => p.Value)
            .ToList();

        var change24hAbs = totalValue - valueYesterday;
        var change24hPct = valueYesterday > 0m ? change24hAbs / valueYesterday * 100m : 0m;
        var totalReturn = totalUnrealized + totalRealized;
        var totalReturnPct = totalInvested > 0m ? totalReturn / totalInvested * 100m : 0m;

        return new PortfolioSummary(totalValue, totalInvested, totalUnrealized, totalRealized,
            totalReturn, totalReturnPct, change24hAbs, change24hPct, positions);
    }
}
