using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Folio.Models;
using Folio.Services.Market;
using Xunit;

namespace Folio.Tests;

public class MarketServiceTests
{
    private const string MarketsJson = """
    [{"id":"bitcoin","symbol":"btc","name":"Bitcoin","image":"i","current_price":50000,
      "price_change_percentage_24h":2.5,"market_cap":1000000000000,"sparkline_in_7d":{"price":[1,2,3]}}]
    """;

    // ---- CacheStore ----

    [Fact]
    public void Cache_WriteRead_RoundTrips()
    {
        var cache = new CacheStore(TestHelpers.TempDir());
        cache.Write("k", new List<int> { 1, 2, 3 });

        var entry = cache.Read<List<int>>("k");

        entry.Should().NotBeNull();
        entry!.Value.Should().Equal(1, 2, 3);
        entry.FetchedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Cache_Missing_ReturnsNull()
    {
        new CacheStore(TestHelpers.TempDir()).Read<string>("nope").Should().BeNull();
    }

    // ---- Catalog search ----

    [Fact]
    public async Task Catalog_Search_RanksExactSymbolFirst()
    {
        var coins = new List<Coin>
        {
            new("bitcoin", "btc", "Bitcoin"),
            new("bitcoin-cash", "bch", "Bitcoin Cash"),
            new("ethereum", "eth", "Ethereum"),
            new("uniswap", "uni", "Uniswap"),
            new("unicorn-token", "uni", "Unicorn Token")
        };
        var svc = new CoinCatalogService(new StubClient(coins), new CacheStore(TestHelpers.TempDir()));
        await svc.EnsureLoadedAsync();

        svc.Search("btc").First().Id.Should().Be("bitcoin");          // exact symbol
        svc.Search("bitcoin").First().Id.Should().Be("bitcoin");      // exact name beats "Bitcoin Cash"
        svc.Search("uni").First().Symbol.Should().Be("uni");          // exact symbol; shorter name wins tie
        svc.Search("uni").First().Id.Should().Be("uniswap");
        svc.Get("ethereum").Should().NotBeNull();
        svc.Search("zzz").Should().BeEmpty();
    }

    // ---- PriceService ----

    [Fact]
    public async Task Price_Refresh_Success_UpdatesAndClearsStale()
    {
        var client = new CoinGeckoClient(FakeHttpMessageHandler.ClientReturning(MarketsJson), _ => TimeSpan.Zero);
        var svc = new PriceService(client, new CacheStore(TestHelpers.TempDir()));

        await svc.RefreshAsync(new[] { "bitcoin" }, "usd");

        svc.IsStale.Should().BeFalse();
        svc.Snapshots.Should().ContainKey("bitcoin");
        svc.Snapshots["bitcoin"].Price.Should().Be(50000m);
    }

    [Fact]
    public async Task Price_Refresh_Offline_KeepsLastKnown_AndMarksStale()
    {
        var dir = TestHelpers.TempDir();

        // 1) a successful refresh writes the disk cache
        var ok = new PriceService(
            new CoinGeckoClient(FakeHttpMessageHandler.ClientReturning(MarketsJson), _ => TimeSpan.Zero),
            new CacheStore(dir));
        await ok.RefreshAsync(new[] { "bitcoin" }, "usd");

        // 2) a fresh service loads that cache, then a failing refresh keeps it
        var offline = new PriceService(
            new CoinGeckoClient(FakeHttpMessageHandler.ClientThrowing(), _ => TimeSpan.Zero),
            new CacheStore(dir));
        offline.Snapshots.Should().ContainKey("bitcoin"); // loaded from cache

        await offline.RefreshAsync(new[] { "bitcoin" }, "usd");

        offline.IsStale.Should().BeTrue();
        offline.Snapshots["bitcoin"].Price.Should().Be(50000m); // last-known retained
    }

    // ---- HistoryService helpers ----

    [Fact]
    public void History_RangeToDays_Maps()
    {
        HistoryService.RangeToDays("24H").Should().Be("1");
        HistoryService.RangeToDays("1Y").Should().Be("365");
        HistoryService.RangeToDays("ALL").Should().Be("max");
    }

    [Fact]
    public void History_Downsample_KeepsEndpointsAndCount()
    {
        var baseDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var points = Enumerable.Range(0, 1000)
            .Select(i => new HistoryPoint(baseDate.AddMinutes(i), i))
            .ToList();

        var sampled = HistoryService.Downsample(points, 100);

        sampled.Should().HaveCount(100);
        sampled.First().Should().Be(points.First());
        sampled.Last().Should().Be(points.Last());
    }

    // ---- FxService ----

    [Fact]
    public void Fx_DefaultsToUsd_AndConverts()
    {
        var fx = new FxService(new StubClient(Array.Empty<Coin>()), new CacheStore(TestHelpers.TempDir()));

        fx.Currency.Should().Be("USD");
        fx.Rate.Should().Be(1m);
        fx.Currency = "EUR";
        fx.Rate.Should().BeGreaterThan(0m);
        fx.Currencies.Should().Contain("TRY");
    }

    private sealed class StubClient : IMarketDataClient
    {
        private readonly IReadOnlyList<Coin> _coins;
        public StubClient(IReadOnlyList<Coin> coins) => _coins = coins;

        public Task<IReadOnlyList<Coin>> GetCoinListAsync(CancellationToken ct = default) =>
            Task.FromResult(_coins);

        public Task<IReadOnlyList<MarketEntry>> GetMarketsAsync(IReadOnlyCollection<string> ids, string vsCurrency, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<MarketEntry>)Array.Empty<MarketEntry>());

        public Task<IReadOnlyList<MarketEntry>> GetTopMarketsAsync(string vsCurrency, int count, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<MarketEntry>)Array.Empty<MarketEntry>());

        public Task<IReadOnlyList<HistoryPoint>> GetMarketChartAsync(string id, string vsCurrency, string days, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<HistoryPoint>)Array.Empty<HistoryPoint>());

        public Task<IReadOnlyDictionary<string, decimal>> GetUsdRatesAsync(IReadOnlyCollection<string> fiatCodes, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyDictionary<string, decimal>)new Dictionary<string, decimal>());
    }
}
