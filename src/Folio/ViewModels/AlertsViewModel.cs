using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Helpers;
using Folio.Models;
using Folio.Services;
using Folio.Services.Market;

namespace Folio.ViewModels;

/// <summary>The Alerts screen: list price alerts and add/edit/enable/delete them.</summary>
public sealed partial class AlertsViewModel : ViewModelBase
{
    private readonly IAlertService _alerts;
    private readonly ICoinCatalogService _catalog;
    private readonly PortfolioSession _session;
    private readonly IPriceService _prices;
    private string? _editingId;

    public AlertsViewModel(IAlertService alerts, ICoinCatalogService catalog,
        PortfolioSession session, IPriceService prices)
    {
        _alerts = alerts;
        _catalog = catalog;
        _session = session;
        _prices = prices;

        _alerts.Changed += OnChanged;
        _prices.Updated += OnChanged;
        _session.Fx.Changed += OnChanged;

        BuildRows();
        _ = _session.RefreshAsync();
    }

    public ObservableCollection<AlertRow> Rows { get; } = new();

    public ObservableCollection<Coin> SearchResults { get; } = new();

    public bool HasAlerts => Rows.Count > 0;

    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private string _editorTitle = "New price alert";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _directionName = "Above";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private Coin? _selectedCoin;

    [ObservableProperty] private string _selectedCoinLabel = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _targetText = string.Empty;

    [ObservableProperty] private string _noteText = string.Empty;

    [RelayCommand]
    private async Task AddAlert()
    {
        _editingId = null;
        EditorTitle = "New price alert";
        SelectedCoin = null;
        SelectedCoinLabel = string.Empty;
        DirectionName = "Above";
        TargetText = string.Empty;
        NoteText = string.Empty;
        SearchText = string.Empty;
        SearchResults.Clear();
        IsEditorOpen = true;
        await _catalog.EnsureLoadedAsync();
    }

    [RelayCommand]
    private async Task EditRow(AlertRow row)
    {
        var a = row.Source;
        _editingId = a.Id;
        EditorTitle = "Edit price alert";
        SelectedCoin = new Coin(a.CoinId, row.Symbol.ToLowerInvariant(), row.Name);
        SelectedCoinLabel = $"{row.Name} ({row.Symbol})";
        DirectionName = a.Direction.ToString();
        TargetText = Format.Amount(a.TargetPrice * _session.Fx.Rate);
        NoteText = a.Note ?? string.Empty;
        SearchText = string.Empty;
        SearchResults.Clear();
        IsEditorOpen = true;
        await _catalog.EnsureLoadedAsync();
    }

    [RelayCommand]
    private void DeleteRow(AlertRow row) => _alerts.Remove(row.AlertId);

    [RelayCommand]
    private void ToggleEnabled(AlertRow row) => _alerts.SetEnabled(row.AlertId, !row.Source.Enabled);

    [RelayCommand]
    private void SetDirection(string direction) => DirectionName = direction;

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
        decimal.TryParse(TargetText, NumberStyles.Number, CultureInfo.InvariantCulture, out var t) && t > 0;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (SelectedCoin == null)
        {
            return;
        }

        decimal.TryParse(TargetText, NumberStyles.Number, CultureInfo.InvariantCulture, out var target);
        var rate = _session.Fx.Rate == 0 ? 1m : _session.Fx.Rate;
        var targetUsd = target / rate;
        var direction = string.Equals(DirectionName, "Below", StringComparison.OrdinalIgnoreCase)
            ? AlertDirection.Below
            : AlertDirection.Above;
        var note = string.IsNullOrWhiteSpace(NoteText) ? null : NoteText.Trim();

        if (_editingId == null)
        {
            _alerts.Add(SelectedCoin.Id, direction, targetUsd, note);
        }
        else
        {
            var existing = _alerts.Alerts.FirstOrDefault(a => a.Id == _editingId);
            if (existing != null)
            {
                _alerts.Update(existing with
                {
                    CoinId = SelectedCoin.Id,
                    Direction = direction,
                    TargetPrice = targetUsd,
                    Note = note
                });
            }
        }

        IsEditorOpen = false;
        _ = _session.RefreshAsync();
    }

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

    private void OnChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(BuildRows);

    private void BuildRows()
    {
        Rows.Clear();
        var sym = _session.Fx.Symbol;
        var rate = _session.Fx.Rate;

        foreach (var alert in _alerts.Alerts.OrderByDescending(a => a.CreatedAt))
        {
            Rows.Add(AlertRow.Build(alert, _session.SnapshotFor(alert.CoinId), sym, rate));
        }

        OnPropertyChanged(nameof(HasAlerts));
    }
}
