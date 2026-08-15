using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Models;
using Folio.Services;
using Folio.Services.Market;

namespace Folio.ViewModels;

/// <summary>The Markets screen: top coins, movers, Fear &amp; Greed, and search → coin detail.</summary>
public sealed partial class MarketsViewModel : ViewModelBase
{
    private readonly IMarketsService _markets;
    private readonly ICoinCatalogService _catalog;
    private readonly PortfolioSession _session;
    private readonly NavigationService _navigation;
    private readonly IHistoryService _history;

    private List<MarketEntry> _entries = new();

    public MarketsViewModel(IMarketsService markets, ICoinCatalogService catalog,
        PortfolioSession session, NavigationService navigation, IHistoryService history)
    {
        _markets = markets;
        _catalog = catalog;
        _session = session;
        _navigation = navigation;
        _history = history;

        _session.Fx.Changed += OnFxChanged;
        _ = LoadAsync();
    }

    public ObservableCollection<MarketRow> TopCoins { get; } = new();
    public ObservableCollection<MarketRow> Gainers { get; } = new();
    public ObservableCollection<MarketRow> Losers { get; } = new();
    public ObservableCollection<Coin> SearchResults { get; } = new();

    public bool HasSearchResults => SearchResults.Count > 0;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private bool _hasFng;
    [ObservableProperty] private string _fngValueText = "—";
    [ObservableProperty] private string _fngLabel = string.Empty;
    [ObservableProperty] private Brush _fngColor = Brushes.Gray;
    [ObservableProperty] private double _fngPosition;

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private void Open(string coinId) =>
        _navigation.Navigate(new CoinDetailViewModel(coinId, _session, _navigation, _history));

    [RelayCommand]
    private void PickCoin(Coin coin)
    {
        SearchResults.Clear();
        OnPropertyChanged(nameof(HasSearchResults));
        SearchText = string.Empty;
        Open(coin.Id);
    }

    partial void OnSearchTextChanged(string value)
    {
        SearchResults.Clear();
        if (!string.IsNullOrWhiteSpace(value))
        {
            foreach (var coin in _catalog.Search(value, 25))
            {
                SearchResults.Add(coin);
            }
        }

        OnPropertyChanged(nameof(HasSearchResults));
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        await _catalog.EnsureLoadedAsync();

        var top = await _markets.GetTopAsync(50);
        _entries = top.ToList();
        BuildRows();

        ApplyFearGreed(await _markets.GetFearGreedAsync());
        IsLoading = false;
    }

    private void OnFxChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(BuildRows);

    private void BuildRows()
    {
        var sym = _session.Fx.Symbol;
        var rate = _session.Fx.Rate;

        TopCoins.Clear();
        var rank = 1;
        foreach (var entry in _entries)
        {
            TopCoins.Add(MarketRow.Build(entry, rank++, sym, rate));
        }

        Gainers.Clear();
        foreach (var entry in _entries.OrderByDescending(x => x.Change24h).Take(5))
        {
            Gainers.Add(MarketRow.Build(entry, 0, sym, rate));
        }

        Losers.Clear();
        foreach (var entry in _entries.OrderBy(x => x.Change24h).Take(5))
        {
            Losers.Add(MarketRow.Build(entry, 0, sym, rate));
        }

        HasData = TopCoins.Count > 0;
    }

    private void ApplyFearGreed(FearGreed? fg)
    {
        if (fg is null)
        {
            HasFng = false;
            return;
        }

        HasFng = true;
        FngValueText = fg.Value.ToString();
        FngLabel = fg.Label;
        FngPosition = Math.Clamp(fg.Value / 100.0, 0, 1);
        FngColor = BrushFor(fg.Value);
    }

    private static Brush BrushFor(int value)
    {
        var hex = value switch
        {
            < 25 => "#F0616D", // extreme fear
            < 45 => "#F2A33D", // fear
            < 55 => "#E8C341", // neutral
            < 75 => "#7FC97F", // greed
            _ => "#2FBF71"     // extreme greed
        };
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
