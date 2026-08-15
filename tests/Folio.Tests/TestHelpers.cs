using System;
using System.IO;
using System.Text.Json;
using Folio.Models;
using Folio.Services.Persistence;

namespace Folio.Tests;

internal static class TestHelpers
{
    public static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "FolioTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static Workspace SampleWorkspace()
    {
        var ws = new Workspace();
        var portfolio = new Portfolio("p1", "Main", PortfolioMode.Transactions, CostBasisMethod.Fifo,
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var pd = new PortfolioData(portfolio);
        pd.Holdings.Add(new Holding("btc", 0.5m, 30000m));
        pd.Transactions.Add(new Transaction("t1", "eth", TransactionType.Buy, 2m, 2000m, 5m,
            new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero), "first buy"));
        ws.Portfolios.Add(pd);
        return ws;
    }

    /// <summary>Stable JSON projection for comparing two workspaces by value.</summary>
    public static string Json(Workspace ws) => JsonSerializer.Serialize(StorageMapper.ToStored(ws));
}
