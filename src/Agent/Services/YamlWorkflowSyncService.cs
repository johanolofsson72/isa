using System.Collections.Concurrent;
using Quartz;
using Agent.Models;
using Agent.Services;

namespace Agent.Services;

/// <summary>
/// Background service that synchronizes YAML workflow files with Quartz.NET jobs.
/// This service maintains the hybrid approach where YAML files are the source of truth
/// and Quartz.NET provides robust scheduling capabilities.
/// </summary>
public class YamlWorkflowSyncService : BackgroundService
{
    private readonly WorkflowService _workflowService;
    private readonly IScheduler _scheduler;
    private readonly QuartzJobRegistrationService _jobRegistrationService;
    private readonly ILogger<YamlWorkflowSyncService> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    // Track last sync times to prevent rapid successive updates
    private readonly ConcurrentDictionary<string, DateTime> _lastSyncTimes = new();
    
    // FileSystemWatcher for real-time YAML file monitoring
    private FileSystemWatcher? _fileWatcher;
    private readonly string _workflowsPath;

    public YamlWorkflowSyncService(
        WorkflowService workflowService,
        ISchedulerFactory schedulerFactory,
        QuartzJobRegistrationService jobRegistrationService,
        ILogger<YamlWorkflowSyncService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _workflowService = workflowService;
        _scheduler = schedulerFactory.GetScheduler().Result; // Get the default scheduler
        _jobRegistrationService = jobRegistrationService;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _workflowsPath = Path.Combine(Directory.GetCurrentDirectory(), "Workflows");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var instanceId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogWarning(
                "🚀 YAML-QUARTZ SYNC SERVICE STARTUP - Instance ID: {InstanceId}",
                instanceId);

            // Wait for Quartz scheduler to be ready
            await WaitForSchedulerReady(stoppingToken);

            // 1. Initial sync: Load all YAML workflows into Quartz
            await SyncAllWorkflowsToQuartz();

            // 2. Initialize FileSystemWatcher for real-time monitoring
            InitializeFileWatcher();

            // 3. Periodic validation and cleanup (every 5 minutes)
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    await ValidateQuartzJobsAgainstYaml();
                }
                catch (OperationCanceledException)
                {
                    break; // Normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during periodic validation");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in YamlWorkflowSyncService");
            throw;
        }
        finally
        {
            _fileWatcher?.Dispose();
        }
    }

    private async Task WaitForSchedulerReady(CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(30);
        var start = DateTime.UtcNow;

        while (!_scheduler.IsStarted && DateTime.UtcNow - start < timeout)
        {
            _logger.LogInformation("⏳ Waiting for Quartz scheduler to start...");
            await Task.Delay(1000, cancellationToken);
        }

        if (!_scheduler.IsStarted)
        {
            throw new InvalidOperationException("Quartz scheduler failed to start within timeout");
        }

        _logger.LogInformation("✅ Quartz scheduler is ready");
    }

    private async Task SyncAllWorkflowsToQuartz()
    {
        _logger.LogInformation("🔄 Starting initial sync of all YAML workflows to Quartz...");

        try
        {
            var workflows = await _workflowService.LoadWorkflowsAsync();
            var scheduledWorkflows = workflows.Where(w => 
                w.Enabled && 
                string.Equals(w.ExecutionType, "scheduled", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(w.Schedule)).ToList();

            _logger.LogWarning(
                "📊 SYNC SUMMARY: Found {ScheduledCount} scheduled workflows out of {TotalCount} total",
                scheduledWorkflows.Count, workflows.Count);

            foreach (var workflow in scheduledWorkflows)
            {
                try
                {
                    await _jobRegistrationService.RegisterWorkflowJobAsync(workflow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync workflow {WorkflowId} to Quartz", workflow.Id);
                }
            }

            _logger.LogWarning("✅ Initial sync completed: {Count} workflows synced to Quartz", scheduledWorkflows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial sync");
            throw;
        }
    }

    private void InitializeFileWatcher()
    {
        try
        {
            if (!Directory.Exists(_workflowsPath))
            {
                _logger.LogWarning("Workflows directory not found: {Path}", _workflowsPath);
                return;
            }

            _fileWatcher = new FileSystemWatcher(_workflowsPath)
            {
                Filter = "*.yaml",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _fileWatcher.Changed += OnWorkflowFileChanged;
            _fileWatcher.Created += OnWorkflowFileChanged;
            _fileWatcher.Deleted += OnWorkflowFileDeleted;

            _logger.LogInformation("📁 FileSystemWatcher initialized for YAML-Quartz sync");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FileSystemWatcher");
        }
    }

    private async void OnWorkflowFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Debounce multiple rapid changes
            var fileKey = e.FullPath;
            var now = DateTime.UtcNow;

            if (_lastSyncTimes.TryGetValue(fileKey, out var lastSync) && 
                (now - lastSync).TotalSeconds < 2)
            {
                return; // Skip rapid successive changes
            }

            _lastSyncTimes[fileKey] = now;

            _logger.LogInformation("📝 YAML file changed: {FileName}", e.Name);

            // Wait a bit for file write to complete
            await Task.Delay(500);

            // Sync specific workflow to Quartz
            await SyncWorkflowFileToQuartz(e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file change: {FilePath}", e.FullPath);
        }
    }

    private async void OnWorkflowFileDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            var workflowId = Path.GetFileNameWithoutExtension(e.Name);
            if (!string.IsNullOrEmpty(workflowId))
            {
                await _jobRegistrationService.UnregisterWorkflowJobAsync(workflowId);
                _logger.LogInformation("🗑️ Removed Quartz job for deleted workflow: {WorkflowId}", workflowId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file deletion: {FilePath}", e.FullPath);
        }
    }

    private async Task SyncWorkflowFileToQuartz(string filePath)
    {
        try
        {
            // Load workflow from file
            using var scope = _serviceProvider.CreateScope();
            var workflowService = scope.ServiceProvider.GetRequiredService<WorkflowService>();
            
            var workflows = await workflowService.LoadWorkflowsAsync();
            var workflowId = Path.GetFileNameWithoutExtension(filePath);
            var workflow = workflows.FirstOrDefault(w => w.Id == workflowId);

            if (workflow == null)
            {
                _logger.LogWarning("Workflow not found for file: {FilePath}", filePath);
                return;
            }

            await _jobRegistrationService.UpdateWorkflowJobAsync(workflow);
            _logger.LogInformation("🔄 Synced workflow {WorkflowId} to Quartz", workflow.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing workflow file {FilePath} to Quartz", filePath);
        }
    }







    private async Task ValidateQuartzJobsAgainstYaml()
    {
        try
        {
            _logger.LogDebug("🔍 Validating Quartz jobs against YAML workflows...");

            var workflows = await _workflowService.LoadWorkflowsAsync();
            var scheduledWorkflows = workflows.Where(w => 
                w.Enabled && 
                string.Equals(w.ExecutionType, "scheduled", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(w.Schedule)).ToList();

            var quartzJobs = await _jobRegistrationService.GetRegisteredWorkflowJobsAsync();

            // Check for missing Quartz jobs
            foreach (var workflow in scheduledWorkflows)
            {
                if (!await _jobRegistrationService.IsWorkflowJobRegisteredAsync(workflow.Id))
                {
                    _logger.LogWarning("🔧 Missing Quartz job for workflow {WorkflowId}, creating...", workflow.Id);
                    await _jobRegistrationService.RegisterWorkflowJobAsync(workflow);
                }
            }

            // Check for orphaned Quartz jobs
            foreach (var jobKey in quartzJobs)
            {
                var workflow = scheduledWorkflows.FirstOrDefault(w => w.Id == jobKey.Name);
                if (workflow == null)
                {
                    _logger.LogWarning("🗑️ Orphaned Quartz job found: {JobKey}, removing...", jobKey);
                    await _jobRegistrationService.UnregisterWorkflowJobAsync(jobKey.Name);
                }
            }

            _logger.LogDebug("✅ Validation completed: {ScheduledCount} workflows, {QuartzCount} jobs", 
                scheduledWorkflows.Count, quartzJobs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation");
        }
    }
}
