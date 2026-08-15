using System.Collections.Generic;
using System.Linq;
using Folio.Models;

namespace Folio.Engine;

/// <summary>
/// One coin's rebalancing line. <see cref="DeltaValue"/> is how much value to add (positive →
/// buy) or remove (negative → sell) to hit the target weight; <see cref="DeltaAmount"/> is the
/// same in coin units.
/// </summary>
public sealed record RebalanceLine(
    string CoinId,
    decimal CurrentValue,
    decimal CurrentPct,
    decimal TargetPct,
    decimal DriftPct,
    decimal DeltaValue,
    decimal DeltaAmount);

/// <summary>
/// Pure target-allocation math: given current positions and target weights (percent per coin),
/// computes the drift and the buy/sell needed to rebalance to those targets.
/// </summary>
public static class RebalanceEngine
{
    public static IReadOnlyList<RebalanceLine> Compute(
        IReadOnlyList<DerivedPosition> positions,
        IReadOnlyDictionary<string, decimal> targetPctByCoin)
    {
        var total = positions.Sum(p => p.Value);
        var lines = new List<RebalanceLine>(positions.Count);

        foreach (var p in positions)
        {
            var currentPct = total > 0m ? p.Value / total * 100m : 0m;
            var targetPct = targetPctByCoin.TryGetValue(p.CoinId, out var t) ? t : 0m;
            var targetValue = total * targetPct / 100m;
            var deltaValue = targetValue - p.Value;
            var deltaAmount = p.Price > 0m ? deltaValue / p.Price : 0m;

            lines.Add(new RebalanceLine(
                p.CoinId, p.Value, currentPct, targetPct, targetPct - currentPct, deltaValue, deltaAmount));
        }

        return lines;
    }

    /// <summary>Scales the given targets so they sum to 100% (equal split if all are zero).</summary>
    public static Dictionary<string, decimal> Normalize(IReadOnlyDictionary<string, decimal> targets)
    {
        var result = new Dictionary<string, decimal>();
        if (targets.Count == 0)
        {
            return result;
        }

        var sum = targets.Values.Sum();
        if (sum <= 0m)
        {
            var equal = 100m / targets.Count;
            foreach (var key in targets.Keys)
            {
                result[key] = equal;
            }

            return result;
        }

        foreach (var (key, value) in targets)
        {
            result[key] = value / sum * 100m;
        }

        return result;
    }
}
