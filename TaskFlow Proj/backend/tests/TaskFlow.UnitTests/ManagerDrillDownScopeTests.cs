using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Security;

namespace TaskFlow.UnitTests;

public class ManagerDrillDownScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static async System.Threading.Tasks.Task<int> TotalAsync(AnalyticsHarness harness, TaskListFilter filter, int userId, string role)
    {
        var page = await harness.Tasks.ListAsync(filter with { PageSize = 100 }, userId, role, CancellationToken.None);
        return page.Total;
    }

    [Fact]
    public async System.Threading.Tasks.Task TamperedDrillDown_NeverReturnsOutOfScopeTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var otherManager = harness.AddUser("m2", SystemRoles.Manager);
        var mine = harness.AddUser("e1", managerId: manager.Id);
        var foreign = harness.AddUser("e2", managerId: otherManager.Id);
        harness.AddTask(mine);
        harness.AddTask(foreign);

        var all = await TotalAsync(harness, new TaskListFilter(Bucket: TaskBucketFilter.All), manager.Id, SystemRoles.Manager);
        var tamperedEmployee = await TotalAsync(harness, new TaskListFilter(EmployeeId: foreign.Id), manager.Id, SystemRoles.Manager);
        var tamperedManager = await TotalAsync(harness, new TaskListFilter(ManagerId: otherManager.Id), manager.Id, SystemRoles.Manager);

        Assert.Equal(1, all);
        Assert.Equal(0, tamperedEmployee);
        Assert.Equal(0, tamperedManager);
    }

    [Fact]
    public async System.Threading.Tasks.Task DrillDown_MatchesTheManagerAnalyticsCount()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var mine = harness.AddUser("e1", managerId: manager.Id);
        harness.AddTask(mine, TaskFlow.Domain.Enums.TaskStatus.ToDo, deadline: Now.UtcDateTime.AddDays(-2));
        harness.AddTask(harness.AddUser("e2"));

        var analytics = await harness.ManagerService.GetAsync(manager.Id, new TaskFlow.Application.Dashboard.Analytics.AnalyticsFilter(), CancellationToken.None);
        var listed = await TotalAsync(harness, new TaskListFilter(Bucket: TaskBucketFilter.Overdue, EmployeeId: mine.Id), manager.Id, SystemRoles.Manager);

        Assert.Equal(analytics.TasksByStatus.Overdue, listed);
    }

    [Fact]
    public async System.Threading.Tasks.Task Administrator_DrillDownIsNotLimitedToATeam()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var admin = harness.AddUser("a1", SystemRoles.Administrator);
        harness.AddTask(harness.AddUser("e1"));
        harness.AddTask(harness.AddUser("e2"));

        var total = await TotalAsync(harness, new TaskListFilter(Bucket: TaskBucketFilter.All), admin.Id, SystemRoles.Administrator);

        Assert.Equal(2, total);
    }
}
