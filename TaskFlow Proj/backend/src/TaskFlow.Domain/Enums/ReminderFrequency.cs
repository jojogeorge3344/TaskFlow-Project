namespace TaskFlow.Domain.Enums;

/// <summary>How often a reminder repeats once a task is overdue. The reminder engine
/// (a later module) consumes this; this change only records the per-task setting.</summary>
public enum ReminderFrequency
{
    Daily,
    Weekly
}
