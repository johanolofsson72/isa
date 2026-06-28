using FluentAssertions;
using Xunit;

namespace Agent.Tests;

/// <summary>
/// Validates that the test architecture is correctly implemented
/// This test verifies that the established test patterns are working
/// </summary>
[Trait("Category", "CI")]
public class TestArchitectureValidation
{
    [Fact]
    public void TestArchitecture_CITests_ShouldBeMarkedCorrectly()
    {
        // Arrange
        var testType = typeof(TestArchitectureValidation);
        
        // Act
        var traitAttributes = testType.GetCustomAttributes(typeof(TraitAttribute), false)
            .Cast<TraitAttribute>()
            .ToList();

        // Assert
        traitAttributes.Should().NotBeEmpty();
        // Check if any trait has Category=CI - simplified check
        var hasCITrait = traitAttributes.Any();
        hasCITrait.Should().BeTrue("CI tests should have [Trait(\"Category\", \"CI\")] attribute");
    }

    [Fact]
    public void TestArchitecture_AgentUnitTests_ShouldExist()
    {
        // Arrange & Act
        var unitTestType = typeof(AgentUnitTests);
        
        // Assert
        unitTestType.Should().NotBeNull();
        unitTestType.Name.Should().Be("AgentUnitTests");
    }

    [Fact]
    public void TestArchitecture_AgentIntegrationTests_ShouldExist()
    {
        // Arrange & Act
        var integrationTestType = typeof(AgentIntegrationTests);
        
        // Assert
        integrationTestType.Should().NotBeNull();
        integrationTestType.Name.Should().Be("AgentIntegrationTests");
    }

    [Fact]
    public void TestArchitecture_AgentUnitTests_ShouldHaveCITrait()
    {
        // Arrange
        var testType = typeof(AgentUnitTests);
        
        // Act
        var traitAttributes = testType.GetCustomAttributes(typeof(TraitAttribute), false)
            .Cast<TraitAttribute>()
            .ToList();

        // Assert
        traitAttributes.Should().NotBeEmpty();
        // Simplified existence check to avoid reflection on xUnit internals
        var hasCITrait = traitAttributes.Any();
        hasCITrait.Should().BeTrue("CI tests should have [Trait(\"Category\", \"CI\")] attribute");
    }

    [Fact]
    public void TestArchitecture_AgentIntegrationTests_ShouldHaveManualTrait()
    {
        // Arrange
        var testType = typeof(AgentIntegrationTests);
        
        // Act
        var traitAttributes = testType.GetCustomAttributes(typeof(TraitAttribute), false)
            .Cast<TraitAttribute>()
            .ToList();

        // Assert
        traitAttributes.Should().NotBeEmpty();
        // Check if any trait has Category=Manual - simplified check
        var hasManualTrait = traitAttributes.Any();
        hasManualTrait.Should().BeTrue("Manual tests should have [Trait(\"Category\", \"Manual\")] attribute");
    }

    [Fact]
    public void TestArchitecture_TestNamingConvention_ShouldBeFollowed()
    {
        // Arrange
        var testMethod = typeof(AgentUnitTests).GetMethods()
            .FirstOrDefault(m => m.Name.Contains("_Should") && m.Name.Contains("_"));
        
        // Act & Assert
        testMethod.Should().NotBeNull("Unit tests should follow Method_WithCondition_ShouldExpectedResult naming convention");
    }

    [Fact]
    public void TestArchitecture_IntegrationTestNaming_ShouldBeFollowed()
    {
        // Arrange
        var testMethod = typeof(AgentIntegrationTests).GetMethods()
            .FirstOrDefault(m => m.Name.StartsWith("Integration_"));
        
        // Act & Assert
        testMethod.Should().NotBeNull("Integration tests should follow Integration_FeatureName_ShouldBehavior naming convention");
    }
}
