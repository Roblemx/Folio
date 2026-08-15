using System;
using System.Collections.Generic;
using System.Linq;
using Folio.Models;

namespace Folio.Engine;

public enum DcaFrequency
{
    Daily,
    Weekly,
    Monthly
}

/// <summary>The outcome of a dollar-cost-averaging backtest, in the history's currency (USD).</summary>
public sealed record DcaResult(
    decimal TotalInvested,
    decimal Coins,
    decimal CurrentValue,
    decimal RoiPct,
    int Contributions,
    decimal AvgBuyPrice,
    decimal LumpSumValue,
    decimal LumpSumRoiPct,
    IReadOnlyList<ValuePoint> ValueOverTime,
    IReadOnlyList<ValuePoint> InvestedOverTime)
{
    public static readonly DcaResult Empty = new(
        0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<ValuePoint>(), Array.Empty<ValuePoint>());
}

/// <summary>
/// "Invest a fixed amount every interval" backtest over a historical price series. Pure and
/// deterministic — buys at the most-recent price on/before each contribution date and compares
/// the result to investing the same total as a single lump sum at the start.
/// </summary>
public static class DcaBacktester
{
    public static DcaResult Run(IReadOnlyList<HistoryPoint> history, decimal amountPerPeriod, DcaFrequency frequency)
    {
        if (history.Count < 2 || amountPerPeriod <= 0)
        {
            return DcaResult.Empty;
        }

        var points = history.OrderBy(p => p.Date).ToList();
        var start = points[0].Date;
        var end = points[^1].Date;

        var contributions = new List<DateTimeOffset>();
        for (var d = start; d <= end; d = Next(d, frequency))
        {
            contributions.Add(d);
        }

        decimal coins = 0m, invested = 0m;
        var ci = 0;
        var valueSeries = new List<ValuePoint>(points.Count);
        var investedSeries = new List<ValuePoint>(points.Count);

        foreach (var p in points)
        {
            while (ci < contributions.Count && contributions[ci] <= p.Date)
            {
                var price = PriceAt(points, contributions[ci]);
                if (price > 0m)
                {
                    coins += amountPerPeriod / price;
                    invested += amountPerPeriod;
                }

                ci++;
            }

            valueSeries.Add(new ValuePoint(p.Date, coins * p.Price));
            investedSeries.Add(new ValuePoint(p.Date, invested));
        }

        var lastPrice = points[^1].Price;
        var currentValue = coins * lastPrice;
        var roi = invested > 0m ? (currentValue - invested) / invested * 100m : 0m;
        var avgBuy = coins > 0m ? invested / coins : 0m;

        var startPrice = points[0].Price;
        var lumpCoins = startPrice > 0m ? invested / startPrice : 0m;
        var lumpValue = lumpCoins * lastPrice;
        var lumpRoi = invested > 0m ? (lumpValue - invested) / invested * 100m : 0m;

        return new DcaResult(invested, coins, currentValue, roi, contributions.Count, avgBuy,
            lumpValue, lumpRoi, valueSeries, investedSeries);
    }

    private static DateTimeOffset Next(DateTimeOffset d, DcaFrequency frequency) => frequency switch
    {
        DcaFrequency.Daily => d.AddDays(1),
        DcaFrequency.Weekly => d.AddDays(7),
        _ => d.AddMonths(1)
    };

    private static decimal PriceAt(List<HistoryPoint> points, DateTimeOffset date)
    {
        var last = points[0].Price;
        foreach (var p in points)
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

        return last;
    }
}
