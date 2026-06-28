using Quartz;

namespace Agent.Services.Interfaces;

/// <summary>
/// Interface for parsing workflow schedule expressions
/// </summary>
public interface IWorkflowScheduleParser
{
    /// <summary>
    /// Parse a schedule expression into a Quartz trigger
    /// </summary>
    /// <param name="schedule">The schedule expression (cron, interval, etc.)</param>
    /// <param name="jobKey">The job key for the trigger</param>
    /// <returns>A configured trigger</returns>
    ITrigger ParseSchedule(string schedule, JobKey jobKey);

    /// <summary>
    /// Validate if a schedule expression is valid
    /// </summary>
    /// <param name="schedule">The schedule expression to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool IsValidSchedule(string schedule);

    /// <summary>
    /// Get a human-readable description of the schedule
    /// </summary>
    /// <param name="schedule">The schedule expression</param>
    /// <returns>Human-readable description</returns>
    string GetScheduleDescription(string schedule);

    /// <summary>
    /// Validate if a timezone is valid
    /// </summary>
    /// <param name="timezone">The timezone to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool IsValidTimezone(string timezone);
}
