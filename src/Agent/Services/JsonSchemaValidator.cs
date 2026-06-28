using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Models;

namespace Agent.Services;

/// <summary>
/// Service for validating JSON schema and structure
/// </summary>
public class JsonSchemaValidator
{
    private readonly ILogger<JsonSchemaValidator> _logger;

    public JsonSchemaValidator(ILogger<JsonSchemaValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates users.json structure and content
    /// </summary>
    public JsonValidationResult ValidateUsersJson(string jsonContent)
    {
        try
        {
            var usersCatalog = JsonSerializer.Deserialize<UsersCatalog>(jsonContent);
            if (usersCatalog == null)
            {
                return JsonValidationResult.Failed("Failed to deserialize users.json");
            }

            var validationContext = new ValidationContext(usersCatalog);
            var validationResults = new List<ValidationResult>();

            // Validate the root object
            if (!Validator.TryValidateObject(usersCatalog, validationContext, validationResults, true))
            {
                var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
                return JsonValidationResult.Failed($"Users catalog validation failed: {errors}");
            }

            // Validate individual users
            if (usersCatalog.Users != null)
            {
                for (int i = 0; i < usersCatalog.Users.Count; i++)
                {
                    var user = usersCatalog.Users[i];
                    var userValidationContext = new ValidationContext(user);
                    var userValidationResults = new List<ValidationResult>();

                    if (!Validator.TryValidateObject(user, userValidationContext, userValidationResults, true))
                    {
                        var errors = string.Join("; ", userValidationResults.Select(r => r.ErrorMessage));
                        return JsonValidationResult.Failed($"User {i} validation failed: {errors}");
                    }

                    // Validate contacts
                    if (user.Contacts != null)
                    {
                        var contactsValidationContext = new ValidationContext(user.Contacts);
                        var contactsValidationResults = new List<ValidationResult>();

                        if (!Validator.TryValidateObject(user.Contacts, contactsValidationContext, contactsValidationResults, true))
                        {
                            var errors = string.Join("; ", contactsValidationResults.Select(r => r.ErrorMessage));
                            return JsonValidationResult.Failed($"User {user.Name} contacts validation failed: {errors}");
                        }
                    }
                }
            }

            _logger.LogDebug("✅ users.json validation passed ({UserCount} users)", usersCatalog.Users?.Count ?? 0);
            return JsonValidationResult.Success($"Valid users.json with {usersCatalog.Users?.Count ?? 0} users");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ JSON parsing error in users.json");
            return JsonValidationResult.Failed($"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error validating users.json");
            return JsonValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates plugins.json structure and content
    /// </summary>
    public JsonValidationResult ValidatePluginsJson(string jsonContent)
    {
        try
        {
            var pluginsCatalog = JsonSerializer.Deserialize<PluginsCatalog>(jsonContent);
            if (pluginsCatalog == null)
            {
                return JsonValidationResult.Failed("Failed to deserialize plugins.json");
            }

            var validationContext = new ValidationContext(pluginsCatalog);
            var validationResults = new List<ValidationResult>();

            // Validate the root object
            if (!Validator.TryValidateObject(pluginsCatalog, validationContext, validationResults, true))
            {
                var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
                return JsonValidationResult.Failed($"Plugins catalog validation failed: {errors}");
            }

            // Validate individual plugins
            if (pluginsCatalog.Plugins != null)
            {
                for (int i = 0; i < pluginsCatalog.Plugins.Count; i++)
                {
                    var plugin = pluginsCatalog.Plugins[i];
                    var pluginValidationContext = new ValidationContext(plugin);
                    var pluginValidationResults = new List<ValidationResult>();

                    if (!Validator.TryValidateObject(plugin, pluginValidationContext, pluginValidationResults, true))
                    {
                        var errors = string.Join("; ", pluginValidationResults.Select(r => r.ErrorMessage));
                        return JsonValidationResult.Failed($"Plugin {i} ({plugin.Name}) validation failed: {errors}");
                    }

                    // Validate functions
                    if (plugin.Functions != null)
                    {
                        for (int j = 0; j < plugin.Functions.Count; j++)
                        {
                            var function = plugin.Functions[j];
                            var functionValidationContext = new ValidationContext(function);
                            var functionValidationResults = new List<ValidationResult>();

                            if (!Validator.TryValidateObject(function, functionValidationContext, functionValidationResults, true))
                            {
                                var errors = string.Join("; ", functionValidationResults.Select(r => r.ErrorMessage));
                                return JsonValidationResult.Failed($"Plugin {plugin.Name} function {j} ({function.Name}) validation failed: {errors}");
                            }
                        }
                    }
                }
            }

            _logger.LogDebug("✅ plugins.json validation passed ({PluginCount} plugins)", pluginsCatalog.Plugins?.Count ?? 0);
            return JsonValidationResult.Success($"Valid plugins.json with {pluginsCatalog.Plugins?.Count ?? 0} plugins");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ JSON parsing error in plugins.json");
            return JsonValidationResult.Failed($"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error validating plugins.json");
            return JsonValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that required files exist and are readable
    /// </summary>
    public JsonValidationResult ValidateFileExistence(string filePath, string fileType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonValidationResult.Failed($"{fileType} path is null or empty");
            }

            if (!File.Exists(filePath))
            {
                return JsonValidationResult.Failed($"{fileType} file not found: {filePath}");
            }

            // Try to read the file
            var content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                return JsonValidationResult.Failed($"{fileType} file is empty: {filePath}");
            }

            _logger.LogDebug("✅ {FileType} file exists and is readable: {FilePath}", fileType, filePath);
            return JsonValidationResult.Success($"{fileType} file is valid");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "❌ Access denied reading {FileType} file: {FilePath}", fileType, filePath);
            return JsonValidationResult.Failed($"Access denied reading {fileType} file: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error reading {FileType} file: {FilePath}", fileType, filePath);
            return JsonValidationResult.Failed($"Error reading {fileType} file: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs comprehensive validation of all JSON files
    /// </summary>
    public async Task<ValidationSummary> ValidateAllAsync(string systemPromptPath, string usersJsonPath, string pluginsJsonPath)
    {
        var summary = new ValidationSummary();

        // Validate file existence
        summary.SystemPromptValidation = ValidateFileExistence(systemPromptPath, "System prompt");
        summary.UsersJsonValidation = ValidateFileExistence(usersJsonPath, "users.json");
        summary.PluginsJsonValidation = ValidateFileExistence(pluginsJsonPath, "plugins.json");

        // Validate JSON content if files exist
        if (summary.UsersJsonValidation.IsValid)
        {
            try
            {
                var usersContent = await File.ReadAllTextAsync(usersJsonPath);
                summary.UsersJsonValidation = ValidateUsersJson(usersContent);
            }
            catch (Exception ex)
            {
                summary.UsersJsonValidation = JsonValidationResult.Failed($"Error reading users.json: {ex.Message}");
            }
        }

        if (summary.PluginsJsonValidation.IsValid)
        {
            try
            {
                var pluginsContent = await File.ReadAllTextAsync(pluginsJsonPath);
                summary.PluginsJsonValidation = ValidatePluginsJson(pluginsContent);
            }
            catch (Exception ex)
            {
                summary.PluginsJsonValidation = JsonValidationResult.Failed($"Error reading plugins.json: {ex.Message}");
            }
        }

        summary.IsOverallValid = summary.SystemPromptValidation.IsValid && 
                                summary.UsersJsonValidation.IsValid && 
                                summary.PluginsJsonValidation.IsValid;

        _logger.LogInformation("📋 Validation summary: Overall={IsValid}, SystemPrompt={SP}, Users={U}, Plugins={P}",
            summary.IsOverallValid ? "✅" : "❌",
            summary.SystemPromptValidation.IsValid ? "✅" : "❌",
            summary.UsersJsonValidation.IsValid ? "✅" : "❌",
            summary.PluginsJsonValidation.IsValid ? "✅" : "❌");

        return summary;
    }
}

/// <summary>
/// Result of a validation operation
/// </summary>
public class JsonValidationResult
{
    public bool IsValid { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public List<string> Errors { get; private set; } = new();

    private JsonValidationResult() { }

    public static JsonValidationResult Success(string message = "Validation passed")
    {
        return new JsonValidationResult { IsValid = true, Message = message };
    }

    public static JsonValidationResult Failed(string error)
    {
        return new JsonValidationResult
        {
            IsValid = false,
            Message = "Validation failed",
            Errors = new List<string> { error }
        };
    }

    public static JsonValidationResult Failed(IEnumerable<string> errors)
    {
        return new JsonValidationResult
        {
            IsValid = false,
            Message = "Validation failed",
            Errors = errors.ToList()
        };
    }
}

/// <summary>
/// Summary of all validation results
/// </summary>
public class ValidationSummary
{
    public bool IsOverallValid { get; set; }
    public JsonValidationResult SystemPromptValidation { get; set; } = JsonValidationResult.Success();
    public JsonValidationResult UsersJsonValidation { get; set; } = JsonValidationResult.Success();
    public JsonValidationResult PluginsJsonValidation { get; set; } = JsonValidationResult.Success();
}
