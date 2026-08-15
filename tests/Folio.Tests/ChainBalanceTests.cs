using FluentAssertions;
using Folio.Services.Market;
using Xunit;

namespace Folio.Tests;

public class ChainBalanceTests
{
    [Fact]
    public void Btc_SumsConfirmedAndUnconfirmed()
    {
        const string json = """
        {"address":"bc1q","chain_stats":{"funded_txo_sum":150000000,"spent_txo_sum":50000000},
         "mempool_stats":{"funded_txo_sum":20000000,"spent_txo_sum":0}}
        """;

        // (150M - 50M) + (20M - 0) = 120M sats = 1.2 BTC
        ChainBalanceClient.ParseBtcBalance(json).Should().Be(1.2m);
    }

    [Fact]
    public void Btc_NoMempool_UsesConfirmedOnly()
    {
        const string json = """{"chain_stats":{"funded_txo_sum":100000000,"spent_txo_sum":0}}""";

        ChainBalanceClient.ParseBtcBalance(json).Should().Be(1m);
    }

    [Fact]
    public void Eth_ConvertsWeiToEther()
    {
        const string json = """{"status":"1","message":"OK","result":"2500000000000000000"}""";

        ChainBalanceClient.ParseEthBalance(json).Should().Be(2.5m);
    }

    [Fact]
    public void Eth_Garbage_ReturnsNull()
    {
        ChainBalanceClient.ParseEthBalance("""{"result":"not-a-number"}""").Should().BeNull();
    }
}
