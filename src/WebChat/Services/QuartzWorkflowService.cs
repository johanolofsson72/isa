using System.Net.Http.Json;
using System.Text.Json;

namespace WebChat.Services;

/// <summary>
/// Service for managing workflows through Quartz.NET APIs
/// Replaces the old WorkflowSchedulingService with Quartz-based operations
/// </summary>
public class QuartzWorkflowService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<QuartzWorkflowService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public QuartzWorkflowService(HttpClient httpClient, ILogger<QuartzWorkflowService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Get all available workflows from Agent
    /// </summary>
    public async Task<List<WorkflowInfo>> GetWorkflowsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching workflows from Agent API");
            
            var response = await _httpClient.GetFromJsonAsync<List<WorkflowDefinitionDto>>(
                "/api/workflows", _jsonOptions);
            
            if (response == null)
            {
                _logger.LogWarning("No workflows returned from API");
                return new List<WorkflowInfo>();
            }

            var workflows = response.Select(w => new WorkflowInfo
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                Enabled = w.Enabled,
                ExecutionType = w.ExecutionType,
                Schedule = w.Schedule,
                LastExecuted = null, // These would come from Quartz job data
                NextExecutionTime = null
            }).ToList();

            _logger.LogInformation("Successfully fetched {Count} workflows", workflows.Count);
            return workflows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching workflows");
            return new List<WorkflowInfo>();
        }
    }

    /// <summary>
    /// Get scheduled workflows using Quartz.NET job information
    /// </summary>
    public async Task<List<WorkflowScheduleInfo>> GetScheduledWorkflowsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching scheduled workflows from Quartz API");
            
            // Get job information from Quartz admin API
            // Compatibility endpoint provided by Agent Monitoring endpoints
            var response = await _httpClient.GetFromJsonAsync<List<QuartzJobInfo>>(
                "/api/quartz/jobs", _jsonOptions);
            
            if (response == null)
            {
                _logger.LogWarning("No scheduled jobs returned from Quartz API");
                return new List<WorkflowScheduleInfo>();
            }

            var scheduledWorkflows = response
                .Where(job => job.Group == "workflows") // Only workflow jobs
                .Select(job => new WorkflowScheduleInfo
                {
                    Id = job.Name,
                    Name = job.Description ?? job.Name,
                    Description = job.JobData?.GetValueOrDefault("WorkflowDescription", "") ?? "",
                    Schedule = job.JobData?.GetValueOrDefault("OriginalSchedule", "") ?? "",
                    Enabled = job.State == "Normal",
                    LastExecuted = job.LastFireTime,
                    NextExecutionTime = job.NextFireTime
                }).ToList();

            _logger.LogInformation("Successfully fetched {Count} scheduled workflows", scheduledWorkflows.Count);
            return scheduledWorkflows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching scheduled workflows");
            return new List<WorkflowScheduleInfo>();
        }
    }

    /// <summary>
    /// Update workflow schedule using Quartz.NET APIs
    /// </summary>
    public async Task<bool> UpdateWorkflowScheduleAsync(string workflowId, WorkflowScheduleUpdateModel update)
    {
        try
        {
            _logger.LogInformation("Updating workflow schedule for {WorkflowId}", workflowId);
            
            var request = new QuartzScheduleUpdateRequest
            {
                WorkflowId = workflowId,
                Schedule = update.Schedule,
                Enabled = update.Enabled,
                Timezone = update.Timezone ?? "Europe/Stockholm",
                MisfireHandling = update.MisfireHandling ?? "Skip",
                OnSuccess = update.OnSuccess,
                OnError = update.OnError
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/quartz/schedule", request, _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated schedule for workflow {WorkflowId}", workflowId);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update schedule for workflow {WorkflowId}: {Error}", 
                workflowId, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow schedule for {WorkflowId}", workflowId);
            return false;
        }
    }

    /// <summary>
    /// Execute workflow immediately using Quartz.NET trigger
    /// </summary>
    public async Task<bool> TriggerWorkflowAsync(string workflowId)
    {
        try
        {
            _logger.LogInformation("Triggering immediate execution for workflow {WorkflowId}", workflowId);
            
            var response = await _httpClient.PostAsync(
                $"/api/quartz/trigger/{workflowId}", null);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully triggered workflow {WorkflowId}", workflowId);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to trigger workflow {WorkflowId}: {Error}", 
                workflowId, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering workflow {WorkflowId}", workflowId);
            return false;
        }
    }

    /// <summary>
    /// Pause a scheduled workflow
    /// </summary>
    public async Task<bool> PauseWorkflowAsync(string workflowId)
    {
        try
        {
            _logger.LogInformation("Pausing workflow {WorkflowId}", workflowId);
            
            var response = await _httpClient.PostAsync(
                $"/api/quartz/pause/{workflowId}", null);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing workflow {WorkflowId}", workflowId);
            return false;
        }
    }

    /// <summary>
    /// Resume a paused workflow
    /// </summary>
    public async Task<bool> ResumeWorkflowAsync(string workflowId)
    {
        try
        {
            _logger.LogInformation("Resuming workflow {WorkflowId}", workflowId);
            
            var response = await _httpClient.PostAsync(
                $"/api/quartz/resume/{workflowId}", null);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming workflow {WorkflowId}", workflowId);
            return false;
        }
    }

    /// <summary>
    /// Delete a scheduled workflow
    /// </summary>
    public async Task<bool> DeleteScheduledWorkflowAsync(string workflowId)
    {
        try
        {
            _logger.LogInformation("Deleting scheduled workflow {WorkflowId}", workflowId);
            
            var response = await _httpClient.DeleteAsync(
                $"/api/quartz/jobs/{workflowId}");

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting scheduled workflow {WorkflowId}", workflowId);
            return false;
        }
    }

    /// <summary>
    /// Get job execution history
    /// </summary>
    public async Task<List<JobExecutionRecord>> GetJobHistoryAsync(string? workflowId = null, int pageSize = 20)
    {
        try
        {
            var url = $"/api/JobHistory/executions?pageSize={pageSize}";
            if (!string.IsNullOrEmpty(workflowId))
            {
                url += $"&workflowId={workflowId}";
            }

            var response = await _httpClient.GetFromJsonAsync<JobHistoryResponse>(url, _jsonOptions);
            return response?.Items ?? new List<JobExecutionRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching job history");
            return new List<JobExecutionRecord>();
        }
    }

    /// <summary>
    /// Get job statistics for a workflow
    /// </summary>
    public async Task<JobStatistics?> GetJobStatisticsAsync(string workflowId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<JobStatistics>(
                $"/api/JobHistory/statistics?workflowId={workflowId}", _jsonOptions);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching job statistics for {WorkflowId}", workflowId);
            return null;
        }
    }
}

// Data models for Quartz integration
public class WorkflowInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string ExecutionType { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public DateTime? LastExecuted { get; set; }
    public DateTime? NextExecutionTime { get; set; }
}

public class WorkflowScheduleInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? LastExecuted { get; set; }
    public DateTime? NextExecutionTime { get; set; }
}

public class QuartzJobInfo
{
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime? LastFireTime { get; set; }
    public DateTime? NextFireTime { get; set; }
    public Dictionary<string, string>? JobData { get; set; }
}

public class QuartzScheduleUpdateRequest
{
    public string WorkflowId { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Timezone { get; set; } = "Europe/Stockholm";
    public string MisfireHandling { get; set; } = "Skip";
    public string? OnSuccess { get; set; }
    public string? OnError { get; set; }
}

public class JobHistoryResponse
{
    public List<JobExecutionRecord> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class JobExecutionRecord
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string JobKey { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? Result { get; set; }
}

public class JobStatistics
{
    public string WorkflowId { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public double SuccessRate { get; set; }
    public double FailureRate { get; set; }
    public double AverageExecutionTime { get; set; }
    public long MinExecutionTime { get; set; }
    public long MaxExecutionTime { get; set; }
    public DateTime? LastExecutionTime { get; set; }
    public string? LastExecutionStatus { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public bool HasRecentActivity { get; set; }
}
