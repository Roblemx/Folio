# Design

Folio aims to feel like a calm, modern, native Windows app — closer to a well-made finance
dashboard than a toy. This note captures the visual system and the UX principles behind it.

## Principles

1. **Local & private, visibly.** Privacy is a feature, so the UI says so — a "Local & private"
   footer, a read-only badge on watch-only balances, and a Settings panel that lists every network
   host. Trust is earned by being legible.
2. **Calm by default.** A dark, low-contrast surface palette; one accent colour; green/red reserved
   for gains/losses. Numbers are the loudest thing on screen.
3. **Always answer "so what?".** Every value is paired with context — a percentage, an allocation,
   a drift, a 24h delta — so a glance is enough.
4. **No dead ends.** Every screen has a real empty state that tells you what to do next.
5. **Resilient, not fragile.** Offline shows last-known data with a clear "offline" badge rather
   than an error.

## Color

Two dictionaries (`Themes/Colors.Dark.xaml`, `Themes/Colors.Light.xaml`) define the **same keys**;
`ThemeService` swaps the active one at runtime. Because every brush is referenced via
`DynamicResource`, the whole UI re-themes live — no restart.

Token roles (not raw values):

| Token | Role |
|---|---|
| `Bg`, `Surface`, `SurfaceAlt` | Page background and layered card surfaces |
| `Border` | Hairline separators and card borders |
| `TextPrimary`, `TextSecondary`, `TextFaint` | Text hierarchy |
| `Accent`, `AccentSoft` | Primary actions, active nav, focus |
| `Positive`, `Negative`, `Warning` | Gains, losses, caution |
| `Cat1`–`Cat8` | Per-coin accent palette (stable hash → colour) |

## Typography

A single UI family (Segoe UI) with a small, deliberate scale:

- **Hero** (34) — page titles and headline totals
- **Heading** (18) — card titles
- **Body** (13–14) — values and labels
- **Caption** (12.5, secondary) — supporting text

Money and amounts use invariant formatting (`Helpers/Format`) so they're unambiguous across locales,
with currency conversion applied at display time (all values are stored in USD).

## Components

Reusable styles live in `Themes/Controls.xaml`:

- **Buttons** — `PrimaryButton`, `GhostButton`, `DangerButton`
- **Inputs** — `TextInput`, `PasswordInput`
- **Toggles** — `Switch`, `RangeTab` (timeframes), `ChipToggle` (segmented choices)
- **Containers** — `Card`, slim custom `ScrollBar`
- **Window** — Win11-style caption buttons via `WindowChrome`
- **Menus** — `MenuItemButton` for the portfolio switcher popup

## Charts

All charts are custom `FrameworkElement`s (in `Controls/`) drawn directly in `OnRender` — no
third-party charting library, which keeps the dependency surface tiny and the look consistent:

- **`Sparkline`** — a minimal 7-day line, coloured by 24h direction.
- **`DonutChart`** — allocation arcs.
- **`AreaChart`** — gradient-filled value-over-time with a hover crosshair and tooltip.

## Layout

A fixed sidebar (logo · portfolio switcher · navigation · privacy footer) and a content host. Each
screen is a `Grid` with a header row, optional toolbar, and a scrolling body, on a consistent
`36px` content margin. Editors (add asset, transaction, alert, address, encryption) are centered
card overlays on a dimmed scrim.

## Motion

Motion is minimal and functional: hover state changes, popup fades, and toast notifications that
auto-dismiss. Nothing animates that would slow down reading a number.
