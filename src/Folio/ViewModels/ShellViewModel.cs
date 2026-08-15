using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Helpers;
using Folio.Models;
using Folio.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.ViewModels;

/// <summary>A row in the portfolio switcher popup.</summary>
public sealed partial class PortfolioListItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ValueText { get; init; }
    public bool IsAll { get; init; }
    [ObservableProperty] private bool _isActive;
}

/// <summary>Root view model: portfolio switcher + sidebar navigation + content host.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly PortfolioSession _session;
    private readonly IAlertService _alerts;

    public ShellViewModel(NavigationService navigation, PortfolioSession session,
        IAlertService alerts, IServiceProvider services)
    {
        Navigation = navigation;
        _session = session;
        _alerts = alerts;
        _services = services;

        NavItems = new ObservableCollection<NavItem>
        {
            new("Dashboard", "IconDashboard"),
            new("Holdings", "IconHoldings"),
            new("Transactions", "IconTransactions"),
            new("Markets", "IconMarkets"),
            new("Alerts", "IconAlerts"),
            new("Watch-only", "IconWatch"),
            new("Backtest", "IconBacktest"),
            new("Rebalance", "IconRebalance"),
            new("Settings", "IconSettings")
        };

        Navigation.PropertyChanged += OnNavigationChanged;
        _session.PortfoliosChanged += (_, _) => BuildPortfolioItems();
        _session.Changed += (_, _) => BuildPortfolioItems();
        _alerts.Triggered += OnAlertTriggered;

        BuildPortfolioItems();
        Navigate(NavItems[0]);
    }

    public NavigationService Navigation { get; }

    public ObservableCollection<NavItem> NavItems { get; }

    public ObservableCollection<PortfolioListItem> PortfolioItems { get; } = new();

    public ObservableCollection<ToastItem> Toasts { get; } = new();

    public string ActivePortfolioName => _session.ActiveName;

    public bool CanModifyActive => !_session.IsAllView && _session.Portfolios.Count >= 1;

    public bool CanDeleteActive => !_session.IsAllView && _session.Portfolios.Count > 1;

    [ObservableProperty] private bool _isSwitcherOpen;
    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _nameInput = string.Empty;

    public bool IsEditingPortfolio => IsCreating || IsRenaming;

    [RelayCommand]
    private void ToggleSwitcher()
    {
        IsSwitcherOpen = !IsSwitcherOpen;
        if (!IsSwitcherOpen)
        {
            CancelEditor();
        }
    }

    [RelayCommand]
    private void SelectPortfolio(PortfolioListItem item)
    {
        _session.SelectPortfolio(item.IsAll ? null : item.Id);
        IsSwitcherOpen = false;
        CancelEditor();
        OnPropertyChanged(nameof(ActivePortfolioName));
        GoDashboard();
    }

    [RelayCommand]
    private void BeginCreate()
    {
        IsCreating = true;
        IsRenaming = false;
        NameInput = string.Empty;
        OnPropertyChanged(nameof(IsEditingPortfolio));
    }

    [RelayCommand]
    private void BeginRename()
    {
        if (_session.IsAllView)
        {
            return;
        }

        IsRenaming = true;
        IsCreating = false;
        NameInput = _session.ActiveName;
        OnPropertyChanged(nameof(IsEditingPortfolio));
    }

    [RelayCommand]
    private void ConfirmEditor()
    {
        if (string.IsNullOrWhiteSpace(NameInput))
        {
            return;
        }

        if (IsCreating)
        {
            _session.CreatePortfolio(NameInput);
            GoDashboard();
        }
        else if (IsRenaming && _session.ActiveId is { } id)
        {
            _session.RenamePortfolio(id, NameInput);
        }

        CancelEditor();
        IsSwitcherOpen = false;
        OnPropertyChanged(nameof(ActivePortfolioName));
    }

    [RelayCommand]
    private void CancelEditor()
    {
        IsCreating = false;
        IsRenaming = false;
        NameInput = string.Empty;
        OnPropertyChanged(nameof(IsEditingPortfolio));
    }

    [RelayCommand]
    private void DeleteActive()
    {
        if (_session.ActiveId is { } id)
        {
            _session.DeletePortfolio(id);
            IsSwitcherOpen = false;
            OnPropertyChanged(nameof(ActivePortfolioName));
            GoDashboard();
        }
    }

    [RelayCommand]
    private void Navigate(NavItem item)
    {
        ViewModelBase page = item.Title switch
        {
            "Dashboard" => _services.GetRequiredService<DashboardViewModel>(),
            "Holdings" => _services.GetRequiredService<HoldingsViewModel>(),
            "Transactions" => _services.GetRequiredService<TransactionsViewModel>(),
            "Markets" => _services.GetRequiredService<MarketsViewModel>(),
            "Alerts" => _services.GetRequiredService<AlertsViewModel>(),
            "Watch-only" => _services.GetRequiredService<WatchViewModel>(),
            "Backtest" => _services.GetRequiredService<DcaViewModel>(),
            "Rebalance" => _services.GetRequiredService<RebalanceViewModel>(),
            "Settings" => _services.GetRequiredService<SettingsViewModel>(),
            _ => _services.GetRequiredService<DashboardViewModel>()
        };

        Navigation.Navigate(page, resetRoot: true);
    }

    private void GoDashboard() => Navigate(NavItems[0]);

    [RelayCommand]
    private void DismissToast(ToastItem toast) => Toasts.Remove(toast);

    private void OnAlertTriggered(object? sender, AlertTriggeredEventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            var sym = e.Snapshot.Symbol?.ToUpperInvariant() ?? e.Alert.CoinId.ToUpperInvariant();
            var name = e.Snapshot.Name ?? sym;
            var verb = e.Alert.Direction == AlertDirection.Above ? "rose above" : "fell below";
            var fxSym = _session.Fx.Symbol;
            var rate = _session.Fx.Rate;

            var toast = new ToastItem
            {
                Title = $"{name} price alert",
                Message = $"{sym} {verb} {Format.Price(e.Alert.TargetPrice, fxSym, rate)} · now {Format.Price(e.Price, fxSym, rate)}",
                IsUp = e.Alert.Direction == AlertDirection.Above
            };

            Toasts.Add(toast);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Toasts.Remove(toast);
            };
            timer.Start();
        });

    private void BuildPortfolioItems()
    {
        PortfolioItems.Clear();
        var sym = _session.Fx.Symbol;
        var rate = _session.Fx.Rate;

        var allValue = 0m;
        foreach (var p in _session.Portfolios)
        {
            allValue += _session.ValueOf(p.Portfolio.Id);
        }

        PortfolioItems.Add(new PortfolioListItem
        {
            Id = string.Empty,
            Name = "All portfolios",
            ValueText = Format.Fiat(allValue, sym, rate),
            IsAll = true,
            IsActive = _session.IsAllView
        });

        foreach (var p in _session.Portfolios)
        {
            PortfolioItems.Add(new PortfolioListItem
            {
                Id = p.Portfolio.Id,
                Name = p.Portfolio.Name,
                ValueText = Format.Fiat(_session.ValueOf(p.Portfolio.Id), sym, rate),
                IsActive = _session.ActiveId == p.Portfolio.Id
            });
        }

        OnPropertyChanged(nameof(ActivePortfolioName));
        OnPropertyChanged(nameof(CanModifyActive));
        OnPropertyChanged(nameof(CanDeleteActive));
    }

    private void OnNavigationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NavigationService.CurrentPage))
        {
            return;
        }

        var title = Navigation.CurrentPage switch
        {
            DashboardViewModel => "Dashboard",
            HoldingsViewModel or CoinDetailViewModel => "Holdings",
            TransactionsViewModel => "Transactions",
            MarketsViewModel => "Markets",
            AlertsViewModel => "Alerts",
            WatchViewModel => "Watch-only",
            DcaViewModel => "Backtest",
            RebalanceViewModel => "Rebalance",
            SettingsViewModel => "Settings",
            _ => null
        };

        foreach (var item in NavItems)
        {
            item.IsActive = item.Title == title;
        }
    }

    partial void OnIsCreatingChanged(bool value) => OnPropertyChanged(nameof(IsEditingPortfolio));

    partial void OnIsRenamingChanged(bool value) => OnPropertyChanged(nameof(IsEditingPortfolio));
}
