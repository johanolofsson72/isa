using Agent.Models;
using Agent.Models.Migration;
using Agent.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Services;

/// <summary>
/// Service for migrating existing workflows to Quartz.NET scheduling system
/// Handles discovery, validation, and migration of YAML workflows
/// </summary>
public class WorkflowMigrationService
{
    private readonly ILogger<WorkflowMigrationService> _logger;
    private readonly IWorkflowService _workflowService;
    private readonly QuartzJobRegistrationService _quartzJobService;
    private readonly IWorkflowValidationService _validationService;
    private readonly IDeserializer _yamlDeserializer;
    private readonly string _workflowsPath;

    public WorkflowMigrationService(
        ILogger<WorkflowMigrationService> logger,
        IWorkflowService workflowService,
        QuartzJobRegistrationService quartzJobService,
        IWorkflowValidationService validationService,
        IConfiguration configuration)
    {
        _logger = logger;
        _workflowService = workflowService;
        _quartzJobService = quartzJobService;
        _validationService = validationService;
        _workflowsPath = configuration["WorkflowSettings:WorkflowsPath"] ?? 
                        Path.Combine(Directory.GetCurrentDirectory(), "workflows");

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Discover all existing workflows that need migration
    /// </summary>
    public async Task<WorkflowDiscoveryResult> DiscoverWorkflowsAsync()
    {
        _logger.LogInformation("🔍 Discovering workflows for migration from: {Path}", _workflowsPath);
        
        var result = new WorkflowDiscoveryResult();
        
        try
        {
            if (!Directory.Exists(_workflowsPath))
            {
                _logger.LogWarning("⚠️ Workflows directory not found: {Path}", _workflowsPath);
                return result;
            }

            var yamlFiles = Directory.GetFiles(_workflowsPath, "*.yaml", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(_workflowsPath, "*.yml", SearchOption.AllDirectories))
                .ToList();

            _logger.LogInformation("📁 Found {Count} YAML files", yamlFiles.Count);

            foreach (var filePath in yamlFiles)
            {
                try
                {
                    var workflowInfo = await AnalyzeWorkflowFileAsync(filePath);
                    if (workflowInfo != null)
                    {
                        result.DiscoveredWorkflows.Add(workflowInfo);
                        
                        if (workflowInfo.IsScheduled)
                        {
                            result.ScheduledWorkflowsCount++;
                        }
                        if (workflowInfo.RequiresMigration)
                        {
                            result.WorkflowsRequiringMigration++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error analyzing workflow file: {FilePath}", filePath);
                    result.ErrorFiles.Add(new WorkflowFileError
                    {
                        FilePath = filePath,
                        ErrorMessage = ex.Message
                    });
                }
            }

            result.TotalWorkflowsFound = result.DiscoveredWorkflows.Count;
            result.IsSuccess = true;

            _logger.LogInformation("✅ Discovery completed: {Total} workflows found, {Scheduled} scheduled, {RequireMigration} require migration",
                result.TotalWorkflowsFound, result.ScheduledWorkflowsCount, result.WorkflowsRequiringMigration);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Critical error during workflow discovery");
            result.IsSuccess = false;
            result.ErrorMessage = $"Discovery failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Migrate all discovered workflows to Quartz.NET
    /// </summary>
    public async Task<WorkflowMigrationResult> MigrateAllWorkflowsAsync(bool dryRun = false)
    {
        _logger.LogInformation("🔄 Starting workflow migration to Quartz.NET (DryRun: {DryRun})", dryRun);
        
        var result = new WorkflowMigrationResult { IsDryRun = dryRun };
        
        try
        {
            // First discover all workflows
            var discoveryResult = await DiscoverWorkflowsAsync();
            if (!discoveryResult.IsSuccess)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Discovery failed: {discoveryResult.ErrorMessage}";
                return result;
            }

            result.TotalWorkflows = discoveryResult.TotalWorkflowsFound;
            var workflowsToMigrate = discoveryResult.DiscoveredWorkflows
                .Where(w => w.RequiresMigration)
                .ToList();

            _logger.LogInformation("📊 Found {Total} workflows, {ToMigrate} require migration", 
                result.TotalWorkflows, workflowsToMigrate.Count);

            // Migrate each workflow
            foreach (var workflowInfo in workflowsToMigrate)
            {
                try
                {
                    var migrationResult = await MigrateWorkflowAsync(workflowInfo, dryRun);
                    result.MigrationResults.Add(migrationResult);

                    if (migrationResult.IsSuccess)
                    {
                        result.SuccessfulMigrations++;
                    }
                    else
                    {
                        result.FailedMigrations++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error migrating workflow: {WorkflowId}", workflowInfo.Id);
                    result.MigrationResults.Add(new WorkflowMigrationItemResult
                    {
                        WorkflowId = workflowInfo.Id,
                        WorkflowName = workflowInfo.Name,
                        IsSuccess = false,
                        ErrorMessage = ex.Message,
                        FilePath = workflowInfo.FilePath
                    });
                    result.FailedMigrations++;
                }
            }

            result.IsSuccess = result.FailedMigrations == 0;
            
            _logger.LogInformation("✅ Migration completed: {Success}/{Total} successful", 
                result.SuccessfulMigrations, workflowsToMigrate.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Critical error during workflow migration");
            result.IsSuccess = false;
            result.ErrorMessage = $"Migration failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Migrate a single workflow to Quartz.NET
    /// </summary>
    public async Task<WorkflowMigrationItemResult> MigrateWorkflowAsync(WorkflowInfo workflowInfo, bool dryRun = false)
    {
        var result = new WorkflowMigrationItemResult
        {
            WorkflowId = workflowInfo.Id,
            WorkflowName = workflowInfo.Name,
            FilePath = workflowInfo.FilePath
        };

        try
        {
            _logger.LogInformation("🔄 Migrating workflow: {WorkflowId} ({WorkflowName})", 
                workflowInfo.Id, workflowInfo.Name);

            // Load and validate the workflow
            var workflowModel = await LoadWorkflowFromFileAsync(workflowInfo.FilePath);
            if (workflowModel == null)
            {
                result.ErrorMessage = "Failed to load workflow from file";
                return result;
            }

            // Validate workflow before migration
            var validationResult = _validationService.ValidateWorkflow(workflowModel);
            if (!validationResult.IsValid)
            {
                result.ErrorMessage = $"Workflow validation failed: {string.Join(", ", validationResult.Errors)}";
                return result;
            }

            if (!dryRun)
            {
                // Check if workflow is already registered in Quartz
                var isAlreadyRegistered = await _quartzJobService.IsWorkflowJobRegisteredAsync(workflowModel.Id);

                if (isAlreadyRegistered)
                {
                    _logger.LogInformation("⚠️ Workflow {WorkflowId} is already registered in Quartz.NET", workflowModel.Id);
                    result.IsSuccess = true;
                    result.AlreadyMigrated = true;
                    result.Notes = "Workflow was already registered in Quartz.NET";
                    return result;
                }

                // Register the workflow in Quartz.NET
                if (workflowInfo.IsScheduled && !string.IsNullOrEmpty(workflowModel.Schedule))
                {
                    var registrationSuccess = await _quartzJobService.RegisterWorkflowJobAsync(workflowModel);
                    if (!registrationSuccess)
                    {
                        result.ErrorMessage = "Failed to register workflow in Quartz.NET";
                        return result;
                    }

                    result.ScheduledInQuartz = true;
                    result.ScheduleType = "Quartz";
                    // Note: NextFireTime would need to be calculated from the schedule
                }
            }

            result.IsSuccess = true;
            result.MigratedAt = DateTimeOffset.Now;
            
            _logger.LogInformation("✅ Successfully migrated workflow: {WorkflowId}", workflowInfo.Id);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error migrating workflow: {WorkflowId}", workflowInfo.Id);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Validate that migration was successful
    /// </summary>
    public async Task<MigrationValidationResult> ValidateMigrationAsync()
    {
        _logger.LogInformation("🔍 Validating migration results");
        
        var result = new MigrationValidationResult();
        
        try
        {
            // Get all workflows from files
            var discoveryResult = await DiscoverWorkflowsAsync();
            if (!discoveryResult.IsSuccess)
            {
                result.IsValid = false;
                result.Issues.Add($"Discovery failed: {discoveryResult.ErrorMessage}");
                return result;
            }

            // Get all registered workflows from Quartz
            var registeredJobKeys = await _quartzJobService.GetRegisteredWorkflowJobsAsync();

            result.TotalWorkflowsInFiles = discoveryResult.TotalWorkflowsFound;
            result.TotalWorkflowsInQuartz = registeredJobKeys.Count;

            // Check that all scheduled workflows from files are in Quartz
            var scheduledFromFiles = discoveryResult.DiscoveredWorkflows.Where(w => w.IsScheduled).ToList();

            foreach (var fileWorkflow in scheduledFromFiles)
            {
                var isRegistered = await _quartzJobService.IsWorkflowJobRegisteredAsync(fileWorkflow.Id);
                if (!isRegistered)
                {
                    result.Issues.Add($"Workflow {fileWorkflow.Id} is scheduled in file but not registered in Quartz");
                }
                else
                {
                    result.SuccessfullyMigratedWorkflows++;

                    // Note: Schedule validation would require additional Quartz API calls
                    // For now, we just verify the job is registered
                }
            }

            result.IsValid = result.Issues.Count == 0;
            
            _logger.LogInformation("✅ Migration validation completed: {Valid}, {Issues} issues, {Warnings} warnings",
                result.IsValid, result.Issues.Count, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error validating migration");
            result.IsValid = false;
            result.Issues.Add($"Validation failed: {ex.Message}");
            return result;
        }
    }

    #region Private Helper Methods

    private async Task<WorkflowInfo?> AnalyzeWorkflowFileAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var workflowModel = _yamlDeserializer.Deserialize<WorkflowDefinition>(content);

            if (workflowModel == null || string.IsNullOrEmpty(workflowModel.Id))
            {
                return null;
            }

            var isScheduled = !string.IsNullOrEmpty(workflowModel.Schedule) &&
                             workflowModel.Schedule.ToLower() != "manual";

            return new WorkflowInfo
            {
                Id = workflowModel.Id,
                Name = workflowModel.Name ?? workflowModel.Id,
                FilePath = filePath,
                Schedule = workflowModel.Schedule,
                IsScheduled = isScheduled,
                RequiresMigration = isScheduled, // Only scheduled workflows need migration
                ExecutionType = workflowModel.ExecutionType ?? (isScheduled ? "scheduled" : "manual"),
                JobCount = workflowModel.Jobs?.Count ?? 0,
                LastModified = File.GetLastWriteTime(filePath)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error analyzing workflow file: {FilePath}", filePath);
            return null;
        }
    }

    private async Task<WorkflowDefinition?> LoadWorkflowFromFileAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            return _yamlDeserializer.Deserialize<WorkflowDefinition>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error loading workflow from file: {FilePath}", filePath);
            return null;
        }
    }

    #endregion
}