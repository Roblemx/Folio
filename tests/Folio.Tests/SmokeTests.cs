using FluentAssertions;
using Xunit;

namespace Folio.Tests;

/// <summary>Phase 0 placeholder — confirms the test pipeline runs. Engine tests arrive in Phase 1.</summary>
public class SmokeTests
{
    [Fact]
    public void TestPipeline_Works()
    {
        var two = 1 + 1;
        two.Should().Be(2);
    }
}
