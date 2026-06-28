using Xunit;
using FluentAssertions;

namespace AppHost.Tests;

public class AppHostTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Placeholder_ShouldPass_WhenProjectBuilds()
    {
        true.Should().BeTrue();
    }
}

