using Xunit;
using FluentAssertions;

namespace Shared.Tests;

public class SharedTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Placeholder_ShouldPass_WhenProjectBuilds()
    {
        true.Should().BeTrue();
    }
}

