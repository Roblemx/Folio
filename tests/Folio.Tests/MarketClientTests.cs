using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Folio.Services.Market;
using Xunit;

namespace Folio.Tests;

public class MarketClientTests
{
    private static CoinGeckoClient Client(string json) =>
        new(FakeHttpMessageHandler.ClientReturning(json), _ => TimeSpan.Zero);

    [Fact]
    public async Task GetMarkets_ParsesAllFields()
    {
        const string json = """
        [{"id":"bitcoin","symbol":"btc","name":"Bitcoin","image":"http://img/btc.png",
          "current_price":50000,"price_change_percentage_24h":2.5,"market_cap":1000000000000,
          "sparkline_in_7d":{"price":[49000,49500,50000]}}]
        """;

        var markets = await Client(json).GetMarketsAsync(new[] { "bitcoin" }, "usd");

        markets.Should().HaveCount(1);
        var e = markets[0];
        e.Id.Should().Be("bitcoin");
        e.Symbol.Should().Be("btc");
        e.Name.Should().Be("Bitcoin");
        e.ImageUrl.Should().Be("http://img/btc.png");
        e.Price.Should().Be(50000m);
        e.Change24h.Should().Be(2.5m);
        e.MarketCap.Should().Be(1000000000000m);
        e.Sparkline7d.Should().Equal(49000m, 49500m, 50000m);
    }

    [Fact]
    public async Task GetTopMarkets_ParsesOrderedEntries()
    {
        const string json = """
        [{"id":"bitcoin","symbol":"btc","name":"Bitcoin","current_price":50000,
          "price_change_percentage_24h":2.5,"market_cap":1000000000000,"sparkline_in_7d":{"price":[1,2,3]}},
         {"id":"ethereum","symbol":"eth","name":"Ethereum","current_price":3000,
          "price_change_percentage_24h":-1.2,"market_cap":400000000000,"sparkline_in_7d":{"price":[3,2,1]}}]
        """;

        var top = await Client(json).GetTopMarketsAsync("usd", 50);

        top.Should().HaveCount(2);
        top[0].Id.Should().Be("bitcoin");
        top[1].Change24h.Should().Be(-1.2m);
    }

    [Fact]
    public async Task GetCoinList_Parses()
    {
        const string json = """[{"id":"bitcoin","symbol":"btc","name":"Bitcoin"},{"id":"ethereum","symbol":"eth","name":"Ethereum"}]""";

        var coins = await Client(json).GetCoinListAsync();

        coins.Should().HaveCount(2);
        coins[0].Id.Should().Be("bitcoin");
        coins[1].Symbol.Should().Be("eth");
    }

    [Fact]
    public async Task GetMarketChart_ParsesTimestampPricePairs()
    {
        const string json = """{"prices":[[1700000000000,100.5],[1700086400000,101.0]]}""";

        var points = await Client(json).GetMarketChartAsync("bitcoin", "usd", "7");

        points.Should().HaveCount(2);
        points[0].Price.Should().Be(100.5m);
        points[0].Date.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
    }

    [Fact]
    public async Task GetUsdRates_DerivesFromBtcPrices()
    {
        const string json = """{"bitcoin":{"usd":50000,"eur":46000,"try":1600000}}""";

        var rates = await Client(json).GetUsdRatesAsync(new[] { "EUR", "TRY" });

        rates["USD"].Should().Be(1m);
        rates["EUR"].Should().Be(0.92m);    // 46000 / 50000
        rates["TRY"].Should().Be(32m);      // 1600000 / 50000
    }

    [Fact]
    public async Task RetriesOn429_ThenSucceeds()
    {
        var handler = new FakeHttpMessageHandler((_, i) =>
            i == 0 ? FakeHttpMessageHandler.Status(HttpStatusCode.TooManyRequests)
                   : FakeHttpMessageHandler.Ok("[]"));
        var client = new CoinGeckoClient(new HttpClient(handler), _ => TimeSpan.Zero);

        var markets = await client.GetMarketsAsync(new[] { "bitcoin" }, "usd");

        markets.Should().BeEmpty();
        handler.CallCount.Should().Be(2); // one retry
    }
}
