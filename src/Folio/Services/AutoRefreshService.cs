using System;
using System.Windows.Threading;
using Folio.Services.Persistence;

namespace Folio.Services;

/// <summary>
/// Periodically refreshes live prices on the user's chosen interval so the app stays current
/// without manual refreshes. Interval 0 disables it; a 15s floor protects the public API.
/// </summary>
public sealed class AutoRefreshService
{
    private readonly PortfolioSession _session;
    private readonly DispatcherTimer _timer = new();

    public AutoRefreshService(PortfolioSession session, ISettingsStore settings)
    {
        _session = session;
        _timer.Tick += (_, _) => _ = _session.RefreshAsync();
        SetInterval(settings.Load().RefreshSeconds);
    }

    public void SetInterval(int seconds)
    {
        _timer.Stop();
        if (seconds > 0)
        {
            _timer.Interval = TimeSpan.FromSeconds(Math.Max(15, seconds));
            _timer.Start();
        }
    }
}
