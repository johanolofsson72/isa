using AnalysisPlugin.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace AnalysisPlugin.Tests.Unit.Services;

[Trait("Category", "CI")]
public class SchemaValidationServiceTests
{
    private readonly SchemaValidationService _service;
    private readonly Mock<ILogger<SchemaValidationService>> _mockLogger;

    public SchemaValidationServiceTests()
    {
        _mockLogger = new Mock<ILogger<SchemaValidationService>>();
        _service = new SchemaValidationService(_mockLogger.Object);
    }

    [Fact]
    public void ValidateComponentConfiguration_ValidConfiguration_ReturnsValid()
    {
        // Arrange
        var validConfig = JsonSerializer.Serialize(new
        {
            componentType = "chart",
            id = "test-chart-1",
            title = "Test Chart",
            dataSource = new
            {
                type = "financial",
                filters = new
                {
                    dateRange = new
                    {
                        startDate = "2023-01-01",
                        endDate = "2023-12-31"
                    }
                }
            },
            chartConfig = new
            {
                chartType = "bar",
                xAxis = "date",
                yAxis = "amount",
                showLegend = true
            }
        });

        // Act
        var result = _service.ValidateComponentConfiguration(validConfig);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateComponentConfiguration_MissingRequiredField_ReturnsInvalid()
    {
        // Arrange
        var invalidConfig = JsonSerializer.Serialize(new
        {
            componentType = "chart",
            // Missing required 'id' field
            title = "Test Chart",
            dataSource = new
            {
                type = "financial"
            }
        });

        // Act
        var result = _service.ValidateComponentConfiguration(invalidConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.ErrorCode == "SCHEMA_VALIDATION");
    }

    [Fact]
    public void ValidateComponentConfiguration_InvalidComponentType_ReturnsInvalid()
    {
        // Arrange
        var invalidConfig = JsonSerializer.Serialize(new
        {
            componentType = "invalid-type",
            id = "test-1",
            title = "Test",
            dataSource = new
            {
                type = "financial"
            }
        });

        // Act
        var result = _service.ValidateComponentConfiguration(invalidConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateComponentConfiguration_InvalidJson_ReturnsInvalid()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act
        var result = _service.ValidateComponentConfiguration(invalidJson);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.ErrorCode == "INVALID_JSON");
    }

    [Fact]
    public void ValidateComponentConfiguration_ChartWithoutChartConfig_ReturnsInvalid()
    {
        // Arrange
        var invalidConfig = JsonSerializer.Serialize(new
        {
            componentType = "chart",
            id = "test-chart",
            title = "Test Chart",
            dataSource = new
            {
                type = "financial"
            }
            // Missing required chartConfig for chart type
        });

        // Act
        var result = _service.ValidateComponentConfiguration(invalidConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateMultipleConfigurations_MixedValidInvalid_ReturnsCorrectResults()
    {
        // Arrange
        var validConfig = JsonSerializer.Serialize(new
        {
            componentType = "table",
            id = "test-table",
            title = "Test Table",
            dataSource = new { type = "financial" },
            tableConfig = new
            {
                columns = new[]
                {
                    new { field = "date", header = "Date" },
                    new { field = "amount", header = "Amount" }
                }
            }
        });

        var invalidConfig = JsonSerializer.Serialize(new
        {
            componentType = "chart",
            id = "test-chart",
            title = "Test Chart"
            // Missing required dataSource
        });

        var configurations = new[] { validConfig, invalidConfig };

        // Act
        var result = _service.ValidateMultipleConfigurations(configurations);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        
        // Check that error paths are prefixed with component index
        Assert.Contains(result.Errors, e => e.FieldPath.StartsWith("[1]."));
    }

    [Fact]
    public void ValidateComponentConfiguration_ForecastWithValidConfig_ReturnsValid()
    {
        // Arrange
        var validConfig = JsonSerializer.Serialize(new
        {
            componentType = "forecast",
            id = "test-forecast",
            title = "Test Forecast",
            dataSource = new
            {
                type = "historical"
            },
            forecastConfig = new
            {
                horizon = 12,
                confidenceLevel = 0.95,
                algorithm = "auto"
            }
        });

        // Act
        var result = _service.ValidateComponentConfiguration(validConfig);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
