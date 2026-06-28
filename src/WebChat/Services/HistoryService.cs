using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WebChat.Services;

/// <summary>
/// Provides API interactions for chat and workflow history
/// </summary>
public class HistoryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HistoryService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public HistoryService(IHttpClientFactory httpClientFactory, ILogger<HistoryService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("agent");
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
    }

    /// <summary>
    /// Gets workflow execution history and maps to chat history format
    /// </summary>
    public async Task<List<HistoryEntryModel>> GetHistoryEntriesAsync(string? filter = null)
    {
        try
        {
            var url = "/api/history";
            if (!string.IsNullOrEmpty(filter))
            {
                url += $"?filter={filter}";
            }

            _logger.LogInformation("🔍 [WebChat History] Requesting history from Agent API: {Url}", url);
            _logger.LogInformation("🔗 [WebChat History] Using HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            _logger.LogInformation("🕐 [WebChat History] Request timeout: {Timeout}", _httpClient.Timeout);

            // Test connection first
            _logger.LogInformation("🧪 [WebChat History] Testing Agent API connection...");
            
            // The API returns workflow execution history, not chat history
            var response = await _httpClient.GetFromJsonAsync<List<WorkflowExecutionHistoryModel>>(
                url,
                _jsonOptions
            );

            _logger.LogInformation("📊 [WebChat History] Received {Count} entries from Agent API", response?.Count ?? 0);
            
            if (response == null || response.Count == 0)
            {
                _logger.LogWarning("⚠️ [WebChat History] No history data received from Agent API");
                return new List<HistoryEntryModel>();
            }

            // If API returns empty list or null, fall back to dummy data
            if (response == null || response.Count == 0)
            {
                _logger.LogWarning("⚠️ [WebChat History] API returned empty history, using dummy data");
                return GetDummyHistoryEntries();
            }

            // Map workflow execution history to chat history format
            var historyEntries = response
                .Select(w => new HistoryEntryModel
                {
                    Id = w.Id,
                    Title = $"{w.WorkflowName}",
                    Preview = CreateWorkflowPreview(w),
                    Timestamp = w.ExecutedAt,
                    MessageCount = w.Duration.HasValue ? (int)w.Duration.Value.TotalSeconds : 1,
                })
                .ToList();

            // Apply filter if specified
            if (!string.IsNullOrEmpty(filter))
            {
                var now = DateTime.UtcNow;
                historyEntries = filter.ToLower() switch
                {
                    "today" => historyEntries.Where(h => h.Timestamp.Date == now.Date).ToList(),
                    "week" => historyEntries.Where(h => h.Timestamp >= now.AddDays(-7)).ToList(),
                    "month" => historyEntries.Where(h => h.Timestamp >= now.AddDays(-30)).ToList(),
                    _ => historyEntries,
                };
            }

            return historyEntries;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "❌ [WebChat History] HTTP error connecting to Agent API: {Message}", httpEx.Message);
            _logger.LogError("🌐 [WebChat History] Failed to connect to: {BaseAddress}/api/history", _httpClient.BaseAddress);
            
            // Return dummy data until API is accessible
            return GetDummyHistoryEntries();
        }
        catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
        {
            _logger.LogError(tcEx, "⏰ [WebChat History] Request timeout to Agent API");
            return GetDummyHistoryEntries();
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "📄 [WebChat History] JSON parsing error from Agent API response");
            return GetDummyHistoryEntries();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 [WebChat History] Unexpected error fetching history entries: {Message}", ex.Message);

            // Return dummy data for now until the API endpoint is working
            return GetDummyHistoryEntries();
        }
    }

    /// <summary>
    /// Gets workflow history (previous runs)
    /// </summary>
    public async Task<List<WorkflowHistoryModel>> GetWorkflowHistoryAsync(string? workflowId = null)
    {
        try
        {
            var url = "/api/workflows/history";
            if (!string.IsNullOrEmpty(workflowId))
            {
                url += $"?workflowId={workflowId}";
            }

            var response = await _httpClient.GetFromJsonAsync<List<WorkflowHistoryModel>>(
                url,
                _jsonOptions
            );

            // If API returns empty list or null, fall back to dummy data
            if (response == null || response.Count == 0)
            {
                _logger.LogInformation("API returned empty workflow history, using dummy data");
                return GetDummyWorkflowHistory();
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching workflow history");

            // Return dummy data for now until the API endpoint is implemented
            return GetDummyWorkflowHistory();
        }
    }

    /// <summary>
    /// Gets detail for a specific chat conversation
    /// </summary>
    public async Task<ConversationDetailModel?> GetConversationDetailAsync(string id)
    {
        try
        {
            // In a real implementation, we'd call the API here
            // For now, return mock data based on the requested ID

            // Uncomment this when the API is available
            /*
            return await _httpClient.GetFromJsonAsync<ConversationDetailModel>(
                $"/api/history/{id}", _jsonOptions);
            */

            // Simulate network delay
            await Task.Delay(800);

            return id switch
            {
                "1" => GetDummyBudgetConversation(),
                "2" => GetDummyInvoiceConversation(),
                "3" => GetDummyTaxConversation(),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching conversation {ConversationId}", id);
            return null;
        }
    }

    /// <summary>
    /// Creates a user-friendly preview text for workflow execution history
    /// </summary>
    private static string CreateWorkflowPreview(WorkflowExecutionHistoryModel workflow)
    {
        var statusText = workflow.Status.ToLower() switch
        {
            "completed" => "✅ Completed successfully",
            "failed" => "❌ Failed",
            "running" => "🔄 Running",
            "cancelled" => "⏹️ Cancelled",
            _ => $"📋 {workflow.Status}",
        };

        var durationText = workflow.Duration.HasValue
            ? $" in {workflow.Duration.Value.TotalSeconds:F1}s"
            : "";

        var errorText = !string.IsNullOrEmpty(workflow.ErrorMessage)
            ? $" - {workflow.ErrorMessage}"
            : "";

        return $"{statusText}{durationText} at {workflow.ExecutedAt:HH:mm}{errorText}";
    }

    // TODO: Remove this method when the real API is implemented
    private List<HistoryEntryModel> GetDummyHistoryEntries()
    {
        return new List<HistoryEntryModel>
        {
            new HistoryEntryModel
            {
                Id = "1",
                Title = "Budget planning assistance",
                Preview =
                    "Let's analyze your spending categories and create a monthly budget plan...",
                Timestamp = DateTime.Now.AddDays(-1),
                MessageCount = 12,
            },
            new HistoryEntryModel
            {
                Id = "2",
                Title = "Invoice processing help",
                Preview = "I need assistance with processing multiple vendor invoices...",
                Timestamp = DateTime.Now.AddHours(-5),
                MessageCount = 8,
            },
            new HistoryEntryModel
            {
                Id = "3",
                Title = "Tax preparation questions",
                Preview = "What documents do I need for filing my quarterly taxes?",
                Timestamp = DateTime.Now.AddDays(-2),
                MessageCount = 15,
            },
        };
    }

    // Implementation moved to the original GetConversationDetailAsync method above

    private ConversationDetailModel GetDummyBudgetConversation()
    {
        return new ConversationDetailModel
        {
            Id = "1",
            Title = "Budget planning assistance",
            Timestamp = DateTime.Now.AddDays(-1),
            Messages = new List<MessageModel>
            {
                new MessageModel
                {
                    Id = "msg1",
                    Text =
                        "I need help creating a monthly budget plan based on my spending habits.",
                    IsUser = true,
                    Timestamp = DateTime.Now.AddDays(-1).AddMinutes(-30),
                },
                new MessageModel
                {
                    Id = "msg2",
                    Text =
                        "I'd be happy to help you create a monthly budget plan! To get started, I'll need some information about your income and regular expenses. Would you like me to access your recent transactions from your bank account to analyze your spending patterns?",
                    IsUser = false,
                    Timestamp = DateTime.Now.AddDays(-1).AddMinutes(-29),
                },
                new MessageModel
                {
                    Id = "msg3",
                    Text = "Yes, please analyze my transactions from the last 3 months.",
                    IsUser = true,
                    Timestamp = DateTime.Now.AddDays(-1).AddMinutes(-28),
                },
                new MessageModel
                {
                    Id = "msg4",
                    Text =
                        "I've analyzed your transactions from the past 3 months. Here's a breakdown of your average monthly spending by category:\n\n• Housing: $1,450\n• Groceries: $580\n• Dining out: $320\n• Transportation: $210\n• Utilities: $175\n• Entertainment: $150\n• Shopping: $290\n• Miscellaneous: $125\n\nYour average monthly income is $3,500. Based on this information, you're spending about $3,300 per month, which leaves $200 for savings.",
                    IsUser = false,
                    Timestamp = DateTime.Now.AddDays(-1).AddMinutes(-25),
                    Actions = new List<MessageActionModel>
                    {
                        new MessageActionModel
                        {
                            Type = "file",
                            Description = "Generated spending analysis report",
                        },
                        new MessageActionModel
                        {
                            Type = "drive",
                            Description = "Saved budget-planning-2023.xlsx to Drive",
                        },
                    },
                },
                new MessageModel
                {
                    Id = "msg5",
                    Text = "Can you suggest how I can improve my budget to save more?",
                    IsUser = true,
                    Timestamp = DateTime.Now.AddDays(-1).AddMinutes(-22),
                },
                new MessageModel
                {
                    Id = "msg6",
                    Text =
                        "Based on your spending patterns, here are some recommendations to increase your monthly savings:\n\n1. Dining out: Consider reducing from $320 to $240 by cooking at home more often.\n2. Shopping: Try to limit discretionary purchases to $200 instead of $290.\n3. Entertainment: Reduce from $150 to $100 by finding free or lower-cost activities.\n\nThese adjustments would free up an additional $220 per month, increasing your potential savings from $200 to $420.",
                    IsUser = false,
                    Timestamp = DateTime.Now.AddDays(-1).AddMinutes(-21),
                },
            },
        };
    }

    private ConversationDetailModel GetDummyInvoiceConversation()
    {
        return new ConversationDetailModel
        {
            Id = "2",
            Title = "Invoice processing help",
            Timestamp = DateTime.Now.AddHours(-5),
            Messages = new List<MessageModel>
            {
                new MessageModel
                {
                    Id = "msg1",
                    Text =
                        "I need assistance with processing multiple vendor invoices that came in today.",
                    IsUser = true,
                    Timestamp = DateTime.Now.AddHours(-5).AddMinutes(-15),
                },
                new MessageModel
                {
                    Id = "msg2",
                    Text =
                        "I'll help you process those vendor invoices. Do you have the invoice files available? If so, you can upload them, and I can extract the key information to prepare them for processing.",
                    IsUser = false,
                    Timestamp = DateTime.Now.AddHours(-5).AddMinutes(-14),
                },
                new MessageModel
                {
                    Id = "msg3",
                    Text = "Yes, I've uploaded the 3 invoices to the drive folder.",
                    IsUser = true,
                    Timestamp = DateTime.Now.AddHours(-5).AddMinutes(-12),
                },
                new MessageModel
                {
                    Id = "msg4",
                    Text =
                        "I've accessed the 3 invoices from your drive folder and extracted the following information:\n\nInvoice #INV-2023-456 from ABC Supplies:\n- Amount: $1,245.78\n- Due date: October 15, 2023\n- Items: Office supplies and equipment\n\nInvoice #89012 from XYZ Services:\n- Amount: $850.00\n- Due date: October 20, 2023\n- Services: IT consulting (20 hours)\n\nInvoice #PO-7734-B from Metropolis Utilities:\n- Amount: $367.42\n- Due date: October 18, 2023\n- Services: Utilities for September\n\nWould you like me to prepare these for payment processing?",
                    IsUser = false,
                    Timestamp = DateTime.Now.AddHours(-5).AddMinutes(-10),
                    Actions = new List<MessageActionModel>
                    {
                        new MessageActionModel
                        {
                            Type = "drive",
                            Description = "Accessed 3 invoice files from Drive",
                        },
                        new MessageActionModel
                        {
                            Type = "file",
                            Description = "Created invoice_summary.xlsx",
                        },
                    },
                },
            },
        };
    }

    private ConversationDetailModel GetDummyTaxConversation()
    {
        return new ConversationDetailModel
        {
            Id = "3",
            Title = "Tax preparation questions",
            Timestamp = DateTime.Now.AddDays(-2),
            Messages = new List<MessageModel>
            {
                new MessageModel
                {
                    Id = "msg1",
                    Text = "What documents do I need for filing my quarterly taxes?",
                    IsUser = true,
                    Timestamp = DateTime.Now.AddDays(-2).AddHours(-3),
                },
                new MessageModel
                {
                    Id = "msg2",
                    Text =
                        "For your quarterly estimated tax payments, you'll need the following documents:\n\n1. Income records:\n   - Sales records or invoices\n   - 1099 forms from clients\n   - Records of other income sources\n\n2. Expense records:\n   - Receipts for business expenses\n   - Home office expenses\n   - Vehicle mileage logs\n   - Health insurance payments\n\n3. Previous tax records:\n   - Last year's tax return\n   - Previous quarterly tax payments for this year\n\nWould you like me to help you organize these documents or calculate your estimated payment?",
                    IsUser = false,
                    Timestamp = DateTime.Now.AddDays(-2).AddHours(-3).AddMinutes(1),
                },
                new MessageModel
                {
                    Id = "msg3",
                    Text =
                        "Can you help me calculate my estimated payment based on my income this quarter?",
                    IsUser = true,
                    Timestamp = DateTime.Now.AddDays(-2).AddHours(-3).AddMinutes(5),
                },
                new MessageModel
                {
                    Id = "msg4",
                    Text =
                        "I'd be happy to help you calculate your estimated quarterly tax payment. Could you provide me with your total income for this quarter and any business expenses you've incurred?",
                    IsUser = false,
                    Timestamp = DateTime.Now.AddDays(-2).AddHours(-3).AddMinutes(6),
                },
                new MessageModel
                {
                    Id = "msg5",
                    Text = "My total income was $24,500 and business expenses were $6,200.",
                    IsUser = true,
                    Timestamp = DateTime.Now.AddDays(-2).AddHours(-3).AddMinutes(8),
                },
                new MessageModel
                {
                    Id = "msg6",
                    Text =
                        "Based on your income of $24,500 and business expenses of $6,200, your net income for the quarter is $18,300.\n\nHere's an estimate of your quarterly tax payment:\n\n1. Self-employment tax (15.3% of 92.35% of net income): $2,595\n2. Federal income tax (estimated at 22% bracket): $4,026\n3. Less: Deductions and credits (standard approximation): -$1,500\n\nEstimated quarterly payment: $5,121\n\nI've created a detailed calculation sheet for you and scheduled a reminder for the payment due date.",
                    IsUser = false,
                    Timestamp = DateTime.Now.AddDays(-2).AddHours(-3).AddMinutes(10),
                    Actions = new List<MessageActionModel>
                    {
                        new MessageActionModel
                        {
                            Type = "file",
                            Description = "Created quarterly_tax_estimate.pdf",
                        },
                        new MessageActionModel
                        {
                            Type = "calendar",
                            Description = "Added tax payment reminder on 10/15/2023",
                        },
                        new MessageActionModel
                        {
                            Type = "email",
                            Description = "Sent tax calculation summary to your email",
                        },
                    },
                },
            },
        };
    }

    /// <summary>
    /// Gets detailed workflow execution information
    /// </summary>
    public async Task<WorkflowExecutionHistoryModel?> GetWorkflowExecutionDetailsAsync(string executionId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<WorkflowExecutionHistoryModel>(
                $"/api/history/workflow-execution/{executionId}"
            );
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching workflow execution details for {ExecutionId}", executionId);
            return null;
        }
    }

    /// <summary>
    /// Gets execution steps for a workflow execution
    /// </summary>
    public async Task<List<ExecutionStepModel>> GetExecutionStepsAsync(string executionId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ExecutionStepModel>>(
                $"/api/history/workflow-execution/{executionId}/steps"
            );
            return response ?? new List<ExecutionStepModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching execution steps for {ExecutionId}", executionId);
            return new List<ExecutionStepModel>();
        }
    }

    /// <summary>
    /// Gets execution logs for a workflow execution
    /// </summary>
    public async Task<List<ExecutionLogModel>> GetExecutionLogsAsync(string executionId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ExecutionLogModel>>(
                $"/api/history/workflow-execution/{executionId}/logs"
            );
            return response ?? new List<ExecutionLogModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching execution logs for {ExecutionId}", executionId);
            return new List<ExecutionLogModel>();
        }
    }

    /// <summary>
    /// Gets detailed execution information for a workflow execution
    /// </summary>
    public async Task<WorkflowHistoryModel?> GetExecutionDetailAsync(string executionId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<WorkflowHistoryModel>(
                $"/api/history/workflow-execution/{executionId}"
            );
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching execution detail for {ExecutionId}", executionId);
            return null;
        }
    }

    // TODO: Remove this method when the real API is implemented
    private List<WorkflowHistoryModel> GetDummyWorkflowHistory()
    {
        return new List<WorkflowHistoryModel>
        {
            new WorkflowHistoryModel
            {
                Id = "run-1",
                WorkflowId = "daily-report",
                WorkflowName = "Daily Morning Report",
                Status = "Completed",
                StartTime = DateTime.Now.AddDays(-1).AddHours(-8),
                EndTime = DateTime.Now.AddDays(-1).AddHours(-8).AddMinutes(3),
                TriggerType = "Schedule",
            },
            new WorkflowHistoryModel
            {
                Id = "run-2",
                WorkflowId = "invoice-process",
                WorkflowName = "Invoice Processing",
                Status = "Failed",
                StartTime = DateTime.Now.AddDays(-2),
                EndTime = DateTime.Now.AddDays(-2).AddMinutes(1),
                TriggerType = "Manual",
                Error = "Could not connect to accounting system",
            },
        };
    }
}

// Models for history
public class HistoryEntryModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int MessageCount { get; set; }
}

public class ConversationDetailModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<MessageModel> Messages { get; set; } = new();
}

public class MessageModel
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
    public List<MessageActionModel>? Actions { get; set; }
}

public class MessageActionModel
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class WorkflowHistoryModel
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string? Error { get; set; }
}

// Model for workflow execution history from Agent API
public class WorkflowExecutionHistoryModel
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public TimeSpan? Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Model for detailed execution step information
/// </summary>
public class ExecutionStepModel
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public TimeSpan? Duration { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Model for execution log entries
/// </summary>
public class ExecutionLogModel
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
