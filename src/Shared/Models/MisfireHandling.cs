namespace Shared.Models;

/// <summary>
/// Defines how the system should handle missed job executions
/// </summary>
public enum MisfireHandling
{
    /// <summary>
    /// Skip the missed execution and wait for the next scheduled time
    /// </summary>
    Skip = 0,

    /// <summary>
    /// Execute the missed job immediately when possible
    /// </summary>
    RunImmediately = 1,

    /// <summary>
    /// Wait for the next scheduled execution time
    /// </summary>
    RunOnNextSchedule = 2,

    /// <summary>
    /// Run immediately and continue with the normal schedule
    /// </summary>
    RunImmediatelyAndContinue = 3
}

/// <summary>
/// Extension methods for MisfireHandling enum
/// </summary>
public static class MisfireHandlingExtensions
{
    /// <summary>
    /// Get user-friendly description of the misfire handling strategy
    /// </summary>
    public static string GetDescription(this MisfireHandling handling)
    {
        return handling switch
        {
            MisfireHandling.Skip => "Skip missed executions and wait for next scheduled time",
            MisfireHandling.RunImmediately => "Execute missed jobs immediately when possible",
            MisfireHandling.RunOnNextSchedule => "Wait for the next scheduled execution time",
            MisfireHandling.RunImmediatelyAndContinue => "Run immediately and continue with normal schedule",
            _ => "Unknown misfire handling strategy"
        };
    }

    /// <summary>
    /// Get short display name for UI
    /// </summary>
    public static string GetDisplayName(this MisfireHandling handling)
    {
        return handling switch
        {
            MisfireHandling.Skip => "Skip",
            MisfireHandling.RunImmediately => "Run Immediately",
            MisfireHandling.RunOnNextSchedule => "Run on Next Schedule",
            MisfireHandling.RunImmediatelyAndContinue => "Run Immediately & Continue",
            _ => "Unknown"
        };
    }


}
