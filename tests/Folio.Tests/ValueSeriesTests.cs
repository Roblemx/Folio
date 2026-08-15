using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Folio.Engine;
using Folio.Models;
using Xunit;

namespace Folio.Tests;

public class ValueSeriesTests
{
    private static readonly DateTimeOffset Day0 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static IReadOnlyDictionary<string, IReadOnlyList<HistoryPoint>> BtcHistory() =>
        new Dictionary<string, IReadOnlyList<HistoryPoint>>
        {
            ["btc"] = new HistoryPoint[]
            {
                new(Day0, 100),
                new(Day0.AddDays(1), 110),
                new(Day0.AddDays(2), 120),
                new(Day0.AddDays(3), 130)
            }
        };

    private static IReadOnlyList<DateTimeOffset> Days() =>
        new[] { Day0, Day0.AddDays(1), Day0.AddDays(2), Day0.AddDays(3) };

    [Fact]
    public void FromTransactions_UsesAmountAndPriceAtEachDate()
    {
        var txns = new[]
        {
            new Transaction("1", "btc", TransactionType.Buy, 1, 0, 0, Day0),
            new Transaction("2", "btc", TransactionType.Buy, 1, 0, 0, Day0.AddDays(2))
        };

        var series = ValueSeries.FromTransactions(txns, BtcHistory(), Days());

        // amounts: d0=1, d1=1, d2=2, d3=2  ->  values 100, 110, 240, 260
        series.Select(v => v.Value).Should().Equal(100m, 110m, 240m, 260m);
    }

    [Fact]
    public void FromConstantHoldings_HoldsAmountConstant()
    {
        var holdings = new[] { new Holding("btc", 2) };

        var series = ValueSeries.FromConstantHoldings(holdings, BtcHistory(), Days());

        series.Select(v => v.Value).Should().Equal(200m, 220m, 240m, 260m);
    }

    [Fact]
    public void PriceAt_UsesMostRecentSampleAtOrBeforeDate()
    {
        // history only has day0 and day2; day1 should reuse day0's price
        var history = new Dictionary<string, IReadOnlyList<HistoryPoint>>
        {
            ["btc"] = new HistoryPoint[] { new(Day0, 100), new(Day0.AddDays(2), 200) }
        };
        var holdings = new[] { new Holding("btc", 1) };
        var days = new[] { Day0, Day0.AddDays(1), Day0.AddDays(2) };

        var series = ValueSeries.FromConstantHoldings(holdings, history, days);

        series.Select(v => v.Value).Should().Equal(100m, 100m, 200m);
    }
}
