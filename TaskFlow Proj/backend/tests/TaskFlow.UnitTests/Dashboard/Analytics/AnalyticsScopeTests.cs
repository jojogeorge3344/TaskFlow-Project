using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Security;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests.Dashboard.Analytics;

public class AnalyticsScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static async Task<List<int>> IdsAsync(AnalyticsHarness harness, AnalyticsFilter filter)
    {
        var scoped = await harness.Scope.ScopeAsync(filter, CancellationToken.None);
        return (await harness.Analytics.GetTaskIdsAsync(scoped.Tasks)).Order().ToList();
    }

    [Fact]
    public async Task Scope_ExcludesDeletedAndCancelledTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        var kept = harness.AddTask(user);
        harness.AddTask(user, TaskStatus.Cancelled);
        harness.AddTask(user, isDeleted: true);

        var ids = await IdsAsync(harness, new AnalyticsFilter());

        Assert.Equal([kept.Id], ids);
    }

    [Fact]
    public async Task Scope_DefaultsToLast30DaysOnCreated()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        var inside = harness.AddTask(user, createdAt: new DateTime(2026, 5, 17, 8, 0, 0, DateTimeKind.Utc));
        harness.AddTask(user, createdAt: new DateTime(2026, 5, 16, 23, 0, 0, DateTimeKind.Utc));

        var scoped = await harness.Scope.ScopeAsync(new AnalyticsFilter(), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 5, 17), scoped.Filter.From);
        Assert.Equal(new DateOnly(2026, 6, 15), scoped.Filter.To);
        Assert.Equal([inside.Id], await harness.Analytics.GetTaskIdsAsync(scoped.Tasks));
    }

    [Theory]
    [InlineData(AnalyticsDateBasis.Created)]
    [InlineData(AnalyticsDateBasis.Deadline)]
    [InlineData(AnalyticsDateBasis.Completed)]
    public async Task Scope_AppliesRangeToTheChosenBasis(AnalyticsDateBasis basis)
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        var inRange = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
        var outOfRange = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

        var match = harness.AddTask(
            user,
            TaskStatus.Completed,
            deadline: basis == AnalyticsDateBasis.Deadline ? inRange : outOfRange,
            createdAt: basis == AnalyticsDateBasis.Created ? inRange : outOfRange,
            completedAt: basis == AnalyticsDateBasis.Completed ? inRange : outOfRange);
        harness.AddTask(user, TaskStatus.Completed, deadline: outOfRange, createdAt: outOfRange, completedAt: outOfRange);

        var ids = await IdsAsync(harness, new AnalyticsFilter(new DateOnly(2026, 6, 8), new DateOnly(2026, 6, 12), basis));

        Assert.Equal([match.Id], ids);
    }

    [Fact]
    public async Task Scope_CompletedBasisIgnoresTasksNeverCompleted()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        harness.AddTask(user, TaskStatus.ToDo);

        var ids = await IdsAsync(harness, new AnalyticsFilter(Basis: AnalyticsDateBasis.Completed));

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Scope_RangeIsInclusiveInTheApplicationTimezone()
    {
        await using var harness = AnalyticsHarness.Create(Now, "Asia/Kolkata");
        var user = harness.AddUser("u1");
        var startsDay = harness.AddTask(user, createdAt: new DateTime(2026, 6, 9, 18, 30, 0, DateTimeKind.Utc));
        var endsDay = harness.AddTask(user, createdAt: new DateTime(2026, 6, 10, 18, 29, 0, DateTimeKind.Utc));
        harness.AddTask(user, createdAt: new DateTime(2026, 6, 9, 18, 29, 0, DateTimeKind.Utc));
        harness.AddTask(user, createdAt: new DateTime(2026, 6, 10, 18, 30, 0, DateTimeKind.Utc));

        var ids = await IdsAsync(harness, new AnalyticsFilter(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 10)));

        Assert.Equal([startsDay.Id, endsDay.Id], ids);
    }

    [Fact]
    public async Task Scope_FiltersByDepartmentOfTheAssignee()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var engineering = harness.AddDepartment("Engineering");
        var sales = harness.AddDepartment("Sales");
        var dev = harness.AddUser("dev", departmentId: engineering.Id);
        var rep = harness.AddUser("rep", departmentId: sales.Id);
        var devTask = harness.AddTask(dev);
        harness.AddTask(rep);

        var ids = await IdsAsync(harness, new AnalyticsFilter(DepartmentId: engineering.Id));

        Assert.Equal([devTask.Id], ids);
    }

    [Fact]
    public async Task Scope_FiltersByProject()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        var alpha = harness.AddProject("Alpha");
        var beta = harness.AddProject("Beta");
        var match = harness.AddTask(user, projectId: alpha.Id);
        harness.AddTask(user, projectId: beta.Id);
        harness.AddTask(user);

        var ids = await IdsAsync(harness, new AnalyticsFilter(ProjectId: alpha.Id));

        Assert.Equal([match.Id], ids);
    }

    [Fact]
    public async Task Scope_EmployeeFilterIncludesAssigneeAndRecipientTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var ann = harness.AddUser("ann");
        var bob = harness.AddUser("bob");
        var own = harness.AddTask(ann);
        var shared = harness.AddTask(bob);
        harness.AddRecipient(shared, ann);
        harness.AddTask(bob);

        var ids = await IdsAsync(harness, new AnalyticsFilter(EmployeeId: ann.Id));

        Assert.Equal([own.Id, shared.Id], ids);
    }

    [Fact]
    public async Task Scope_ManagerTeamIsDirectReportsUnionManagedDepartmentMembers()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("mgr", SystemRoles.Manager);
        var otherManager = harness.AddUser("other", SystemRoles.Manager);
        var department = harness.AddDepartment("Engineering", manager);
        var report = harness.AddUser("report", managerId: manager.Id);
        var member = harness.AddUser("member", departmentId: department.Id);
        var both = harness.AddUser("both", departmentId: department.Id, managerId: manager.Id);
        var outsider = harness.AddUser("outsider", managerId: otherManager.Id);

        var reportTask = harness.AddTask(report);
        var memberTask = harness.AddTask(member);
        var bothTask = harness.AddTask(both);
        harness.AddTask(outsider);
        harness.AddTask(manager);

        var ids = await IdsAsync(harness, new AnalyticsFilter(ManagerId: manager.Id));

        Assert.Equal([reportTask.Id, memberTask.Id, bothTask.Id], ids);
    }

    [Fact]
    public async Task Scope_CombinedFiltersMustAllMatch()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var engineering = harness.AddDepartment("Engineering");
        var dev = harness.AddUser("dev", departmentId: engineering.Id);
        var alpha = harness.AddProject("Alpha");
        var beta = harness.AddProject("Beta");
        var match = harness.AddTask(dev, projectId: alpha.Id);
        harness.AddTask(dev, projectId: beta.Id);

        var ids = await IdsAsync(harness, new AnalyticsFilter(DepartmentId: engineering.Id, ProjectId: alpha.Id));

        Assert.Equal([match.Id], ids);
    }

    [Fact]
    public async Task Scope_RejectsEndBeforeStart()
    {
        await using var harness = AnalyticsHarness.Create(Now);

        await Assert.ThrowsAsync<AnalyticsFilterException>(() =>
            harness.Scope.ScopeAsync(new AnalyticsFilter(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 9)), CancellationToken.None));
    }

    [Fact]
    public async Task Scope_RejectsUnreasonablyLongRange()
    {
        await using var harness = AnalyticsHarness.Create(Now);

        await Assert.ThrowsAsync<AnalyticsFilterException>(() =>
            harness.Scope.ScopeAsync(new AnalyticsFilter(new DateOnly(1900, 1, 1), new DateOnly(2026, 6, 9)), CancellationToken.None));
    }

    [Theory]
    [InlineData("department")]
    [InlineData("project")]
    [InlineData("employee")]
    [InlineData("manager")]
    public async Task Scope_UnknownIdsAreNotFound(string kind)
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var filter = kind switch
        {
            "department" => new AnalyticsFilter(DepartmentId: 999),
            "project" => new AnalyticsFilter(ProjectId: 999),
            "employee" => new AnalyticsFilter(EmployeeId: 999),
            _ => new AnalyticsFilter(ManagerId: 999)
        };

        await Assert.ThrowsAsync<AnalyticsEntityNotFoundException>(() =>
            harness.Scope.ScopeAsync(filter, CancellationToken.None));
    }

    [Fact]
    public async Task Buckets_SumToTotalAcrossFilterCombinations()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var engineering = harness.AddDepartment("Engineering");
        var dev = harness.AddUser("dev", departmentId: engineering.Id);
        var other = harness.AddUser("other");
        var alpha = harness.AddProject("Alpha");
        var past = Now.UtcDateTime.AddDays(-3);
        var future = Now.UtcDateTime.AddDays(3);
        foreach (var status in Enum.GetValues<TaskStatus>())
        {
            harness.AddTask(dev, status, deadline: past, projectId: alpha.Id);
            harness.AddTask(dev, status, deadline: future);
            harness.AddTask(other, status, deadline: past, projectId: alpha.Id);
            harness.AddTask(other, status, deadline: future);
        }

        var filters = new[]
        {
            new AnalyticsFilter(),
            new AnalyticsFilter(DepartmentId: engineering.Id),
            new AnalyticsFilter(ProjectId: alpha.Id),
            new AnalyticsFilter(EmployeeId: other.Id, ProjectId: alpha.Id),
            new AnalyticsFilter(Basis: AnalyticsDateBasis.Deadline)
        };

        foreach (var filter in filters)
        {
            var scoped = await harness.Scope.ScopeAsync(filter, CancellationToken.None);
            var total = await harness.Analytics.CountTasksAsync(scoped.Tasks);
            var buckets = await harness.Analytics.CountByStatusAsync(scoped.Tasks, scoped.Filter.NowUtc);
            var sum = buckets.Pending + buckets.InProgress + buckets.Completed + buckets.Overdue;

            Assert.Equal(total, sum);
        }
    }
}
