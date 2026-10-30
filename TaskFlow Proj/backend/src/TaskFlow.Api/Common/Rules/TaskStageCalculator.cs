using TaskFlow.Api.Models.Enums;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Common.Rules;

/// <summary>The display stage of a task, derived at read time from its status and deadline.
/// Never stored (agent.md: overdue is never stored).</summary>
public enum TaskStage
{
    Upcoming,
    DueToday,
    Overdue,
    Completed
}

public static class TaskStageCalculator
{
    /// <summary>Compares calendar days in <paramref name="timeZone"/> (the application timezone, BR-17), so
    /// "due today" agrees with the deadline reminders. Without a timezone, days are UTC days.</summary>
    public static TaskStage Calculate(TaskStatus status, DateTime deadline, DateTime utcNow, TimeZoneInfo? timeZone = null)
    {
        if (status is TaskStatus.Completed or TaskStatus.Cancelled)
        {
            return TaskStage.Completed;
        }

        var zone = timeZone ?? TimeZoneInfo.Utc;
        var deadlineDay = ReminderCalculator.LocalDate(deadline, zone);
        var today = ReminderCalculator.LocalDate(utcNow, zone);

        if (deadlineDay < today)
        {
            return TaskStage.Overdue;
        }

        if (deadlineDay == today)
        {
            return TaskStage.DueToday;
        }

        return TaskStage.Upcoming;
    }
}
