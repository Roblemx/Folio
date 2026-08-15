using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Folio.Engine;
using Folio.Models;
using Xunit;

namespace Folio.Tests;

public class RebalanceEngineTests
{
    private static DerivedPosition Pos(string id, decimal amount, decimal price) =>
        new(id, amount, price, amount * price, null, null, null, 0m, 0m, 0m);

    [Fact]
    public void Compute_DriftAndBuySell()
    {
        var positions = new[]
        {
            Pos("btc", 6, 100),   // value 600
            Pos("eth", 40, 10)    // value 400  → total 1000
        };
        var targets = new Dictionary<string, decimal> { ["btc"] = 50m, ["eth"] = 50m };

        var lines = RebalanceEngine.Compute(positions, targets).ToDictionary(l => l.CoinId);

        var btc = lines["btc"];
        btc.CurrentPct.Should().Be(60m);
        btc.DriftPct.Should().Be(-10m);          // overweight
        btc.DeltaValue.Should().Be(-100m);       // sell $100
        btc.DeltaAmount.Should().Be(-1m);        // sell 1 BTC

        var eth = lines["eth"];
        eth.DriftPct.Should().Be(10m);           // underweight
        eth.DeltaValue.Should().Be(100m);        // buy $100
        eth.DeltaAmount.Should().Be(10m);        // buy 10 ETH
    }

    [Fact]
    public void Compute_MissingTarget_TreatedAsZero()
    {
        var positions = new[] { Pos("btc", 1, 100) };

        var line = RebalanceEngine.Compute(positions, new Dictionary<string, decimal>()).Single();

        line.TargetPct.Should().Be(0m);
        line.DeltaValue.Should().Be(-100m);   // sell everything to reach 0%
    }

    [Fact]
    public void Normalize_ScalesToHundred()
    {
        var targets = new Dictionary<string, decimal> { ["a"] = 30m, ["b"] = 30m };

        var n = RebalanceEngine.Normalize(targets);

        n["a"].Should().Be(50m);
        n["b"].Should().Be(50m);
    }

    [Fact]
    public void Normalize_AllZero_SplitsEqually()
    {
        var targets = new Dictionary<string, decimal> { ["a"] = 0m, ["b"] = 0m, ["c"] = 0m };

        var n = RebalanceEngine.Normalize(targets);

        n.Values.Should().AllBeEquivalentTo(100m / 3m);
    }
}
