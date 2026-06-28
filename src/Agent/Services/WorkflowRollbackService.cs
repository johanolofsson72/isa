using Agent.Models.Migration;
using Agent.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Services;

/// <summary>
/// Service for rolling back workflow migrations and preserving schedules
/// Provides backup, restore, and rollback capabilities for workflow migrations
/// </summary>
public class WorkflowRollbackService
{
    private readonly ILogger<WorkflowRollbackService> _logger;
    private readonly QuartzJobRegistrationService _quartzJobService;
    private readonly WorkflowMigrationService _migrationService;
    private readonly string _backupPath;

    public WorkflowRollbackService(
        ILogger<WorkflowRollbackService> logger,
        QuartzJobRegistrationService quartzJobService,
        WorkflowMigrationService migrationService,
        IConfiguration configuration)
    {
        _logger = logger;
        _quartzJobService = quartzJobService;
        _migrationService = migrationService;
        _backupPath = configuration["WorkflowSettings:BackupPath"] ?? 
                     Path.Combine(Directory.GetCurrentDirectory(), "backups", "workflows");
    }

    /// <summary>
    /// Create a backup of current workflow state before migration
    /// </summary>
    public async Task<WorkflowBackupResult> CreateBackupAsync(string? backupName = null)
    {
        backupName ??= $"backup_{DateTime.Now:yyyyMMdd_HHmmss}";
        
        _logger.LogInformation("🔄 Creating workflow backup: {BackupName}", backupName);
        
        var result = new WorkflowBackupResult
        {
            BackupName = backupName,
            BackupPath = Path.Combine(_backupPath, backupName)
        };

        try
        {
            // Ensure backup directory exists
            Directory.CreateDirectory(result.BackupPath);

            // Discover current workflows
            var discoveryResult = await _migrationService.DiscoverWorkflowsAsync();
            if (!discoveryResult.IsSuccess)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Failed to discover workflows: {discoveryResult.ErrorMessage}";
                return result;
            }

            // Get current Quartz job state
            var registeredJobs = await _quartzJobService.GetRegisteredWorkflowJobsAsync();

            // Create backup manifest
            var backupManifest = new WorkflowBackupManifest
            {
                BackupName = backupName,
                CreatedAt = DateTimeOffset.Now,
                TotalWorkflows = discoveryResult.TotalWorkflowsFound,
                ScheduledWorkflows = discoveryResult.ScheduledWorkflowsCount,
                RegisteredQuartzJobs = registeredJobs.Count,
                WorkflowFiles = new List<WorkflowFileBackup>(),
                QuartzJobKeys = registeredJobs.ToList()
            };

            // Backup workflow files
            foreach (var workflow in discoveryResult.DiscoveredWorkflows)
            {
                try
                {
                    var fileContent = await File.ReadAllTextAsync(workflow.FilePath);
                    var backupFileName = Path.GetFileName(workflow.FilePath);
                    var backupFilePath = Path.Combine(result.BackupPath, "workflows", backupFileName);
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);
                    await File.WriteAllTextAsync(backupFilePath, fileContent);

                    backupManifest.WorkflowFiles.Add(new WorkflowFileBackup
                    {
                        WorkflowId = workflow.Id,
                        OriginalPath = workflow.FilePath,
                        BackupPath = backupFilePath,
                        IsScheduled = workflow.IsScheduled,
                        Schedule = workflow.Schedule,
                        LastModified = workflow.LastModified
                    });

                    result.BackedUpWorkflows++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to backup workflow file: {FilePath}", workflow.FilePath);
                    result.FailedBackups++;
                }
            }

            // Save backup manifest
            var manifestPath = Path.Combine(result.BackupPath, "manifest.json");
            var manifestJson = JsonSerializer.Serialize(backupManifest, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(manifestPath, manifestJson);

            result.IsSuccess = result.FailedBackups == 0;
            result.ManifestPath = manifestPath;

            _logger.LogInformation("✅ Backup completed: {BackupName}, {Success}/{Total} workflows backed up",
                backupName, result.BackedUpWorkflows, discoveryResult.TotalWorkflowsFound);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Critical error creating backup: {BackupName}", backupName);
            result.IsSuccess = false;
            result.ErrorMessage = $"Backup failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Rollback to a previous backup state
    /// </summary>
    public async Task<WorkflowRollbackResult> RollbackAsync(string backupName)
    {
        _logger.LogInformation("🔄 Starting rollback to backup: {BackupName}", backupName);
        
        var result = new WorkflowRollbackResult
        {
            BackupName = backupName
        };

        try
        {
            var backupPath = Path.Combine(_backupPath, backupName);
            var manifestPath = Path.Combine(backupPath, "manifest.json");

            if (!File.Exists(manifestPath))
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Backup manifest not found: {manifestPath}";
                return result;
            }

            // Load backup manifest
            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<WorkflowBackupManifest>(manifestJson);
            
            if (manifest == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Failed to parse backup manifest";
                return result;
            }

            // Step 1: Remove current Quartz jobs
            var currentJobs = await _quartzJobService.GetRegisteredWorkflowJobsAsync();
            foreach (var jobKey in currentJobs)
            {
                try
                {
                    await _quartzJobService.UnregisterWorkflowJobAsync(jobKey.Name);
                    result.RemovedQuartzJobs++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to remove Quartz job: {JobKey}", jobKey);
                    result.FailedRemovals++;
                }
            }

            // Step 2: Restore workflow files
            foreach (var fileBackup in manifest.WorkflowFiles)
            {
                try
                {
                    if (File.Exists(fileBackup.BackupPath))
                    {
                        var content = await File.ReadAllTextAsync(fileBackup.BackupPath);
                        
                        // Ensure target directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(fileBackup.OriginalPath)!);
                        
                        await File.WriteAllTextAsync(fileBackup.OriginalPath, content);
                        result.RestoredWorkflows++;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Backup file not found: {BackupPath}", fileBackup.BackupPath);
                        result.FailedRestores++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to restore workflow file: {OriginalPath}", fileBackup.OriginalPath);
                    result.FailedRestores++;
                }
            }

            // Step 3: Re-register scheduled workflows in Quartz
            foreach (var fileBackup in manifest.WorkflowFiles.Where(f => f.IsScheduled))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(fileBackup.OriginalPath);
                    var yamlDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build();
                    
                    var workflow = yamlDeserializer.Deserialize<WorkflowDefinition>(content);
                    if (workflow != null && !string.IsNullOrEmpty(workflow.Schedule))
                    {
                        var registered = await _quartzJobService.RegisterWorkflowJobAsync(workflow);
                        if (registered)
                        {
                            result.ReregisteredJobs++;
                        }
                        else
                        {
                            result.FailedReregistrations++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to re-register workflow: {WorkflowId}", fileBackup.WorkflowId);
                    result.FailedReregistrations++;
                }
            }

            result.IsSuccess = result.FailedRemovals == 0 && 
                              result.FailedRestores == 0 && 
                              result.FailedReregistrations == 0;

            _logger.LogInformation("✅ Rollback completed: {BackupName}, Success: {IsSuccess}", 
                backupName, result.IsSuccess);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Critical error during rollback: {BackupName}", backupName);
            result.IsSuccess = false;
            result.ErrorMessage = $"Rollback failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// List available backups
    /// </summary>
    public async Task<List<WorkflowBackupInfo>> ListBackupsAsync()
    {
        var backups = new List<WorkflowBackupInfo>();

        try
        {
            if (!Directory.Exists(_backupPath))
            {
                return backups;
            }

            var backupDirectories = Directory.GetDirectories(_backupPath);
            
            foreach (var backupDir in backupDirectories)
            {
                var manifestPath = Path.Combine(backupDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var manifestJson = await File.ReadAllTextAsync(manifestPath);
                        var manifest = JsonSerializer.Deserialize<WorkflowBackupManifest>(manifestJson);
                        
                        if (manifest != null)
                        {
                            backups.Add(new WorkflowBackupInfo
                            {
                                BackupName = manifest.BackupName,
                                CreatedAt = manifest.CreatedAt,
                                TotalWorkflows = manifest.TotalWorkflows,
                                ScheduledWorkflows = manifest.ScheduledWorkflows,
                                BackupPath = backupDir,
                                Size = GetDirectorySize(backupDir)
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Failed to read backup manifest: {ManifestPath}", manifestPath);
                    }
                }
            }

            return backups.OrderByDescending(b => b.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error listing backups");
            return backups;
        }
    }

    /// <summary>
    /// Delete a backup
    /// </summary>
    public Task<bool> DeleteBackupAsync(string backupName)
    {
        try
        {
            var backupPath = Path.Combine(_backupPath, backupName);
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, recursive: true);
                _logger.LogInformation("✅ Deleted backup: {BackupName}", backupName);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to delete backup: {BackupName}", backupName);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Validate schedule preservation after migration
    /// </summary>
    public async Task<SchedulePreservationResult> ValidateSchedulePreservationAsync()
    {
        _logger.LogInformation("🔍 Validating schedule preservation");
        
        var result = new SchedulePreservationResult();

        try
        {
            // Get workflows from files
            var discoveryResult = await _migrationService.DiscoverWorkflowsAsync();
            if (!discoveryResult.IsSuccess)
            {
                result.IsValid = false;
                result.Issues.Add($"Failed to discover workflows: {discoveryResult.ErrorMessage}");
                return result;
            }

            // Get registered Quartz jobs
            var registeredJobs = await _quartzJobService.GetRegisteredWorkflowJobsAsync();

            var scheduledWorkflows = discoveryResult.DiscoveredWorkflows.Where(w => w.IsScheduled).ToList();
            
            foreach (var workflow in scheduledWorkflows)
            {
                var isRegistered = await _quartzJobService.IsWorkflowJobRegisteredAsync(workflow.Id);
                
                if (!isRegistered)
                {
                    result.Issues.Add($"Scheduled workflow {workflow.Id} is not registered in Quartz");
                }
                else
                {
                    result.PreservedSchedules++;
                    
                    // Additional validation could include checking actual schedule expressions
                    // This would require additional Quartz API calls to get trigger details
                }
            }

            result.TotalScheduledWorkflows = scheduledWorkflows.Count;
            result.IsValid = result.Issues.Count == 0;

            _logger.LogInformation("✅ Schedule preservation validation completed: {Valid}, {Preserved}/{Total} schedules preserved",
                result.IsValid, result.PreservedSchedules, result.TotalScheduledWorkflows);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error validating schedule preservation");
            result.IsValid = false;
            result.Issues.Add($"Validation failed: {ex.Message}");
            return result;
        }
    }

    #region Private Helper Methods

    private long GetDirectorySize(string directoryPath)
    {
        try
        {
            return Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
        }
        catch
        {
            return 0;
        }
    }

    #endregion
}
