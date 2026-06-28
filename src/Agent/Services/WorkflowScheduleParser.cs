using System.Text.RegularExpressions;
using Quartz;
using Agent.Services.Interfaces;

namespace Agent.Services;

/// <summary>
/// Enhanced parser for workflow schedules supporting both CRON expressions and human-readable formats.
/// Provides detailed validation, error messages, and suggestions for schedule corrections.
/// </summary>
public class WorkflowScheduleParser : IWorkflowScheduleParser
{
    private readonly ILogger<WorkflowScheduleParser> _logger;



    public WorkflowScheduleParser(ILogger<WorkflowScheduleParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse and validate a schedule string, supporting both CRON and human-readable formats
    /// </summary>
    public ScheduleParseResult ParseSchedule(string schedule, string? timezone = null)
    {
        if (string.IsNullOrWhiteSpace(schedule))
        {
            return ScheduleParseResult.Error("Schedule cannot be empty",
                "Please provide a valid schedule like 'daily at 9am' or '0 9 * * *'");
        }

        var normalizedSchedule = schedule.Trim().ToLowerInvariant();

        // Try direct pattern matching first for common cases
        var directResult = TryDirectPatternMatch(normalizedSchedule);
        if (directResult.IsSuccess)
        {
            return ValidateCronExpression(directResult.CronExpression!, timezone, schedule);
        }

        // Try to parse as CRON expression
        var cronResult = TryParseCronExpression(schedule);
        if (cronResult.IsSuccess)
        {
            return ValidateCronExpression(cronResult.CronExpression!, timezone, schedule);
        }

        // Neither format worked, provide helpful suggestions
        return ScheduleParseResult.Error(
            $"Unable to parse schedule: '{schedule}'",
            GetScheduleSuggestions(schedule));
    }

    /// <summary>
    /// Validate timezone string
    /// </summary>
    public bool IsValidTimezone(string timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
            return false;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get list of common timezone IDs
    /// </summary>
    public static IReadOnlyList<string> GetCommonTimezones()
    {
        return new[]
        {
            "Europe/Stockholm",
            "Europe/London",
            "Europe/Berlin",
            "UTC",
            "America/New_York",
            "America/Los_Angeles",
            "Asia/Tokyo",
            "Australia/Sydney"
        };
    }

    #region Private Methods

    private ScheduleParseResult TryDirectPatternMatch(string schedule)
    {
        // Direct pattern matching for common cases
        switch (schedule)
        {
            case "hourly":
                return ScheduleParseResult.Success("0 * * * ?", "Every hour");
            case "daily":
                return ScheduleParseResult.Success("0 9 * * ?", "Daily at 9 AM");
            case "weekly":
                return ScheduleParseResult.Success("0 9 ? * 1", "Weekly on Monday at 9 AM");
            case "monthly":
                return ScheduleParseResult.Success("0 9 1 * ?", "Monthly on 1st at 9 AM");
        }

        // Pattern matching for "daily at Xam/pm"
        if (schedule.StartsWith("daily at "))
        {
            var timeStr = schedule.Substring("daily at ".Length);
            var cronExpr = ParseTimeString(timeStr);
            if (cronExpr != null)
            {
                return ScheduleParseResult.Success($"{cronExpr} * * ?", $"Daily at {timeStr}");
            }
        }

        // Pattern matching for "every [day] at Xam/pm"
        if (schedule.StartsWith("every ") && schedule.Contains(" at "))
        {
            var parts = schedule.Split(" at ");
            if (parts.Length == 2)
            {
                var dayPart = parts[0].Substring("every ".Length);
                var timeStr = parts[1];

                var dayNum = GetDayNumber(dayPart);
                var cronTime = ParseTimeString(timeStr);

                if (dayNum.HasValue && cronTime != null)
                {
                    return ScheduleParseResult.Success($"{cronTime} ? * {dayNum}", $"Every {dayPart} at {timeStr}");
                }
            }
        }

        return ScheduleParseResult.Error("Not a recognized human-readable format", "");
    }

    private string? ParseTimeString(string timeStr)
    {
        // Parse "9am", "2pm", "10:30am", etc.
        var match = Regex.Match(timeStr, @"^(\d{1,2})(?::(\d{2}))?(am|pm)$");
        if (match.Success)
        {
            var hour = int.Parse(match.Groups[1].Value);
            var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var ampm = match.Groups[3].Value;

            // Convert to 24-hour format
            if (ampm == "pm" && hour != 12) hour += 12;
            if (ampm == "am" && hour == 12) hour = 0;

            return $"0 {minute} {hour}";
        }

        return null;
    }

    private int? GetDayNumber(string dayName)
    {
        return dayName.ToLower() switch
        {
            "sunday" => 0,
            "monday" => 1,
            "tuesday" => 2,
            "wednesday" => 3,
            "thursday" => 4,
            "friday" => 5,
            "saturday" => 6,
            _ => null
        };
    }



    private ScheduleParseResult TryParseCronExpression(string schedule)
    {
        try
        {
            // First try the schedule as-is
            if (CronExpression.IsValidExpression(schedule))
            {
                return ScheduleParseResult.Success(schedule, "Valid CRON expression");
            }

            // If it's a 5-field CRON (minute hour day month dayOfWeek), convert to 6-field
            var parts = schedule.Split(' ');
            if (parts.Length == 5)
            {
                // Convert 5-field to 6-field Quartz format
                // 5-field: minute hour day month dayOfWeek
                // 6-field: second minute hour day month dayOfWeek

                var minute = parts[0];
                var hour = parts[1];
                var day = parts[2];
                var month = parts[3];
                var dayOfWeek = parts[4];

                // For Quartz.NET, we need to handle day/dayOfWeek conflict
                // If day is *, use ? for dayOfWeek, and vice versa
                if (day == "*" && dayOfWeek != "*")
                {
                    day = "?";
                }
                else if (dayOfWeek == "*" && day != "*")
                {
                    dayOfWeek = "?";
                }
                else if (day == "*" && dayOfWeek == "*")
                {
                    // Both are *, set dayOfWeek to ?
                    dayOfWeek = "?";
                }

                var sixFieldCron = $"0 {minute} {hour} {day} {month} {dayOfWeek}";

                if (CronExpression.IsValidExpression(sixFieldCron))
                {
                    return ScheduleParseResult.Success(sixFieldCron,
                        $"Converted 5-field CRON to 6-field: '{schedule}' → '{sixFieldCron}'");
                }
            }

            return ScheduleParseResult.Error("Invalid CRON expression syntax",
                "CRON format: 'second minute hour day month dayOfWeek' or 'minute hour day month dayOfWeek'");
        }
        catch (Exception ex)
        {
            return ScheduleParseResult.Error($"CRON parsing error: {ex.Message}",
                "Please check CRON syntax: https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontrigger.html");
        }
    }

    private ScheduleParseResult ValidateCronExpression(string cronExpression, string? timezone, string originalSchedule)
    {
        try
        {
            var cron = new CronExpression(cronExpression);

            // Validate/resolve timezone if provided (cross-platform tolerant)
            TimeZoneInfo? tz = null;
            if (!string.IsNullOrEmpty(timezone))
            {
                tz = ResolveTimeZoneCrossPlatform(timezone);
                if (tz != null)
                {
                    cron.TimeZone = tz;
                }
                // If tz is null we gracefully fall back to default (local) without failing validation
            }

            // Check if schedule is reasonable (not too frequent)
            var now = DateTimeOffset.UtcNow;
            var next1 = cron.GetNextValidTimeAfter(now);
            var next2 = next1.HasValue ? cron.GetNextValidTimeAfter(next1.Value) : null;

            if (next1.HasValue && next2.HasValue)
            {
                var interval = next2.Value - next1.Value;
                if (interval.TotalSeconds < 30)
                {
                    return ScheduleParseResult.Warning(cronExpression,
                        $"Schedule runs very frequently (every {interval.TotalSeconds:F0} seconds). Consider if this is intended.",
                        $"Parsed from: '{originalSchedule}'");
                }
            }

            return ScheduleParseResult.Success(cronExpression, $"Parsed from: '{originalSchedule}'");
        }
        catch (Exception ex)
        {
            return ScheduleParseResult.Error($"CRON validation error: {ex.Message}",
                "Please verify the CRON expression is valid");
        }
    }

    private string GetScheduleSuggestions(string schedule)
    {
        var suggestions = new List<string>
        {
            "Try these formats:",
            "• 'daily at 9am' - runs every day at 9 AM",
            "• 'every monday at 2pm' - runs every Monday at 2 PM",
            "• 'hourly' - runs every hour",
            "• 'weekly' - runs every Monday at 9 AM",
            "• '0 9 * * *' - CRON expression for daily at 9 AM"
        };

        // Add specific suggestions based on input
        if (schedule.Contains("every") && !schedule.Contains("at"))
        {
            suggestions.Add("• Add time: 'every monday at 9am'");
        }

        if (schedule.Contains("daily") && !schedule.Contains("at"))
        {
            suggestions.Add("• Add time: 'daily at 9am'");
        }

        return string.Join("\n", suggestions);
    }

    private TimeZoneInfo? ResolveTimeZoneCrossPlatform(string timezone)
    {
        try
        {
            // Direct attempt
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch
        {
            // Common aliases mapping (Windows -> IANA)
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"W. Europe Standard Time", "Europe/Berlin"},
                {"Central Europe Standard Time", "Europe/Budapest"},
                {"Romance Standard Time", "Europe/Paris"},
                {"Central European Standard Time", "Europe/Warsaw"},
                {"FLE Standard Time", "Europe/Helsinki"}
            };
            if (map.TryGetValue(timezone, out var iana))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(iana); } catch { }
            }
            return null;
        }
    }

    #endregion

    #region Interface Implementation

    // Interface implementation methods
    public ITrigger ParseSchedule(string schedule, JobKey jobKey)
    {
        var result = ParseSchedule(schedule, "Europe/Stockholm");
        if (!result.IsSuccess)
        {
            throw new ArgumentException($"Invalid schedule: {result.ErrorMessage}");
        }

        return TriggerBuilder.Create()
            .WithIdentity($"{jobKey.Name}_trigger", jobKey.Group)
            .WithCronSchedule(result.CronExpression!)
            .Build();
    }

    public bool IsValidSchedule(string schedule)
    {
        var result = ParseSchedule(schedule, "Europe/Stockholm");
        return result.IsSuccess;
    }

    public string GetScheduleDescription(string schedule)
    {
        var result = ParseSchedule(schedule, "Europe/Stockholm");
        return result.IsSuccess ? result.Description ?? "Valid schedule" : result.ErrorMessage ?? "Invalid schedule";
    }



    #endregion
}

/// <summary>
/// Result of schedule parsing operation
/// </summary>
public class ScheduleParseResult
{
    public bool IsSuccess { get; private set; }
    public bool IsWarning { get; private set; }
    public string? CronExpression { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? Suggestion { get; private set; }
    public string? Description { get; private set; }

    private ScheduleParseResult() { }

    public static ScheduleParseResult Success(string cronExpression, string description)
    {
        return new ScheduleParseResult
        {
            IsSuccess = true,
            CronExpression = cronExpression,
            Description = description
        };
    }

    public static ScheduleParseResult Warning(string cronExpression, string warning, string description)
    {
        return new ScheduleParseResult
        {
            IsSuccess = true,
            IsWarning = true,
            CronExpression = cronExpression,
            ErrorMessage = warning,
            Description = description
        };
    }

    public static ScheduleParseResult Error(string errorMessage, string suggestion)
    {
        return new ScheduleParseResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Suggestion = suggestion
        };
    }
}
