using System;

namespace Agent.Models;

// Workflow execution result models
public class WorkflowExecutionResult
{
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public System.DateTime StartTime { get; set; }
    public System.DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public string Status { get; set; } = string.Empty; // Running, Completed, Failed
    public string? ErrorMessage { get; set; }
    public List<JobExecutionResult> JobResults { get; set; } = new();
}

public class JobExecutionResult
{
    public string JobName { get; set; } = string.Empty;
    public System.DateTime StartTime { get; set; }
    public System.DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public string Status { get; set; } = string.Empty; // Running, Completed, Failed
    public string? ErrorMessage { get; set; }
    public List<StepExecutionResult> StepResults { get; set; } = new();
}

public class StepExecutionResult
{
    public string StepId { get; set; } = string.Empty;
    public System.DateTime StartTime { get; set; }
    public System.DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public string Status { get; set; } = string.Empty; // Running, Completed, Failed
    public string? ErrorMessage { get; set; }
    public string? Output { get; set; }
}
