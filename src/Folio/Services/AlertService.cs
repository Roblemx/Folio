using System;
using System.Collections.Generic;
using System.Linq;
using Folio.Models;
using Folio.Services.Market;
using Folio.Services.Persistence;

namespace Folio.Services;

public sealed class AlertTriggeredEventArgs : EventArgs
{
    public required Alert Alert { get; init; }
    public required PriceSnapshot Snapshot { get; init; }
    public required decimal Price { get; init; }
}

public interface IAlertService
{
    IReadOnlyList<Alert> Alerts { get; }

    Alert Add(string coinId, AlertDirection direction, decimal targetPriceUsd, string? note);
    void Update(Alert alert);
    void Remove(string id);
    void SetEnabled(string id, bool enabled);

    /// <summary>Raised when the alert set changes (add/update/remove/enable/trigger).</summary>
    event EventHandler? Changed;

    /// <summary>Raised when an alert's price condition is crossed.</summary>
    event EventHandler<AlertTriggeredEventArgs>? Triggered;
}

/// <summary>
/// Evaluates price alerts against the latest snapshots on every price refresh. Alerts are
/// edge-triggered (fire on crossing, re-arm on the reverse) and persisted independently.
/// </summary>
public sealed class AlertService : IAlertService
{
    private readonly IAlertStore _store;
    private readonly IPriceService _prices;
    private readonly List<Alert> _alerts;

    // Per-alert "condition currently met" state for edge detection (seeded silently on first sight).
    private readonly Dictionary<string, bool> _met = new();

    public AlertService(IAlertStore store, IPriceService prices)
    {
        _store = store;
        _prices = prices;
        _alerts = store.Load();

        _prices.Updated += (_, _) => Evaluate();
    }

    public IReadOnlyList<Alert> Alerts => _alerts;

    public event EventHandler? Changed;
    public event EventHandler<AlertTriggeredEventArgs>? Triggered;

    public Alert Add(string coinId, AlertDirection direction, decimal targetPriceUsd, string? note)
    {
        var alert = new Alert(Guid.NewGuid().ToString("N"), coinId, direction, targetPriceUsd,
            true, note, DateTimeOffset.Now, null);
        _alerts.Add(alert);
        Persist();
        return alert;
    }

    public void Update(Alert alert)
    {
        var index = _alerts.FindIndex(a => a.Id == alert.Id);
        if (index >= 0)
        {
            _alerts[index] = alert;
            _met.Remove(alert.Id); // re-seed against the (possibly new) target
            Persist();
        }
    }

    public void Remove(string id)
    {
        _alerts.RemoveAll(a => a.Id == id);
        _met.Remove(id);
        Persist();
    }

    public void SetEnabled(string id, bool enabled)
    {
        var index = _alerts.FindIndex(a => a.Id == id);
        if (index >= 0)
        {
            _alerts[index] = _alerts[index] with { Enabled = enabled };
            Persist();
        }
    }

    /// <summary>Evaluates all alerts; fires <see cref="Triggered"/> on fresh crossings. Public for tests.</summary>
    public void Evaluate()
    {
        var fired = false;
        for (var i = 0; i < _alerts.Count; i++)
        {
            var alert = _alerts[i];
            if (!_prices.Snapshots.TryGetValue(alert.CoinId, out var snap))
            {
                continue;
            }

            var met = alert.Direction == AlertDirection.Above
                ? snap.Price >= alert.TargetPrice
                : snap.Price <= alert.TargetPrice;

            var hadState = _met.TryGetValue(alert.Id, out var prev);
            _met[alert.Id] = met;

            if (!hadState)
            {
                continue; // seed silently — never fire on the first observation
            }

            if (alert.Enabled && met && !prev)
            {
                var updated = alert with { LastTriggeredAt = DateTimeOffset.Now };
                _alerts[i] = updated;
                fired = true;
                Triggered?.Invoke(this, new AlertTriggeredEventArgs
                {
                    Alert = updated,
                    Snapshot = snap,
                    Price = snap.Price
                });
            }
        }

        if (fired)
        {
            _store.Save(_alerts);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Persist()
    {
        _store.Save(_alerts);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
