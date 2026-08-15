using System;
using System.Collections.Generic;
using System.Linq;
using Folio.Models;

namespace Folio.Engine;

/// <summary>
/// Builds a portfolio value-over-time series. Price at a date is the most recent history
/// sample at or before it; amount at a date is the net signed transaction amount up to it
/// (or a constant, in manual mode).
/// </summary>
public static class ValueSeries
{
    public static IReadOnlyList<ValuePoint> FromTransactions(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyDictionary<string, IReadOnlyList<HistoryPoint>> history,
        IReadOnlyList<DateTimeOffset> dates)
    {
        var byCoin = transactions
            .GroupBy(t => t.CoinId)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Timestamp).ToList());

        var result = new List<ValuePoint>(dates.Count);
        foreach (var date in dates.OrderBy(d => d))
        {
            decimal value = 0;
            foreach (var (coinId, txns) in byCoin)
            {
                var amount = AmountAt(txns, date);
                if (amount <= 0)
                {
                    continue;
                }

                if (PriceAt(history, coinId, date) is { } price)
                {
                    value += amount * price;
                }
            }

            result.Add(new ValuePoint(date, value));
        }

        return result;
    }

    public static IReadOnlyList<ValuePoint> FromConstantHoldings(
        IReadOnlyList<Holding> holdings,
        IReadOnlyDictionary<string, IReadOnlyList<HistoryPoint>> history,
        IReadOnlyList<DateTimeOffset> dates)
    {
        var result = new List<ValuePoint>(dates.Count);
        foreach (var date in dates.OrderBy(d => d))
        {
            decimal value = 0;
            foreach (var h in holdings)
            {
                if (h.Amount <= 0)
                {
                    continue;
                }

                if (PriceAt(history, h.CoinId, date) is { } price)
                {
                    value += h.Amount * price;
                }
            }

            result.Add(new ValuePoint(date, value));
        }

        return result;
    }

    private static decimal AmountAt(List<Transaction> ordered, DateTimeOffset date)
    {
        decimal amount = 0;
        foreach (var t in ordered)
        {
            if (t.Timestamp > date)
            {
                break;
            }

            amount += CostBasis.IsInflow(t.Type) ? t.Amount : -t.Amount;
        }

        return amount < 0 ? 0 : amount;
    }

    private static decimal? PriceAt(
        IReadOnlyDictionary<string, IReadOnlyList<HistoryPoint>> history,
        string coinId,
        DateTimeOffset date)
    {
        if (!history.TryGetValue(coinId, out var points) || points.Count == 0)
        {
            return null;
        }

        decimal? last = null;
        foreach (var p in points) // assumed ascending by Date
        {
            if (p.Date <= date)
            {
                last = p.Price;
            }
            else
            {
                break;
            }
        }

        return last ?? points[0].Price;
    }
}
