using System;
using System.Collections.Generic;
using Folio.Models;

namespace Folio.Services.Persistence;

/// <summary>Maps between the domain <see cref="Workspace"/> and the storage DTOs.</summary>
public static class StorageMapper
{
    public static StoredState ToStored(Workspace workspace)
    {
        var state = new StoredState { SchemaVersion = StorageMigrator.CurrentVersion };

        foreach (var pd in workspace.Portfolios)
        {
            var sp = new StoredPortfolio
            {
                Id = pd.Portfolio.Id,
                Name = pd.Portfolio.Name,
                Mode = pd.Portfolio.Mode.ToString(),
                CostBasis = pd.Portfolio.CostBasis.ToString(),
                CreatedAt = pd.Portfolio.CreatedAt
            };

            foreach (var h in pd.Holdings)
            {
                sp.Holdings.Add(new StoredHolding { CoinId = h.CoinId, Amount = h.Amount, ManualAvgPrice = h.ManualAvgPrice });
            }

            foreach (var t in pd.Transactions)
            {
                sp.Transactions.Add(new StoredTransaction
                {
                    Id = t.Id,
                    CoinId = t.CoinId,
                    Type = t.Type.ToString(),
                    Amount = t.Amount,
                    PricePerCoin = t.PricePerCoin,
                    Fee = t.Fee,
                    Timestamp = t.Timestamp,
                    Note = t.Note
                });
            }

            sp.Targets = new Dictionary<string, decimal>(pd.Targets);
            state.Portfolios.Add(sp);
        }

        return state;
    }

    public static Workspace ToWorkspace(StoredState state)
    {
        var ws = new Workspace();

        foreach (var sp in state.Portfolios)
        {
            var portfolio = new Portfolio(
                sp.Id,
                sp.Name,
                ParseEnum(sp.Mode, PortfolioMode.Manual),
                ParseEnum(sp.CostBasis, CostBasisMethod.Average),
                sp.CreatedAt);

            var pd = new PortfolioData(portfolio);

            foreach (var h in sp.Holdings)
            {
                pd.Holdings.Add(new Holding(h.CoinId, h.Amount, h.ManualAvgPrice));
            }

            foreach (var t in sp.Transactions)
            {
                pd.Transactions.Add(new Transaction(
                    t.Id, t.CoinId, ParseEnum(t.Type, TransactionType.Buy),
                    t.Amount, t.PricePerCoin, t.Fee, t.Timestamp, t.Note));
            }

            if (sp.Targets != null)
            {
                foreach (var (coinId, pct) in sp.Targets)
                {
                    pd.Targets[coinId] = pct;
                }
            }

            ws.Portfolios.Add(pd);
        }

        return ws;
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
