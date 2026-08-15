# Privacy & data

Folio is **local-first**: it is designed so that your portfolio never leaves your computer, and so
that you can verify that claim. This document is the honest, complete version.

## TL;DR

- Your holdings, transactions, alerts and watched addresses are stored **only** on your PC.
- Folio has **no account, no server, no sync, and no telemetry/analytics**.
- The only network calls are to **read public market data** — never to send your data anywhere.
- Folio **never asks for, stores, or transmits a private key or seed phrase** and cannot move funds.
- You can **encrypt** your portfolio file with a password (AES-256-GCM).

## Where your data lives

Everything is under `%AppData%\Folio\`:

| File | Contents |
|---|---|
| `portfolio.json` | Portfolios, holdings, transactions, target allocations. Optionally encrypted. |
| `settings.json` | Theme, currency, refresh interval, encrypted-flag. Plaintext by design. |
| `alerts.json` | Your price alerts. |
| `watch.json` | Watch-only addresses + their last-known balances. |
| `cache/` | Last-known prices, market list, history, FX — so the app works offline. |

These are plain files you fully control: back them up, move them, or delete them. Removing the
folder resets the app to a clean first run. Nothing is written anywhere else.

## What is sent over the network, and to whom

Folio makes outbound HTTPS requests **only** to fetch public, read-only market data. It never
includes any of your portfolio in a request. The complete list of hosts (also shown in
**Settings → Privacy & data**):

| Host | Purpose | What is sent |
|---|---|---|
| `api.coingecko.com` | Prices, markets, 7d sparklines, history, FX rates | Public coin ids (e.g. `bitcoin`) you hold or view |
| `coin-images.coingecko.com` | Coin logos (cached locally after first download) | A request for a public coin logo image |
| `api.alternative.me` | Crypto Fear & Greed index | Nothing identifying |
| `mempool.space` | Watch-only **BTC** balance | The public BTC address you chose to watch |
| `eth.blockscout.com` | Watch-only **ETH** balance | The public ETH address you chose to watch |

The coin ids and public addresses sent are, by definition, public information. Your amounts, cost
basis, transactions and totals are **never** transmitted.

If you never use watch-only addresses, the explorer hosts are never contacted. If you set the
auto-refresh interval to **Off** and don't click Refresh, no requests are made at all.

## Encryption

Encryption is opt-in (**Settings → Security → Encrypt data**):

- The portfolio file is encrypted with **AES-256-GCM**.
- The key is derived from your password with **PBKDF2 (200,000 iterations)**.
- A random salt and nonce are stored alongside the ciphertext; GCM provides authentication, so a
  tampered file fails to decrypt rather than loading corrupt data (**fail-closed**).
- On startup Folio asks for the password before loading anything. A wrong password simply can't
  open the file — there is no recovery backdoor, so **don't lose your password**.
- You can change the password or remove encryption at any time from Settings.

`settings.json` is intentionally left unencrypted so your theme and the "is-encrypted" flag are
readable before unlocking.

## The "no keys" guarantee

Folio is a **tracker**, not a wallet. It has no signing code and no concept of a private key. The
watch-only feature reads a **public address's** balance from a block explorer — exactly what any
block explorer website shows you — and nothing more. If any screen ever appears to ask for a seed
phrase or private key, it is not Folio.

## How to verify

- The code is open source — `Services/Market/CoinGeckoClient.cs` and `ChainBalanceClient.cs` are
  the only files that make network calls.
- Watch your own traffic (e.g. Fiddler / Wireshark): you'll see only the hosts above.
- Pull the network cable: Folio keeps working from its cache and shows an "offline" badge.

## Disclaimer

Market data comes from third-party APIs and may be delayed or wrong. Folio is for tracking and
education only and is **not financial advice**.
