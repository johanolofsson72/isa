using Fortnox;
using FortnoxPlugin;
using Xunit;

namespace FortnoxPlugin.Tests;

/// <summary>
/// Comprehensive tests for FortnoxPlugin functionality
/// Tests verify Fortnox accounting integration
/// </summary>
public class FortnoxPluginUnitTests : BasePluginTest, IDisposable
{
    [Fact(Skip = "API credentials not configured")]
    public void FortnoxPlugin_Should_InstantiateWithoutErrors()
    {
        // Arrange & Act & Assert
        ValidatePluginInstantiation<Plugin>((config, logger) => new Plugin(config, logger));
    }

    //TODO: Fix this test to call FetchAccessTokenAsync with auth code.
    /*[Fact]
    public async Task FetchAccessTokenAsync_Should_ReturnAccessToken()
    {
        // Arrange
        var logger = CreateLogger<Plugin>();
        var plugin = new Plugin(Configuration, logger);

        // Act
        var result = await plugin.FetchAccessTokenFromFortnoxAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(!result.StartsWith("ERROR") && result.Length > 10, "Result should be an access token and not an error message.");
    }*/

    [Fact(Skip = "Requires valid access token file")]
    public async Task GetValidAccessTokenAsync_Should_ReturnValidToken()
    {
        // Arrange
        var logger = CreateLogger<Plugin>();
        var plugin = new Plugin(Configuration, logger);

        // Act
        var result = await plugin.GetValidAccessTokenFromFileAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(!result.StartsWith("ERROR") && result.Length > 10, "Result should be an access token and not an error message.");
    }

    [Fact(Skip = "Requires valid access token file")]
    public async Task GetValidAccessTokenRefreshAsync_Should_ReturnValidToken()
    {
        // Arrange
        var logger = CreateLogger<Plugin>();
        var plugin = new Plugin(Configuration, logger);

        // Act
        var result = await plugin.RefreshAccessTokenAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(!result.StartsWith("ERROR") && result.Length > 10, "Result should be an access token and not an error message.");
    }

    [Fact(Skip = "Requires valid access token file")]
    public async Task CreateCustomerAsync_Should_CreateCustomer()
    {
        // Arrange
        var logger = CreateLogger<Plugin>();
        var plugin = new Plugin(Configuration, logger);

        // Use unique values to avoid duplicate customer errors
        var customer = new Customer
        {
            Active = true,
            Address1 = "Testgatan 1",
            City = "Stockholm",
            CountryCode = "SE",
            Email = $"test{Guid.NewGuid():N}@example.com",
            Name = $"Test Customer {Guid.NewGuid()}",
            Type = "PRIVATE",
            ZipCode = "11122"
        };

        var request = new CustomerRequest
        {
            Customer = customer
        };

        // Act
        var result = await plugin.CreateCustomerAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("ERROR", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Customer", result, StringComparison.OrdinalIgnoreCase); // Fortnox API returns the created customer object
    }

    public new void Dispose()
    {
        Cleanup();
    }
}
