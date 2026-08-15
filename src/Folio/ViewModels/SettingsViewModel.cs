using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Models;
using Folio.Services;
using Folio.Services.Persistence;

namespace Folio.ViewModels;

/// <summary>Real settings: currency, theme, auto-refresh interval, plus an about/privacy panel.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _store;
    private readonly IThemeService _theme;
    private readonly PortfolioSession _session;
    private readonly AutoRefreshService _autoRefresh;
    private readonly AppSettings _settings;

    public SettingsViewModel(ISettingsStore store, IThemeService theme,
        PortfolioSession session, AutoRefreshService autoRefresh)
    {
        _store = store;
        _theme = theme;
        _session = session;
        _autoRefresh = autoRefresh;
        _settings = store.Load();

        _selectedCurrency = session.Fx.Currency;
        _selectedTheme = _settings.Theme;
        _refreshSecondsText = _settings.RefreshSeconds.ToString(CultureInfo.InvariantCulture);
        _isEncrypted = session.IsEncrypted;
    }

    public IReadOnlyList<string> Currencies => _session.Fx.Currencies;
    public string[] Themes { get; } = { "Dark", "Light", "System" };

    public string DataLocation => AppPaths.DataDirectory;

    public string[] NetworkHosts { get; } =
    {
        "api.coingecko.com — prices, markets, history, FX",
        "coin-images.coingecko.com — coin logos",
        "api.alternative.me — Fear & Greed index",
        "mempool.space — watch-only BTC balances",
        "eth.blockscout.com — watch-only ETH balances"
    };

    [ObservableProperty] private string _selectedCurrency;
    [ObservableProperty] private string _selectedTheme;
    [ObservableProperty] private string _refreshSecondsText;

    // ----- Encryption -----
    [ObservableProperty] private bool _isEncrypted;
    [ObservableProperty] private bool _isSecurityEditorOpen;
    [ObservableProperty] private string _securityMode = "Enable";
    [ObservableProperty] private string _securityTitle = string.Empty;
    [ObservableProperty] private bool _securityNeedsPassword = true;
    [ObservableProperty] private string _securityError = string.Empty;

    [RelayCommand]
    private void SetCurrency(string code)
    {
        _session.Fx.Currency = code;
        SelectedCurrency = _session.Fx.Currency;
        _settings.Currency = SelectedCurrency;
        _store.Save(_settings);
    }

    [RelayCommand]
    private void SetTheme(string theme)
    {
        _theme.Apply(theme);
        SelectedTheme = theme;
        _settings.Theme = theme;
        _store.Save(_settings);
    }

    [RelayCommand]
    private void SetRefresh(string seconds)
    {
        if (!int.TryParse(seconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        RefreshSecondsText = seconds;
        _settings.RefreshSeconds = value;
        _autoRefresh.SetInterval(value);
        _store.Save(_settings);
    }

    [RelayCommand]
    private void BeginEnableEncryption()
    {
        SecurityMode = "Enable";
        SecurityTitle = "Encrypt your data";
        SecurityNeedsPassword = true;
        SecurityError = string.Empty;
        IsSecurityEditorOpen = true;
    }

    [RelayCommand]
    private void BeginChangePassword()
    {
        SecurityMode = "Change";
        SecurityTitle = "Change password";
        SecurityNeedsPassword = true;
        SecurityError = string.Empty;
        IsSecurityEditorOpen = true;
    }

    [RelayCommand]
    private void BeginRemoveEncryption()
    {
        SecurityMode = "Remove";
        SecurityTitle = "Remove encryption";
        SecurityNeedsPassword = false;
        SecurityError = string.Empty;
        IsSecurityEditorOpen = true;
    }

    [RelayCommand]
    private void CancelSecurity() => IsSecurityEditorOpen = false;

    /// <summary>Called from the view with the password-box values (passwords aren't bindable).</summary>
    public void ConfirmSecurity(string password, string confirm)
    {
        SecurityError = string.Empty;

        if (SecurityMode == "Remove")
        {
            _session.DisableEncryption();
            ApplyEncrypted(false);
            return;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 4)
        {
            SecurityError = "Use a password of at least 4 characters.";
            return;
        }

        if (password != confirm)
        {
            SecurityError = "Passwords don't match.";
            return;
        }

        if (SecurityMode == "Enable")
        {
            _session.EnableEncryption(password);
        }
        else
        {
            _session.ChangePassword(password);
        }

        ApplyEncrypted(true);
    }

    private void ApplyEncrypted(bool encrypted)
    {
        IsEncrypted = encrypted;
        _settings.Encrypted = encrypted;
        _store.Save(_settings);
        IsSecurityEditorOpen = false;
    }
}
