using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Controls;
using Folio.Engine;
using Folio.Helpers;
using Folio.Models;
using Folio.Services;
using Folio.Services.Market;

namespace Folio.ViewModels;

/// <summary>Allocation legend entry.</summary>
public sealed class LegendItem
{
    public required string Name { get; init; }
    public required Brush Color { get; init; }
    public required string PctText { get; init; }
}

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly PortfolioSession _session;
    private readonly NavigationService _navigation;
    private readonly IHistoryService _history;
    private int _chartToken;
    private HashSet<string> _chartCoins = new();

    public DashboardViewModel(PortfolioSession session, NavigationService navigation, IHistoryService history)
    {
        _session = session;
        _navigation = navigation;
        _history = history;

        _session.Changed += OnSessionChanged;
        RefreshStats();
        ReloadChart();
        _ = _session.RefreshAsync();
    }

    public ObservableCollection<PositionRow> TopHoldings { get; } = new();
    public ObservableCollection<PositionRow> TopMovers { get; } = new();
    public ObservableCollection<DonutSegment> Segments { get; } = new();
    public ObservableCollection<LegendItem> Legend { get; } = new();

    public bool HasHoldings => _session.Summary.Positions.Count > 0;
    public bool IsStale => _session.IsStale;

    [ObservableProperty] private string _totalValueText = "$0.00";
    [ObservableProperty] private string _btcValueText = string.Empty;
    [ObservableProperty] private bool _hasBtcValue;
    [ObservableProperty] private string _change24hText = string.Empty;
    [ObservableProperty] private bool _change24hUp;
    [ObservableProperty] private string _pnlText = string.Empty;
    [ObservableProperty] private bool _hasPnl;
    [ObservableProperty] private bool _pnlUp;
    [ObservableProperty] private string _realizedText = string.Empty;
    [ObservableProperty] private bool _hasRealized;
    [ObservableProperty] private bool _realizedUp;

    [ObservableProperty] private IReadOnlyList<double> _chartValues = Array.Empty<double>();
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RangeLabel))] private string _selectedRange = "1M";
    [ObservableProperty] private string _rangeChangeText = string.Empty;
    [ObservableProperty] private bool _rangeIsUp;

    public string RangeLabel => SelectedRange;

    public string[] Ranges { get; } = { "24H", "1W", "1M", "1Y", "ALL" };

    [RelayCommand]
    private void OpenAsset(string coinId) =>
        _navigation.Navigate(new CoinDetailViewModel(coinId, _session, _navigation, _history));

    [RelayCommand]
    private void GoToHoldings()
    {
        // The shell handles sidebar nav; here we just no-op to keep the empty-state button simple.
    }

    [RelayCommand]
    private async Task Refresh() => await _session.RefreshAsync();

    partial void OnSelectedRangeChanged(string value) => ReloadChart();

    private void OnSessionChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            RefreshStats();
            var coins = _session.Summary.Positions.Select(p => p.CoinId).ToHashSet();
            if (!coins.SetEquals(_chartCoins))
            {
                ReloadChart();
            }
        });

    private void RefreshStats()
    {
        var sym = _session.Fx.Symbol;
        var rate = _session.Fx.Rate;
        var s = _session.Summary;

        TotalValueText = Format.Fiat(s.TotalValue, sym, rate);
        var btcPrice = _session.SnapshotFor("bitcoin")?.Price ?? 0m;
        BtcValueText = "≈ " + Format.Btc(s.TotalValue, btcPrice);
        HasBtcValue = s.TotalValue > 0m && btcPrice > 0m;
        Change24hText = $"{Format.Fiat(s.Change24hAbs, sym, rate)}  ({Format.Percent(s.Change24hPct)})";
        Change24hUp = s.Change24hAbs >= 0m;
        HasPnl = s.TotalInvested > 0m;
        PnlText = $"{Format.Fiat(s.TotalReturn, sym, rate)}  ({Format.Percent(s.TotalReturnPct)})";
        PnlUp = s.TotalReturn >= 0m;
        HasRealized = s.TotalRealized != 0m;
        RealizedText = Format.Fiat(s.TotalRealized, sym, rate);
        RealizedUp = s.TotalRealized >= 0m;

        TopHoldings.Clear();
        foreach (var pos in s.Positions.Take(5))
        {
            TopHoldings.Add(PositionRow.Build(pos, _session.SnapshotFor(pos.CoinId), sym, rate));
        }

        TopMovers.Clear();
        foreach (var pos in s.Positions.OrderByDescending(p => Math.Abs(p.Change24h)).Take(5))
        {
            TopMovers.Add(PositionRow.Build(pos, _session.SnapshotFor(pos.CoinId), sym, rate));
        }

        Segments.Clear();
        Legend.Clear();
        foreach (var pos in s.Positions.Where(p => p.Value > 0))
        {
            var color = Format.AccentFor(pos.CoinId);
            Segments.Add(new DonutSegment { Value = (double)pos.Value, Color = color });
            Legend.Add(new LegendItem
            {
                Name = _session.SnapshotFor(pos.CoinId)?.Symbol?.ToUpperInvariant() ?? pos.CoinId.ToUpperInvariant(),
                Color = color,
                PctText = pos.AllocationPct.ToString("0.0") + "%"
            });
        }

        OnPropertyChanged(nameof(HasHoldings));
        OnPropertyChanged(nameof(IsStale));
    }

    private void ReloadChart() => _ = LoadChartAsync(SelectedRange, ++_chartToken);

    private async Task LoadChartAsync(string range, int token)
    {
        // Derived positions so the chart works in both manual and transaction modes.
        var holdings = _session.Summary.Positions
            .Where(p => p.Amount > 0)
            .Select(p => new Holding(p.CoinId, p.Amount, null))
            .ToList();
        _chartCoins = holdings.Select(h => h.CoinId).ToHashSet();

        if (holdings.Count == 0)
        {
            ChartValues = Array.Empty<double>();
            RangeChangeText = string.Empty;
            return;
        }

        var historyDict = new Dictionary<string, IReadOnlyList<HistoryPoint>>();
        foreach (var h in holdings)
        {
            historyDict[h.CoinId] = await _history.GetAsync(h.CoinId, "usd", range);
        }

        if (token != _chartToken)
        {
            return;
        }

        var grid = historyDict.Values.Where(v => v.Count > 0).OrderByDescending(v => v.Count).FirstOrDefault();
        if (grid is null)
        {
            ChartValues = Array.Empty<double>();
            return;
        }

        var dates = grid.Select(p => p.Date).ToList();
        var series = ValueSeries.FromConstantHoldings(holdings, historyDict, dates);
        var rate = _session.Fx.Rate;
        ChartValues = series.Select(v => (double)(v.Value * rate)).ToList();

        if (series.Count >= 2 && series[0].Value > 0)
        {
            var change = (series[^1].Value - series[0].Value) / series[0].Value * 100m;
            RangeChangeText = Format.Percent(change);
            RangeIsUp = change >= 0;
        }
        else
        {
            RangeChangeText = string.Empty;
        }
    }
}
