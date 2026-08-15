using System;
using System.Net.Http;
using System.Windows;
using Folio.Services;
using Folio.Services.Market;
using Folio.Services.Persistence;
using Folio.ViewModels;
using Folio.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Folio;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keep the app alive while the (optional) modal unlock window opens and closes before
        // the main window exists — otherwise WPF shuts down on the transient zero-window state.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();

        // Persistence
        services.AddSingleton<IPortfolioStore>(_ => new PortfolioStore(AppPaths.DataDirectory));
        services.AddSingleton<ISettingsStore>(_ => new SettingsStore(AppPaths.DataDirectory));
        services.AddSingleton<IAlertStore>(_ => new AlertStore(AppPaths.DataDirectory));
        services.AddSingleton<IWatchStore>(_ => new WatchStore(AppPaths.DataDirectory));

        // Market data
        services.AddSingleton(_ =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.Add("User-Agent", "Folio/0.1");
            http.DefaultRequestHeaders.Add("Accept", "application/json");
            return http;
        });
        services.AddSingleton(_ => new CacheStore(AppPaths.DataDirectory));
        services.AddSingleton<IMarketDataClient>(sp => new CoinGeckoClient(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<ICoinCatalogService, CoinCatalogService>();
        services.AddSingleton<IIconService, IconService>();
        services.AddSingleton<IPriceService, PriceService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IFxService, FxService>();
        services.AddSingleton<IMarketsService, MarketsService>();
        services.AddSingleton<IChainBalanceClient>(sp => new ChainBalanceClient(sp.GetRequiredService<HttpClient>()));

        // Services
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<PortfolioSession>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<IWatchService, WatchService>();
        services.AddSingleton<AutoRefreshService>();

        // View models
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<HoldingsViewModel>();
        services.AddTransient<TransactionsViewModel>();
        services.AddTransient<MarketsViewModel>();
        services.AddTransient<AlertsViewModel>();
        services.AddTransient<WatchViewModel>();
        services.AddTransient<DcaViewModel>();
        services.AddTransient<RebalanceViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();

        // Apply the saved theme before showing any UI.
        var settings = _services.GetRequiredService<ISettingsStore>().Load();
        _services.GetRequiredService<IThemeService>().Apply(settings.Theme);
        _services.GetRequiredService<IFxService>().Currency = settings.Currency;

        // If the data file is encrypted, unlock it BEFORE anything loads the portfolio.
        var store = _services.GetRequiredService<IPortfolioStore>();
        if (store.FileExists && store.IsEncrypted)
        {
            var unlock = new UnlockWindow(store);
            if (unlock.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        // Start background services (alerts evaluation + auto-refresh timer) and the icon cache.
        _services.GetRequiredService<IIconService>();
        _services.GetRequiredService<IAlertService>();
        _services.GetRequiredService<AutoRefreshService>();

        var window = _services.GetRequiredService<MainWindow>();
        window.DataContext = _services.GetRequiredService<ShellViewModel>();
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
