using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Folio.Engine;
using Folio.Models;
using Xunit;

namespace Folio.Tests;

public class PortfolioEngineTests
{
    private static readonly DateTimeOffset Day0 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ComputeManual_TotalsAllocationAndChange()
    {
        var holdings = new[]
        {
            new Holding("btc", 2, 100),   // value 300, invested 200
            new Holding("eth", 10, 20)    // value 250, invested 200
        };
        var prices = new Dictionary<string, PricePoint>
        {
            ["btc"] = new(150, 50),       // up 50% -> yesterday 100
            ["eth"] = new(25, 0)          // flat
        };

        var s = PortfolioEngine.ComputeManual(holdings, prices);

        s.TotalValue.Should().Be(550m);
        s.TotalInvested.Should().Be(400m);
        s.TotalUnrealized.Should().Be(150m);
        s.TotalRealized.Should().Be(0m);
        s.TotalReturn.Should().Be(150m);
        s.TotalReturnPct.Should().BeApproximately(37.5m, 0.0001m);

        // yesterday value = 200 (btc) + 250 (eth) = 450
        s.Change24hAbs.Should().Be(100m);
        s.Change24hPct.Should().BeApproximately(22.2222m, 0.001m);

        s.Positions.Should().HaveCount(2);
        var btc = s.Positions.First(p => p.CoinId == "btc");
        btc.Value.Should().Be(300m);
        btc.Unrealized.Should().Be(100m);
        btc.AllocationPct.Should().BeApproximately(54.5454m, 0.001m);
    }

    [Fact]
    public void ComputeManual_WithoutCostBasis_LeavesPnlNull()
    {
        var holdings = new[] { new Holding("btc", 1) };          // no manual avg price
        var prices = new Dictionary<string, PricePoint> { ["btc"] = new(100, 0) };

        var s = PortfolioEngine.ComputeManual(holdings, prices);

        s.TotalValue.Should().Be(100m);
        s.TotalInvested.Should().Be(0m);
        s.Positions.Single().Invested.Should().BeNull();
        s.Positions.Single().Unrealized.Should().BeNull();
    }

    [Fact]
    public void ComputeFromTransactions_Fifo_RealizedAndUnrealized()
    {
        var txns = new[]
        {
            new Transaction("1", "btc", TransactionType.Buy, 2, 100, 0, Day0),
            new Transaction("2", "btc", TransactionType.Buy, 2, 200, 0, Day0.AddDays(1)),
            new Transaction("3", "btc", TransactionType.Sell, 3, 300, 0, Day0.AddDays(2))
        };
        var prices = new Dictionary<string, PricePoint> { ["btc"] = new(250, 0) };

        var s = PortfolioEngine.ComputeFromTransactions(txns, CostBasisMethod.Fifo, prices);

        s.TotalValue.Should().Be(250m);       // 1 remaining @ 250
        s.TotalInvested.Should().Be(200m);    // remaining lot cost
        s.TotalUnrealized.Should().Be(50m);   // 250 - 200
        s.TotalRealized.Should().Be(500m);
        s.TotalReturn.Should().Be(550m);
        s.TotalReturnPct.Should().Be(275m);
    }

    [Fact]
    public void Empty_ReturnsZeroedSummary()
    {
        var s = PortfolioEngine.ComputeManual(Array.Empty<Holding>(), new Dictionary<string, PricePoint>());

        s.TotalValue.Should().Be(0m);
        s.Positions.Should().BeEmpty();
    }

    [Fact]
    public void Combine_PoolsPositions_AcrossPortfolios()
    {
        var prices = new Dictionary<string, PricePoint>
        {
            ["btc"] = new(100, 0),
            ["eth"] = new(20, 0)
        };

        // Portfolio A: 1 BTC @ cost 60, 5 ETH @ cost 10
        var a = PortfolioEngine.ComputeManual(
            new[] { new Holding("btc", 1, 60), new Holding("eth", 5, 10) }, prices);

        // Portfolio B: 2 BTC @ cost 80
        var b = PortfolioEngine.ComputeManual(
            new[] { new Holding("btc", 2, 80) }, prices);

        var combined = PortfolioEngine.Combine(new[] { a, b });

        // BTC pooled: 3 coins, value 300, invested 60 + 160 = 220, avg cost 220/3
        var btc = combined.Positions.First(p => p.CoinId == "btc");
        btc.Amount.Should().Be(3m);
        btc.Value.Should().Be(300m);
        btc.Invested.Should().Be(220m);
        btc.AvgCost.Should().BeApproximately(73.3333m, 0.001m);

        // ETH only in A: 5 coins, value 100, invested 50
        var eth = combined.Positions.First(p => p.CoinId == "eth");
        eth.Amount.Should().Be(5m);
        eth.Value.Should().Be(100m);

        combined.TotalValue.Should().Be(400m);       // 300 + 100
        combined.TotalInvested.Should().Be(270m);     // 220 + 50
        combined.TotalUnrealized.Should().Be(130m);   // (300-220) + (100-50)
    }

    [Fact]
    public void Positions_AreSortedByValueDescending()
    {
        var holdings = new[]
        {
            new Holding("a", 1),
            new Holding("b", 1),
            new Holding("c", 1)
        };
        var prices = new Dictionary<string, PricePoint>
        {
            ["a"] = new(10, 0),
            ["b"] = new(30, 0),
            ["c"] = new(20, 0)
        };

        var s = PortfolioEngine.ComputeManual(holdings, prices);

        s.Positions.Select(p => p.CoinId).Should().ContainInOrder("b", "c", "a");
    }
}
