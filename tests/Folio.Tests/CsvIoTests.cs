using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Folio.Engine;
using Folio.Helpers;
using Folio.Models;
using Xunit;

namespace Folio.Tests;

public class CsvIoTests
{
    private static readonly DateTimeOffset Day0 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Transaction Tx(string id, TransactionType type, decimal amount, decimal price,
        int day, decimal fee = 0m, string? note = null) =>
        new(id, "btc", type, amount, price, fee, Day0.AddDays(day), note);

    [Fact]
    public void Csv_RoundTrips_AllFields()
    {
        var txns = new[]
        {
            Tx("a1", TransactionType.Buy, 0.5m, 30000m, 0),
            Tx("b2", TransactionType.Sell, 0.25m, 42000m, 5, fee: 12.5m, note: "took profit"),
            Tx("c3", TransactionType.SwapIn, 1.5m, 0.8m, 9, note: "note, with comma \"and quotes\"")
        };

        var back = CsvIo.ImportTransactions(CsvIo.ExportTransactions(txns));

        back.Should().HaveCount(3);
        foreach (var original in txns)
        {
            var r = back.Single(x => x.Id == original.Id);
            r.CoinId.Should().Be(original.CoinId);
            r.Type.Should().Be(original.Type);
            r.Amount.Should().Be(original.Amount);
            r.PricePerCoin.Should().Be(original.PricePerCoin);
            r.Fee.Should().Be(original.Fee);
            r.Timestamp.Should().Be(original.Timestamp);
            r.Note.Should().Be(original.Note);
        }
    }

    [Fact]
    public void Import_SkipsHeader_AndGeneratesIdWhenMissing()
    {
        var csv = "Id,CoinId,Type,Amount,PricePerCoin,Fee,Timestamp,Note\n" +
                  ",eth,Buy,2,1000,0,2024-01-01T00:00:00.0000000+00:00,";

        var back = CsvIo.ImportTransactions(csv);

        back.Should().HaveCount(1);
        back[0].Id.Should().NotBeNullOrWhiteSpace();
        back[0].CoinId.Should().Be("eth");
        back[0].Amount.Should().Be(2m);
        back[0].Note.Should().BeNull();
    }

    [Fact]
    public void Import_IgnoresMalformedRows()
    {
        var csv = "Id,CoinId,Type,Amount,PricePerCoin,Fee,Timestamp,Note\n" +
                  "x,btc,Buy,notanumber,1000,0,2024-01-01T00:00:00+00:00,\n" +
                  "y,btc,Buy,1,1000,0,2024-01-01T00:00:00+00:00,";

        var back = CsvIo.ImportTransactions(csv);

        back.Should().ContainSingle().Which.Id.Should().Be("y");
    }

    [Fact]
    public void ImportedLedger_ReconstructsHoldingsWithRealizedPnl()
    {
        var txns = new[]
        {
            Tx("1", TransactionType.Buy, 2, 100, 0),
            Tx("2", TransactionType.Buy, 2, 200, 1),
            Tx("3", TransactionType.Sell, 1, 300, 2)
        };
        var imported = CsvIo.ImportTransactions(CsvIo.ExportTransactions(txns));

        var prices = new Dictionary<string, PricePoint> { ["btc"] = new(150m, 0m) };
        var summary = PortfolioEngine.ComputeFromTransactions(imported, CostBasisMethod.Average, prices);

        var pos = summary.Positions.Single(p => p.CoinId == "btc");
        pos.Amount.Should().Be(3m);
        pos.AvgCost.Should().Be(150m);
        summary.TotalRealized.Should().Be(150m);   // sold 1 @ 300, avg cost 150
    }
}
