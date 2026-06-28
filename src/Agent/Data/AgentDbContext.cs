using Agent.Models;
using Agent.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Agent.Data;

/// <summary>
/// Entity Framework DbContext for SQLite database - unified for all agent data
/// </summary>
public class AgentDbContext : DbContext
{
    public AgentDbContext(DbContextOptions<AgentDbContext> options)
        : base(options) { }

    // Parameterless constructor for Docker compatibility
    public AgentDbContext() : base() { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback configuration for Docker environment - use single database
            optionsBuilder.UseSqlite("Data Source=agent.db");
        }
    }

    // Cache and audit tables
    public DbSet<OpenAICache> OpenAICache { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<WorkflowExecution> WorkflowExecutions { get; set; } = null!;

    // Plugin management
    public DbSet<PluginState> PluginStates { get; set; } = null!;

    // Job execution tracking
    public DbSet<JobExecutionRecord> JobExecutions { get; set; } = null!;
    public DbSet<JobStatistics> JobStatistics { get; set; } = null!;

    // Idempotency-key replay cache
    public DbSet<IdempotencyEntry> IdempotencyEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // OpenAI Cache configuration
        modelBuilder.Entity<OpenAICache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequestHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.RequestHash).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.LastAccessedAt);
            entity.HasIndex(e => e.ExpiresAt);
        });

        // Audit Log configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.PluginName);
            entity.HasIndex(e => e.Status);
        });

        // Workflow Execution configuration
        modelBuilder.Entity<WorkflowExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExecutionId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.WorkflowName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.WorkflowPath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Trigger).HasMaxLength(20).IsRequired();
            entity.Property(e => e.TriggeredBy).HasMaxLength(100);
            entity.HasIndex(e => e.ExecutionId).IsUnique();
            entity.HasIndex(e => e.WorkflowName);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Trigger);
            entity.HasIndex(e => new { e.WorkflowName, e.StartedAt });
        });

        // Plugin State configuration
        modelBuilder.Entity<PluginState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PluginId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.PluginName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.HasIndex(e => e.PluginId).IsUnique();
            entity.HasIndex(e => e.PluginName);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.LastModified);
        });

        // Job Execution Record configuration
        modelBuilder.Entity<JobExecutionRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.JobName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.JobGroup).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TriggerKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TriggerName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TriggerGroup).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.StackTrace).HasMaxLength(4000);
            entity.Property(e => e.WorkflowId).HasMaxLength(100);
            entity.Property(e => e.Metadata).HasMaxLength(1000);

            // Indexes for efficient querying
            entity.HasIndex(e => e.JobKey);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => new { e.JobKey, e.StartTime });
        });

        // Job Statistics configuration
        modelBuilder.Entity<JobStatistics>(entity =>
        {
            entity.HasKey(e => e.JobKey);
            entity.Property(e => e.JobKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.JobName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.JobGroup).HasMaxLength(100).IsRequired();
            entity.Property(e => e.WorkflowId).HasMaxLength(100);

            // Indexes for efficient querying
            entity.HasIndex(e => e.JobName);
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.LastExecutionTime);
            entity.HasIndex(e => e.NextScheduledTime);
        });

        // Idempotency entry configuration
        modelBuilder.Entity<IdempotencyEntry>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.HasIndex(e => e.ExpiresAt);
        });






    }
}
