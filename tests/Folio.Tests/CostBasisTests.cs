using System;
using FluentAssertions;
using Folio.Engine;
using Folio.Models;
using Xunit;

namespace Folio.Tests;

public class CostBasisTests
{
    private static readonly DateTimeOffset Day0 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Transaction Tx(TransactionType type, decimal amount, decimal price, int day, decimal fee = 0m) =>
        new(Guid.NewGuid().ToString(), "btc", type, amount, price, fee, Day0.AddDays(day));

    [Fact]
    public void Average_BuyBuySell_ComputesRealizedAndRemainingCost()
    {
        var txns = new[]
        {
            Tx(TransactionType.Buy, 2, 100, 0),
            Tx(TransactionType.Buy, 2, 200, 1),
            Tx(TransactionType.Sell, 1, 300, 2)
        };

        var r = CostBasis.Process(txns, CostBasisMethod.Average);

        r.Amount.Should().Be(3m);
        r.CostBasis.Should().Be(450m);
        r.Realized.Should().Be(150m);   // proceeds 300 - avg-cost 150
        r.AvgCost.Should().Be(150m);
    }

    [Fact]
    public void Fifo_BuyBuySell_ConsumesOldestLotsFirst()
    {
        var txns = new[]
        {
            Tx(TransactionType.Buy, 2, 100, 0),
            Tx(TransactionType.Buy, 2, 200, 1),
            Tx(TransactionType.Sell, 3, 300, 2)
        };

        var r = CostBasis.Process(txns, CostBasisMethod.Fifo);

        r.Amount.Should().Be(1m);
        r.CostBasis.Should().Be(200m);  // remaining lot: 1 @ 200
        r.Realized.Should().Be(500m);   // proceeds 900 - cost (200+200) 400
        r.AvgCost.Should().Be(200m);
    }

    [Fact]
    public void Average_And_Fifo_DifferOnRealized()
    {
        var txns = new[]
        {
            Tx(TransactionType.Buy, 2, 100, 0),
            Tx(TransactionType.Buy, 2, 200, 1),
            Tx(TransactionType.Sell, 3, 300, 2)
        };

        CostBasis.Process(txns, CostBasisMethod.Average).Realized.Should().Be(450m);
        CostBasis.Process(txns, CostBasisMethod.Fifo).Realized.Should().Be(500m);
    }

    [Fact]
    public void Fee_OnBuy_IncreasesCostBasis()
    {
        var txns = new[] { Tx(TransactionType.Buy, 1, 100, 0, fee: 10) };

        var r = CostBasis.Process(txns, CostBasisMethod.Average);

        r.CostBasis.Should().Be(110m);
        r.AvgCost.Should().Be(110m);
    }

    [Fact]
    public void Fee_OnSell_ReducesProceeds()
    {
        var txns = new[]
        {
            Tx(TransactionType.Buy, 1, 100, 0),
            Tx(TransactionType.Sell, 1, 300, 1, fee: 20)
        };

        var r = CostBasis.Process(txns, CostBasisMethod.Average);

        r.Amount.Should().Be(0m);
        r.Realized.Should().Be(180m);   // (300 - 20) - 100
    }

    [Fact]
    public void SellMoreThanHeld_ClampsToAvailable()
    {
        var txns = new[]
        {
            Tx(TransactionType.Buy, 1, 100, 0),
            Tx(TransactionType.Sell, 5, 200, 1)
        };

        var r = CostBasis.Process(txns, CostBasisMethod.Average);

        r.Amount.Should().Be(0m);
        r.Realized.Should().Be(100m);   // sold the 1 held: 200 - 100
    }

    [Fact]
    public void TransferOut_RemovesAtCost_WithoutRealizing()
    {
        var txns = new[]
        {
            Tx(TransactionType.Buy, 2, 100, 0),
            Tx(TransactionType.TransferOut, 1, 999, 1)   // price irrelevant
        };

        var r = CostBasis.Process(txns, CostBasisMethod.Average);

        r.Amount.Should().Be(1m);
        r.CostBasis.Should().Be(100m);
        r.Realized.Should().Be(0m);
    }

    [Fact]
    public void Airdrop_AddsAmountAtZeroCost()
    {
        var txns = new[] { Tx(TransactionType.Airdrop, 5, 0, 0) };

        var r = CostBasis.Process(txns, CostBasisMethod.Fifo);

        r.Amount.Should().Be(5m);
        r.CostBasis.Should().Be(0m);
    }
}
