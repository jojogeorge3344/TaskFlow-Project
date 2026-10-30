namespace TaskFlow.Domain.Rules;

/// <summary>Ordering rule for a task's reminder intervals: the second reminder may not come before the
/// first. Equal intervals collapse to one reminder and 0 means "on the deadline day", so both are allowed.</summary>
public static class ReminderIntervals
{
    public const string OrderMessage = "The second reminder cannot come before the first reminder.";

    public static bool AreOrdered(int firstReminderDays, int secondReminderDays) =>
        firstReminderDays == 0 || secondReminderDays <= firstReminderDays;
}
