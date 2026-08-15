using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Folio.Engine;
using Folio.Models;
using Xunit;

namespace Folio.Tests;

public class DcaBacktesterTests
{
    private static readonly DateTimeOffset Day0 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static List<HistoryPoint> Daily(params decimal[] prices) =>
        prices.Select((p, i) => new HistoryPoint(Day0.AddDays(i), p)).ToList();

    [Fact]
    public void Daily_AccumulatesAndValuesAtLatestPrice()
    {
        // 4 days at $10, $20, $40, $50. Invest $100/day → buys 10, 5, 2.5, 2 coins = 19.5 coins.
        var history = Daily(10m, 20m, 40m, 50m);

        var r = DcaBacktester.Run(history, 100m, DcaFrequency.Daily);

        r.Contributions.Should().Be(4);
        r.TotalInvested.Should().Be(400m);
        r.Coins.Should().Be(19.5m);
        r.CurrentValue.Should().Be(975m);          // 19.5 * 50
        r.AvgBuyPrice.Should().BeApproximately(20.5128m, 0.001m); // 400 / 19.5
        r.RoiPct.Should().BeApproximately(143.75m, 0.001m);       // (975-400)/400
    }

    [Fact]
    public void ComparesAgainstLumpSum()
    {
        // Rising market: lump sum at the (low) start beats DCA.
        var history = Daily(10m, 20m, 30m, 40m);

        var r = DcaBacktester.Run(history, 100m, DcaFrequency.Daily);

        // Lump: invest $400 at $10 → 40 coins → 40*40 = $1600.
        r.LumpSumValue.Should().Be(1600m);
        r.LumpSumRoiPct.Should().Be(300m);
        r.LumpSumRoiPct.Should().BeGreaterThan(r.RoiPct); // lump beats DCA in a steady rise
    }

    [Fact]
    public void Weekly_OnlyBuysOnWeekBoundaries()
    {
        // 8 daily points; weekly stepping from day 0 → buys on day 0 and day 7 only.
        var history = Daily(10m, 11m, 12m, 13m, 14m, 15m, 16m, 17m);

        var r = DcaBacktester.Run(history, 100m, DcaFrequency.Weekly);

        r.Contributions.Should().Be(2);          // day 0 and day 7
        r.TotalInvested.Should().Be(200m);
    }

    [Fact]
    public void ProducesValueAndInvestedSeries()
    {
        var r = DcaBacktester.Run(Daily(10m, 20m, 40m), 100m, DcaFrequency.Daily);

        r.ValueOverTime.Should().HaveCount(3);
        r.InvestedOverTime.Last().Value.Should().Be(300m);
        r.ValueOverTime.Last().Value.Should().Be(r.CurrentValue);
    }

    [Fact]
    public void EmptyOrZeroAmount_ReturnsEmpty()
    {
        DcaBacktester.Run(Daily(10m, 20m), 0m, DcaFrequency.Daily).Should().Be(DcaResult.Empty);
        DcaBacktester.Run(new List<HistoryPoint>(), 100m, DcaFrequency.Daily).Should().Be(DcaResult.Empty);
    }
}
