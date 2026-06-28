# Test Architecture Guide

## Overview

This document defines the **mandatory** test architecture for the AccountingAgent project. All testing work follows our **mock-free pattern** to ensure consistency, reliability, and proper CI/CD integration.

## 🎯 Current Status: 265 Plugin Unit Tests

**10 plugins with comprehensive unit test coverage:**
- **SmsPlugin**: 25 tests
- **WhatsAppPlugin**: 39 tests
- **MicrosoftOfficePlugin**: 50 tests
- **SlackPlugin**: 37 tests
- **DropboxPlugin**: 18 tests
- **GoogleWorkspacePlugin**: 9 tests
- **OpenMeteoPlugin**: 16 tests
- **OpenCagePlugin**: 17 tests
- **DateTimePlugin**: 35 tests
- **GoogleSearchPlugin**: 19 tests

## Test Categories

### 1. Plugin Unit Tests (`[Trait("Category", "CI")]`)

**Purpose**: Mock-free unit tests for plugin functionality
**Location**: `tests/{PluginName}.Tests/{PluginName}Tests.cs`
**CI/CD**: ✅ **RUNS IN CI/CD** - Nightly tests at 02:00 UTC

**Mock-Free Characteristics**:
- ✅ No external API calls in CI tests
- ✅ Real objects instead of mocks
- ✅ TestConfiguration class for IConfiguration
- ✅ NullLogger<Plugin>.Instance for logging
- ✅ Fast execution (1-5 seconds per plugin)
- ✅ Deterministic results
- ✅ Safe mode testing (no real API keys)

**Example Mock-Free Pattern**:
```csharp
[Trait("Category", "CI")]
public class SmsPluginTests
{
    private readonly ILogger<Plugin> _logger;
    private readonly TestConfiguration _testConfiguration;

    public SmsPluginTests()
    {
        _logger = NullLogger<Plugin>.Instance;
        _testConfiguration = new TestConfiguration();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldInitialize()
    {
        // Arrange - Real objects, no mocks
        _testConfiguration["Twilio:AccountSid"] = "test-sid";
        _testConfiguration["Twilio:AuthToken"] = "test-token";

        // Act
        var plugin = new Plugin(_testConfiguration, _logger);

        // Assert
        plugin.Should().NotBeNull();
    }
}
```

### 2. Integration Tests (`[Trait("Category", "Manual")]`)

**Purpose**: End-to-end tests with real external dependencies
**Location**: `tests/Agent.Tests/AgentIntegrationTests.cs`
**CI/CD**: ❌ **NOT IN CI/CD - MANUAL EXECUTION ONLY**

**Characteristics**:
- ⚠️ Requires external dependencies (database, API keys, Quartz scheduler)
- ⚠️ Requires real credentials and network access
- ⚠️ Long-running tests (can take several minutes)
- ⚠️ Tests complete workflow lifecycle
- ⚠️ Tests backup and rollback functionality
- ⚠️ Tests health checks and monitoring

**What Integration Tests Cover**:
- Complete workflow execution with Quartz.NET
- Backup and rollback of workflows
- Health checks of system components
- Semantic Kernel with real API keys
- Notification system with external services
- Database operations and migrations
- ⚠️ May require credentials/API keys
- ⚠️ Network communications
- ⚠️ Real database connections
- ⚠️ File system operations
- ⚠️ Slower execution
- ⚠️ May have non-deterministic results
- ⚠️ Environment-dependent

**Example**:
```csharp
[Trait("Category", "Manual")]
public class AgentIntegrationTests
{
    [Fact]
    public async Task EndToEnd_WorkflowExecution_ShouldCompleteSuccessfully()
    {
        // Uses real Quartz.NET scheduler, database, external APIs
        // Requires proper environment setup with credentials
    }
}
```

## 🌙 CI/CD & Nightly Tests

### GitHub Actions Pipeline
- **Trigger**: Push/PR to main, develop, trigger branches
- **Nightly**: Automated tests at 02:00 UTC every night
- **Email Reports**: Failure notifications to AccountingAgent team:
  - colleague@example.com
  - user@example.com
  - joakim.winkler@example.com

### Test Execution Commands
```bash
# Run all CI tests (what runs in pipeline)
dotnet test --configuration Release --filter "Category=CI"

# Run specific plugin tests
dotnet test tests/SmsPlugin.Tests/ --configuration Release --filter "Category=CI"

# Run manual integration tests (local only)
dotnet test --configuration Release --filter "Category=Manual"

# Test email functionality
bash scripts/test-email-report.sh
```

## File Structure

```
tests/
├── Agent.Tests/
│   ├── AgentUnitTests.cs              # Main unit tests (CI/CD)
│   ├── AgentIntegrationTests.cs       # Integration tests (Manual)
│   └── TestArchitectureValidation.cs  # Architecture validation
├── SmsPlugin.Tests/
│   └── SmsPluginTests.cs              # 25 unit tests (CI/CD)
├── WhatsAppPlugin.Tests/
│   └── WhatsAppPluginTests.cs         # 39 unit tests (CI/CD)
├── MicrosoftOfficePlugin.Tests/
│   └── MicrosoftOfficePluginTests.cs  # 50 unit tests (CI/CD)
├── SlackPlugin.Tests/
│   └── SlackPluginTests.cs            # 37 unit tests (CI/CD)
├── DropboxPlugin.Tests/
│   └── DropboxPluginTests.cs          # 18 unit tests (CI/CD)
├── GoogleWorkspacePlugin.Tests/
│   └── GoogleWorkspacePluginTests.cs  # 9 unit tests (CI/CD)
├── OpenMeteoPlugin.Tests/
│   └── OpenMeteoPluginTests.cs        # 16 unit tests (CI/CD)
├── OpenCagePlugin.Tests/
│   └── OpenCagePluginTests.cs         # 17 unit tests (CI/CD)
├── DateTimePlugin.Tests/
│   └── DateTimePluginTests.cs         # 35 unit tests (CI/CD)
└── GoogleSearchPlugin.Tests/
    └── GoogleSearchPluginTests.cs     # 19 unit tests (CI/CD)
```

## Naming Conventions

### Test Classes
- **Plugin Unit Tests**: `{PluginName}Tests.cs` (e.g., `SmsPluginTests.cs`)
- **Integration Tests**: `AgentIntegrationTests.cs`

### Test Methods
- **Plugin Unit Tests**: `Method_Should<Expected>_When<Condition>()`
- **Integration Tests**: `Integration_FeatureName_ShouldBehavior()`

### Traits
- **Plugin Unit Tests**: `[Trait("Category", "CI")]`
- **Integration Tests**: `[Trait("Category", "Manual")]`

## Implementation Guidelines

### Mock-Free Plugin Unit Tests Requirements

1. **Use Real Objects, Not Mocks**:
```csharp
private readonly ILogger<Plugin> _logger;
private readonly TestConfiguration _testConfiguration;

public PluginTests()
{
    _logger = NullLogger<Plugin>.Instance;  // Real NullLogger
    _testConfiguration = new TestConfiguration();  // Real TestConfiguration
}
```

2. **TestConfiguration Implementation**:
```csharp
private class TestConfiguration : IConfiguration
{
    private readonly Dictionary<string, string?> _values = new();
    public string? this[string key] { get => _values.TryGetValue(key, out var value) ? value : null; set => _values[key] = value; }
    // ... implement other IConfiguration members
    public void SetValue(string key, string? value) => _values[key] = value;
}
```

3. **Test Categories**:
```csharp
// Constructor tests
[Fact]
public void Constructor_WithValidConfiguration_ShouldInitialize()

// Parameter validation tests
[Theory]
[InlineData("")]
[InlineData(null)]
public void Method_WithInvalidInput_ShouldThrowArgumentException(string input)

// Method functionality tests
[Fact]
public void Method_WithValidInput_ShouldReturnExpectedResult()

// Error handling tests
[Fact]
public async Task Method_WithInvalidCredentials_ShouldHandleGracefully()
```

### Integration Tests Requirements

1. **Real Service Setup**:
```csharp
public class MyIntegrationTests : IAsyncLifetime
{
    private readonly IHost _host;
    
    public async Task InitializeAsync()
    {
        await _host.StartAsync(); // Start real services
    }
}
```

2. **Environment Cleanup**:
```csharp
public async Task DisposeAsync()
{
    await _host.StopAsync();
    // Clean up test data, files, etc.
}
```

3. **Comprehensive Scenarios**:
```csharp
[Fact]
public async Task Integration_CompleteWorkflowLifecycle_ShouldSucceed()
{
    // Test entire workflow: create → migrate → validate → rollback
}
```

## CI/CD Integration

### GitHub Actions Configuration

```yaml
- name: Run Unit Tests
  run: dotnet test --filter "Category=CI" --logger trx --results-directory TestResults/

- name: Run Integration Tests (Manual)
  if: github.event_name == 'workflow_dispatch' # Manual trigger only
  run: dotnet test --filter "Category=Manual" --logger trx --results-directory TestResults/
```

### Test Execution Commands

```bash
# CI/CD Pipeline (automated)
dotnet test --filter "Category=CI"

# Manual Integration Testing
dotnet test --filter "Category=Manual"

# All Tests (local development)
dotnet test
```

## Future Testing Work

### When Adding New Features

1. **Always create unit tests** in appropriate `*UnitTests.cs` file
2. **Mark with `[Trait("Category", "CI")]`**
3. **Mock all external dependencies**
4. **Add integration tests** in appropriate `*IntegrationTests.cs` file
5. **Mark with `[Trait("Category", "Manual")]`**
6. **Test real scenarios end-to-end**

### Examples for Future Features

#### Job History Feature
- **Unit Tests**: `JobHistoryServiceUnitTests.cs` - Test data processing, filtering, pagination logic
- **Integration Tests**: `JobHistoryIntegrationTests.cs` - Test with real database, actual job executions

#### Execution Statistics Feature
- **Unit Tests**: `ExecutionStatisticsUnitTests.cs` - Test calculation algorithms, aggregation logic
- **Integration Tests**: `ExecutionStatisticsIntegrationTests.cs` - Test with real metrics collection, database queries

#### Monitoring Features
- **Unit Tests**: `MonitoringServiceUnitTests.cs` - Test alert logic, threshold calculations
- **Integration Tests**: `MonitoringIntegrationTests.cs` - Test real notifications, external service calls

## Best Practices

### ✅ Do
- Follow the established naming conventions
- Use appropriate traits for test categorization
- Mock external dependencies in unit tests
- Clean up resources in integration tests
- Write descriptive test names
- Test both success and failure scenarios

### ❌ Don't
- Mix unit and integration test concerns
- Put external dependencies in CI tests
- Skip cleanup in integration tests
- Use hardcoded paths or credentials
- Create tests without proper categorization
- Ignore the established architecture

## Conclusion

This architecture ensures:
- **Fast CI/CD pipelines** with reliable unit tests
- **Comprehensive validation** through manual integration tests
- **Clear separation** of concerns and dependencies
- **Consistent patterns** across all test code
- **Maintainable test suite** that scales with the project

**All future testing work must follow this pattern without exception.**
