using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Engine;
using Folio.Helpers;
using Folio.Models;
using Folio.Services;
using Folio.Services.Market;

namespace Folio.ViewModels;

/// <summary>The DCA / what-if backtester: "invest $X every interval" over real price history.</summary>
public sealed partial class DcaViewModel : ViewModelBase
{
    private readonly ICoinCatalogService _catalog;
    private readonly IHistoryService _history;
    private readonly PortfolioSession _session;
    private int _runToken;

    public DcaViewModel(ICoinCatalogService catalog, IHistoryService history, PortfolioSession session)
    {
        _catalog = catalog;
        _history = history;
        _session = session;

        _session.Fx.Changed += (_, _) => Application.Current.Dispatcher.Invoke(() => _ = RunAsync());
        _ = _catalog.EnsureLoadedAsync();
    }

    public ObservableCollection<Coin> SearchResults { get; } = new();

    public bool HasSearchResults => SearchResults.Count > 0;

    public string[] Frequencies { get; } = { "Daily", "Weekly", "Monthly" };
    public string[] Ranges { get; } = { "1M", "1Y", "ALL" };

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private Coin? _selectedCoin;
    [ObservableProperty] private string _selectedCoinLabel = string.Empty;
    [ObservableProperty] private string _amountText = "100";
    [ObservableProperty] private string _frequencyName = "Monthly";
    [ObservableProperty] private string _rangeName = "1Y";

    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private bool _isRunning;

    [ObservableProperty] private string _investedText = "—";
    [ObservableProperty] private string _currentValueText = "—";
    [ObservableProperty] private string _roiText = "—";
    [ObservableProperty] private bool _roiUp;
    [ObservableProperty] private string _coinsText = "—";
    [ObservableProperty] private string _avgPriceText = "—";
    [ObservableProperty] private string _contributionsText = "—";
    [ObservableProperty] private string _lumpValueText = "—";
    [ObservableProperty] private string _lumpRoiText = "—";
    [ObservableProperty] private string _verdictText = string.Empty;
    [ObservableProperty] private bool _dcaWon;

    [ObservableProperty] private IReadOnlyList<double> _chartValues = Array.Empty<double>();

    [RelayCommand]
    private void PickCoin(Coin coin)
    {
        SelectedCoin = coin;
        SelectedCoinLabel = $"{coin.Name} ({coin.Symbol.ToUpperInvariant()})";
        SearchResults.Clear();
        OnPropertyChanged(nameof(HasSearchResults));
        SearchText = string.Empty;
        _ = RunAsync();
    }

    [RelayCommand]
    private void SetFrequency(string frequency)
    {
        FrequencyName = frequency;
        _ = RunAsync();
    }

    [RelayCommand]
    private void SetRange(string range)
    {
        RangeName = range;
        _ = RunAsync();
    }

    partial void OnAmountTextChanged(string value) => _ = RunAsync();

    partial void OnSearchTextChanged(string value)
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var coin in _catalog.Search(value, 25))
        {
            SearchResults.Add(coin);
        }

        OnPropertyChanged(nameof(HasSearchResults));
    }

    private async Task RunAsync()
    {
        if (SelectedCoin is null ||
            !decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0)
        {
            HasResult = false;
            return;
        }

        var token = ++_runToken;
        IsRunning = true;

        var history = await _history.GetAsync(SelectedCoin.Id, "usd", RangeName);
        if (token != _runToken)
        {
            return;
        }

        var rate = _session.Fx.Rate == 0 ? 1m : _session.Fx.Rate;
        var frequency = FrequencyName switch
        {
            "Daily" => DcaFrequency.Daily,
            "Weekly" => DcaFrequency.Weekly,
            _ => DcaFrequency.Monthly
        };

        var result = DcaBacktester.Run(history, amount / rate, frequency);
        IsRunning = false;

        if (result.Contributions == 0 || result.TotalInvested == 0m)
        {
            HasResult = false;
            return;
        }

        var sym = _session.Fx.Symbol;
        InvestedText = Format.Fiat(result.TotalInvested, sym, rate);
        CurrentValueText = Format.Fiat(result.CurrentValue, sym, rate);
        RoiText = Format.Percent(result.RoiPct);
        RoiUp = result.RoiPct >= 0m;
        CoinsText = $"{Format.Amount(result.Coins)} {SelectedCoin.Symbol.ToUpperInvariant()}";
        AvgPriceText = Format.Price(result.AvgBuyPrice, sym, rate);
        ContributionsText = result.Contributions.ToString();
        LumpValueText = Format.Fiat(result.LumpSumValue, sym, rate);
        LumpRoiText = Format.Percent(result.LumpSumRoiPct);
        DcaWon = result.RoiPct >= result.LumpSumRoiPct;
        VerdictText = DcaWon
            ? "DCA would have beaten a single lump-sum buy."
            : "A lump-sum buy at the start would have done better.";

        ChartValues = result.ValueOverTime.Select(v => (double)(v.Value * rate)).ToList();
        HasResult = true;
    }
}
