using Agent.Models;
using Agent.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Services;

public class WorkflowService : IWorkflowService, IDisposable
{
    private readonly ILogger<WorkflowService> _logger;
    private readonly string _workflowsPath;
    private FileSystemWatcher? _workflowWatcher;
    private readonly Dictionary<string, WorkflowDefinition> _workflowCache;
    private readonly object _cacheLock = new object();

    public WorkflowService(
        ILogger<WorkflowService> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        _logger = logger;
        _workflowCache = new Dictionary<string, WorkflowDefinition>();

        // Get workflows path from configuration
        _workflowsPath =
            configuration["WorkflowSettings:WorkflowsPath"]
            ?? Path.Combine(environment.ContentRootPath, "Workflows");

        _logger.LogInformation($"Using workflows path from configuration: {_workflowsPath}");

        // Ensure the path exists
        if (!Directory.Exists(_workflowsPath))
        {
            _logger.LogWarning($"Workflows directory does not exist: {_workflowsPath}");

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

        // 🚀 SMART: Initialize FileSystemWatcher for real-time updates
        InitializeFileWatcher();
    }

    private void InitializeFileWatcher()
    {
        try
        {
            _workflowWatcher = new FileSystemWatcher(_workflowsPath)
            {
                Filter = "*.yaml",
                NotifyFilter =
                    NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };

            _workflowWatcher.Changed += OnWorkflowFileChanged;
            _workflowWatcher.Created += OnWorkflowFileChanged;
            _workflowWatcher.Deleted += OnWorkflowFileDeleted;

            _logger.LogInformation(
                "📁 FileSystemWatcher initialized for real-time workflow updates"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to initialize FileSystemWatcher - falling back to polling"
            );
        }
    }

    private async void OnWorkflowFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogInformation("🔄 Workflow file changed: {FileName}", e.Name);
            await LoadSingleWorkflowAsync(e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling workflow file change: {FilePath}", e.FullPath);
        }
    }

    private void OnWorkflowFileDeleted(object sender, FileSystemEventArgs e)
    {
        var workflowId = Path.GetFileNameWithoutExtension(e.Name);
        if (!string.IsNullOrEmpty(workflowId))
        {
            lock (_cacheLock)
            {
                if (_workflowCache.ContainsKey(workflowId))
                {
                    _workflowCache.Remove(workflowId);
                    _logger.LogInformation(
                        "🗑️ Removed deleted workflow from cache: {WorkflowId}",
                        workflowId
                    );
                }
            }
        }
    }

    public void Dispose()
    {
        _workflowWatcher?.Dispose();
    }

    private async Task LoadSingleWorkflowAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) || Path.GetFileName(filePath).StartsWith("."))
                return;

            var yamlContent = await File.ReadAllTextAsync(filePath);
            // Configure YAML deserializer with enhanced backward compatibility
            var deserializer = CreateBackwardCompatibleDeserializer();

            using var reader = new StringReader(yamlContent);
            var workflow = deserializer.Deserialize<WorkflowDefinition>(reader);

            if (string.IsNullOrEmpty(workflow.Id))
            {
                workflow.Id = Path.GetFileNameWithoutExtension(filePath);
            }

            lock (_cacheLock)
            {
                _workflowCache[workflow.Id] = workflow;
            }

            _logger.LogInformation(
                "✅ Updated workflow cache: {WorkflowName} (ID: {WorkflowId})",
                workflow.Name,
                workflow.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading single workflow: {FilePath}", filePath);
        }
    }

    public async Task<List<WorkflowDefinition>> LoadWorkflowsAsync()
    {
        // Check if cache is populated, if not do initial load
        lock (_cacheLock)
        {
            if (_workflowCache.Any())
            {
                _logger.LogDebug(
                    "🚀 CACHE HIT: Returning {Count} cached workflows",
                    _workflowCache.Count
                );
                return _workflowCache.Values.ToList();
            }
        }

        // Initial load or cache miss
        return await LoadWorkflowsFromDiskAsync();
    }

    private async Task<List<WorkflowDefinition>> LoadWorkflowsFromDiskAsync()
    {
        var workflows = new List<WorkflowDefinition>();

        if (!Directory.Exists(_workflowsPath))
        {
            _logger.LogError($"Workflows directory not found: {_workflowsPath}");
            return workflows;
        }

        // Log the directory contents for debugging purposes
        var files = Directory.GetFiles(_workflowsPath, "*.yaml").ToList();
        var ymlFiles = Directory.GetFiles(_workflowsPath, "*.yml").ToList();

        _logger.LogInformation(
            $"💾 INITIAL LOAD: Found {files.Count} .yaml files and {ymlFiles.Count} .yml files in {_workflowsPath}"
        );

        var allFiles = files.Concat(ymlFiles).ToList();

        if (allFiles.Count == 0)
        {
            _logger.LogWarning($"No workflow files found in {_workflowsPath}");
            return workflows;
        }

        // Configure YAML deserializer with enhanced backward compatibility
        var deserializer = CreateBackwardCompatibleDeserializer();

        foreach (var file in allFiles)
        {
            try
            {
                // Skip hidden files like .DS_Store
                if (Path.GetFileName(file).StartsWith("."))
                    continue;

                _logger.LogInformation($"📄 Reading workflow file: {file}");
                var yamlContent = await File.ReadAllTextAsync(file);

                _logger.LogDebug(
                    $"File content for {Path.GetFileName(file)}: {yamlContent.Substring(0, Math.Min(100, yamlContent.Length))}..."
                );

                using var reader = new StringReader(yamlContent);
                var workflow = deserializer.Deserialize<WorkflowDefinition>(reader);

                // If ID is missing, use the filename
                if (string.IsNullOrEmpty(workflow.Id))
                {
                    workflow.Id = Path.GetFileNameWithoutExtension(file);
                    _logger.LogWarning($"Workflow missing ID, using filename: {workflow.Id}");
                }

                workflows.Add(workflow);

                // Add to cache
                lock (_cacheLock)
                {
                    _workflowCache[workflow.Id] = workflow;
                }

                _logger.LogInformation(
                    $"✅ Successfully loaded workflow: {workflow.Name} (ID: {workflow.Id}) from {file} [CamelCase YAML parsing active]"
                );
            }
            catch (YamlDotNet.Core.YamlException yamlEx)
            {
                _logger.LogError(
                    yamlEx,
                    $"YAML parsing error in workflow file: {file}. Error: {yamlEx.Message}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading workflow file: {file}");
            }
        }

        return workflows;
    }

    public async Task<WorkflowDefinition?> GetWorkflowAsync(string workflowId)
    {
        var workflows = await LoadWorkflowsAsync();
        return workflows.FirstOrDefault(w => w.Id == workflowId);
    }

    public async Task SaveWorkflowAsync(WorkflowDefinition workflow)
    {
        if (string.IsNullOrEmpty(workflow.Id))
        {
            throw new ArgumentException("Workflow ID cannot be null or empty");
        }

        if (!Directory.Exists(_workflowsPath))
        {
            try
            {
                Directory.CreateDirectory(_workflowsPath);
                _logger.LogInformation($"Created workflows directory: {_workflowsPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create workflows directory: {_workflowsPath}");
                throw;
            }
        }

        // YAML serialization with camelCase naming convention to match YAML files
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        using var writer = new StringWriter();
        serializer.Serialize(writer, workflow);
        var yaml = writer.ToString();
        var filePath = Path.Combine(_workflowsPath, $"{workflow.Id}.yaml");

        try
        {
            await File.WriteAllTextAsync(filePath, yaml);
            _logger.LogInformation(
                $"Successfully saved workflow: {workflow.Name} (ID: {workflow.Id}) to {filePath}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                $"Failed to save workflow: {workflow.Name} (ID: {workflow.Id}) to {filePath}"
            );
            throw;
        }
    }

    /// <summary>
    /// Deletes a workflow file by its ID
    /// </summary>
    public Task<bool> DeleteWorkflowAsync(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("Cannot delete workflow: ID is null or empty");
                return Task.FromResult(false);
            }

            // Allow deletion of all workflows

            var yamlPath = Path.Combine(_workflowsPath, $"{id}.yaml");
            var ymlPath = Path.Combine(_workflowsPath, $"{id}.yml");

            if (File.Exists(yamlPath))
            {
                File.Delete(yamlPath);
                _logger.LogInformation($"Successfully deleted workflow file: {yamlPath}");
                return Task.FromResult(true);
            }
            else if (File.Exists(ymlPath))
            {
                File.Delete(ymlPath);
                _logger.LogInformation($"Successfully deleted workflow file: {ymlPath}");
                return Task.FromResult(true);
            }
            else
            {
                _logger.LogWarning($"Workflow file not found for ID: {id}");
                return Task.FromResult(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to delete workflow: {id}");
            return Task.FromResult(false);
        }
    }

    // Add method to check if a workflow file exists
    public bool WorkflowFileExists(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        var yamlPath = Path.Combine(_workflowsPath, $"{id}.yaml");
        var ymlPath = Path.Combine(_workflowsPath, $"{id}.yml");

        return File.Exists(yamlPath) || File.Exists(ymlPath);
    }

    /// <summary>
    /// Creates a YAML deserializer with enhanced backward compatibility
    /// Handles both PascalCase and camelCase properties, missing properties, and legacy formats
    /// </summary>
    private IDeserializer CreateBackwardCompatibleDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithTypeConverter(new BackwardCompatibleWorkflowConverter())
            .Build();
    }
}
