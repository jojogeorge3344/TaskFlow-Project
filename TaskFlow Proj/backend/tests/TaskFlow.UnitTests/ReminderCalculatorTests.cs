using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class ReminderCalculatorTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    // Deadline: 15 October, 17:00 UTC.
    private static readonly DateTime Deadline = new(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc);

    private static ReminderTaskSnapshot Task(
        int first = 2,
        int second = 1,
        ReminderFrequency overdue = ReminderFrequency.Daily,
        TaskStatus status = TaskStatus.ToDo,
        DateTime? deadline = null) =>
        new(status, deadline ?? Deadline, first, second, overdue);

    private static DateTime On(int day, int hour = 10) => new(2026, 10, day, hour, 0, 0, DateTimeKind.Utc);

    private static ReminderType? Single(ReminderTaskSnapshot task, DateTime now, bool deadlineDayEnabled = true, TimeZoneInfo? zone = null)
    {
        var due = ReminderCalculator.DueReminders(task, now, zone ?? Utc, deadlineDayEnabled);
        Assert.True(due.Count <= 1);
        return due.Count == 0 ? null : due[0].Type;
    }

    public static TheoryData<int, ReminderType?> DefaultScheduleByDay => new()
    {
        { 12, null },
        { 13, ReminderType.FirstReminder },
        { 14, ReminderType.SecondReminder },
        { 15, ReminderType.DueToday },
        { 16, ReminderType.Overdue },
        { 17, ReminderType.Overdue },
        { 18, ReminderType.Overdue }
    };

    [Theory]
    [MemberData(nameof(DefaultScheduleByDay))]
    public void Default_schedule_follows_two_one_due_today_then_daily_overdue(int day, ReminderType? expected)
    {
        Assert.Equal(expected, Single(Task(), On(day)));
    }

    public static TheoryData<int, ReminderType?> WeeklyOverdueByDay => new()
    {
        { 16, null },
        { 21, null },
        { 22, ReminderType.Overdue },
        { 23, null },
        { 29, ReminderType.Overdue }
    };

    [Theory]
    [MemberData(nameof(WeeklyOverdueByDay))]
    public void Weekly_overdue_repeats_every_seven_days(int day, ReminderType? expected)
    {
        Assert.Equal(expected, Single(Task(overdue: ReminderFrequency.Weekly), On(day)));
    }

    [Fact]
    public void Second_interval_of_zero_gives_a_single_due_today_reminder()
    {
        var task = Task(first: 2, second: 0);

        Assert.Equal(ReminderType.FirstReminder, Single(task, On(13)));
        Assert.Null(Single(task, On(14)));
        Assert.Equal(ReminderType.DueToday, Single(task, On(15)));
    }

    [Fact]
    public void First_interval_of_zero_gives_a_single_due_today_reminder()
    {
        var task = Task(first: 0, second: 1);

        Assert.Null(Single(task, On(13)));
        Assert.Equal(ReminderType.SecondReminder, Single(task, On(14)));
        Assert.Equal(ReminderType.DueToday, Single(task, On(15)));
    }

    [Fact]
    public void Equal_intervals_produce_one_reminder_for_that_day()
    {
        var due = ReminderCalculator.DueReminders(Task(first: 1, second: 1), On(14), Utc, deadlineDayEnabled: true);

        var reminder = Assert.Single(due);
        Assert.Equal(ReminderType.SecondReminder, reminder.Type);
    }

    [Fact]
    public void Due_today_is_not_generated_after_the_deadline_time_has_passed()
    {
        Assert.Equal(ReminderType.DueToday, Single(Task(), On(15, hour: 16)));
        Assert.Null(Single(Task(), On(15, hour: 18)));
    }

    [Theory]
    [InlineData(TaskStatus.Completed)]
    [InlineData(TaskStatus.Cancelled)]
    public void Finished_tasks_never_get_reminders(TaskStatus status)
    {
        foreach (var day in new[] { 13, 14, 15, 16, 20 })
        {
            Assert.Null(Single(Task(status: status), On(day)));
        }
    }

    [Theory]
    [InlineData(TaskStatus.ToDo)]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Blocked)]
    public void Open_statuses_get_reminders(TaskStatus status)
    {
        Assert.Equal(ReminderType.SecondReminder, Single(Task(status: status), On(14)));
    }

    [Fact]
    public void Only_todays_reminder_is_returned_so_past_days_are_not_backfilled()
    {
        // Created on 14 October with a 2-day first interval: the 13 October reminder is never produced.
        var due = ReminderCalculator.DueReminders(Task(), On(14), Utc, deadlineDayEnabled: true);

        var reminder = Assert.Single(due);
        Assert.Equal(ReminderType.SecondReminder, reminder.Type);
        Assert.Equal(new DateOnly(2026, 10, 14), reminder.Day);
    }

    [Fact]
    public void Deadline_change_moves_reminders_to_the_new_schedule()
    {
        var extended = Task(deadline: new DateTime(2026, 10, 20, 17, 0, 0, DateTimeKind.Utc));

        Assert.Null(Single(extended, On(13)));
        Assert.Null(Single(extended, On(14)));
        Assert.Null(Single(extended, On(15)));
        Assert.Equal(ReminderType.FirstReminder, Single(extended, On(18)));
    }

    [Fact]
    public void Overdue_task_given_a_future_deadline_stops_overdue_reminders()
    {
        var rescheduled = Task(deadline: new DateTime(2026, 10, 19, 17, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ReminderType.FirstReminder, Single(rescheduled, On(17)));
    }

    [Fact]
    public void Deadline_day_is_taken_in_the_application_timezone()
    {
        // 20:00 UTC on 14 October is 01:30 IST on 15 October.
        var task = Task(deadline: new DateTime(2026, 10, 14, 20, 0, 0, DateTimeKind.Utc));

        // 18:30 UTC on 13 October is 00:00 IST on 14 October: one day before the local deadline day.
        Assert.Equal(ReminderType.SecondReminder, Single(task, new DateTime(2026, 10, 13, 18, 30, 0, DateTimeKind.Utc), zone: India));

        // 19:00 UTC on 14 October is 00:30 IST on 15 October: the deadline day, before the deadline.
        Assert.Equal(ReminderType.DueToday, Single(task, new DateTime(2026, 10, 14, 19, 0, 0, DateTimeKind.Utc), zone: India));

        // In UTC the same deadline falls on 14 October.
        Assert.Equal(ReminderType.DueToday, Single(task, new DateTime(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc), zone: Utc));
    }

    [Fact]
    public void Scheduled_slot_is_local_midnight_in_utc()
    {
        var due = ReminderCalculator.DueReminders(Task(), new DateTime(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc), India, true);

        var reminder = Assert.Single(due);
        Assert.Equal(new DateTime(2026, 10, 13, 18, 30, 0, DateTimeKind.Utc), reminder.ScheduledForUtc);
    }

    [Fact]
    public void Scheduled_slot_is_the_same_for_every_moment_of_the_day()
    {
        var morning = ReminderCalculator.DueReminders(Task(), On(14, hour: 1), Utc, true);
        var evening = ReminderCalculator.DueReminders(Task(), On(14, hour: 23), Utc, true);

        Assert.Equal(morning[0].ScheduledForUtc, evening[0].ScheduledForUtc);
    }

    [Fact]
    public void Deadline_day_toggle_off_suppresses_due_today_only()
    {
        Assert.Equal(ReminderType.FirstReminder, Single(Task(), On(13), deadlineDayEnabled: false));
        Assert.Equal(ReminderType.SecondReminder, Single(Task(), On(14), deadlineDayEnabled: false));
        Assert.Null(Single(Task(), On(15), deadlineDayEnabled: false));
        Assert.Equal(ReminderType.Overdue, Single(Task(), On(16), deadlineDayEnabled: false));
    }

    [Fact]
    public void Task_with_zero_interval_still_gets_due_today_when_toggle_is_off()
    {
        Assert.Equal(ReminderType.DueToday, Single(Task(first: 2, second: 0), On(15), deadlineDayEnabled: false));
    }

    [Fact]
    public void Unspecified_kind_deadline_is_treated_as_utc()
    {
        var unspecified = DateTime.SpecifyKind(Deadline, DateTimeKind.Unspecified);

        Assert.Equal(ReminderType.SecondReminder, Single(Task(deadline: unspecified), On(14)));
    }

    [Fact]
    public void Ambiguous_local_midnight_uses_the_earlier_instant()
    {
        // A zone (UTC+0, +1 in summer) whose summer time ends at 01:00 local on 1 November, so 00:00-01:00 that
        // day happens twice.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 29),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 11, 1));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test/MidnightFallBack", TimeSpan.Zero, "Test", "Test", "Test DST", [rule]);

        var start = ReminderCalculator.LocalMidnightUtc(new DateOnly(2026, 11, 1), zone);

        Assert.True(zone.IsAmbiguousTime(new DateTime(2026, 11, 1, 0, 0, 0)));
        Assert.Equal(new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc), start);
    }
}
