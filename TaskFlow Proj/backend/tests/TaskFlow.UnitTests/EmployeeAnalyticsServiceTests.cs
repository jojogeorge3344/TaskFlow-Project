using System.Text.Json;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Domain.Enums;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;
using User = TaskFlow.Domain.Entities.User;

namespace TaskFlow.UnitTests;

public class EmployeeAnalyticsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Past = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Task<EmployeeAnalyticsResponseDto> GetAsync(AnalyticsHarness harness, User user, string? period = null) =>
        harness.EmployeeService.GetAsync(user.Id, period, CancellationToken.None);

    // 1.1 scope

    [Fact]
    public async Task Scope_CountsAssignedAndSharedTasksButNotUnrelatedOnes()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");
        var colleague = harness.AddUser("colleague");
        harness.AddTask(me);
        var shared = harness.AddTask(colleague);
        harness.AddRecipient(shared, me);
        harness.AddTask(colleague);
        harness.AddTask(me, isDeleted: true);
        harness.AddTask(me, TaskStatus.Cancelled);

        var result = await GetAsync(harness, me);

        Assert.Equal(2, result.Summary.TotalTasks);
    }

    // 1.2 buckets, deadlines, completed

    [Fact]
    public async Task Buckets_ReconcileWithSummaryAndPriority()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");
        harness.AddTask(me, TaskStatus.ToDo, priority: TaskPriority.High);
        harness.AddTask(me, TaskStatus.InProgress, priority: TaskPriority.Low);
        harness.AddTask(me, TaskStatus.Completed);
        harness.AddTask(me, TaskStatus.ToDo, deadline: Past, priority: TaskPriority.High);
        harness.AddTask(me, TaskStatus.Completed);
        harness.AddTask(me, TaskStatus.Completed);
        harness.AddTask(me, TaskStatus.Completed);
        harness.AddTask(me, TaskStatus.Completed);

        var result = await GetAsync(harness, me);

        Assert.Equal(new BucketCountsDto(1, 1, 5, 1), result.TasksByStatus);
        Assert.Equal(8, result.Summary.TotalTasks);
        Assert.Equal(63, result.Summary.CompletionPercent);
        Assert.Equal(5, result.Summary.Completed);
        Assert.Equal(3, result.Summary.Outstanding);
        Assert.Equal(8, result.TasksByPriority.Sum(priority => priority.Count));
    }

    [Fact]
    public async Task Summary_ThreeOfTwelveCompleted_IsTwentyFivePercent()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");
        for (var i = 0; i < 3; i++)
        {
            harness.AddTask(me, TaskStatus.Completed);
        }

        for (var i = 0; i < 9; i++)
        {
            harness.AddTask(me);
        }

        var result = await GetAsync(harness, me);

        Assert.Equal(25, result.Summary.CompletionPercent);
        Assert.Equal(3, result.Summary.Completed);
        Assert.Equal(9, result.Summary.Outstanding);
    }

    [Fact]
    public async Task UpcomingDeadlines_AreNextSevenDaysSoonestFirstAndFlagOverdue()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");
        var later = harness.AddTask(me, deadline: Now.UtcDateTime.AddDays(5));
        var soon = harness.AddTask(me, deadline: Now.UtcDateTime.AddDays(1));
        var overdue = harness.AddTask(me, deadline: Now.UtcDateTime.AddDays(-3));
        harness.AddTask(me, deadline: Now.UtcDateTime.AddDays(20));
        harness.AddTask(me, TaskStatus.Completed, deadline: Now.UtcDateTime.AddDays(2));

        var result = await GetAsync(harness, me);

        Assert.Equal([overdue.Id, soon.Id, later.Id], result.UpcomingDeadlines.Select(row => row.TaskId));
        var first = result.UpcomingDeadlines[0];
        Assert.True(first.IsOverdue);
        Assert.Equal(3, first.DaysOverdue);
        Assert.False(result.UpcomingDeadlines[1].IsOverdue);
        Assert.Equal(1, result.UpcomingDeadlines[1].DaysRemaining);
    }

    [Fact]
    public async Task NoTasks_ReturnsZerosAndEmptyLists()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");

        var result = await GetAsync(harness, me);

        Assert.Equal(0, result.Summary.TotalTasks);
        Assert.Equal(0, result.Summary.CompletionPercent);
        Assert.Empty(result.UpcomingDeadlines);
        Assert.Empty(result.Projects);
        Assert.Equal(0, result.CompletedInPeriod);
        Assert.Null(result.Department);
    }

    // 1.3 department overview

    [Fact]
    public async Task Department_ShowsAggregatesOfTheDepartmentTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var department = harness.AddDepartment("Engineering");
        var me = harness.AddUser("me", departmentId: department.Id);
        var a = harness.AddUser("a", departmentId: department.Id);
        var b = harness.AddUser("b", departmentId: department.Id);
        harness.AddTask(me);
        harness.AddTask(a, TaskStatus.Completed);
        harness.AddTask(b, TaskStatus.ToDo, deadline: Past);
        harness.AddTask(harness.AddUser("elsewhere"));

        var result = await GetAsync(harness, me);

        Assert.NotNull(result.Department);
        Assert.Equal("Engineering", result.Department!.Name);
        Assert.False(result.Department.Limited);
        Assert.Equal(3, result.Department.TotalTasks);
        Assert.Equal(new BucketCountsDto(1, 0, 1, 1), result.Department.Tasks);
    }

    [Fact]
    public async Task Department_NoDepartment_IsNull()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");
        harness.AddTask(me);

        Assert.Null((await GetAsync(harness, me)).Department);
    }

    [Fact]
    public async Task Department_BelowMinimumSize_HidesTheBreakdown()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var department = harness.AddDepartment("Tiny");
        var me = harness.AddUser("me", departmentId: department.Id);
        var other = harness.AddUser("other", departmentId: department.Id);
        harness.AddTask(me);
        harness.AddTask(other, TaskStatus.Completed);

        var result = await GetAsync(harness, me);

        Assert.True(result.Department!.Limited);
        Assert.Equal(2, result.Department.TotalTasks);
        Assert.Null(result.Department.Tasks);
    }

    [Fact]
    public async Task Department_ResponseCarriesNoColleagueData()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var department = harness.AddDepartment("Engineering");
        var me = harness.AddUser("me", departmentId: department.Id);
        var a = harness.AddUser("secretcolleague", departmentId: department.Id);
        var b = harness.AddUser("b", departmentId: department.Id);
        var theirTask = harness.AddTask(a);
        theirTask.Title = "Confidential colleague title";
        harness.Db.SaveChanges();
        harness.AddTask(b);
        harness.AddTask(me);

        var json = JsonSerializer.Serialize(await GetAsync(harness, me));

        Assert.DoesNotContain("secretcolleague", json);
        Assert.DoesNotContain("Confidential colleague title", json);
    }

    // 1.4 projects

    [Fact]
    public async Task Projects_CountOnlyTheUsersOwnTasksEvenWhenColleaguesShareTheProject()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");
        var colleague = harness.AddUser("colleague");
        var project = harness.AddProject("Apollo");
        harness.AddTask(me, TaskStatus.Completed, projectId: project.Id);
        harness.AddTask(me, deadline: Now.UtcDateTime.AddDays(9), projectId: project.Id);
        harness.AddTask(me, deadline: Now.UtcDateTime.AddDays(3), projectId: project.Id);
        harness.AddTask(me, TaskStatus.Completed, projectId: project.Id);
        for (var i = 0; i < 5; i++)
        {
            harness.AddTask(colleague, projectId: project.Id);
        }

        var result = await GetAsync(harness, me);

        var row = Assert.Single(result.Projects);
        Assert.Equal(4, row.TotalTasks);
        Assert.Equal(2, row.CompletedTasks);
        Assert.Equal(50, row.ProgressPercent);
        Assert.Equal(Now.UtcDateTime.AddDays(9), row.LatestDeadline);
    }

    [Fact]
    public async Task Projects_IncludeMembershipWithoutTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");
        var project = harness.AddProject("Member only");
        me.ProjectId = project.Id;
        harness.Db.SaveChanges();

        var row = Assert.Single((await GetAsync(harness, me)).Projects);

        Assert.Equal("Member only", row.Name);
        Assert.Equal(0, row.TotalTasks);
        Assert.Null(row.LatestDeadline);
    }

    // 1.5 trend and period

    [Fact]
    public async Task Trend_Default30Days_IsZeroFilledAndMatchesCompletedInPeriod()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");
        harness.AddTask(me, TaskStatus.Completed, createdAt: Now.UtcDateTime.AddDays(-2), completedAt: Now.UtcDateTime.AddDays(-1));
        harness.AddTask(me, TaskStatus.Completed, createdAt: Now.UtcDateTime.AddDays(-40), completedAt: Now.UtcDateTime.AddDays(-35));
        harness.AddTask(me, createdAt: Now.UtcDateTime.AddDays(-1));

        var result = await GetAsync(harness, me);

        Assert.Equal(EmployeeAnalyticsPeriod.Last30Days, result.Period);
        Assert.Equal(30, result.Trend.Count);
        Assert.Equal(new DateOnly(2026, 6, 15), result.Trend[^1].Date);
        Assert.Equal(2, result.Trend.Sum(point => point.Created));
        Assert.Equal(1, result.Trend.Sum(point => point.Completed));
        Assert.Equal(1, result.CompletedInPeriod);
    }

    [Fact]
    public async Task Trend_SevenDays_HasSevenPoints()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");

        var result = await GetAsync(harness, me, "7d");

        Assert.Equal(7, result.Trend.Count);
        Assert.Equal(new DateOnly(2026, 6, 9), result.From);
    }

    [Fact]
    public async Task Trend_ThisMonth_StartsOnTheFirstInTheApplicationTimezone()
    {
        // 00:30 UTC on 1 July is still 30 June in New York (UTC-4), so the "month" is June, not July.
        await using var harness = AnalyticsHarness.Create(new DateTimeOffset(2026, 7, 1, 0, 30, 0, TimeSpan.Zero), "America/New_York");
        var me = harness.AddUser("me");
        harness.AddTask(
            me,
            TaskStatus.Completed,
            createdAt: new DateTime(2026, 6, 30, 23, 0, 0, DateTimeKind.Utc),
            completedAt: new DateTime(2026, 7, 1, 0, 10, 0, DateTimeKind.Utc));

        var result = await GetAsync(harness, me, "month");

        Assert.Equal(new DateOnly(2026, 6, 1), result.From);
        Assert.Equal(new DateOnly(2026, 6, 30), result.To);
        Assert.Equal(30, result.Trend.Count);
        Assert.Equal(1, result.Trend[^1].Completed);
        Assert.Equal(1, result.CompletedInPeriod);
    }

    [Fact]
    public async Task Trend_ThisMonth_AcrossMonthBoundary_ExcludesPreviousMonth()
    {
        await using var harness = AnalyticsHarness.Create(new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero));
        var me = harness.AddUser("me");
        harness.AddTask(me, TaskStatus.Completed, completedAt: new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc));
        harness.AddTask(me, TaskStatus.Completed, completedAt: new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc));

        var result = await GetAsync(harness, me, "month");

        Assert.Equal(3, result.Trend.Count);
        Assert.Equal(1, result.CompletedInPeriod);
    }

    [Fact]
    public async Task Period_Unknown_IsRejected()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var me = harness.AddUser("me");

        await Assert.ThrowsAsync<AnalyticsFilterException>(() => GetAsync(harness, me, "year"));
    }
}
