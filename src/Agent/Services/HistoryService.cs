using Agent.Models;
using System.Text.Json;

namespace Agent.Services;

public class HistoryService
{
    private readonly ILogger<HistoryService> _logger;
    private readonly string _historyPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public HistoryService(ILogger<HistoryService> logger, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _logger = logger;
        
        // Get history path from configuration or use default
        var basePath = configuration["HistorySettings:HistoryPath"] 
            ?? Path.Combine(environment.ContentRootPath, "Data");
        
        _historyPath = Path.Combine(basePath, "history.json");
        
        _logger.LogInformation($"Using history file: {_historyPath}");
        
        // Ensure the directory exists
        var directory = Path.GetDirectoryName(_historyPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
            _logger.LogInformation($"Created history directory: {directory}");
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<List<WorkflowExecutionHistory>> GetHistoryAsync()
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                _logger.LogInformation("History file does not exist, returning empty list");
                return new List<WorkflowExecutionHistory>();
            }

            var json = await File.ReadAllTextAsync(_historyPath);
            var history = JsonSerializer.Deserialize<List<WorkflowExecutionHistory>>(json, _jsonOptions);
            
            return history ?? new List<WorkflowExecutionHistory>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading history file");
            return new List<WorkflowExecutionHistory>();
        }
    }

    public async Task SaveExecutionAsync(WorkflowExecutionHistory execution)
    {
        try
        {
            var history = await GetHistoryAsync();
            
            // Add new execution at the beginning (most recent first)
            history.Insert(0, execution);
            
            // Keep only the last 100 executions to prevent file from growing too large
            if (history.Count > 100)
            {
                history = history.Take(100).ToList();
            }

            var json = JsonSerializer.Serialize(history, _jsonOptions);
            await File.WriteAllTextAsync(_historyPath, json);
            
            _logger.LogInformation($"Saved workflow execution history: {execution.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving execution history");
        }
    }

    public async Task<WorkflowExecutionHistory> StartExecutionAsync(string workflowId, string workflowName, string triggerType = "Manual")
    {
        var execution = new WorkflowExecutionHistory
        {
            Id = Guid.NewGuid().ToString(),
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            StartTime = DateTime.UtcNow,
            Status = "Running",
            TriggerType = triggerType,
            ExecutionLog = new List<string> { $"Started execution at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC" }
        };

        await SaveExecutionAsync(execution);
        return execution;
    }

    public async Task CompleteExecutionAsync(string executionId, bool success, string? errorMessage = null)
    {
        try
        {
            var history = await GetHistoryAsync();
            var execution = history.FirstOrDefault(h => h.Id == executionId);
            
            if (execution != null)
            {
                execution.EndTime = DateTime.UtcNow;
                execution.Status = success ? "Completed" : "Failed";
                execution.ErrorMessage = errorMessage;
                execution.ExecutionLog.Add($"Execution {execution.Status.ToLower()} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                
                if (!success && !string.IsNullOrEmpty(errorMessage))
                {
                    execution.ExecutionLog.Add($"Error: {errorMessage}");
                }

                var json = JsonSerializer.Serialize(history, _jsonOptions);
                await File.WriteAllTextAsync(_historyPath, json);
                
                _logger.LogInformation($"Completed workflow execution: {executionId} - {execution.Status}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing execution history");
        }
    }

    public async Task<WorkflowExecutionHistory?> GetHistoryByIdAsync(string executionId)
    {
        try
        {
            var history = await GetHistoryAsync();
            return history.FirstOrDefault(h => h.Id == executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history by ID: {ExecutionId}", executionId);
            return null;
        }
    }

    public async Task AddExecutionLogAsync(string executionId, string logMessage)
    {
        try
        {
            var history = await GetHistoryAsync();
            var execution = history.FirstOrDefault(h => h.Id == executionId);
            
            if (execution != null)
            {
                execution.ExecutionLog.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC: {logMessage}");
                
                var json = JsonSerializer.Serialize(history, _jsonOptions);
                await File.WriteAllTextAsync(_historyPath, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding execution log");
        }
    }
}
