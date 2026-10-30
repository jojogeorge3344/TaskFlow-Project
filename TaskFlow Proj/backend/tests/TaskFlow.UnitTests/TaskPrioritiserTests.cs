using TaskFlow.Application.Assistant;
using TaskFlow.Application.Tasks;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class TaskPrioritiserTests
{
    // 2026-10-18 12:00 UTC is the afternoon of 18 October in UTC.
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private static TaskListItemDto Task(
        int id,
        string title,
        int daysFromToday,
        TaskPriority priority = TaskPriority.Medium,
        TaskStatus status = TaskStatus.InProgress) =>
        new(
            id,
            $"TF-{id}",
            title,
            1,
            "Asha",
            2,
            "Ravi",
            priority,
            Now.AddDays(daysFromToday - 10),
            Now.AddDays(daysFromToday),
            4,
            status,
            "Upcoming",
            false);

    private static IReadOnlyList<PrioritisedTask> Rank(params TaskListItemDto[] tasks) =>
        TaskPrioritiser.Rank(tasks, Now, TimeZoneInfo.Utc);

    [Fact]
    public void AnOverdueMediumTaskOutranksAHighTaskDueInThreeDays()
    {
        var ranked = Rank(Task(1, "High in three days", 3, TaskPriority.High), Task(2, "Overdue medium", -1, TaskPriority.Medium));

        Assert.Equal([2, 1], ranked.Select(task => task.TaskId));
        Assert.Equal(TaskStage.Overdue, ranked[0].Stage);
    }

    [Fact]
    public void TheOrderIsOverdueThenDueTodayThenNearestDeadline()
    {
        var ranked = Rank(
            Task(1, "Next week", 7, TaskPriority.Critical),
            Task(2, "Tomorrow", 1),
            Task(3, "Today", 0),
            Task(4, "Overdue three days", -3),
            Task(5, "Overdue one day", -1));

        Assert.Equal([4, 5, 3, 2, 1], ranked.Select(task => task.TaskId));
        Assert.Equal([1, 2, 3, 4, 5], ranked.Select(task => task.Rank));
    }

    [Fact]
    public void PriorityBreaksTiesOnTheSameDay()
    {
        var ranked = Rank(
            Task(1, "Low today", 0, TaskPriority.Low),
            Task(2, "Critical today", 0, TaskPriority.Critical),
            Task(3, "High today", 0, TaskPriority.High));

        Assert.Equal([2, 3, 1], ranked.Select(task => task.TaskId));
    }

    [Fact]
    public void TaskCodeBreaksARemainingTie()
    {
        var ranked = Rank(Task(20, "B", 2), Task(10, "A", 2));

        Assert.Equal([10, 20], ranked.Select(task => task.TaskId));
    }

    [Fact]
    public void CompletedAndCancelledTasksAreExcluded()
    {
        var ranked = Rank(
            Task(1, "Done", -2, status: TaskStatus.Completed),
            Task(2, "Cancelled", -2, status: TaskStatus.Cancelled),
            Task(3, "Open", 1));

        var only = Assert.Single(ranked);
        Assert.Equal(3, only.TaskId);
    }

    [Fact]
    public void ABlockedTaskStaysInTheListAndIsFlagged()
    {
        var ranked = Rank(Task(1, "Blocked", 2, status: TaskStatus.Blocked), Task(2, "Free", 5));

        Assert.Equal([1, 2], ranked.Select(task => task.TaskId));
        Assert.True(ranked[0].IsBlocked);
        Assert.False(ranked[1].IsBlocked);
    }

    [Fact]
    public void TheSameInputGivesTheSameOrderEveryTime()
    {
        var tasks = new[]
        {
            Task(1, "A", 3, TaskPriority.High),
            Task(2, "B", -1),
            Task(3, "C", 0, TaskPriority.Low),
            Task(4, "D", 3, TaskPriority.Critical)
        };

        var first = TaskPrioritiser.Rank(tasks, Now, TimeZoneInfo.Utc).Select(task => task.TaskId).ToList();
        var shuffled = TaskPrioritiser.Rank(tasks.Reverse(), Now, TimeZoneInfo.Utc).Select(task => task.TaskId).ToList();

        Assert.Equal(first, shuffled);
        Assert.Equal([2, 3, 4, 1], first);
    }

    [Fact]
    public void DaysUntilDueIsNegativeWhenOverdue()
    {
        var ranked = Rank(Task(1, "Overdue", -2), Task(2, "Today", 0), Task(3, "In 4 days", 4));

        Assert.Equal([-2, 0, 4], ranked.Select(task => task.DaysUntilDue));
    }

    [Fact]
    public void DueTodayFollowsTheApplicationTimezone()
    {
        // A deadline of 20:00 UTC on 18 October is 01:30 on 19 October in Kolkata (UTC+5:30). At 00:30 UTC on 19 October
        // it is already overdue by UTC days, but still due today by Kolkata days.
        var lateEvening = new DateTime(2026, 10, 19, 0, 30, 0, DateTimeKind.Utc);
        var kolkata = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
        var task = new TaskListItemDto(
            1, "TF-1", "Evening deadline", 1, "Asha", 2, "Ravi", TaskPriority.Medium,
            lateEvening.AddDays(-5), new DateTime(2026, 10, 18, 20, 0, 0, DateTimeKind.Utc), 4, TaskStatus.InProgress, "Upcoming", false);

        var inUtc = TaskPrioritiser.Rank([task], lateEvening, TimeZoneInfo.Utc).Single();
        var inKolkata = TaskPrioritiser.Rank([task], lateEvening, kolkata).Single();

        Assert.Equal(TaskStage.Overdue, inUtc.Stage);
        Assert.Equal(TaskStage.DueToday, inKolkata.Stage);
    }

    [Fact]
    public void NoOpenTasksGivesAnEmptyList()
    {
        Assert.Empty(Rank());
        Assert.Empty(Rank(Task(1, "Done", 1, status: TaskStatus.Completed)));
    }
}
