using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Folio.Models;
using Folio.Services;
using Folio.Services.Market;
using Folio.Services.Persistence;
using Xunit;

namespace Folio.Tests;

public class AlertServiceTests
{
    [Fact]
    public void Above_FiresOnceOnCrossing_ThenReArms()
    {
        var prices = new FakePrices();
        prices.Set("btc", 90m);
        var svc = new AlertService(new MemAlertStore(), prices);
        var fired = new List<AlertTriggeredEventArgs>();
        svc.Triggered += (_, e) => fired.Add(e);

        svc.Add("btc", AlertDirection.Above, 100m, null);

        prices.Raise();                          // seed (90 < 100), no fire
        fired.Should().BeEmpty();

        prices.Set("btc", 110m); prices.Raise(); // crosses above → fire
        fired.Should().HaveCount(1);

        prices.Raise();                          // still above → no repeat
        fired.Should().HaveCount(1);

        prices.Set("btc", 95m); prices.Raise();  // back below → re-arm
        prices.Set("btc", 105m); prices.Raise(); // crosses again → fire
        fired.Should().HaveCount(2);
    }

    [Fact]
    public void Below_FiresWhenPriceDropsThroughTarget()
    {
        var prices = new FakePrices();
        prices.Set("eth", 120m);
        var svc = new AlertService(new MemAlertStore(), prices);
        var fired = 0;
        svc.Triggered += (_, _) => fired++;

        svc.Add("eth", AlertDirection.Below, 100m, null);

        prices.Raise();                          // seed (120 > 100), no fire
        prices.Set("eth", 90m); prices.Raise();  // drops below → fire
        fired.Should().Be(1);

        svc.Alerts.Single().LastTriggeredAt.Should().NotBeNull();
    }

    [Fact]
    public void DisabledAlert_DoesNotFire()
    {
        var prices = new FakePrices();
        prices.Set("btc", 90m);
        var svc = new AlertService(new MemAlertStore(), prices);
        var fired = 0;
        svc.Triggered += (_, _) => fired++;

        var alert = svc.Add("btc", AlertDirection.Above, 100m, null);
        svc.SetEnabled(alert.Id, false);

        prices.Raise();
        prices.Set("btc", 130m); prices.Raise(); // crosses, but disabled
        fired.Should().Be(0);
    }

    [Fact]
    public void Trigger_PersistsLastTriggeredAt()
    {
        var prices = new FakePrices();
        prices.Set("btc", 90m);
        var store = new MemAlertStore();
        var svc = new AlertService(store, prices);

        svc.Add("btc", AlertDirection.Above, 100m, null);
        prices.Raise();
        prices.Set("btc", 110m); prices.Raise();

        store.Saved.Single().LastTriggeredAt.Should().NotBeNull();
    }

    private sealed class FakePrices : IPriceService
    {
        public Dictionary<string, PriceSnapshot> Map { get; } = new();
        public IReadOnlyDictionary<string, PriceSnapshot> Snapshots => Map;
        public bool IsStale { get; private set; }
        public DateTimeOffset? LastUpdated { get; private set; }
        public event EventHandler? Updated;

        public Task RefreshAsync(IReadOnlyCollection<string> ids, string vsCurrency, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void Set(string id, decimal price) =>
            Map[id] = new PriceSnapshot(id, id, id, null, price, 0m, 0m, Array.Empty<decimal>(), DateTimeOffset.UtcNow);

        public void Raise() => Updated?.Invoke(this, EventArgs.Empty);
    }

    private sealed class MemAlertStore : IAlertStore
    {
        public List<Alert> Saved { get; private set; } = new();
        public List<Alert> Load() => new(Saved);
        public void Save(IEnumerable<Alert> alerts) => Saved = alerts.ToList();
    }
}
