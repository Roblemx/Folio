# Architecture

Folio is a **WPF (.NET 8)** desktop app built with **MVVM**. The guiding rule is a hard split
between a **pure calculation engine** (no UI, no IO — fully unit-tested) and the **services** that
fetch market data and persist state. Everything the user sees is a thin view over a view model
that binds to those services.

```
┌─────────────────────────────────────────────────────────────┐
│  Views (XAML)         one UserControl per screen + charts    │
│  ▲ data binding                                              │
│  ViewModels           CommunityToolkit.Mvvm, one per screen  │
│  ▲                                                           │
│  PortfolioSession     live app state + mutation API          │
│  ├── Engine           pure math (no IO) ── unit tested       │
│  └── Services         market data · persistence · theme · …  │
└─────────────────────────────────────────────────────────────┘
```

## Projects

| Path | What it is |
|---|---|
| `src/Folio` | The application (WPF). |
| `tests/Folio.Tests` | xUnit + FluentAssertions; network-free via a fake `HttpMessageHandler`. |

## Layers

### Engine (`src/Folio/Engine`)

Pure, deterministic, side-effect-free static classes. No `HttpClient`, no files, no `DateTime.Now`
in the math paths. This is where correctness lives, and it's where the tests concentrate.

- **`CostBasis`** — Average and FIFO accounting over a transaction list; realized vs cost-removed.
- **`PortfolioEngine`** — turns holdings/transactions + prices into per-coin `DerivedPosition`s and
  rolled-up `PortfolioSummary` totals (value, invested, P&L, 24h, allocation). `Combine` merges
  several portfolios into the "All portfolios" view.
- **`ValueSeries`** — portfolio value over time from a price history grid.
- **`DcaBacktester`** — dollar-cost-averaging simulation vs a lump-sum buy.
- **`RebalanceEngine`** — target-weight drift and the buy/sell deltas to correct it.

### Models (`src/Folio/Models`)

Immutable domain records (`Portfolio`, `Holding`, `Transaction`, `DerivedPosition`,
`PortfolioSummary`, `Alert`, `WatchedAddress`, …) plus market types (`PricePoint`, `HistoryPoint`,
`FearGreed`). Storage DTOs live separately under `Services/Persistence` so the on-disk shape can
evolve via migrations without coupling to the domain.

### Services (`src/Folio/Services`)

- **Market data** (`Services/Market`)
  - `CoinGeckoClient` (`IMarketDataClient`) — the single point of network IO for prices, markets,
    history and FX. Small retry/backoff on 429/5xx. **This is the unit of mocking in tests.**
  - `ChainBalanceClient` — read-only BTC (mempool.space) / ETH (Blockscout) balances.
  - `PriceService`, `HistoryService`, `FxService`, `MarketsService`, `CoinCatalogService` —
    typed caches over the client; each keeps a **last-known** copy and a **stale flag** so the UI
    works offline.
  - `IconService` — downloads coin logos once and disk-caches them (`%AppData%/Folio/icons`) so the
    UI shows real icons and keeps showing them offline.
  - `CacheStore` — a tiny timestamped JSON disk cache (`%AppData%/Folio/cache`).
- **Persistence** (`Services/Persistence`)
  - `PortfolioStore` (`IPortfolioStore`) — loads/saves the workspace to `portfolio.json`, optionally
    AES-256-GCM encrypted. `AtomicFile` does tmp-write + rename + `.bak`, and a corrupt primary
    falls back to the backup. `StorageMigrator` versions the schema; `StorageMapper` maps DTO ↔ domain.
  - `SettingsStore`, `AlertStore`, `WatchStore` — small plaintext JSON stores for their concerns.
  - `FileCrypto` — AES-GCM + PBKDF2 (200k iterations); fail-closed.
- **App services**
  - `PortfolioSession` — the **live state**: the active portfolio (or the combined view), the latest
    `PortfolioSummary`, and the whole mutation API. Recomputes whenever prices, FX or data change
    and persists on every mutation. Raises `Changed` (recompute) and `PortfoliosChanged` (structure).
  - `AlertService` — edge-triggered price-alert evaluation on each price refresh.
  - `WatchService` — refreshes watch-only balances.
  - `AutoRefreshService` — a `DispatcherTimer` that refreshes prices on the chosen interval.
  - `NavigationService`, `ThemeService` — back-stack navigation and runtime theme swapping.

### ViewModels (`src/Folio/ViewModels`)

One per screen, using `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`,
`[RelayCommand]`). View models never touch the network or disk directly — they go through
`PortfolioSession` and the services, and they rebuild their display rows in response to the
`Changed` event. `ShellViewModel` hosts the sidebar, the portfolio switcher and the toast host.

### Views (`src/Folio/Views`, `Controls`, `Themes`, `Converters`)

XAML user controls bound to their view models via `DataTemplate`s in `App.xaml`. Charts
(`Sparkline`, `DonutChart`, `AreaChart`) are **custom `FrameworkElement`s** drawn in `OnRender` —
no charting dependency. `CoinIcon` shows a coin's real logo (via `IconService`) with an initials
fallback. All colors are `DynamicResource`s so the theme can be swapped at runtime.

## Key flows

**Startup** (`App.xaml.cs`) — build the DI container → apply saved theme + currency → if the data
file is encrypted, show the modal **unlock window** before anything loads it → start background
services (alerts, auto-refresh) → show the main window.

**A price refresh** — a screen (or the auto-refresh timer) calls `PortfolioSession.RefreshAsync()`
→ `FxService` + `PriceService` fetch and cache → `PriceService.Updated` fires → `PortfolioSession`
recomputes the summary and raises `Changed` → every view model rebuilds its rows → `AlertService`
evaluates alerts and may raise a toast.

**A mutation** (add holding, record transaction, edit target, …) — the view model calls a
`PortfolioSession` method → the workspace is updated, **persisted atomically**, and recomputed →
`Changed` propagates to the UI.

## Testing

`tests/Folio.Tests` covers the engine (cost basis, portfolio math, value series, DCA, rebalancing),
persistence (round-trip, backup recovery, migration, encryption), and the market/alert services
(parsing, retry, edge-triggering) — all **without a network** by injecting a fake
`HttpMessageHandler`. Run with `dotnet test`.
