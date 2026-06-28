namespace Agent.Models.Migration;

/// <summary>
/// Result of workflow migration operation
/// </summary>
public class WorkflowMigrationResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsDryRun { get; set; }
    public int TotalWorkflows { get; set; }
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }
    public List<WorkflowMigrationItemResult> MigrationResults { get; set; } = new();
    public DateTimeOffset MigrationStarted { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? MigrationCompleted { get; set; }

    public double SuccessRate => TotalWorkflows > 0 ? (double)SuccessfulMigrations / TotalWorkflows * 100 : 0;
}

/// <summary>
/// Result of migrating a single workflow
/// </summary>
public class WorkflowMigrationItemResult
{
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public bool AlreadyMigrated { get; set; }
    public bool ScheduledInQuartz { get; set; }
    public DateTimeOffset? NextFireTime { get; set; }
    public string? ScheduleType { get; set; }
    public DateTimeOffset? MigratedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Result of migration validation
/// </summary>
public class MigrationValidationResult
{
    public bool IsValid { get; set; }
    public int TotalWorkflowsInFiles { get; set; }
    public int TotalWorkflowsInQuartz { get; set; }
    public int SuccessfullyMigratedWorkflows { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTimeOffset ValidatedAt { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// Result of workflow backup operation
/// </summary>
public class WorkflowBackupResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string BackupName { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public string? ManifestPath { get; set; }
    public int BackedUpWorkflows { get; set; }
    public int FailedBackups { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// Result of workflow rollback operation
/// </summary>
public class WorkflowRollbackResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string BackupName { get; set; } = string.Empty;
    public int RemovedQuartzJobs { get; set; }
    public int FailedRemovals { get; set; }
    public int RestoredWorkflows { get; set; }
    public int FailedRestores { get; set; }
    public int ReregisteredJobs { get; set; }
    public int FailedReregistrations { get; set; }
    public DateTimeOffset RolledBackAt { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// Backup manifest containing metadata about a workflow backup
/// </summary>
public class WorkflowBackupManifest
{
    public string BackupName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public int TotalWorkflows { get; set; }
    public int ScheduledWorkflows { get; set; }
    public int RegisteredQuartzJobs { get; set; }
    public List<WorkflowFileBackup> WorkflowFiles { get; set; } = new();
    public List<Quartz.JobKey> QuartzJobKeys { get; set; } = new();
}

/// <summary>
/// Information about a backed up workflow file
/// </summary>
public class WorkflowFileBackup
{
    public string WorkflowId { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public bool IsScheduled { get; set; }
    public string? Schedule { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Information about an available backup
/// </summary>
public class WorkflowBackupInfo
{
    public string BackupName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public int TotalWorkflows { get; set; }
    public int ScheduledWorkflows { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public long Size { get; set; }
}

/// <summary>
/// Result of schedule preservation validation
/// </summary>
public class SchedulePreservationResult
{
    public bool IsValid { get; set; }
    public int TotalScheduledWorkflows { get; set; }
    public int PreservedSchedules { get; set; }
    public List<string> Issues { get; set; } = new();
    public DateTimeOffset ValidatedAt { get; set; } = DateTimeOffset.Now;
}
