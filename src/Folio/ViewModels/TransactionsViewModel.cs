using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Helpers;
using Folio.Models;
using Folio.Services;
using Folio.Services.Market;
using Microsoft.Win32;

namespace Folio.ViewModels;

/// <summary>A selectable transaction-type chip in the editor.</summary>
public sealed partial class TxTypeOption : ObservableObject
{
    public TxTypeOption(TransactionType type)
    {
        Type = type;
        Name = type.ToString();
        Label = TransactionRow.LabelFor(type);
    }

    public TransactionType Type { get; }
    public string Name { get; }
    public string Label { get; }

    [ObservableProperty] private bool _isSelected;
}

/// <summary>
/// The transaction ledger: list + add/edit/delete, the manual↔transaction mode switch,
/// the cost-basis method, realized P&amp;L surfacing, and CSV/JSON import &amp; export.
/// </summary>
public sealed partial class TransactionsViewModel : ViewModelBase
{
    private readonly PortfolioSession _session;
    private readonly ICoinCatalogService _catalog;
    private string? _editingId;

    public TransactionsViewModel(PortfolioSession session, ICoinCatalogService catalog)
    {
        _session = session;
        _catalog = catalog;

        TypeOptions = Enum.GetValues<TransactionType>().Select(t => new TxTypeOption(t)).ToList();
        _isTransactionMode = session.Mode == PortfolioMode.Transactions;
        SyncTypeChips();

        _session.Changed += OnSessionChanged;
        BuildRows();
        _ = _session.RefreshAsync();
    }

    public ObservableCollection<TransactionRow> Rows { get; } = new();

    public ObservableCollection<Coin> SearchResults { get; } = new();

    public IReadOnlyList<TxTypeOption> TypeOptions { get; }

    public bool HasTransactions => Rows.Count > 0;

    public bool IsStale => _session.IsStale;

    /// <summary>Editing the ledger requires a concrete portfolio (disabled in the combined view).</summary>
    public bool IsEditable => _session.IsEditable;

    public string CostBasisName => _session.CostBasis.ToString();

    public bool HasRealized => _session.Mode == PortfolioMode.Transactions && _session.Summary.TotalRealized != 0m;

    public string RealizedText => Format.Fiat(_session.Summary.TotalRealized, _session.Fx.Symbol, _session.Fx.Rate);

    public bool RealizedUp => _session.Summary.TotalRealized >= 0m;

    [ObservableProperty] private bool _isTransactionMode;

    [ObservableProperty] private string _statusText = string.Empty;

    // ----- Editor state -----
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private string _editorTitle = "Add transaction";
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTypeName))]
    private TransactionType _selectedType = TransactionType.Buy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private Coin? _selectedCoin;

    [ObservableProperty] private string _selectedCoinLabel = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _amountText = string.Empty;

    [ObservableProperty] private string _priceText = string.Empty;
    [ObservableProperty] private string _feeText = string.Empty;
    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;

    public string SelectedTypeName => SelectedType.ToString();

    partial void OnIsTransactionModeChanged(bool value)
    {
        var target = value ? PortfolioMode.Transactions : PortfolioMode.Manual;
        if (_session.Mode != target)
        {
            _session.SetMode(target);
        }
    }

    partial void OnSelectedTypeChanged(TransactionType value) => SyncTypeChips();

    private void SyncTypeChips()
    {
        foreach (var opt in TypeOptions)
        {
            opt.IsSelected = opt.Type == SelectedType;
        }
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

    [RelayCommand]
    private void SetType(TxTypeOption option) => SelectedType = option.Type;

    [RelayCommand]
    private void SetCostBasis(string name)
    {
        if (Enum.TryParse<CostBasisMethod>(name, true, out var method))
        {
            _session.SetCostBasis(method);
        }
    }

    [RelayCommand]
    private async Task AddTransaction()
    {
        _editingId = null;
        EditorTitle = "Add transaction";
        SelectedType = TransactionType.Buy;
        SelectedCoin = null;
        SelectedCoinLabel = string.Empty;
        AmountText = string.Empty;
        PriceText = string.Empty;
        FeeText = string.Empty;
        NoteText = string.Empty;
        DateText = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        SearchText = string.Empty;
        SearchResults.Clear();
        SyncTypeChips();
        IsEditorOpen = true;
        await _catalog.EnsureLoadedAsync();
    }

    [RelayCommand]
    private async Task EditRow(TransactionRow row)
    {
        var t = row.Source;
        _editingId = t.Id;
        EditorTitle = "Edit transaction";
        SelectedType = t.Type;
        SelectedCoin = new Coin(t.CoinId, row.Symbol.ToLowerInvariant(), row.CoinName);
        SelectedCoinLabel = $"{row.CoinName} ({row.Symbol})";

        var rate = _session.Fx.Rate;
        AmountText = Format.Amount(t.Amount);
        PriceText = t.PricePerCoin > 0 ? Format.Amount(t.PricePerCoin * rate) : string.Empty;
        FeeText = t.Fee > 0 ? Format.Amount(t.Fee * rate) : string.Empty;
        DateText = t.Timestamp.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        NoteText = t.Note ?? string.Empty;
        SearchText = string.Empty;
        SearchResults.Clear();
        SyncTypeChips();
        IsEditorOpen = true;
        await _catalog.EnsureLoadedAsync();
    }

    [RelayCommand]
    private void DeleteRow(TransactionRow row) => _session.RemoveTransaction(row.Source.Id);

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

        var rate = _session.Fx.Rate == 0 ? 1m : _session.Fx.Rate;
        decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);

        decimal priceUsd = 0m;
        if (decimal.TryParse(PriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price > 0)
        {
            priceUsd = price / rate;
        }

        decimal feeUsd = 0m;
        if (decimal.TryParse(FeeText, NumberStyles.Number, CultureInfo.InvariantCulture, out var fee) && fee > 0)
        {
            feeUsd = fee / rate;
        }

        var ts = DateTimeOffset.TryParse(DateText, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal, out var dt)
            ? dt
            : DateTimeOffset.Now;

        var note = string.IsNullOrWhiteSpace(NoteText) ? null : NoteText.Trim();
        var id = _editingId ?? Guid.NewGuid().ToString("N");
        var tx = new Transaction(id, SelectedCoin.Id, SelectedType, amount, priceUsd, feeUsd, ts, note);

        if (_editingId == null)
        {
            _session.AddTransaction(tx);
        }
        else
        {
            _session.UpdateTransaction(tx);
        }

        IsEditorOpen = false;
    }

    [RelayCommand]
    private async Task Refresh() => await _session.RefreshAsync();

    [RelayCommand]
    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export transactions",
            Filter = "CSV file (*.csv)|*.csv",
            FileName = "folio-transactions.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, CsvIo.ExportTransactions(_session.Transactions));
            StatusText = $"Exported {_session.Transactions.Count} transactions.";
        }
    }

    [RelayCommand]
    private void ExportJson()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export portfolio (JSON)",
            Filter = "JSON file (*.json)|*.json",
            FileName = "folio-backup.json"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, _session.ExportWorkspaceJson());
            StatusText = "Portfolio exported as JSON.";
        }
    }

    [RelayCommand]
    private void ImportCsv()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import transactions",
            Filter = "CSV file (*.csv)|*.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var imported = CsvIo.ImportTransactions(File.ReadAllText(dialog.FileName));
            if (imported.Count == 0)
            {
                StatusText = "No transactions found in that file.";
                return;
            }

            _session.ImportTransactions(imported, replace: false);
            IsTransactionMode = true; // imported ledgers drive the portfolio
            StatusText = $"Imported {imported.Count} transactions.";
        }
        catch (Exception ex)
        {
            StatusText = "Import failed: " + ex.Message;
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(BuildRows);

    private void BuildRows()
    {
        Rows.Clear();
        var sym = _session.Fx.Symbol;
        var rate = _session.Fx.Rate;

        foreach (var t in _session.Transactions.OrderByDescending(t => t.Timestamp))
        {
            Rows.Add(TransactionRow.Build(t, _session.SnapshotFor(t.CoinId), sym, rate));
        }

        if (IsTransactionMode != (_session.Mode == PortfolioMode.Transactions))
        {
            IsTransactionMode = _session.Mode == PortfolioMode.Transactions;
        }

        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(CostBasisName));
        OnPropertyChanged(nameof(HasRealized));
        OnPropertyChanged(nameof(RealizedText));
        OnPropertyChanged(nameof(RealizedUp));
    }
}
