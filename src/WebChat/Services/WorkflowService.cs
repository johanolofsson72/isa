using System.IO;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WebChat.Services;

/// <summary>
/// Provides API interactions for workflow management
/// </summary>
public class WorkflowService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WorkflowService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _workflowsPath;

    // Tracking running workflows - key is workflow id, value is (startTime, status)
    private readonly Dictionary<string, (DateTime StartTime, string Status)> _runningWorkflows =
        new();

    // Event for workflow status changes
    public event Action<string, string>? OnWorkflowStatusChanged;

    /// <summary>
    /// Gets the base address of the agent API for debugging
    /// </summary>
    public string GetBaseAddress()
    {
        return _httpClient.BaseAddress?.ToString() ?? "No base address set";
    }

    /// <summary>
    /// Exposes HttpClient for direct API calls from UI components
    /// </summary>
    public HttpClient HttpClient => _httpClient;

    public WorkflowService(
        IHttpClientFactory httpClientFactory,
        ILogger<WorkflowService> logger,
        IConfiguration configuration
    )
    {
        _httpClient = httpClientFactory.CreateClient("agent");
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        // Get workflows path from configuration
        _workflowsPath =
            configuration["WorkflowSettings:WorkflowsPath"]
            ?? "/path/to/accountingagent/src/Agent/Workflows/";

        _logger.LogInformation($"Using workflows path from configuration: {_workflowsPath}");

        // Ensure the path exists
        if (!Directory.Exists(_workflowsPath))
        {
            _logger.LogWarning($"Configured workflows directory does not exist: {_workflowsPath}");

            // Try to create the directory
            try
            {
                Directory.CreateDirectory(_workflowsPath);
                _logger.LogInformation($"Created workflows directory: {_workflowsPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create workflows directory: {_workflowsPath}");
            }
        }

        _logger.LogInformation($"Using workflows path: {_workflowsPath}");

        // Check if the directory exists and log detailed info about its contents
        if (Directory.Exists(_workflowsPath))
        {
            var files = Directory.GetFiles(_workflowsPath, "*.yaml").ToList();
            var ymlFiles = Directory.GetFiles(_workflowsPath, "*.yml").ToList();

            _logger.LogInformation(
                $"Found {files.Count} .yaml files and {ymlFiles.Count} .yml files in {_workflowsPath}"
            );

            foreach (var file in files.Concat(ymlFiles))
            {
                _logger.LogInformation(
                    $"Workflow file: {Path.GetFileName(file)} ({new FileInfo(file).Length} bytes)"
                );
                try
                {
                    // Log first few lines of each file for debugging
                    var firstLines = File.ReadLines(file).Take(3).ToArray();
                    _logger.LogInformation(
                        $"First lines of {Path.GetFileName(file)}: {string.Join(" | ", firstLines)}"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error reading first lines of workflow file: {file}");
                }
            }
        }
        else
        {
            _logger.LogError($"Workflows directory not found: {_workflowsPath}");
        }
    }

    /// <summary>
    /// Gets all available workflows
    /// </summary>
    public async Task<List<WorkflowModel>> GetWorkflowsAsync()
    {
        try
        {
            _logger.LogInformation("Getting workflows - will try both API and file system");
            var allWorkflows = new List<WorkflowModel>();

            // Always try to get workflows from files first as a reliable source
            var fileWorkflows = ReadWorkflowsFromFiles();
            _logger.LogInformation($"Read {fileWorkflows.Count} workflows from file system");
            allWorkflows.AddRange(fileWorkflows);

            // Try to get workflows from the API as well
            try
            {
                _logger.LogInformation("Attempting to fetch workflows from API");
                var apiWorkflows = await _httpClient.GetFromJsonAsync<List<WorkflowModel>>(
                    "/api/workflows",
                    _jsonOptions
                );

                if (apiWorkflows != null && apiWorkflows.Count > 0)
                {
                    _logger.LogInformation(
                        $"Successfully fetched {apiWorkflows.Count} workflows from API"
                    );

                    // Merge API workflows with file workflows, giving priority to API data
                    foreach (var apiWorkflow in apiWorkflows)
                    {
                        var existingIndex = allWorkflows.FindIndex(w => w.Id == apiWorkflow.Id);
                        if (existingIndex >= 0)
                        {
                            // Replace file-based workflow with API data
                            allWorkflows[existingIndex] = apiWorkflow;
                        }
                        else
                        {
                            // Add new workflow from API
                            allWorkflows.Add(apiWorkflow);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("API returned empty workflow list");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not fetch workflows from API, using file system data only"
                );
            }

            _logger.LogInformation($"Total workflows available: {allWorkflows.Count}");
            return allWorkflows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching workflows, returning empty list");
            return new List<WorkflowModel>();
        }
    }

    /// <summary>
    /// Gets scheduled workflow tasks
    /// </summary>
    public async Task<List<WorkflowScheduleModel>> GetScheduledWorkflowsAsync()
    {
        try
        {
            _logger.LogInformation("Getting all workflows and filtering for scheduled ones");

            // Hämta alla workflows först
            var allWorkflows = await GetWorkflowsAsync();

            _logger.LogInformation(
                $"Got {allWorkflows.Count} total workflows, filtering for scheduled ones"
            );

            // Debug: Log all workflow types
            foreach (var w in allWorkflows)
            {
                _logger.LogInformation(
                    $"Workflow found: Id={w.Id}, Name={w.Name}, ExecutionType={w.ExecutionType}, HasSchedule={!string.IsNullOrEmpty(w.Schedule)}"
                );
            }

            // Filtrera ut bara de som är schemalagda - OBS vi kollar både "scheduled" och "Scheduled" för att vara säkra
            var scheduledWorkflows = allWorkflows
                .Where(w =>
                    // Match "scheduled" case-insensitive
                    (
                        w.ExecutionType?.ToLowerInvariant() == "scheduled"
                        || w.ExecutionType?.ToLowerInvariant() == "schedule"
                    ) && !string.IsNullOrEmpty(w.Schedule)
                )
                .Select(w => new WorkflowScheduleModel
                {
                    Id = w.Id,
                    Name = w.Name,
                    Description = w.Description,
                    Schedule = w.Schedule ?? string.Empty,
                    Enabled = w.Enabled,
                    LastExecuted = null, // Vi har inte denna info från GetWorkflowsAsync
                    NextExecutionTime = CalculateNextRunTime(w.Schedule ?? ""), // Calculate next run time
                })
                .ToList();

            _logger.LogInformation(
                $"Found {scheduledWorkflows.Count} scheduled workflows after filtering"
            );

            // Logga varje schemalagt arbetsflöde
            foreach (var workflow in scheduledWorkflows)
            {
                _logger.LogInformation(
                    $"Scheduled workflow: {workflow.Id} - {workflow.Name} - Schedule: {workflow.Schedule} - Enabled: {workflow.Enabled} - Next Run: {workflow.NextExecutionTime}"
                );
            }

            if (scheduledWorkflows.Count > 0)
            {
                return scheduledWorkflows;
            }

            _logger.LogWarning(
                "No scheduled workflows found, adding hardcoded example for testing"
            );

            // Lägg till en hårdkodad exempelschemaläggning för felsökning
            return new List<WorkflowScheduleModel>
            {
                new WorkflowScheduleModel
                {
                    Id = "daily-morning-report-workflow",
                    Name = "Daglig Morgonrapport (Hårdkodad för test)",
                    Description = "Genererar och skickar en daglig rapport varje vardag kl 9:00",
                    Schedule = "0 9 * * 1-5",
                    Enabled = true,
                    LastExecuted = null,
                    NextExecutionTime = DateTime.Now.Date.AddDays(1).AddHours(9),
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduled workflows");
            return new List<WorkflowScheduleModel>();
        }
    }

    // Helper method to calculate next run time from a cron expression
    private DateTime? CalculateNextRunTime(string cronExpression)
    {
        if (string.IsNullOrEmpty(cronExpression))
            return null;

        try
        {
            // Simple implementation for common patterns
            // In a real app, you would use a cron parser library

            // Parse parts
            var parts = cronExpression.Split(' ');
            if (parts.Length != 5)
                return null;

            var minute = parts[0];
            var hour = parts[1];
            var dayOfMonth = parts[2];
            var month = parts[3];
            var dayOfWeek = parts[4];

            var now = DateTime.Now;
            var result = now;

            // For simplicity, let's just handle some common cases

            // Daily at specific hour (e.g., "0 9 * * *" for 9:00 AM daily)
            if (hour != "*" && dayOfMonth == "*" && month == "*" && dayOfWeek == "*")
            {
                var hourValue = int.Parse(hour);
                var minuteValue = minute == "*" ? 0 : int.Parse(minute);

                result = new DateTime(now.Year, now.Month, now.Day, hourValue, minuteValue, 0);
                if (result <= now)
                    result = result.AddDays(1);

                return result;
            }

            // Weekdays (e.g., "0 9 * * 1-5" for 9:00 AM Monday-Friday)
            if (dayOfWeek == "1-5")
            {
                var hourValue = int.Parse(hour);
                var minuteValue = minute == "*" ? 0 : int.Parse(minute);

                result = new DateTime(now.Year, now.Month, now.Day, hourValue, minuteValue, 0);

                // If today is weekend or time has passed, find next weekday
                if (now.DayOfWeek == DayOfWeek.Saturday)
                    result = result.AddDays(2); // Next Monday
                else if (now.DayOfWeek == DayOfWeek.Sunday)
                    result = result.AddDays(1); // Next Monday
                else if (result <= now)
                {
                    // Add one day, and if it's Saturday, add 2 more
                    result = result.AddDays(1);
                    if (result.DayOfWeek == DayOfWeek.Saturday)
                        result = result.AddDays(2);
                }

                return result;
            }

            // For more complex expressions, fall back to tomorrow at same time
            return now.AddDays(1);
        }
        catch
        {
            return null;
        }
    }

        /// <summary>
        /// Public helper to expose next execution calculation to UI components
        /// </summary>
        public DateTime? GetNextExecutionTime(string? cronExpression)
        {
            if (string.IsNullOrWhiteSpace(cronExpression)) return null;
            return CalculateNextRunTime(cronExpression);
        }


    /// <summary>
    /// Gets a workflow by its ID
    /// </summary>
    public async Task<WorkflowModel?> GetWorkflowAsync(string id)
    {
        try
        {
            _logger.LogInformation($"GetWorkflowAsync called with ID: '{id}'");

            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("GetWorkflowAsync called with null or empty ID");
                return null;
            }

            // First try to get from the file system as the primary source
            _logger.LogInformation(
                $"Looking for workflow {id} in file system at: {_workflowsPath}"
            );
            var (exists, filePath) = WorkflowExists(id);

            if (exists && filePath != null)
            {
                _logger.LogInformation($"Found workflow {id} at {filePath}, reading content");
                try
                {
                    var yaml = File.ReadAllText(filePath);
                    _logger.LogDebug(
                        $"File content for {id}: {yaml.Substring(0, Math.Min(200, yaml.Length))}..."
                    );

                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build();

                    var workflowYaml = deserializer.Deserialize<WorkflowYamlModel>(yaml);

                    // No standard workflow IDs - all workflows are now file-based
                    var standardWorkflowIds = new string[0];

                    // Convert YAML model to our WorkflowModel
                    var workflow = new WorkflowModel
                    {
                        Id = workflowYaml.Id ?? Path.GetFileNameWithoutExtension(filePath),
                        Name = workflowYaml.Name ?? Path.GetFileNameWithoutExtension(filePath),
                        Description = workflowYaml.Description ?? "No description available",
                        Enabled = workflowYaml.Enabled,
                        ExecutionType = workflowYaml.ExecutionType ?? "manual",
                        Schedule = workflowYaml.Schedule,
                        OnSuccess = workflowYaml.OnSuccess,
                        OnError = workflowYaml.OnError,
                        IsStandard =
                            standardWorkflowIds.Contains(workflowYaml.Id)
                            || standardWorkflowIds.Contains(
                                Path.GetFileNameWithoutExtension(filePath)
                            ),
                        Steps = ConvertYamlStepsToStepModels(workflowYaml),
                    };

                    _logger.LogInformation(
                        $"Successfully loaded workflow {id} from file system: {workflow.Name}"
                    );
                    return workflow;
                }
                catch (Exception fileEx)
                {
                    _logger.LogError(fileEx, $"Error parsing workflow file {filePath}");
                }
            }
            else
            {
                _logger.LogWarning($"Workflow {id} not found in file system");

                // Let's also log what files DO exist
                try
                {
                    var files = Directory
                        .GetFiles(_workflowsPath, "*.yaml")
                        .Concat(Directory.GetFiles(_workflowsPath, "*.yml"))
                        .ToList();
                    _logger.LogInformation(
                        $"Available workflow files: {string.Join(", ", files.Select(Path.GetFileName))}"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error listing workflow files");
                }
            }

            // If file system fails, try the API as a fallback
            try
            {
                _logger.LogInformation($"Attempting to fetch workflow {id} from API as fallback");
                var workflowDefinition = await _httpClient.GetFromJsonAsync<WorkflowDefinitionDto>(
                    $"/api/workflows/{id}",
                    _jsonOptions
                );

                if (workflowDefinition != null)
                {
                    _logger.LogInformation($"Successfully fetched workflow {id} from API");
                    var workflow = ConvertWorkflowDefinitionToModel(workflowDefinition);
                    return workflow;
                }

                _logger.LogWarning($"API returned null for workflow {id}");
            }
            catch (Exception apiEx)
            {
                _logger.LogWarning(apiEx, $"Could not fetch workflow {id} from API");
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching workflow {WorkflowId}", id);
            return null;
        }
    }

    /// <summary>
    /// Updates a workflow schedule
    /// </summary>
    public async Task<bool> UpdateWorkflowScheduleAsync(
        string id,
        WorkflowScheduleUpdateModel update
    )
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(update, _jsonOptions),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PutAsync($"/api/workflows/{id}/schedule", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow schedule {WorkflowId}", id);
            return false;
        }
    }

    /// <summary>
    /// Runs a workflow immediately
    /// </summary>
    public async Task<bool> RunWorkflowAsync(string id)
    {
        try
        {
            // Mark as running
            _runningWorkflows[id] = (DateTime.Now, "Running");
            OnWorkflowStatusChanged?.Invoke(id, "Running");

            _logger.LogInformation($"Starting workflow execution for: {id}");

            // Use a CancellationToken with a 4 minute timeout (less than HttpClient's 5 minute timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));

            var response = await _httpClient.PostAsync($"/api/workflows/{id}/run", null, cts.Token);

            // Update status based on response
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Workflow execution completed successfully for: {id}");
                _runningWorkflows[id] = (DateTime.Now, "Completed");
                OnWorkflowStatusChanged?.Invoke(id, "Completed");

                // Start a timer to clear the status after 10 seconds
                _ = Task.Run(async () =>
                {
                    await Task.Delay(10000);
                    if (
                        _runningWorkflows.ContainsKey(id)
                        && _runningWorkflows[id].Status == "Completed"
                    )
                    {
                        _runningWorkflows.Remove(id);
                        OnWorkflowStatusChanged?.Invoke(id, "");
                    }
                });

                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    $"Workflow execution failed for {id}: {response.StatusCode} - {errorContent}"
                );

                _runningWorkflows[id] = (DateTime.Now, $"Failed: {response.StatusCode}");
                OnWorkflowStatusChanged?.Invoke(id, $"Failed: {response.StatusCode}");
                return false;
            }
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            _logger.LogError($"Workflow execution timed out for {id}");
            _runningWorkflows[id] = (DateTime.Now, "Timeout: Workflow execution took too long");
            OnWorkflowStatusChanged?.Invoke(id, "Timeout: Workflow execution took too long");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error running workflow {WorkflowId}", id);
            _runningWorkflows[id] = (DateTime.Now, $"Network Error: {ex.Message}");
            OnWorkflowStatusChanged?.Invoke(id, $"Network Error: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running workflow {WorkflowId}", id);
            _runningWorkflows[id] = (DateTime.Now, $"Error: {ex.Message}");
            OnWorkflowStatusChanged?.Invoke(id, $"Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Triggers a workflow immediately (alias for RunWorkflowAsync)
    /// </summary>
    public async Task<bool> TriggerWorkflowAsync(string id)
    {
        return await RunWorkflowAsync(id);
    }

    /// <summary>
    /// Starts tracking a workflow run
    /// </summary>
    public void StartWorkflowRun(string id)
    {
        _runningWorkflows[id] = (DateTime.UtcNow, "Running");
        OnWorkflowStatusChanged?.Invoke(id, "Running");
    }

    /// <summary>
    /// Stops tracking a workflow run
    /// </summary>
    public void StopWorkflowRun(string id, string status)
    {
        if (_runningWorkflows.ContainsKey(id))
        {
            _runningWorkflows[id] = (_runningWorkflows[id].StartTime, status);
            OnWorkflowStatusChanged?.Invoke(id, status);
        }
    }

    /// <summary>
    /// Gets the status of a running workflow
    /// </summary>
    public string GetWorkflowRunStatus(string id)
    {
        return _runningWorkflows.TryGetValue(id, out var value) ? value.Status : "Not running";
    }

    /// <summary>
    /// Gets the current running status of a workflow
    /// </summary>
    public string GetWorkflowRunningStatus(string id)
    {
        if (_runningWorkflows.TryGetValue(id, out var status))
        {
            return status.Status;
        }

        return "";
    }

    /// <summary>
    /// Checks if a workflow is currently running
    /// </summary>
    public bool IsWorkflowRunning(string id)
    {
        return _runningWorkflows.TryGetValue(id, out var status) && status.Status == "Running";
    }

    /// <summary>
    /// Reads workflow files from the Agent/Workflows directory
    /// </summary>
    private List<WorkflowModel> ReadWorkflowsFromFiles()
    {
        var workflows = new List<WorkflowModel>();

        if (!Directory.Exists(_workflowsPath))
        {
            _logger.LogError($"Workflows directory not found: {_workflowsPath}");

            // As a fallback, create static workflow models for the two standard workflows
            AddStaticStandardWorkflows(workflows);

            return workflows;
        }

        // Try both .yaml and .yml files
        var files = new List<string>();
        files.AddRange(Directory.GetFiles(_workflowsPath, "*.yaml"));
        files.AddRange(Directory.GetFiles(_workflowsPath, "*.yml"));

        _logger.LogInformation($"Found {files.Count} workflow files to process");

        if (files.Count == 0)
        {
            _logger.LogWarning(
                $"No workflow files found in {_workflowsPath}, adding static workflows as fallback"
            );
            AddStaticStandardWorkflows(workflows);
            return workflows;
        }

        foreach (var file in files)
        {
            try
            {
                // Skip hidden files like .DS_Store
                if (Path.GetFileName(file).StartsWith("."))
                    continue;

                _logger.LogInformation($"Reading workflow file: {file}");
                var yaml = File.ReadAllText(file);

                _logger.LogDebug(
                    $"File content for {Path.GetFileName(file)}: {yaml.Substring(0, Math.Min(100, yaml.Length))}..."
                );

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                try
                {
                    var workflowYaml = deserializer.Deserialize<WorkflowYamlModel>(yaml);

                    // No standard workflow IDs - all workflows are now file-based
                    var standardWorkflowIds = new string[0];

                    // Convert YAML model to our WorkflowModel
                    var workflow = new WorkflowModel
                    {
                        Id = workflowYaml.Id ?? Path.GetFileNameWithoutExtension(file),
                        Name = workflowYaml.Name ?? Path.GetFileNameWithoutExtension(file),
                        Description = workflowYaml.Description ?? "No description available",
                        Enabled = workflowYaml.Enabled,
                        ExecutionType = workflowYaml.ExecutionType ?? "manual",
                        Schedule = workflowYaml.Schedule,
                        OnSuccess = workflowYaml.OnSuccess,
                        OnError = workflowYaml.OnError,
                        IsStandard =
                            standardWorkflowIds.Contains(workflowYaml.Id)
                            || standardWorkflowIds.Contains(Path.GetFileNameWithoutExtension(file)),
                    };

                    _logger.LogInformation(
                        $"Successfully parsed workflow: {workflow.Id} - {workflow.Name}"
                    );
                    workflows.Add(workflow);
                }
                catch (YamlDotNet.Core.YamlException yamlEx)
                {
                    _logger.LogError(
                        yamlEx,
                        $"YAML parsing error in workflow file: {file}. Error: {yamlEx.Message}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading workflow file: {file}");
            }
        }

        if (workflows.Count == 0)
        {
            _logger.LogWarning(
                "No workflows could be parsed from files, adding static workflows as fallback"
            );
            AddStaticStandardWorkflows(workflows);
        }

        _logger.LogInformation($"Total workflows loaded: {workflows.Count}");
        return workflows;
    }

    /// <summary>
    /// Adds static workflow models for the standard workflows as a fallback
    /// </summary>
    private void AddStaticStandardWorkflows(List<WorkflowModel> workflows)
    {
        // No static workflows added - all workflows are now file-based
        _logger.LogInformation("No static standard workflows added - using file-based workflows only");
    }

    /// <summary>
    /// Deletes a workflow by its ID
    /// </summary>
    public async Task<bool> DeleteWorkflowAsync(string id)
    {
        try
        {
            // Allow deletion of all workflows

            // Try to delete via API
            var encodedId = Uri.EscapeDataString(id);
            var response = await _httpClient.DeleteAsync($"/api/workflows/{encodedId}");
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully deleted workflow: {id}");
                return true;
            }

            _logger.LogWarning($"Failed to delete workflow: {id}. Status: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting workflow: {id}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a workflow exists in the file system
    /// </summary>
    private (bool exists, string? filePath) WorkflowExists(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return (false, null);
        }

        var yamlPath = Path.Combine(_workflowsPath, $"{id}.yaml");
        var ymlPath = Path.Combine(_workflowsPath, $"{id}.yml");

        if (File.Exists(yamlPath))
        {
            return (true, yamlPath);
        }
        else if (File.Exists(ymlPath))
        {
            return (true, ymlPath);
        }

        return (false, null);
    }

    /// <summary>
    /// Creates a new workflow
    /// </summary>
    public async Task<bool> CreateWorkflowAsync(WorkflowModel workflow)
    {
        try
        {
            // Generate ID if not provided
            if (string.IsNullOrEmpty(workflow.Id))
            {
                workflow.Id = GenerateWorkflowId(workflow.Name);
            }

            // Try to create via API first
            var createRequest = new WorkflowCreateRequestDto
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description,
                ExecutionType = workflow.ExecutionType,
                Schedule = workflow.Schedule,
                Enabled = workflow.Enabled,
                OnSuccess = workflow.OnSuccess,
                OnError = workflow.OnError,
                Steps = workflow
                    .Steps.Select(s => new WorkflowStepRequestDto
                    {
                        Name = s.Name,
                        Content = s.Content,
                        // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
                        Order = s.Order,
                        Timeout = s.Timeout,
                        Retries = s.Retries,
                        OnSuccess = s.OnSuccess,
                        OnFailure = s.OnFailure,
                    })
                    .ToList(),
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/workflows",
                createRequest,
                _jsonOptions
            );
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully created workflow via API: {workflow.Id}");
                return true;
            }

            // Fallback: Create as YAML file
            var success = await CreateWorkflowFileAsync(workflow);
            if (success)
            {
                _logger.LogInformation($"Successfully created workflow file: {workflow.Id}");
                return true;
            }

            _logger.LogWarning($"Failed to create workflow: {workflow.Id}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating workflow: {workflow.Id}");
            return false;
        }
    }

    /// <summary>
    /// Updates an existing workflow
    /// </summary>
    public async Task<bool> UpdateWorkflowAsync(WorkflowModel workflow)
    {
        try
        {
            // Try to update via API first
            var updateRequest = new WorkflowUpdateRequestDto
            {
                Name = workflow.Name,
                Description = workflow.Description,
                ExecutionType = workflow.ExecutionType,
                Schedule = workflow.Schedule,
                Enabled = workflow.Enabled,
                OnSuccess = workflow.OnSuccess,
                OnError = workflow.OnError,
                Steps = workflow
                    .Steps.Select(s => new WorkflowStepRequestDto
                    {
                        Name = s.Name,
                        Content = s.Content,
                        // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
                        Order = s.Order,
                        Timeout = s.Timeout,
                        Retries = s.Retries,
                        OnSuccess = s.OnSuccess,
                        OnFailure = s.OnFailure,
                    })
                    .ToList(),
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"/api/workflows/{workflow.Id}",
                updateRequest,
                _jsonOptions
            );
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully updated workflow via API: {workflow.Id}");
                return true;
            }

            // Fallback: Update YAML file
            var success = await UpdateWorkflowFileAsync(workflow);
            if (success)
            {
                _logger.LogInformation($"Successfully updated workflow file: {workflow.Id}");
                return true;
            }

            _logger.LogWarning($"Failed to update workflow: {workflow.Id}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating workflow: {workflow.Id}");
            return false;
        }
    }

    /// <summary>
    /// Creates a workflow YAML file
    /// </summary>
    private async Task<bool> CreateWorkflowFileAsync(WorkflowModel workflow)
    {
        try
        {
            var yamlContent = GenerateWorkflowYaml(workflow);
            var filePath = Path.Combine(_workflowsPath, $"{workflow.Id}.yaml");

            await File.WriteAllTextAsync(filePath, yamlContent);
            _logger.LogInformation($"Created workflow file: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to create workflow file for: {workflow.Id}");
            return false;
        }
    }

    /// <summary>
    /// Updates a workflow YAML file
    /// </summary>
    private async Task<bool> UpdateWorkflowFileAsync(WorkflowModel workflow)
    {
        try
        {
            var (exists, filePath) = WorkflowExists(workflow.Id);
            if (!exists || filePath == null)
            {
                // File doesn't exist, create it
                return await CreateWorkflowFileAsync(workflow);
            }

            var yamlContent = GenerateWorkflowYaml(workflow);
            await File.WriteAllTextAsync(filePath, yamlContent);
            _logger.LogInformation($"Updated workflow file: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to update workflow file for: {workflow.Id}");
            return false;
        }
    }

    /// <summary>
    /// Generates a workflow ID from name
    /// </summary>
    private string GenerateWorkflowId(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return $"workflow-{Guid.NewGuid():N}";
        }

        // Convert name to kebab-case
        var id = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace("å", "a")
            .Replace("ä", "a")
            .Replace("ö", "o")
            .Replace("é", "e")
            .Replace("ü", "u");

        // Remove non-alphanumeric characters except hyphens
        id = new string(id.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        // Ensure it doesn't end with "-workflow"
        if (!id.EndsWith("-workflow"))
        {
            id += "-workflow";
        }

        return id;
    }

    /// <summary>
    /// Generates YAML content for a workflow
    /// </summary>
    private string GenerateWorkflowYaml(WorkflowModel workflow)
    {
        var yamlWorkflow = new WorkflowYaml
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            ExecutionType = workflow.ExecutionType,
            Schedule = workflow.Schedule,
            Enabled = workflow.Enabled,
            SystemPrompt = "default",
            LastExecuted = null,
            NextExecutionTime = null,
            OnSuccess = workflow.OnSuccess,
            OnError = workflow.OnError,
            Jobs = new List<WorkflowJobYaml>
            {
                new WorkflowJobYaml
                {
                    Name = "Main Job",
                    Description = "Auto-generated job for workflow steps",
                    Order = 1,
                    Enabled = true,
                    Steps = workflow
                        .Steps.Select(
                            (step, index) =>
                                new WorkflowStepYaml
                                {
                                    Id = $"step{index + 1}",
                                    Prompt = step.Content,
                                    Order = index + 1,
                                    ActionType = "prompt", // 🎯 AI väljer verktyg automatiskt
                                    // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
                                    Timeout = step.Timeout > 0 ? step.Timeout : 300,
                                    Retries = step.Retries >= 0 ? step.Retries : 2,
                                    OnSuccess = !string.IsNullOrWhiteSpace(step.OnSuccess)
                                        ? step.OnSuccess
                                        : (index == workflow.Steps.Count - 1 ? "completed" : $"step{index + 2}"),
                                    OnFailure = !string.IsNullOrWhiteSpace(step.OnFailure) ? step.OnFailure : "failed",
                                }
                        )
                        .ToList(),
                },
            },
        };

        // If no steps provided, add a default step
        if (yamlWorkflow.Jobs.Count == 0 || yamlWorkflow.Jobs[0].Steps.Count == 0)
        {
            if (yamlWorkflow.Jobs.Count == 0)
            {
                yamlWorkflow.Jobs.Add(
                    new WorkflowJobYaml
                    {
                        Name = "Main Job",
                        Description = "Auto-generated job for workflow steps",
                        Order = 1,
                        Enabled = true,
                        Steps = new List<WorkflowStepYaml>(),
                    }
                );
            }

            yamlWorkflow
                .Jobs[0]
                .Steps.Add(
                    new WorkflowStepYaml
                    {
                        Id = "step1",
                        Prompt =
                            "Detta är ett exempel på ett arbetsflödessteg. Redigera detta för att anpassa till dina behov.",
                        Order = 1,
                        ActionType = "prompt",
                        Timeout = 300,
                        Retries = 2,
                        OnSuccess = "completed",
                        OnFailure = "failed",
                    }
                );
        }

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return serializer.Serialize(yamlWorkflow);
    }

    /// <summary>
    /// Converts WorkflowDefinition from Agent API to WorkflowModel for WebChat
    /// </summary>
    private WorkflowModel ConvertWorkflowDefinitionToModel(WorkflowDefinitionDto workflowDefinition)
    {
        var steps = new List<WorkflowStepModel>();

        // Extract steps from jobs
        if (workflowDefinition.Jobs?.Any() == true)
        {
            foreach (var job in workflowDefinition.Jobs.OrderBy(j => j.Order))
            {
                if (job.Steps?.Any() == true)
                {
                    foreach (var step in job.Steps.OrderBy(s => s.Order))
                    {
                        steps.Add(
                            new WorkflowStepModel
                            {
                                Name = step.Id ?? $"Steg {step.Order}",
                                Content = step.Prompt ?? "",
                                // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
                                Order = step.Order,
                                Timeout = step.Timeout,
                                Retries = step.Retries,
                                OnSuccess = step.OnSuccess,
                                OnFailure = step.OnFailure,
                            }
                        );
                    }
                }
            }
        }

        return new WorkflowModel
        {
            Id = workflowDefinition.Id,
            Name = workflowDefinition.Name,
            Description = workflowDefinition.Description,
            Enabled = workflowDefinition.Enabled,
            ExecutionType = workflowDefinition.ExecutionType,
            Schedule = workflowDefinition.Schedule,
            OnSuccess = workflowDefinition.OnSuccess,
            OnError = workflowDefinition.OnError,
            Steps = steps,
        };
    }

    /// <summary>
    /// Converts YAML workflow steps to WorkflowStepModel list
    /// </summary>
    private List<WorkflowStepModel> ConvertYamlStepsToStepModels(WorkflowYamlModel yamlWorkflow)
    {
        var steps = new List<WorkflowStepModel>();

        // If there are direct steps in the workflow
        if (yamlWorkflow.Jobs?.Any() == true)
        {
            foreach (var job in yamlWorkflow.Jobs.OrderBy(j => j.Order))
            {
                if (job.Steps?.Any() == true)
                {
                    foreach (var yamlStep in job.Steps.OrderBy(s => s.Order))
                    {
                        steps.Add(
                            new WorkflowStepModel
                            {
                                Name = yamlStep.Id ?? $"Steg {yamlStep.Order}",
                                Content = yamlStep.Prompt ?? "",
                                // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
                                Order = yamlStep.Order,
                                Timeout = yamlStep.Timeout,
                                Retries = yamlStep.Retries,
                                OnSuccess = yamlStep.OnSuccess,
                                OnFailure = yamlStep.OnFailure,
                            }
                        );
                    }
                }
            }
        }

        return steps;
    }

    /// <summary>
    /// YAML model for workflow files
    /// </summary>
    private class WorkflowYamlModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string ExecutionType { get; set; } = string.Empty;
        public string? Schedule { get; set; }
        public string SystemPrompt { get; set; } = string.Empty;
        public string? OnSuccess { get; set; }
        public string? OnError { get; set; }
        public List<WorkflowJobYaml> Jobs { get; set; } = new();
    }

    /// <summary>
    /// YAML model for workflow jobs
    /// </summary>
    private class WorkflowJobYaml
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool Enabled { get; set; }
        public List<WorkflowStepYaml> Steps { get; set; } = new();
    }

    /// <summary>
    /// YAML model for workflow steps
    /// </summary>
    private class WorkflowStepYaml
    {
        public string Id { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public int Order { get; set; }
        public string ActionType { get; set; } = string.Empty;
        // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
        public int Timeout { get; set; }
        public int Retries { get; set; }
        public string? OnSuccess { get; set; }
        public string? OnFailure { get; set; }
    }

    /// <summary>
    /// YAML model for workflow files (simple version for generation)
    /// </summary>
    private class WorkflowYaml
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string ExecutionType { get; set; } = string.Empty;
        public string? Schedule { get; set; }
        public string SystemPrompt { get; set; } = "default";
        public DateTime? LastExecuted { get; set; }
        public DateTime? NextExecutionTime { get; set; }
        public string? OnSuccess { get; set; }
        public string? OnError { get; set; }
        public List<WorkflowJobYaml> Jobs { get; set; } = new();
    }

    /// <summary>
    /// Beräknar nästa exekveringstid baserat på ett cron-uttryck
    /// </summary>
    private DateTime? CalculateNextExecutionTime(string? cronExpression)
    {
        if (string.IsNullOrEmpty(cronExpression))
        {
            return null;
        }

        try
        {
            // Very basic cron parsing for common formats
            // Format: minute hour day-of-month month day-of-week
            var parts = cronExpression.Split(' ');
            if (parts.Length < 5)
            {
                _logger.LogWarning(
                    $"Invalid cron expression: {cronExpression} - incorrect number of parts"
                );
                return null;
            }

            // För enkla vardagsscheman (t.ex. "0 9 * * 1-5" för vardagar kl 9)
            var now = DateTime.Now;
            var tomorrow = now.AddDays(1);

            // Detta är en förenklad implementering som fungerar för typiska scheman
            // För en fullständig cron-implementation skulle ett bibliotek som Cronos eller NCrontab behövas
            if (cronExpression.Contains("* * *"))
            {
                // För dagliga scheman, enkelt sätt nästa dag vid angiven tid
                return tomorrow.Date.AddHours(9); // Antar 9:00 för exemplets skull
            }
            else if (cronExpression.StartsWith("0 9") && cronExpression.EndsWith("1-5"))
            {
                // För vardag morgonscheman kl 9
                var nextDay = tomorrow;
                // Säkerställ att nästa dag är en vardag (1-5 är måndag-fredag)
                while (
                    nextDay.DayOfWeek == DayOfWeek.Saturday || nextDay.DayOfWeek == DayOfWeek.Sunday
                )
                {
                    nextDay = nextDay.AddDays(1);
                }
                return nextDay.Date.AddHours(9);
            }

            // Fallback till imorgon samma tid
            return tomorrow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error parsing cron expression: {cronExpression}");
            return null;
        }
    }

    /// <summary>
    /// Duplicerar ett arbetsflöde med ett nytt namn och ID
    /// </summary>
    public async Task<bool> DuplicateWorkflowAsync(string id, string newName = "")
    {
        try
        {
            _logger.LogInformation($"Attempting to duplicate workflow {id}");

            // Hämta det ursprungliga arbetsflödet
            var originalWorkflow = await GetWorkflowAsync(id);
            if (originalWorkflow == null)
            {
                _logger.LogWarning($"Cannot duplicate workflow {id} - not found");
                return false;
            }

            // Skapa en kopia med nytt ID och namn
            var duplicatedWorkflow = new WorkflowModel
            {
                Id = GenerateWorkflowId(originalWorkflow.Name + "-copy"),
                Name = string.IsNullOrEmpty(newName) ? originalWorkflow.Name + " (kopia)" : newName,
                Description = originalWorkflow.Description,
                ExecutionType = originalWorkflow.ExecutionType,
                Schedule = originalWorkflow.Schedule,
                Enabled = originalWorkflow.Enabled,
                Steps = originalWorkflow
                    .Steps.Select(s => new WorkflowStepModel
                    {
                        Name = s.Name,
                        Content = s.Content,
                        // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
                        Order = s.Order,
                    })
                    .ToList(),
            };

            // Skapa det nya arbetsflödet
            var success = await CreateWorkflowAsync(duplicatedWorkflow);
            if (success)
            {
                _logger.LogInformation(
                    $"Successfully duplicated workflow {id} to {duplicatedWorkflow.Id}"
                );
            }
            else
            {
                _logger.LogWarning($"Failed to duplicate workflow {id}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error duplicating workflow {id}");
            return false;
        }
    }
}

/// <summary>
/// DTO for creating workflows via API
/// </summary>
public class WorkflowCreateRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string ExecutionType { get; set; } = "manual";
    public string? Schedule { get; set; }
    public string? OnSuccess { get; set; }
    public string? OnError { get; set; }
    public List<WorkflowStepRequestDto> Steps { get; set; } = new();
}

/// <summary>
/// DTO for updating workflows via API
/// </summary>
public class WorkflowUpdateRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string ExecutionType { get; set; } = string.Empty;
    public string? Schedule { get; set; }
    public string? OnSuccess { get; set; }
    public string? OnError { get; set; }
    public List<WorkflowStepRequestDto> Steps { get; set; } = new();
}

/// <summary>
/// DTO for workflow steps in API requests
/// </summary>
public class WorkflowStepRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
    public int Order { get; set; }
    public int Timeout { get; set; } = 300;
    public int Retries { get; set; } = 2;
    public string? OnSuccess { get; set; }
    public string? OnFailure { get; set; }
}

/// <summary>
/// DTO for receiving WorkflowDefinition from Agent API
/// </summary>
public class WorkflowDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string ExecutionType { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public string? OnSuccess { get; set; }
    public string? OnError { get; set; }
    public List<WorkflowJobDto> Jobs { get; set; } = new();
}

/// <summary>
/// DTO for workflow jobs from Agent API
/// </summary>
public class WorkflowJobDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool Enabled { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
}

/// <summary>
/// DTO for workflow steps from Agent API
/// </summary>
public class WorkflowStepDto
{
    public string Id { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int Order { get; set; }
    public string ActionType { get; set; } = string.Empty;
    // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
    public int Timeout { get; set; }
    public int Retries { get; set; }
    public string OnSuccess { get; set; } = string.Empty;
    public string OnFailure { get; set; } = string.Empty;
}

// Models for workflows
public class WorkflowModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string ExecutionType { get; set; } = string.Empty;
    public string? Schedule { get; set; }
    public string? OnSuccess { get; set; }
    public string? OnError { get; set; }
    public bool IsStandard { get; set; } // Added to identify standard workflows
    public List<WorkflowStepModel> Steps { get; set; } = new();
}

public class WorkflowStepModel
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    // 🎯 ToolName BORTTAGET: AI väljer verktyg automatiskt
    public int Order { get; set; }
    public int Timeout { get; set; } = 300;
    public int Retries { get; set; } = 2;
    public string? OnSuccess { get; set; }
    public string? OnFailure { get; set; }
}

public class WorkflowScheduleModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? LastExecuted { get; set; }
    public DateTime? NextExecutionTime { get; set; }
}

public class WorkflowScheduleUpdateModel
{
    public string Schedule { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? Timezone { get; set; }
    public string? MisfireHandling { get; set; }
    public string? OnSuccess { get; set; }
    public string? OnError { get; set; }
}
