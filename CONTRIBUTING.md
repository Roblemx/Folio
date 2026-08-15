# Contributing to Folio

Thanks for your interest! Folio is a small, focused codebase and contributions are welcome.

## Getting set up

**Requirements:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows 10/11.

```bash
git clone https://github.com/YOUR-USERNAME/folio.git
cd folio
dotnet build          # build everything
dotnet test           # run the test suite
dotnet run --project src/Folio
```

## Project layout

```
src/Folio/
  Engine/        Pure calculation (no UI, no IO) — add tests for anything here
  Models/        Domain records + storage DTOs
  Services/      Market data, persistence, session, theme, alerts, watch
  ViewModels/    One per screen (CommunityToolkit.Mvvm)
  Views/         XAML + custom-drawn charts + themes
tests/Folio.Tests/   xUnit + FluentAssertions
docs/                Architecture, design, privacy, screenshots
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before making larger changes.

## Ground rules

- **Privacy is non-negotiable.** No telemetry, analytics, accounts, or cloud sync. Any new network
  call must be public, read-only market data and must be documented in
  [docs/PRIVACY.md](docs/PRIVACY.md) and the Settings panel.
- **No keys.** Folio never handles private keys or seed phrases.
- **Engine stays pure.** Put calculations in `Engine/` with no IO or `DateTime.Now`, and cover them
  with tests. View models orchestrate; they don't compute.

## Code style

- C# 12, nullable enabled, `dotnet format` clean (an `.editorconfig` is included).
- Follow the existing patterns: `[ObservableProperty]` / `[RelayCommand]` in view models,
  `DynamicResource` for all colours, reusable styles in `Themes/Controls.xaml`.
- Keep public APIs documented with brief XML summaries where it helps.

## Adding a screen (quick recipe)

1. Add a `XxxViewModel` in `ViewModels/` (inherit `ViewModelBase`).
2. Add a `XxxView.xaml` in `Views/` and a `DataTemplate` in `App.xaml`.
3. Register the view model in `App.xaml.cs` and add a nav item + route in `ShellViewModel`.

## Pull requests

- Keep PRs focused and describe the user-visible change.
- `dotnet test` must pass; add tests for new engine logic.
- Include a screenshot for UI changes.

## Reporting issues

Open a GitHub issue with steps to reproduce, what you expected, and your OS / .NET version. Please
don't include real wallet addresses or amounts you'd rather keep private.
