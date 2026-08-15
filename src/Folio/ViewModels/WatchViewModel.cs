using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Helpers;
using Folio.Models;
using Folio.Services;

namespace Folio.ViewModels;

/// <summary>The Watch-only screen: track public addresses' balances read-only, no keys.</summary>
public sealed partial class WatchViewModel : ViewModelBase
{
    private static readonly Regex BtcPattern =
        new("^(bc1[a-z0-9]{6,87}|[13][a-km-zA-HJ-NP-Z1-9]{20,40})$", RegexOptions.Compiled);

    private static readonly Regex EthPattern =
        new("^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

    private readonly IWatchService _watch;
    private readonly PortfolioSession _session;

    public WatchViewModel(IWatchService watch, PortfolioSession session)
    {
        _watch = watch;
        _session = session;

        _watch.Changed += OnChanged;
        _session.Prices.Updated += OnChanged;
        _session.Fx.Changed += OnChanged;

        BuildRows();
        _ = LoadAsync();
    }

    public ObservableCollection<WatchRow> Rows { get; } = new();

    public bool HasAddresses => Rows.Count > 0;

    [ObservableProperty] private string _totalText = string.Empty;
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private string _chainName = "Bitcoin";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _addressText = string.Empty;

    [ObservableProperty] private string _labelText = string.Empty;

    [RelayCommand]
    private void AddAddress()
    {
        ChainName = "Bitcoin";
        AddressText = string.Empty;
        LabelText = string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void SetChain(string chain)
    {
        ChainName = chain;
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CancelEditor() => IsEditorOpen = false;

    [RelayCommand]
    private void DeleteRow(WatchRow row) => _watch.Remove(row.Id);

    private bool CanConfirm()
    {
        var addr = AddressText?.Trim() ?? string.Empty;
        if (addr.Length == 0)
        {
            return false;
        }

        return ChainName == "Ethereum" ? EthPattern.IsMatch(addr) : BtcPattern.IsMatch(addr);
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        var chain = ChainName == "Ethereum" ? WatchChain.Ethereum : WatchChain.Bitcoin;
        _watch.Add(chain, AddressText.Trim(), LabelText);
        IsEditorOpen = false;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    private async Task LoadAsync()
    {
        await _session.Prices.RefreshAsync(new[] { "bitcoin", "ethereum" }, "usd");
        await _watch.RefreshAsync();
    }

    private void OnChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(BuildRows);

    private void BuildRows()
    {
        Rows.Clear();
        var sym = _session.Fx.Symbol;
        var rate = _session.Fx.Rate;

        foreach (var addr in _watch.Addresses.OrderByDescending(a => a.AddedAt))
        {
            Rows.Add(WatchRow.Build(addr, _session.SnapshotFor(addr.CoinId), sym, rate));
        }

        var total = Rows.Sum(r => r.ValueUsd);
        TotalText = Format.Fiat(total, sym, rate);

        OnPropertyChanged(nameof(HasAddresses));
    }
}
