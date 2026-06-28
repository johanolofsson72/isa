using Xunit;
using FluentAssertions;

namespace GetDropboxToken.Tests;

public class GetDropboxTokenTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Placeholder_ShouldPass_WhenProjectBuilds()
    {
        true.Should().BeTrue();
    }
}

