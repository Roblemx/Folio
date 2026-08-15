using FluentAssertions;
using Folio.Services.Market;
using Xunit;

namespace Folio.Tests;

public class FearGreedTests
{
    [Fact]
    public void Parse_ReadsValueLabelAndTimestamp()
    {
        const string json = """
        {"name":"Fear and Greed Index",
         "data":[{"value":"39","value_classification":"Fear","timestamp":"1700000000","time_until_update":"3600"}]}
        """;

        var fg = MarketsService.ParseFearGreed(json);

        fg.Should().NotBeNull();
        fg!.Value.Should().Be(39);
        fg.Label.Should().Be("Fear");
        fg.At.ToUnixTimeSeconds().Should().Be(1700000000);
    }

    [Fact]
    public void Parse_EmptyData_ReturnsNull()
    {
        MarketsService.ParseFearGreed("""{"data":[]}""").Should().BeNull();
    }

    [Fact]
    public void Parse_Garbage_ReturnsNull()
    {
        MarketsService.ParseFearGreed("""{"data":[{"value":"oops"}]}""").Should().BeNull();
    }
}
