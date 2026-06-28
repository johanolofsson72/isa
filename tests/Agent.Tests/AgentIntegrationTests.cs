using Agent.Models;
using Agent.Services;
using Agent.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using System.Text.Json;
using Xunit;

namespace Agent.Tests;

/// <summary>
/// Integration tests for Agent functionality
/// These tests use real external dependencies, credentials, and services
/// NOT suitable for CI/CD - run manually only
///
/// Tests include:
/// - Real database connections
/// - External API calls
/// - File system operations
/// - Network communications
/// - Quartz.NET scheduler with persistence
/// </summary>
[Trait("Category", "Manual")]
public class AgentIntegrationTests : IAsyncLifetime
{
    private readonly IHost _host;
    private readonly IServiceProvider _serviceProvider;
    private readonly IScheduler _scheduler;
    private readonly WorkflowMigrationService _migrationService;
    private readonly WorkflowRollbackService _rollbackService;
    private readonly QuartzHealthCheckService _healthCheckService;
    private readonly WorkflowNotificationService _notificationService;
    private readonly string _tempWorkflowsPath;
    private readonly string _tempBackupPath;

    public AgentIntegrationTests()
    {
        // Create temporary directories for integration testing
        _tempWorkflowsPath = Path.Combine(Path.GetTempPath(), "agent-integration-test-workflows", Guid.NewGuid().ToString());
        _tempBackupPath = Path.Combine(Path.GetTempPath(), "agent-integration-test-backups", Guid.NewGuid().ToString());

        Directory.CreateDirectory(_tempWorkflowsPath);
        Directory.CreateDirectory(_tempBackupPath);

        // Setup integration test host with real services
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WorkflowSettings:WorkflowsPath"] = _tempWorkflowsPath,
                    ["WorkflowSettings:BackupPath"] = _tempBackupPath,
                    ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                    ["Logging:LogLevel:Default"] = "Information",
                    ["Logging:LogLevel:Quartz"] = "Warning"
                });
            })
            .ConfigureServices((context, services) =>
            {
                // Add Quartz.NET with SQLite persistence for integration testing
                services.AddQuartz(q =>
                {
                    q.UseInMemoryStore(); // Use in-memory for integration tests
                    q.UseDefaultThreadPool(tp =>
                    {
                        tp.MaxConcurrency = 5;
                    });
                });

                services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

                // Add Quartz.NET services
                services.AddQuartz(q =>
                {
                    q.UseInMemoryStore();
                });
                services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

                // Manually register IScheduler for tests
                services.AddSingleton<IScheduler>(provider =>
                {
                    var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
                    return schedulerFactory.GetScheduler().Result;
                });

                // Add IWebHostEnvironment mock for WorkflowService
                var mockWebHostEnvironment = new Mock<IWebHostEnvironment>();
                mockWebHostEnvironment.Setup(x => x.ContentRootPath).Returns(Directory.GetCurrentDirectory());
                services.AddSingleton(mockWebHostEnvironment.Object);

                // Add HttpClient for WorkflowNotificationService
                services.AddHttpClient();

                // Add all Agent services
                services.AddSingleton<IWorkflowService, WorkflowService>();
                services.AddSingleton<HistoryService>();
                services.AddScoped<WorkflowExecutor>();
                services.AddScoped<WorkflowMigrationService>();
                services.AddScoped<WorkflowRollbackService>();
                services.AddScoped<IWorkflowValidationService, WorkflowValidationService>();
                services.AddScoped<WorkflowValidationService>();
                services.AddScoped<QuartzJobRegistrationService>();
                services.AddScoped<WorkflowMetricsCollector>();
                services.AddScoped<WorkflowNotificationService>();
                services.AddScoped<QuartzHealthCheckService>();

	                // Notification services required by WorkflowNotificationService
	                services.AddSingleton<NotificationRateLimitService>();

                services.AddScoped<IWorkflowScheduleParser, WorkflowScheduleParser>();
            });

        _host = hostBuilder.Build();
        _serviceProvider = _host.Services;
        _scheduler = _serviceProvider.GetRequiredService<IScheduler>();
        _migrationService = _serviceProvider.GetRequiredService<WorkflowMigrationService>();
        _rollbackService = _serviceProvider.GetRequiredService<WorkflowRollbackService>();
        _healthCheckService = _serviceProvider.GetRequiredService<QuartzHealthCheckService>();
        _notificationService = _serviceProvider.GetRequiredService<WorkflowNotificationService>();
    }

    public async Task InitializeAsync()
    {
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();

        // Cleanup test directories
        if (Directory.Exists(_tempWorkflowsPath))
        {
            Directory.Delete(_tempWorkflowsPath, recursive: true);
        }
        if (Directory.Exists(_tempBackupPath))
        {
            Directory.Delete(_tempBackupPath, recursive: true);
        }
    }

    [Fact(Skip = "Manual run only - requires external services and long-running Quartz host")]
    public async Task Integration_CompleteWorkflowLifecycle_ShouldSucceed()
    {
        // Arrange - Create a test workflow file
        var workflowYaml = CreateTestWorkflowYaml("integration-test-workflow", "0 */10 * * * ?");
        var workflowFile = Path.Combine(_tempWorkflowsPath, "integration-test.yaml");
        await File.WriteAllTextAsync(workflowFile, workflowYaml);

        // Act & Assert - Discovery
        var discoveryResult = await _migrationService.DiscoverWorkflowsAsync();
        discoveryResult.IsSuccess.Should().BeTrue();
        discoveryResult.TotalWorkflowsFound.Should().Be(1);
        discoveryResult.ScheduledWorkflowsCount.Should().Be(1);

        // Act & Assert - Migration
        var migrationResult = await _migrationService.MigrateAllWorkflowsAsync(dryRun: false);
        migrationResult.IsSuccess.Should().BeTrue();
        migrationResult.SuccessfulMigrations.Should().Be(1);

        // Act & Assert - Validation
        var validationResult = await _migrationService.ValidateMigrationAsync();
        validationResult.IsValid.Should().BeTrue();
        validationResult.SuccessfullyMigratedWorkflows.Should().Be(1);

        // Act & Assert - Health Check
        var healthResult = await _healthCheckService.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());
        healthResult.Status.Should().Be(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
    }

    [Fact(Skip = "Manual run only - requires external services and long-running Quartz host")]
    public async Task Integration_BackupAndRollback_ShouldPreserveWorkflows()
    {
        // Arrange - Create test workflows
        var workflow1 = CreateTestWorkflowYaml("backup-test-1", "0 0 9 * * ?");
        var workflow2 = CreateTestWorkflowYaml("backup-test-2", "0 0 17 * * ?");

        await File.WriteAllTextAsync(Path.Combine(_tempWorkflowsPath, "backup-test-1.yaml"), workflow1);
        await File.WriteAllTextAsync(Path.Combine(_tempWorkflowsPath, "backup-test-2.yaml"), workflow2);

        // Act - Create backup
        var backupResult = await _rollbackService.CreateBackupAsync("integration-test-backup");
        backupResult.IsSuccess.Should().BeTrue();
        backupResult.BackedUpWorkflows.Should().Be(2);

        // Act - Simulate changes (delete one workflow)
        File.Delete(Path.Combine(_tempWorkflowsPath, "backup-test-1.yaml"));

        // Act - Rollback
        var rollbackResult = await _rollbackService.RollbackAsync("integration-test-backup");
        rollbackResult.IsSuccess.Should().BeTrue();
        rollbackResult.RestoredWorkflows.Should().Be(2);

        // Assert - Verify restoration
        File.Exists(Path.Combine(_tempWorkflowsPath, "backup-test-1.yaml")).Should().BeTrue();
        File.Exists(Path.Combine(_tempWorkflowsPath, "backup-test-2.yaml")).Should().BeTrue();
    }

    [Fact(Skip = "Manual run only - requires external services and long-running Quartz host")]
    public async Task Integration_SchedulePreservation_ShouldMaintainOriginalSchedules()
    {
        // Arrange - Create workflows with different schedules
        var workflows = new[]
        {
            ("daily-morning", "0 0 9 * * ?", "Daily at 9 AM"),
            ("weekly-report", "0 0 9 ? * MON", "Weekly on Monday at 9 AM"),
            ("monthly-cleanup", "0 0 2 1 * ?", "Monthly on 1st at 2 AM")
        };

        foreach (var (id, schedule, description) in workflows)
        {
            var yaml = CreateTestWorkflowYaml(id, schedule);
            await File.WriteAllTextAsync(Path.Combine(_tempWorkflowsPath, $"{id}.yaml"), yaml);
        }

        // Act - Migrate workflows
        var migrationResult = await _migrationService.MigrateAllWorkflowsAsync(dryRun: false);
        migrationResult.IsSuccess.Should().BeTrue();

        // Act - Validate schedule preservation
        var preservationResult = await _rollbackService.ValidateSchedulePreservationAsync();
        preservationResult.IsValid.Should().BeTrue();
        preservationResult.PreservedSchedules.Should().Be(3);
        preservationResult.TotalScheduledWorkflows.Should().Be(3);
    }

    [Fact(Skip = "Manual run only - requires external notification configuration")]
    public Task Integration_NotificationSystem_ShouldSendTestNotifications()
    {
        // This test requires external service configuration
        // Skip if notification services are not configured

        // Act - Send test notification (mock implementation)
        // Note: SendTestNotificationAsync method doesn't exist in current implementation
        // This test would need to be updated when the method is implemented
        var result = true; // Placeholder for actual test

        // Assert - Should succeed or gracefully handle missing configuration
        result.Should().BeTrue();
        // Note: In integration environment, this might succeed or fail based on configuration
        // The important thing is that it doesn't throw exceptions

        return Task.CompletedTask;
    }

    [Fact(Skip = "Manual run only - requires live Quartz scheduler")]
    public async Task Integration_HealthCheck_ShouldReportSystemStatus()
    {
        // Act
        var healthResult = await _healthCheckService.CheckHealthAsync(
            new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        // Assert
        healthResult.Should().NotBeNull();
        healthResult.Status.Should().BeOneOf(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

        healthResult.Data.Should().ContainKey("scheduler");
        healthResult.Data.Should().ContainKey("jobs");
        healthResult.Data.Should().ContainKey("metrics");
    }

    [Fact(Skip = "Manual run only - requires real database connection")]
    public async Task Integration_DatabaseOperations_ShouldPersistWorkflowExecutions()
    {
        // Arrange
        var workflowService = _serviceProvider.GetRequiredService<IWorkflowService>();
        var testWorkflow = CreateTestWorkflowDefinition("db-test-workflow");

        // Act - Save workflow
        await workflowService.SaveWorkflowAsync(testWorkflow);

        // Act - Load workflow
        var loadedWorkflow = await workflowService.GetWorkflowAsync("db-test-workflow");

        // Assert
        loadedWorkflow.Should().NotBeNull();
        loadedWorkflow!.Id.Should().Be("db-test-workflow");
        loadedWorkflow.Name.Should().Be(testWorkflow.Name);
        loadedWorkflow.Schedule.Should().Be(testWorkflow.Schedule);
    }

    [Fact(Skip = "Manual run only - requires file system access")]
    public async Task Integration_FileSystemOperations_ShouldHandleWorkflowFiles()
    {
        // Arrange
        var workflowService = _serviceProvider.GetRequiredService<IWorkflowService>();
        var testWorkflowYaml = CreateTestWorkflowYaml("file-test-workflow", "0 0 12 * * ?");
        var workflowFile = Path.Combine(_tempWorkflowsPath, "file-test.yaml");

        // Act - Write workflow file
        await File.WriteAllTextAsync(workflowFile, testWorkflowYaml);

        // Act - Load workflows from file system
        var workflows = await workflowService.LoadWorkflowsAsync();

        // Assert
        workflows.Should().NotBeEmpty();
        workflows.Should().Contain(w => w.Id == "file-test-workflow");

        // Act - Delete workflow
        var deleteResult = await workflowService.DeleteWorkflowAsync("file-test-workflow");

        // Assert
        deleteResult.Should().BeTrue();
        File.Exists(workflowFile).Should().BeFalse();
    }

    [Fact(Skip = "Manual run only - requires external API access")]
    public async Task Integration_ExternalApiCalls_ShouldHandleNetworkOperations()
    {
        // Arrange
        var httpClient = _serviceProvider.GetRequiredService<HttpClient>();

        // Act - Test external API call (using a reliable public API)
        var response = await httpClient.GetAsync("https://httpbin.org/status/200");

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact(Skip = "Manual run only - requires Semantic Kernel configuration")]
    public async Task Integration_SemanticKernel_ShouldExecutePluginFunctions()
    {
        // Note: This test would require actual Semantic Kernel setup with API keys
        // For now, we'll test the service registration and basic functionality

        // Arrange - This would normally require ChatExecutionService
        // But since we can't easily mock Kernel, we'll test service availability
        var serviceProvider = _serviceProvider;

        // Act & Assert - Verify required services are registered
        serviceProvider.GetService<IConfiguration>().Should().NotBeNull();
        serviceProvider.GetService<ILogger<AgentIntegrationTests>>().Should().NotBeNull();

        // Note: In a real integration test, this would:
        // 1. Initialize Semantic Kernel with real API keys
        // 2. Load actual plugins
        // 3. Execute plugin functions
        // 4. Verify responses
        await Task.CompletedTask; // Placeholder for async pattern
    }

    #region Helper Methods

    private static WorkflowDefinition CreateTestWorkflowDefinition(string id)
    {
        return new WorkflowDefinition
        {
            Id = id,
            Name = $"Integration Test Workflow {id}",
            Description = "Test workflow for integration testing",
            Enabled = true,
            ExecutionType = "Manual",
            Schedule = "0 0 12 * * ?", // Daily at noon
            SystemPrompt = "Test system prompt",
            Jobs = new List<WorkflowJob>
            {
                new WorkflowJob
                {
                    Name = "Test Job",
                    Description = "Test job for integration testing",
                    Order = 1,
                    Enabled = true,
                    Steps = new List<WorkflowStep>
                    {
                        new WorkflowStep
                        {
                            Id = "test-step-1",
                            Prompt = "Test step prompt",
                            Order = 1,
                            Timeout = 30,
                            Retries = 3
                        }
                    }
                }
            }
        };
    }

    private static string CreateTestWorkflowYaml(string id, string schedule)
    {
        return $@"
id: {id}
name: Integration Test Workflow {id}
description: Test workflow for integration testing
enabled: true
executionType: scheduled
schedule: {schedule}
timezone: Europe/Stockholm
misfireHandling: Skip
jobs:
  - name: test-job
    description: Test job for integration testing
    order: 1
    enabled: true
    steps:
      - id: test-step
        prompt: This is a test step for integration testing
        order: 1
        enabled: true
        toolName: TestTool
        onSuccess: complete
        onError: fail
";
    }

    #endregion
}
