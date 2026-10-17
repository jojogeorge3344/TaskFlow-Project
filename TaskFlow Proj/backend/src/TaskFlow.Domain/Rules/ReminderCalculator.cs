using TaskFlow.Domain.Enums;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Domain.Rules;

/// <summary>The reminder-relevant fields of a task. Deadline is UTC.</summary>
public sealed record ReminderTaskSnapshot(
    TaskStatus Status,
    DateTime DeadlineUtc,
    int FirstReminderDays,
    int SecondReminderDays,
    ReminderFrequency OverdueReminderFrequency);

/// <summary>A reminder that is due today. ScheduledForUtc is the UTC instant of local midnight of
/// <see cref="Day"/> and forms part of the dedup key.</summary>
public sealed record DueReminder(ReminderType Type, DateOnly Day, DateTime ScheduledForUtc);

/// <summary>Decides which deadline reminders a task is due for right now (BRD §3.2). Only reminders
/// whose day is today in the application timezone are returned, so a day that has already ended is
/// never backfilled (Screen 13 FR-6). Because it reads the task's current deadline and status, a
/// deadline change or completion takes effect on the next evaluation with no extra bookkeeping.</summary>
public static class ReminderCalculator
{
    private const int WeeklyStepDays = 7;

    public static IReadOnlyList<DueReminder> DueReminders(
        ReminderTaskSnapshot task,
        DateTime utcNow,
        TimeZoneInfo timeZone,
        bool deadlineDayEnabled)
    {
        // BR-10: completed and cancelled tasks never receive further reminders.
        if (task.Status is TaskStatus.Completed or TaskStatus.Cancelled)
        {
            return [];
        }

        var deadlineUtc = AsUtc(task.DeadlineUtc);
        var nowUtc = AsUtc(utcNow);
        var deadlineDay = LocalDate(deadlineUtc, timeZone);
        var today = LocalDate(nowUtc, timeZone);
        var daysUntilDeadline = deadlineDay.DayNumber - today.DayNumber;

        var reminderType = daysUntilDeadline switch
        {
            > 0 => PreDeadlineReminder(task, daysUntilDeadline),
            0 => DueTodayReminder(task, nowUtc, deadlineUtc, deadlineDayEnabled),
            _ => OverdueReminder(task, -daysUntilDeadline)
        };

        return reminderType is { } type
            ? [new DueReminder(type, today, LocalMidnightUtc(today, timeZone))]
            : [];
    }

    /// <summary>The calendar date of a UTC instant in the given timezone.</summary>
    public static DateOnly LocalDate(DateTime utc, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), timeZone));

    /// <summary>The UTC instant at which <paramref name="day"/> starts in the given timezone. When a DST
    /// gap removes local midnight, the earliest valid local time that day is used; when a DST fall-back makes
    /// local midnight occur twice, the earlier of the two instants is used.</summary>
    public static DateTime LocalMidnightUtc(DateOnly day, TimeZoneInfo timeZone) =>
        LocalTimeUtc(day, TimeOnly.MinValue, timeZone);

    /// <summary>The UTC instant of <paramref name="time"/> on <paramref name="day"/> in the given timezone, with
    /// the same DST handling as <see cref="LocalMidnightUtc"/>.</summary>
    public static DateTime LocalTimeUtc(DateOnly day, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            // The larger offset is the daylight interpretation, which happens first.
            var offset = timeZone.GetAmbiguousTimeOffsets(local).Max();
            return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    private static ReminderType? PreDeadlineReminder(ReminderTaskSnapshot task, int daysUntilDeadline)
    {
        // Equal intervals collapse to a single reminder for that day.
        if (task.SecondReminderDays > 0 && daysUntilDeadline == task.SecondReminderDays)
        {
            return ReminderType.SecondReminder;
        }

        if (task.FirstReminderDays > 0 && daysUntilDeadline == task.FirstReminderDays)
        {
            return ReminderType.FirstReminder;
        }

        return null;
    }

    private static ReminderType? DueTodayReminder(
        ReminderTaskSnapshot task,
        DateTime nowUtc,
        DateTime deadlineUtc,
        bool deadlineDayEnabled)
    {
        // Once the deadline instant has passed the task is overdue; overdue reminders start the next day.
        if (nowUtc >= deadlineUtc)
        {
            return null;
        }

        // An interval of 0 means "on the deadline day" and asks for this reminder even when the
        // global deadline-day reminder is switched off.
        var taskAsksForDeadlineDay = task.FirstReminderDays == 0 || task.SecondReminderDays == 0;
        return deadlineDayEnabled || taskAsksForDeadlineDay ? ReminderType.DueToday : null;
    }

    private static ReminderType? OverdueReminder(ReminderTaskSnapshot task, int daysOverdue)
    {
        var step = task.OverdueReminderFrequency == ReminderFrequency.Weekly ? WeeklyStepDays : 1;
        return daysOverdue % step == 0 ? ReminderType.Overdue : null;
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
