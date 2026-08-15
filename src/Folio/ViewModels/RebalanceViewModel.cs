using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Engine;
using Folio.Helpers;
using Folio.Models;
using Folio.Services;

namespace Folio.ViewModels;

/// <summary>The Rebalance screen: set target weights per coin and get buy/sell hints to hit them.</summary>
public sealed partial class RebalanceViewModel : ViewModelBase
{
    private readonly PortfolioSession _session;
    private HashSet<string> _coinIds = new();
    private bool _applyingTargets;

    public RebalanceViewModel(PortfolioSession session)
    {
        _session = session;
        _session.Changed += OnSessionChanged;
        BuildRows();
        _ = _session.RefreshAsync();
    }

    public ObservableCollection<RebalanceRow> Rows { get; } = new();

    public bool HasHoldings => Rows.Count > 0;

    [ObservableProperty] private string _targetsSumText = "0%";
    [ObservableProperty] private bool _isBalanced;
    [ObservableProperty] private string _totalValueText = string.Empty;

    [RelayCommand]
    private void Normalize()
    {
        var targets = CurrentTargets();
        var normalized = RebalanceEngine.Normalize(targets);
        foreach (var row in Rows)
        {
            row.TargetText = normalized.TryGetValue(row.CoinId, out var pct)
                ? pct.ToString("0.#", CultureInfo.InvariantCulture)
                : "0";
        }

        RecomputeDerived();
        PersistTargets();
    }

    [RelayCommand]
    private void Clear()
    {
        foreach (var row in Rows)
        {
            row.TargetText = "0";
        }

        RecomputeDerived();
        PersistTargets();
    }

    private void OnSessionChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_applyingTargets)
            {
                return; // our own target write — don't rebuild and steal focus
            }

            var ids = _session.Summary.Positions.Select(p => p.CoinId).ToHashSet();
            if (ids.SetEquals(_coinIds))
            {
                RecomputeDerived(); // prices changed → values/drift change
            }
            else
            {
                BuildRows();
            }
        });

    private void BuildRows()
    {
        Rows.Clear();
        _coinIds = _session.Summary.Positions.Select(p => p.CoinId).ToHashSet();
        var targets = _session.Targets;

        foreach (var pos in _session.Summary.Positions)
        {
            var snap = _session.SnapshotFor(pos.CoinId);
            var symbol = snap?.Symbol?.ToUpperInvariant() ?? pos.CoinId.ToUpperInvariant();
            var row = new RebalanceRow
            {
                CoinId = pos.CoinId,
                Symbol = symbol,
                Name = snap?.Name ?? pos.CoinId,
                Initial = Format.Initial(symbol),
                Accent = Format.AccentFor(pos.CoinId),
                ImageUrl = snap?.ImageUrl,
                TargetText = targets.TryGetValue(pos.CoinId, out var t)
                    ? t.ToString("0.#", CultureInfo.InvariantCulture)
                    : "0"
            };
            row.TargetChanged = OnRowTargetChanged;
            Rows.Add(row);
        }

        RecomputeDerived();
        OnPropertyChanged(nameof(HasHoldings));
    }

    private void OnRowTargetChanged()
    {
        RecomputeDerived();
        PersistTargets();
    }

    private Dictionary<string, decimal> CurrentTargets()
    {
        var targets = new Dictionary<string, decimal>();
        foreach (var row in Rows)
        {
            targets[row.CoinId] = decimal.TryParse(row.TargetText, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var pct) && pct > 0
                ? pct
                : 0m;
        }

        return targets;
    }

    private void RecomputeDerived()
    {
        var sym = _session.Fx.Symbol;
        var rate = _session.Fx.Rate;
        var targets = CurrentTargets();
        var lines = RebalanceEngine.Compute(_session.Summary.Positions, targets)
            .ToDictionary(l => l.CoinId);

        foreach (var row in Rows)
        {
            if (!lines.TryGetValue(row.CoinId, out var line))
            {
                continue;
            }

            row.CurrentText = $"{Format.Fiat(line.CurrentValue, sym, rate)}  ·  {line.CurrentPct:0.0}%";
            row.DriftText = (line.DriftPct >= 0 ? "+" : string.Empty) + line.DriftPct.ToString("0.0") + "%";
            row.Underweight = line.DriftPct >= 0;

            var onTarget = Math.Abs(line.DriftPct) < 0.5m || Math.Abs(line.DeltaValue * rate) < 1m;
            row.IsOnTarget = onTarget;
            row.IsBuy = line.DeltaAmount >= 0;
            if (onTarget)
            {
                row.ActionText = "On target";
            }
            else
            {
                var verb = line.DeltaAmount >= 0 ? "Buy" : "Sell";
                var amount = Format.Amount(Math.Abs(line.DeltaAmount));
                var value = Format.Fiat(Math.Abs(line.DeltaValue), sym, rate);
                row.ActionText = $"{verb} {amount} {row.Symbol}  ({value})";
            }
        }

        var sum = targets.Values.Sum();
        TargetsSumText = sum.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        IsBalanced = Math.Abs(sum - 100m) < 0.1m;
        TotalValueText = Format.Fiat(_session.Summary.TotalValue, sym, rate);
    }

    private void PersistTargets()
    {
        _applyingTargets = true;
        _session.UpdateTargets(CurrentTargets());
        _applyingTargets = false;
    }
}
