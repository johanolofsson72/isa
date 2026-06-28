using Xunit;
using FluentAssertions;

namespace ServiceDefaults.Tests;

public class ServiceDefaultsTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Placeholder_ShouldPass_WhenProjectBuilds()
    {
        true.Should().BeTrue();
    }
}

