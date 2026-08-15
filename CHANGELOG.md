# Changelog

All notable changes to Folio are documented here. This project adheres to
[Semantic Versioning](https://semver.org/).

## [1.0.0]

First public release. A complete, local-first crypto portfolio tracker for Windows.

### Portfolio
- Multiple portfolios with a switcher and a combined **"All portfolios"** view.
- **Manual** and **transaction-ledger** modes per portfolio (buy/sell/transfer/airdrop/swap/fee).
- Cost basis (**Average** / **FIFO**) with realized vs unrealized P&L.
- Dashboard: total value (also in **₿ / sats**), 24h change, all-time P&L, allocation donut,
  top movers, value-over-time chart.
- CSV import; CSV / JSON export.

### Analysis
- **DCA backtester** vs lump-sum, over real price history.
- **Rebalancing** with editable target weights and buy/sell hints.
- Per-coin detail with live price chart and position P&L.

### Markets & alerts
- **Markets** — top coins with sparklines, gainers/losers, Crypto **Fear & Greed** index.
- **Price alerts** — above/below targets, edge-triggered on refresh, with in-app toasts.

### Watch-only
- Read-only **BTC** and **ETH** balances from public explorers — no keys.

### Privacy & comfort
- Optional **AES-256-GCM** encryption (PBKDF2) with a startup unlock.
- Dark / Light / System themes (live), 5 display currencies, configurable auto-refresh.
- Offline-tolerant: last-known data with a clear stale indicator.
- Privacy panel listing every network host the app contacts.
- Real coin logos throughout, downloaded once and cached locally, with an initials fallback.

### Engineering
- Pure, unit-tested calculation engine; **69** tests (engine, persistence, services).
- Atomic file writes with backup recovery and schema migration.
