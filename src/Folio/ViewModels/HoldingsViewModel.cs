using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Models;
using Folio.Services;
using Folio.Services.Market;

namespace Folio.ViewModels;

public sealed partial class HoldingsViewModel : ViewModelBase
{
    private readonly PortfolioSession _session;
    private readonly NavigationService _navigation;
    private readonly ICoinCatalogService _catalog;
    private readonly IHistoryService _history;

    public HoldingsViewModel(PortfolioSession session, NavigationService navigation,
        ICoinCatalogService catalog, IHistoryService history)
    {
        _session = session;
        _navigation = navigation;
        _catalog = catalog;
        _history = history;

        _session.Changed += OnSessionChanged;
        BuildRows();
        _ = _session.RefreshAsync();
    }

    public ObservableCollection<PositionRow> Rows { get; } = new();

    public ObservableCollection<Coin> SearchResults { get; } = new();

    public bool HasHoldings => Rows.Count > 0;

    public bool IsStale => _session.IsStale;

    /// <summary>Manual holdings can only be added in a concrete, manual-mode portfolio (not the combined view).</summary>
    public bool IsManualMode => _session.IsEditable && _session.Mode == PortfolioMode.Manual;

    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private Coin? _selectedCoin;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _amountText = string.Empty;

    [ObservableProperty] private string _avgPriceText = string.Empty;
    [ObservableProperty] private string _selectedCoinLabel = string.Empty;

    [RelayCommand]
    private async Task AddAsset()
    {
        SelectedCoin = null;
        SelectedCoinLabel = string.Empty;
        AmountText = string.Empty;
        AvgPriceText = string.Empty;
        SearchText = string.Empty;
        SearchResults.Clear();
        IsEditorOpen = true;
        await _catalog.EnsureLoadedAsync();
    }

    [RelayCommand]
    private void CancelEditor() => IsEditorOpen = false;

    [RelayCommand]
    private void PickCoin(Coin coin)
    {
        SelectedCoin = coin;
        SelectedCoinLabel = $"{coin.Name} ({coin.Symbol.ToUpperInvariant()})";
        SearchResults.Clear();
        SearchText = string.Empty;
    }

    private bool CanConfirm() =>
        SelectedCoin != null &&
        decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) && a > 0;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (SelectedCoin == null)
        {
            return;
        }

        decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);

        decimal? usdPrice = null;
        if (decimal.TryParse(AvgPriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price > 0)
        {
            var rate = _session.Fx.Rate == 0 ? 1m : _session.Fx.Rate;
            usdPrice = price / rate;
        }

        _session.AddOrUpdate(SelectedCoin.Id, amount, usdPrice);
        IsEditorOpen = false;
    }

    [RelayCommand]
    private void OpenAsset(string coinId) =>
        _navigation.Navigate(new CoinDetailViewModel(coinId, _session, _navigation, _history));

    [RelayCommand]
    private async Task Refresh() => await _session.RefreshAsync();

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
    }

    private void OnSessionChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(BuildRows);

    private void BuildRows()
    {
        Rows.Clear();
        var sym = _session.Fx.Symbol;
        var rate = _session.Fx.Rate;
        foreach (var pos in _session.Summary.Positions)
        {
            Rows.Add(PositionRow.Build(pos, _session.SnapshotFor(pos.CoinId), sym, rate));
        }

        OnPropertyChanged(nameof(HasHoldings));
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(IsManualMode));
    }
}
