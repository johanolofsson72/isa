using Agent.Data;
using Agent.Models;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Agent.Services;

/// <summary>
/// Service for tracking job executions and maintaining statistics
/// </summary>
public class JobExecutionTrackingService
{
    private readonly AgentDbContext _dbContext;
    private readonly ILogger<JobExecutionTrackingService> _logger;

    public JobExecutionTrackingService(
        AgentDbContext dbContext,
        ILogger<JobExecutionTrackingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Record the start of a job execution
    /// </summary>
    public async Task<JobExecutionRecord> RecordJobStartAsync(IJobExecutionContext context)
    {
        try
        {
            var jobDetail = context.JobDetail;
            var trigger = context.Trigger;
            var scheduledTime = context.ScheduledFireTimeUtc?.UtcDateTime;
            var actualStartTime = context.FireTimeUtc.UtcDateTime;

            var record = new JobExecutionRecord
            {
                JobKey = jobDetail.Key.ToString(),
                JobName = jobDetail.Key.Name,
                JobGroup = jobDetail.Key.Group,
                TriggerKey = trigger.Key.ToString(),
                TriggerName = trigger.Key.Name,
                TriggerGroup = trigger.Key.Group,
                StartTime = actualStartTime,
                ScheduledTime = scheduledTime,
                Status = JobExecutionStatus.Running,
                WorkflowId = jobDetail.JobDataMap.GetString("WorkflowId"),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.JobExecutions.Add(record);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("📊 Job execution started: {JobKey} at {StartTime}", 
                record.JobKey, record.StartTime);

            return record;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to record job start for {JobKey}", 
                context.JobDetail.Key);
            throw;
        }
    }

    /// <summary>
    /// Record the completion of a job execution
    /// </summary>
    public async Task RecordJobCompletionAsync(
        JobExecutionRecord record, 
        IJobExecutionContext context, 
        JobExecutionException? jobException = null)
    {
        try
        {
            var endTime = DateTime.UtcNow;
            var duration = endTime - record.StartTime;

            record.EndTime = endTime;
            record.DurationMs = (long)duration.TotalMilliseconds;

            if (jobException != null)
            {
                record.Status = JobExecutionStatus.Failed;
                record.ErrorMessage = jobException.Message;
                record.StackTrace = jobException.StackTrace;
            }
            else
            {
                record.Status = JobExecutionStatus.Success;
            }

            _dbContext.JobExecutions.Update(record);
            await _dbContext.SaveChangesAsync();

            // Update statistics
            await UpdateJobStatisticsAsync(record);

            _logger.LogInformation("📊 Job execution completed: {JobKey} - Status: {Status}, Duration: {Duration}ms", 
                record.JobKey, record.Status, record.DurationMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to record job completion for {JobKey}", 
                record.JobKey);
            throw;
        }
    }

    /// <summary>
    /// Record a vetoed job execution
    /// </summary>
    public async Task RecordJobVetoedAsync(IJobExecutionContext context)
    {
        try
        {
            var jobDetail = context.JobDetail;
            var trigger = context.Trigger;

            var record = new JobExecutionRecord
            {
                JobKey = jobDetail.Key.ToString(),
                JobName = jobDetail.Key.Name,
                JobGroup = jobDetail.Key.Group,
                TriggerKey = trigger.Key.ToString(),
                TriggerName = trigger.Key.Name,
                TriggerGroup = trigger.Key.Group,
                StartTime = context.FireTimeUtc.UtcDateTime,
                EndTime = DateTime.UtcNow,
                DurationMs = 0,
                Status = JobExecutionStatus.Vetoed,
                WorkflowId = jobDetail.JobDataMap.GetString("WorkflowId"),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.JobExecutions.Add(record);
            await _dbContext.SaveChangesAsync();

            // Update statistics
            await UpdateJobStatisticsAsync(record);

            _logger.LogWarning("⚠️ Job execution vetoed: {JobKey}", record.JobKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to record job veto for {JobKey}", 
                context.JobDetail.Key);
            throw;
        }
    }

    /// <summary>
    /// Update aggregated statistics for a job
    /// </summary>
    private async Task UpdateJobStatisticsAsync(JobExecutionRecord execution)
    {
        try
        {
            var stats = await _dbContext.JobStatistics
                .FirstOrDefaultAsync(s => s.JobKey == execution.JobKey);

            if (stats == null)
            {
                // Create new statistics record
                stats = new JobStatistics
                {
                    JobKey = execution.JobKey,
                    JobName = execution.JobName,
                    JobGroup = execution.JobGroup,
                    WorkflowId = execution.WorkflowId,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.JobStatistics.Add(stats);
            }

            // Update counters
            stats.TotalExecutions++;
            switch (execution.Status)
            {
                case JobExecutionStatus.Success:
                    stats.SuccessfulExecutions++;
                    break;
                case JobExecutionStatus.Failed:
                    stats.FailedExecutions++;
                    break;
                case JobExecutionStatus.Cancelled:
                    stats.CancelledExecutions++;
                    break;
                case JobExecutionStatus.Vetoed:
                    stats.VetoedExecutions++;
                    break;
            }

            // Update timing statistics (only for completed executions)
            if (execution.DurationMs.HasValue && execution.Status != JobExecutionStatus.Vetoed)
            {
                var durationMs = execution.DurationMs.Value;
                
                if (stats.TotalExecutions == 1)
                {
                    // First execution
                    stats.AverageExecutionTimeMs = durationMs;
                    stats.MinExecutionTimeMs = durationMs;
                    stats.MaxExecutionTimeMs = durationMs;
                }
                else
                {
                    // Update average (weighted)
                    var totalTime = stats.AverageExecutionTimeMs * (stats.TotalExecutions - 1) + durationMs;
                    stats.AverageExecutionTimeMs = totalTime / stats.TotalExecutions;
                    
                    // Update min/max
                    stats.MinExecutionTimeMs = Math.Min(stats.MinExecutionTimeMs, durationMs);
                    stats.MaxExecutionTimeMs = Math.Max(stats.MaxExecutionTimeMs, durationMs);
                }
            }

            // Update last execution info
            stats.LastExecutionTime = execution.StartTime;
            stats.LastExecutionStatus = execution.Status;
            stats.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogDebug("📈 Updated statistics for job {JobKey}: {TotalExecutions} total, {SuccessRate:F1}% success rate", 
                stats.JobKey, stats.TotalExecutions, stats.SuccessRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to update statistics for job {JobKey}", 
                execution.JobKey);
            // Don't rethrow - statistics update failure shouldn't break job execution
        }
    }

    /// <summary>
    /// Get job execution history with pagination
    /// </summary>
    public async Task<(List<JobExecutionRecord> Records, int TotalCount)> GetJobExecutionHistoryAsync(
        string? jobKey = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        JobExecutionStatus? status = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        var query = _dbContext.JobExecutions.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(jobKey))
            query = query.Where(e => e.JobKey == jobKey);

        if (fromDate.HasValue)
            query = query.Where(e => e.StartTime >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.StartTime <= toDate.Value);

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        var totalCount = await query.CountAsync();

        var records = await query
            .OrderByDescending(e => e.StartTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (records, totalCount);
    }

    /// <summary>
    /// Get job statistics
    /// </summary>
    public async Task<List<JobStatistics>> GetJobStatisticsAsync(string? jobKey = null)
    {
        var query = _dbContext.JobStatistics.AsQueryable();

        if (!string.IsNullOrEmpty(jobKey))
            query = query.Where(s => s.JobKey == jobKey);

        return await query
            .OrderByDescending(s => s.LastExecutionTime)
            .ToListAsync();
    }

    /// <summary>
    /// Clean up old execution records
    /// </summary>
    public async Task CleanupOldRecordsAsync(TimeSpan retentionPeriod)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - retentionPeriod;
            
            var oldRecords = await _dbContext.JobExecutions
                .Where(e => e.CreatedAt < cutoffDate)
                .ToListAsync();

            if (oldRecords.Any())
            {
                _dbContext.JobExecutions.RemoveRange(oldRecords);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("🧹 Cleaned up {Count} old job execution records older than {CutoffDate}", 
                    oldRecords.Count, cutoffDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to cleanup old job execution records");
        }
    }
}
