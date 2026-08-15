using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Folio.Engine;
using Folio.Models;
using Folio.Services.Market;
using Folio.Services.Persistence;

namespace Folio.Services;

/// <summary>
/// The live application state: the active portfolio (or the combined "All portfolios" view),
/// the latest computed summary (engine × live prices), and the mutation API. Recomputes
/// whenever prices, FX or data change, and persists on every mutation.
/// </summary>
public sealed class PortfolioSession : ObservableObject
{
    private readonly IPortfolioStore _store;
    private readonly Workspace _workspace;

    /// <summary>The selected portfolio, or <c>null</c> for the combined "All portfolios" view.</summary>
    private PortfolioData? _active;

    public PortfolioSession(IPortfolioStore store, IPriceService prices, IFxService fx)
    {
        _store = store;
        Prices = prices;
        Fx = fx;

        _workspace = store.Load();
        if (_workspace.Portfolios.Count == 0)
        {
            _workspace.Portfolios.Add(new PortfolioData(NewPortfolio("My Portfolio")));
        }

        _active = _workspace.Portfolios[0];

        prices.Updated += (_, _) => Recompute();
        fx.Changed += (_, _) => Recompute();
        Recompute();
    }

    public IPriceService Prices { get; }

    public IFxService Fx { get; }

    public PortfolioSummary Summary { get; private set; } = PortfolioSummary.Empty;

    public bool IsStale => Prices.IsStale;

    /// <summary>All portfolios in the workspace (the source for the switcher).</summary>
    public IReadOnlyList<PortfolioData> Portfolios => _workspace.Portfolios;

    /// <summary>The active portfolio id, or <c>null</c> when the combined view is selected.</summary>
    public string? ActiveId => _active?.Portfolio.Id;

    public bool IsAllView => _active is null;

    /// <summary>Editing (manual holdings / transactions / settings) requires a concrete portfolio.</summary>
    public bool IsEditable => _active is not null;

    public string ActiveName => _active?.Portfolio.Name ?? "All portfolios";

    public IReadOnlyList<Holding> Holdings =>
        _active?.Holdings ?? (IReadOnlyList<Holding>)Array.Empty<Holding>();

    public IReadOnlyList<Transaction> Transactions =>
        _active?.Transactions ?? _workspace.Portfolios.SelectMany(p => p.Transactions).ToList();

    public PortfolioMode Mode => _active?.Portfolio.Mode ?? PortfolioMode.Manual;

    public CostBasisMethod CostBasis => _active?.Portfolio.CostBasis ?? CostBasisMethod.Average;

    /// <summary>Raised when the portfolio set or the active selection changes (structural).</summary>
    public event EventHandler? PortfoliosChanged;

    /// <summary>Raised on every recompute (prices/FX/data change). UI rebinds from <see cref="Summary"/>.</summary>
    public event EventHandler? Changed;

    public PriceSnapshot? SnapshotFor(string coinId) =>
        Prices.Snapshots.TryGetValue(coinId, out var s) ? s : null;

    public Holding? HoldingFor(string coinId) =>
        _active?.Holdings.FirstOrDefault(h => h.CoinId == coinId);

    /// <summary>Current total value of one portfolio (used by the switcher).</summary>
    public decimal ValueOf(string portfolioId)
    {
        var pd = _workspace.Portfolios.FirstOrDefault(p => p.Portfolio.Id == portfolioId);
        return pd is null ? 0m : ComputeOne(pd).TotalValue;
    }

    public async Task RefreshAsync()
    {
        // Refresh prices for every coin across all portfolios so the combined view and alerts stay fresh.
        var ids = _workspace.Portfolios
            .SelectMany(p => p.Holdings.Select(h => h.CoinId).Concat(p.Transactions.Select(t => t.CoinId)))
            .Distinct()
            .ToList();
        await Fx.RefreshAsync();
        await Prices.RefreshAsync(ids, "usd");
    }

    // ----- Portfolio management -----

    public void SelectPortfolio(string? id)
    {
        _active = id is null ? null : _workspace.Portfolios.FirstOrDefault(p => p.Portfolio.Id == id);
        RaiseStructuralChange();
        Recompute();
    }

    public PortfolioData CreatePortfolio(string name)
    {
        var pd = new PortfolioData(NewPortfolio(string.IsNullOrWhiteSpace(name) ? "New Portfolio" : name.Trim()));
        _workspace.Portfolios.Add(pd);
        _active = pd;
        _store.Save(_workspace);
        RaiseStructuralChange();
        Recompute();
        return pd;
    }

    public void RenamePortfolio(string id, string name)
    {
        var pd = _workspace.Portfolios.FirstOrDefault(p => p.Portfolio.Id == id);
        if (pd is null || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        pd.Portfolio = pd.Portfolio with { Name = name.Trim() };
        _store.Save(_workspace);
        RaiseStructuralChange();
        OnPropertyChanged(nameof(ActiveName));
    }

    public void DeletePortfolio(string id)
    {
        if (_workspace.Portfolios.Count <= 1)
        {
            return; // always keep at least one portfolio
        }

        var pd = _workspace.Portfolios.FirstOrDefault(p => p.Portfolio.Id == id);
        if (pd is null)
        {
            return;
        }

        var wasActive = _active == pd;
        _workspace.Portfolios.Remove(pd);
        if (wasActive)
        {
            _active = _workspace.Portfolios[0];
        }

        _store.Save(_workspace);
        RaiseStructuralChange();
        Recompute();
    }

    // ----- Manual holdings -----

    public void AddOrUpdate(string coinId, decimal amount, decimal? avgPrice)
    {
        if (_active is null)
        {
            return;
        }

        var index = _active.Holdings.FindIndex(h => h.CoinId == coinId);
        var holding = new Holding(coinId, amount, avgPrice);
        if (index >= 0)
        {
            _active.Holdings[index] = holding;
        }
        else
        {
            _active.Holdings.Add(holding);
        }

        PersistAndRecompute();
        _ = RefreshAsync();
    }

    public void Remove(string coinId)
    {
        if (_active is null)
        {
            return;
        }

        _active.Holdings.RemoveAll(h => h.CoinId == coinId);
        PersistAndRecompute();
    }

    // ----- Transactions -----

    public void AddTransaction(Transaction transaction)
    {
        if (_active is null)
        {
            return;
        }

        _active.Transactions.Add(transaction);
        PersistAndRecompute();
        _ = RefreshAsync();
    }

    public void UpdateTransaction(Transaction transaction)
    {
        if (_active is null)
        {
            return;
        }

        var index = _active.Transactions.FindIndex(t => t.Id == transaction.Id);
        if (index >= 0)
        {
            _active.Transactions[index] = transaction;
        }

        PersistAndRecompute();
    }

    public void RemoveTransaction(string id)
    {
        if (_active is null)
        {
            return;
        }

        _active.Transactions.RemoveAll(t => t.Id == id);
        PersistAndRecompute();
    }

    public void ImportTransactions(IEnumerable<Transaction> transactions, bool replace)
    {
        if (_active is null)
        {
            return;
        }

        if (replace)
        {
            _active.Transactions.Clear();
        }

        _active.Transactions.AddRange(transactions);
        PersistAndRecompute();
        _ = RefreshAsync();
    }

    /// <summary>Serializes the whole workspace as portable JSON (for backup/export).</summary>
    public string ExportWorkspaceJson() => Helpers.CsvIo.ExportWorkspaceJson(_workspace);

    // ----- Encryption (data file at rest) -----

    public bool IsEncrypted => _store.IsEncrypted;

    // ----- Target allocations (rebalancing) -----

    public IReadOnlyDictionary<string, decimal> Targets =>
        _active?.Targets ?? (IReadOnlyDictionary<string, decimal>)new Dictionary<string, decimal>();

    public void UpdateTargets(IReadOnlyDictionary<string, decimal> targets)
    {
        if (_active is null)
        {
            return;
        }

        _active.Targets.Clear();
        foreach (var (coinId, pct) in targets)
        {
            _active.Targets[coinId] = pct;
        }

        PersistAndRecompute();
    }

    public void EnableEncryption(string password) => _store.EnableEncryption(password, _workspace);

    public void ChangePassword(string password) => _store.ChangePassword(password, _workspace);

    public void DisableEncryption() => _store.DisableEncryption(_workspace);

    // ----- Portfolio settings -----

    public void SetMode(PortfolioMode mode)
    {
        if (_active is null)
        {
            return;
        }

        _active.Portfolio = _active.Portfolio with { Mode = mode };
        OnPropertyChanged(nameof(Mode));
        PersistAndRecompute();
    }

    public void SetCostBasis(CostBasisMethod method)
    {
        if (_active is null)
        {
            return;
        }

        _active.Portfolio = _active.Portfolio with { CostBasis = method };
        OnPropertyChanged(nameof(CostBasis));
        PersistAndRecompute();
    }

    private static Portfolio NewPortfolio(string name) =>
        new(Guid.NewGuid().ToString("N"), name, PortfolioMode.Manual, CostBasisMethod.Average, DateTimeOffset.Now);

    private PortfolioSummary ComputeOne(PortfolioData pd)
    {
        var prices = Prices.Snapshots.ToDictionary(kv => kv.Key, kv => kv.Value.ToPricePoint());
        return pd.Portfolio.Mode == PortfolioMode.Transactions
            ? PortfolioEngine.ComputeFromTransactions(pd.Transactions, pd.Portfolio.CostBasis, prices)
            : PortfolioEngine.ComputeManual(pd.Holdings, prices);
    }

    private void RaiseStructuralChange()
    {
        OnPropertyChanged(nameof(ActiveId));
        OnPropertyChanged(nameof(ActiveName));
        OnPropertyChanged(nameof(IsAllView));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(CostBasis));
        PortfoliosChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PersistAndRecompute()
    {
        _store.Save(_workspace);
        Recompute();
    }

    private void Recompute()
    {
        Summary = _active is null
            ? PortfolioEngine.Combine(_workspace.Portfolios.Select(ComputeOne).ToList())
            : ComputeOne(_active);

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IsStale));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
