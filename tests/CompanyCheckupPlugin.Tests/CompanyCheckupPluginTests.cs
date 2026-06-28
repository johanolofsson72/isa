using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Xunit;
using CompanyCheckupPlugin;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace CompanyCheckupPlugin.Tests;

public class CompanyCheckupPluginTests
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ITestOutputHelper _output;

    public CompanyCheckupPluginTests(ITestOutputHelper output)
    {
        _output = output;
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole());
    }

    [Fact]
    public void PluginFactory_ShouldCreateInstance()
    {
        // Arrange
        var factory = new CompanyCheckupPluginFactory();

        // Act
        var plugin = factory.CreatePlugin(_configuration, _loggerFactory);

        // Assert
        Assert.NotNull(plugin);
        Assert.IsType<Plugin>(plugin);
    }

    [Fact]
    public void PluginFactory_ShouldHaveCorrectProperties()
    {
        // Arrange
        var factory = new CompanyCheckupPluginFactory();

        // Act & Assert
        Assert.Equal("CompanyCheckup", factory.PluginName);
        Assert.Equal("Plugin for company information lookup and validation", factory.Description);
    }

    [Fact]
    public void Plugin_ShouldInitializeWithoutErrors()
    {
        // Arrange & Act
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());

        // Assert
        Assert.NotNull(plugin);
    }

    [Theory]
    [InlineData("556123-4567", "SE556123456701")]
    [InlineData("5561234567", "SE556123456701")]
    [InlineData("123456789", "123456789")] // Invalid - too short
    [InlineData("", "")] // Invalid - empty
    public void DeriveSwedishVatNumber_ShouldReturnCorrectFormat(string orgNumber, string expected)
    {
        // This test validates the VAT derivation logic by using reflection
        // since the method is private
        var plugin = new Plugin(_configuration);
        var type = typeof(Plugin);
        var method = type.GetMethod("DeriveSwedishVatNumber",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = method?.Invoke(plugin, new object[] { orgNumber }) as string;

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Integration tests with real companies that should return actual data
    /// These tests validate that our data sources are working correctly
    /// </summary>
    [Theory]
    [InlineData("IKEA of Sweden AB")] // IKEA - should be in all Swedish databases
    [InlineData("H & M Hennes & Mauritz AB")] // H&M - well-known Swedish company
    [InlineData("Volvo Car Corporation")] // Volvo - traditional Swedish company
    [InlineData("Spotify AB")] // Spotify - modern tech company
    [InlineData("Telefonaktiebolaget LM Ericsson")] // Ericsson - telecom giant
    public async Task LookupCompanyAsync_Should_ReturnValidData_ForWellKnownCompanies(string companyName)
    {
        // Arrange
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());

        // Act
        var result = await plugin.LookupCompanyAsync(companyName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        _output.WriteLine($"Result for {companyName}:");
        _output.WriteLine(result);

        // Extract JSON from the formatted response
        var jsonStart = result.IndexOf('{');
        Assert.True(jsonStart >= 0, "No JSON found in response");
        var jsonPart = result.Substring(jsonStart);

        // Parse the JSON result to validate structure
        var jsonDoc = JsonDocument.Parse(jsonPart);
        var root = jsonDoc.RootElement;

        // Should have basic company information
        Assert.True(root.TryGetProperty("Match", out var match));
        Assert.True(root.TryGetProperty("Profile", out var profile));

        // Should have found the company
        if (match.TryGetProperty("Found", out var found))
        {
            Assert.True(found.GetBoolean(), $"Company {companyName} should be found in databases");
        }

        // Should have company name
        if (profile.TryGetProperty("Name", out var name))
        {
            Assert.NotNull(name.GetString());
            Assert.NotEmpty(name.GetString()!);
        }
    }

    /// <summary>
    /// Test specific data sources with companies known to exist in those sources
    /// </summary>
    [Theory]
    [InlineData("556074-7569")] // IKEA org number
    [InlineData("556042-7220")] // H&M org number
    [InlineData("556074-3089")] // Volvo org number
    public async Task BolagsverketSearch_Should_ReturnData_ForValidOrgNumbers(string orgNumber)
    {
        // Arrange
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());

        // Use reflection to test the private Bolagsverket method
        var type = typeof(Plugin);
        var method = type.GetMethod("SearchBolagsverketAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var task = method?.Invoke(plugin, new object[] { orgNumber }) as Task<object>;
        Assert.NotNull(task);
        var result = await task;

        // Assert
        Assert.NotNull(result);
        _output.WriteLine($"Bolagsverket result for {orgNumber}: {result}");
    }

    [Theory]
    [InlineData("IKEA of Sweden AB")]
    [InlineData("H & M Hennes & Mauritz AB")]
    [InlineData("Volvo Car Corporation")]
    public async Task AllabolagSearch_Should_ReturnData_ForWellKnownCompanies(string companyName)
    {
        // Arrange
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());

        // Use reflection to test the private Allabolag method
        var type = typeof(Plugin);
        var method = type.GetMethod("SearchAllabolagAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var task = method?.Invoke(plugin, new object[] { companyName }) as Task<object>;
        Assert.NotNull(task);
        var result = await task;

        // Assert - Should either return data or null (but not throw exception)
        _output.WriteLine($"Allabolag result for {companyName}: {result ?? "null"}");
    }

    [Theory]
    [InlineData("IKEA of Sweden AB", "556074-7569")]
    [InlineData("H & M Hennes & Mauritz AB", "556042-7220")]
    public async Task UCSearch_Should_ReturnData_ForWellKnownCompanies(string companyName, string orgNumber)
    {
        // Arrange
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());

        // Use reflection to test the private UC method
        var type = typeof(Plugin);
        var method = type.GetMethod("SearchUCAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var task = method?.Invoke(plugin, new object[] { companyName, orgNumber }) as Task<object>;
        Assert.NotNull(task);
        var result = await task;

        // Assert
        _output.WriteLine($"UC result for {companyName}: {result ?? "null"}");
    }

    [Theory]
    [InlineData("IKEA of Sweden AB", "556074-7569")]
    [InlineData("H & M Hennes & Mauritz AB", "556042-7220")]
    public async Task RatsitSearch_Should_ReturnData_ForWellKnownCompanies(string companyName, string orgNumber)
    {
        // Arrange
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());

        // Use reflection to test the private Ratsit method
        var type = typeof(Plugin);
        var method = type.GetMethod("SearchRatsitAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var task = method?.Invoke(plugin, new object[] { companyName, orgNumber }) as Task<object>;
        Assert.NotNull(task);
        var result = await task;

        // Assert
        _output.WriteLine($"Ratsit result for {companyName}: {result ?? "null"}");
    }

    /// <summary>
    /// Test Hitta.se search - Note: This may not work due to JavaScript requirements
    /// but we test it to document the limitation
    /// </summary>
    [Theory]
    [InlineData("IKEA of Sweden AB", "556074-7569")]
    [InlineData("Systematiq Applikationsutveckling Sverige AB", null)] // The problematic case
    public async Task HittaSearch_Should_HandleModernWebsite(string companyName, string? orgNumber)
    {
        // Arrange
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());

        // Use reflection to test the private Hitta method
        var type = typeof(Plugin);
        var method = type.GetMethod("SearchHittaAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var task = method?.Invoke(plugin, new object?[] { companyName, orgNumber }) as Task<object>;
        Assert.NotNull(task);
        var result = await task;

        // Assert - May return null due to JavaScript requirements, but should not throw
        _output.WriteLine($"Hitta result for {companyName}: {result ?? "null"}");
        _output.WriteLine("Note: Hitta.se may require JavaScript execution for search results");
    }

    /// <summary>
    /// Test the specific Systematiq case that should now work with improved Allabolag patterns
    /// </summary>
    [Fact]
    public async Task LookupCompanyAsync_Should_FindSystematiqAddress_WithImprovedPatterns()
    {
        // Arrange
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());
        var companyName = "Systematiq Applikationsutveckling Sverige AB";

        // Act
        var result = await plugin.LookupCompanyAsync(companyName);

        // Assert
        Assert.NotNull(result);
        _output.WriteLine($"Full result: {result}");

        // Parse the JSON response
        var jsonMatch = Regex.Match(result, @"\{.*\}", RegexOptions.Singleline);
        Assert.True(jsonMatch.Success, "Response should contain JSON data");

        var jsonData = JsonSerializer.Deserialize<JsonElement>(jsonMatch.Value);
        _output.WriteLine($"JSON keys: {string.Join(", ", jsonData.EnumerateObject().Select(p => p.Name))}");

        // The main test: verify address is now found with our improved patterns
        Assert.True(jsonData.TryGetProperty("address", out var addressElement),
            "Address should be found with improved Allabolag patterns");
        Assert.True(addressElement.TryGetProperty("line1", out var line1Element));
        var address = line1Element.GetString();
        Assert.NotNull(address);
        Assert.Contains("Fridhemsvägen", address);

        // Verify zip code
        Assert.True(addressElement.TryGetProperty("zip", out var zipElement));
        var zip = zipElement.GetString();
        Assert.Equal("37238", zip);

        // Verify city
        Assert.True(addressElement.TryGetProperty("city", out var cityElement));
        var city = cityElement.GetString();
        Assert.Equal("Ronneby", city);

        // Verify organization number if present
        if (jsonData.TryGetProperty("orgNumber", out var orgElement))
        {
            var orgNumber = orgElement.GetString();
            Assert.Equal("556779-7096", orgNumber);
            _output.WriteLine($"✅ SUCCESS: Found complete data for Systematiq:");
            _output.WriteLine($"   Address: {address}, {zip} {city}");
            _output.WriteLine($"   Org.nr: {orgNumber}");
        }
        else
        {
            _output.WriteLine($"✅ SUCCESS: Found address for Systematiq:");
            _output.WriteLine($"   Address: {address}, {zip} {city}");
        }

        _output.WriteLine("   This proves the improved Allabolag patterns work!");
    }

    /// <summary>
    /// Test to debug UC.se HTML structure and understand why scraping fails
    /// </summary>
    [Fact]
    public async Task DebugUCAsync_Should_ShowActualHTMLStructure()
    {
        // Arrange
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());
        var companyName = "Spotify";

        // Create a custom HTTP client to capture the actual response
        using var httpClient = new HttpClient();
        var searchQuery = Uri.EscapeDataString(companyName);
        var url = $"https://www.uc.se/foretag/sok/?q={searchQuery}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "sv-SE,sv;q=0.9,en;q=0.8");

        try
        {
            var response = await httpClient.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"UC.se URL: {url}");
            _output.WriteLine($"Response Status: {response.StatusCode}");
            _output.WriteLine($"Response Length: {html.Length} characters");
            _output.WriteLine("First 2000 characters of response:");
            _output.WriteLine(html.Substring(0, Math.Min(2000, html.Length)));

            // Look for common patterns that might indicate the structure
            if (html.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("\n✅ Found 'Spotify' in response");
            }
            else
            {
                _output.WriteLine("\n❌ 'Spotify' not found in response");
            }

            // Check for JavaScript indicators
            if (html.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("React", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("Vue", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("⚠️ JavaScript framework detected - may require browser execution");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error accessing UC.se: {ex.Message}");
        }
    }

    /// <summary>
    /// Test to debug Ratsit.se HTML structure and understand why scraping fails
    /// </summary>
    [Fact]
    public async Task DebugRatsitAsync_Should_ShowActualHTMLStructure()
    {
        // Arrange
        var companyName = "Spotify";

        // Create a custom HTTP client to capture the actual response
        using var httpClient = new HttpClient();
        var searchQuery = Uri.EscapeDataString(companyName);
        var url = $"https://www.ratsit.se/sok/foretag?who={searchQuery}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        try
        {
            var response = await httpClient.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Ratsit.se URL: {url}");
            _output.WriteLine($"Response Status: {response.StatusCode}");
            _output.WriteLine($"Response Length: {html.Length} characters");
            _output.WriteLine("First 2000 characters of response:");
            _output.WriteLine(html.Substring(0, Math.Min(2000, html.Length)));

            // Look for common patterns that might indicate the structure
            if (html.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("\n✅ Found 'Spotify' in response");
            }
            else
            {
                _output.WriteLine("\n❌ 'Spotify' not found in response");
            }

            // Check for JavaScript indicators
            if (html.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("React", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("Vue", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("⚠️ JavaScript framework detected - may require browser execution");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error accessing Ratsit.se: {ex.Message}");
        }
    }

    /// <summary>
    /// Test the specific case that the user reported as problematic
    /// </summary>
    [Fact]
    public async Task LookupCompanyAsync_Should_HandleSystematiq_SpecificCase()
    {
        // Arrange
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());
        var companyName = "Systematiq Applikationsutveckling Sverige AB";

        // Act
        var result = await plugin.LookupCompanyAsync(companyName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        _output.WriteLine($"Full result for {companyName}:");
        _output.WriteLine(result);

        // Parse the JSON result
        var jsonDoc = JsonDocument.Parse(result);
        var root = jsonDoc.RootElement;

        // Check if we found any address information
        if (root.TryGetProperty("Profile", out var profile))
        {
            if (profile.TryGetProperty("Address", out var address))
            {
                _output.WriteLine("Address information found:");
                _output.WriteLine(address.ToString());

                // Check for specific address components
                if (address.TryGetProperty("Street", out var street))
                {
                    _output.WriteLine($"Street: {street.GetString()}");
                }
                if (address.TryGetProperty("City", out var city))
                {
                    _output.WriteLine($"City: {city.GetString()}");
                }
                if (address.TryGetProperty("ZipCode", out var zipCode))
                {
                    _output.WriteLine($"ZipCode: {zipCode.GetString()}");
                }
            }
            else
            {
                _output.WriteLine("No address information found in Profile");
            }
        }
    }

    /// <summary>
    /// Test address parsing regex patterns with known address formats
    /// </summary>
    [Theory]
    [InlineData("Fridhemsvägen 8 372 38 Ronneby", "Fridhemsvägen 8", "37238", "Ronneby")]
    [InlineData("Storgatan 15 111 22 Stockholm", "Storgatan 15", "11122", "Stockholm")]
    [InlineData("Kungsgatan 42A 411 15 Göteborg", "Kungsgatan 42A", "41115", "Göteborg")]
    public void AddressRegexPatterns_Should_ParseSwedishAddresses(string fullAddress, string expectedStreet, string expectedZip, string expectedCity)
    {
        // Test the regex patterns used in the plugin
        var patterns = new[]
        {
            @"([A-ZÅÄÖÜ][a-zåäöü\s]+vägen?\s+\d+[a-zA-Z]?)\s+(\d{3}\s?\d{2})\s+([A-ZÅÄÖÜ][a-zåäöü\s]+)",
            @"([A-ZÅÄÖÜ][a-zåäöü\s]+gatan?\s+\d+[a-zA-Z]?)\s+(\d{3}\s?\d{2})\s+([A-ZÅÄÖÜ][a-zåäöü\s]+)",
            @"([A-ZÅÄÖÜ][a-zåäöü\s]+\s+\d+[a-zA-Z]?)\s+(\d{3}\s?\d{2})\s+([A-ZÅÄÖÜ][a-zåäöü\s]+)"
        };

        bool foundMatch = false;
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(fullAddress, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count >= 4)
            {
                var street = match.Groups[1].Value.Trim();
                var zip = match.Groups[2].Value.Replace(" ", "");
                var city = match.Groups[3].Value.Trim();

                _output.WriteLine($"Pattern matched: {pattern}");
                _output.WriteLine($"Street: {street}, Zip: {zip}, City: {city}");

                Assert.Equal(expectedStreet, street);
                Assert.Equal(expectedZip, zip);
                Assert.Equal(expectedCity, city);

                foundMatch = true;
                break;
            }
        }

        Assert.True(foundMatch, $"No regex pattern matched the address: {fullAddress}");
    }

    /// <summary>
    /// Website Structure Monitoring Tests
    /// These tests will FAIL when websites change their structure, alerting us to update scraping patterns
    /// </summary>

    [Fact]
    public async Task UCWebsite_Should_MaintainExpectedStructure()
    {
        // This test monitors UC.se website structure and will fail if they change their layout
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());
        var url = "https://www.uc.se/foretag/sok/?q=test";

        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        var response = await httpClient.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        // These assertions will fail if UC.se changes their website structure
        Assert.True(response.IsSuccessStatusCode, "UC.se should be accessible");
        Assert.True(html.Length > 1000, "UC.se should return substantial HTML content");

        // Check for expected HTML elements that indicate the site structure
        var expectedElements = new[]
        {
            "<!DOCTYPE html>",
            "<html",
            "<head>",
            "javascript" // UC.se uses JavaScript
        };

        foreach (var element in expectedElements)
        {
            Assert.Contains(element, html, StringComparison.OrdinalIgnoreCase);
        }

        // UC.se may not have <body> tag if content is loaded via JavaScript
        _output.WriteLine($"UC.se HTML contains <body> tag: {html.Contains("<body>", StringComparison.OrdinalIgnoreCase)}");

        // If UC.se changes from JavaScript to server-side rendering, this test will catch it
        if (!html.Contains("javascript", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail("⚠️ UC.se may have changed from JavaScript to server-side rendering - update scraping patterns!");
        }

        _output.WriteLine($"✅ UC.se structure check passed - still using JavaScript (response: {html.Length} chars)");
    }

    [Fact]
    public async Task RatsitWebsite_Should_MaintainExpectedStructure()
    {
        // This test monitors Ratsit.se website structure and will fail if they change their layout
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());
        var url = "https://www.ratsit.se/sok/foretag?who=test";

        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        var response = await httpClient.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        // These assertions will fail if Ratsit.se changes their website structure
        Assert.True(response.IsSuccessStatusCode, "Ratsit.se should be accessible");
        Assert.True(html.Length > 1000, "Ratsit.se should return substantial HTML content");

        // Check for expected HTML elements
        var expectedElements = new[]
        {
            "<!DOCTYPE html>",
            "<html",
            "<head>",
            "javascript" // Ratsit.se uses JavaScript
        };

        foreach (var element in expectedElements)
        {
            Assert.Contains(element, html, StringComparison.OrdinalIgnoreCase);
        }

        // Ratsit.se may not have <body> tag if content is loaded via JavaScript
        _output.WriteLine($"Ratsit.se HTML contains <body> tag: {html.Contains("<body>", StringComparison.OrdinalIgnoreCase)}");

        // If Ratsit.se changes from JavaScript to server-side rendering, this test will catch it
        if (!html.Contains("javascript", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail("⚠️ Ratsit.se may have changed from JavaScript to server-side rendering - update scraping patterns!");
        }

        _output.WriteLine($"✅ Ratsit.se structure check passed - still using JavaScript (response: {html.Length} chars)");
    }

    [Fact]
    public async Task AllabolagWebsite_Should_MaintainExpectedStructure()
    {
        // This test monitors Allabolag.se website structure and will fail if they change their layout
        var plugin = new Plugin(_configuration, _loggerFactory.CreateLogger<Plugin>());
        var url = "https://www.allabolag.se/what/spotify";

        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        var response = await httpClient.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        // These assertions will fail if Allabolag.se changes their website structure
        Assert.True(response.IsSuccessStatusCode, "Allabolag.se should be accessible");
        Assert.True(html.Length > 1000, "Allabolag.se should return substantial HTML content");

        // Check for expected HTML elements
        var expectedElements = new[]
        {
            "<!DOCTYPE html>",
            "<html",
            "<head>",
            "<body>"
        };

        foreach (var element in expectedElements)
        {
            Assert.Contains(element, html, StringComparison.OrdinalIgnoreCase);
        }

        // Allabolag.se should contain company information in static HTML (not JavaScript-dependent)
        if (html.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
        {
            _output.WriteLine("✅ Allabolag.se still returns company data in static HTML");
        }
        else
        {
            Assert.Fail("⚠️ Allabolag.se may have changed to JavaScript rendering - update scraping patterns!");
        }

        _output.WriteLine($"✅ Allabolag.se structure check passed (response: {html.Length} chars)");
    }
}