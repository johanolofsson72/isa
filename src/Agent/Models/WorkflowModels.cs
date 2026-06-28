using System;

namespace Agent.Models;

// Workflow models
public class WorkflowDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string ExecutionType { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    // New scheduling properties
    public string Schedule { get; set; } = string.Empty; // CRON expression or human-readable format
    public DateTime? LastExecuted { get; set; }
    public DateTime? NextExecutionTime { get; set; }

    /// <summary>
    /// Timezone for schedule execution (e.g., "Europe/Stockholm", "UTC")
    /// Defaults to "Europe/Stockholm" if not specified
    /// </summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// How to handle missed executions (misfire)
    /// Options: "Skip", "RunImmediately", "RunOnNextSchedule", "RunImmediatelyAndContinue"
    /// Defaults to "Skip" if not specified
    /// </summary>
    public string? MisfireHandling { get; set; }

    /// <summary>
    /// Human-readable description of the schedule
    /// Auto-generated if not provided
    /// </summary>
    public string? ScheduleDescription { get; set; }

    /// <summary>
    /// Step name to execute when the workflow completes successfully
    /// </summary>
    public string? OnSuccess { get; set; }

    /// <summary>
    /// Step name to execute when the workflow fails or encounters an error
    /// </summary>
    public string? OnError { get; set; }

    public List<WorkflowJob> Jobs { get; set; } = new();
}

// Workflow schedule information that is sent to clients
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

public class WorkflowJob
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool Enabled { get; set; }
    public List<WorkflowStep> Steps { get; set; } = new();
}

public class WorkflowStep
{
    public string Id { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int Order { get; set; }



    public int Timeout { get; set; }
    public int Retries { get; set; }
    public string OnSuccess { get; set; } = string.Empty;
    public string OnFailure { get; set; } = string.Empty;
}

// Workflow execution history models
public class WorkflowExecutionHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty; // "Running", "Completed", "Failed"
    public string? ErrorMessage { get; set; }
    public List<string> ExecutionLog { get; set; } = new();
    public string TriggerType { get; set; } = string.Empty; // "Manual", "Scheduled", "API"
}

public class WorkflowHistoryEntry
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}
