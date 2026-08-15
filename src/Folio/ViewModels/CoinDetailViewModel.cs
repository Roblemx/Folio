using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Helpers;
using Folio.Services;
using Folio.Services.Market;

namespace Folio.ViewModels;

public sealed partial class CoinDetailViewModel : ViewModelBase
{
    private readonly PortfolioSession _session;
    private readonly NavigationService _navigation;
    private readonly IHistoryService _history;
    private int _chartToken;

    public CoinDetailViewModel(string coinId, PortfolioSession session, NavigationService navigation, IHistoryService history)
    {
        CoinId = coinId;
        _session = session;
        _navigation = navigation;
        _history = history;

        var snap = session.SnapshotFor(coinId);
        Symbol = snap?.Symbol?.ToUpperInvariant() ?? coinId.ToUpperInvariant();
        Name = snap?.Name ?? coinId;
        Initial = Format.Initial(Symbol);
        Accent = Format.AccentFor(coinId);
        ImageUrl = snap?.ImageUrl;

        var holding = session.HoldingFor(coinId);
        _amountText = holding != null ? Format.Amount(holding.Amount) : string.Empty;
        _avgPriceText = holding?.ManualAvgPrice is { } p ? Format.Amount(p * session.Fx.Rate) : string.Empty;

        _session.Changed += OnSessionChanged;
        RefreshStats();
        _ = LoadChartAsync(SelectedRange, ++_chartToken);
    }

    public string CoinId { get; }
    public string Symbol { get; }
    public string Name { get; }
    public string Initial { get; }
    public Brush Accent { get; }
    public string? ImageUrl { get; }

    public string[] Ranges { get; } = { "24H", "1W", "1M", "1Y", "ALL" };

    [ObservableProperty] private string _marketPriceText = "—";
    [ObservableProperty] private string _changeText = string.Empty;
    [ObservableProperty] private bool _isUp;
    [ObservableProperty] private string _marketCapText = "—";

    [ObservableProperty] private string _valueText = string.Empty;
    [ObservableProperty] private string _avgCostText = "—";
    [ObservableProperty] private string _pnlText = string.Empty;
    [ObservableProperty] private bool _hasPnl;
    [ObservableProperty] private bool _pnlUp;

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private string _avgPriceText = string.Empty;

    [ObservableProperty] private IReadOnlyList<double> _chartValues = Array.Empty<double>();
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RangeLabel))] private string _selectedRange = "1W";
    [ObservableProperty] private string _rangeChangeText = string.Empty;
    [ObservableProperty] private bool _rangeIsUp;

    public string RangeLabel => SelectedRange;

    public bool Holds => _session.HoldingFor(CoinId) != null;

    /// <summary>The manual editor only applies to a concrete, manual-mode portfolio.</summary>
    public bool CanEdit => _session.IsEditable && _session.Mode == Folio.Models.PortfolioMode.Manual;

    [RelayCommand]
    private void Save()
    {
        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            return;
        }

        decimal? usdPrice = null;
        if (decimal.TryParse(AvgPriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price > 0)
        {
            var rate = _session.Fx.Rate == 0 ? 1m : _session.Fx.Rate;
            usdPrice = price / rate;
        }

        _session.AddOrUpdate(CoinId, amount, usdPrice);
        OnPropertyChanged(nameof(Holds));
    }

    [RelayCommand]
    private void Remove()
    {
        _session.Remove(CoinId);
        _navigation.GoBack();
    }

    [RelayCommand]
    private void SelectRange(string range) => SelectedRange = range;

    [RelayCommand]
    private void Back() => _navigation.GoBack();

    partial void OnSelectedRangeChanged(string value) => _ = LoadChartAsync(value, ++_chartToken);

    private async Task LoadChartAsync(string range, int token)
    {
        var points = await _history.GetAsync(CoinId, "usd", range);
        if (token != _chartToken)
        {
            return;
        }

        var rate = _session.Fx.Rate;
        ChartValues = points.Select(p => (double)(p.Price * rate)).ToList();

        if (points.Count >= 2 && points[0].Price > 0)
        {
            var change = (points[^1].Price - points[0].Price) / points[0].Price * 100m;
            RangeChangeText = Format.Percent(change);
            RangeIsUp = change >= 0;
        }
        else
        {
            RangeChangeText = string.Empty;
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(RefreshStats);

    private void RefreshStats()
    {
        var sym = _session.Fx.Symbol;
        var rate = _session.Fx.Rate;

        var snap = _session.SnapshotFor(CoinId);
        if (snap != null)
        {
            MarketPriceText = Format.Price(snap.Price, sym, rate);
            ChangeText = Format.Percent(snap.Change24h);
            IsUp = snap.Change24h >= 0;
            MarketCapText = snap.MarketCap > 0 ? Format.MarketCap(snap.MarketCap, sym, rate) : "—";
        }

        var pos = _session.Summary.Positions.FirstOrDefault(p => p.CoinId == CoinId);
        if (pos != null)
        {
            ValueText = Format.Fiat(pos.Value, sym, rate);
            AvgCostText = pos.AvgCost is { } a ? Format.Price(a, sym, rate) : "—";
            HasPnl = pos.Unrealized.HasValue;
            PnlText = pos.Unrealized is { } u ? Format.Fiat(u, sym, rate) : string.Empty;
            PnlUp = (pos.Unrealized ?? 0m) >= 0m;
        }
        else
        {
            ValueText = Format.Fiat(0m, sym, rate);
            AvgCostText = "—";
            HasPnl = false;
        }

        OnPropertyChanged(nameof(Holds));
        OnPropertyChanged(nameof(CanEdit));
    }
}
