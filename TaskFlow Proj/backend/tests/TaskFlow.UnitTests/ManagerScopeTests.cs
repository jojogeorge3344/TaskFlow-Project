using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Domain.Security;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class ManagerScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static async Task<List<int>> TaskIdsAsync(AnalyticsHarness harness, int managerId, AnalyticsFilter? filter = null)
    {
        var scoped = await harness.ManagerScope.ScopeAsync(managerId, filter ?? new AnalyticsFilter(), CancellationToken.None);
        return await scoped.Tasks.Select(task => task.Id).OrderBy(id => id).ToListAsync();
    }

    [Fact]
    public async Task Team_IncludesDirectReportOutsideManagedDepartments()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var other = harness.AddDepartment("Other");
        var report = harness.AddUser("e1", departmentId: other.Id, managerId: manager.Id);

        Assert.Equal([report.Id], await harness.ManagerScope.TeamUserIds(manager.Id).ToListAsync());
    }

    [Fact]
    public async Task Team_IncludesManagedDepartmentMemberWhoDoesNotReportToManager()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var department = harness.AddDepartment("Eng", manager);
        var member = harness.AddUser("e1", departmentId: department.Id);

        Assert.Equal([member.Id], await harness.ManagerScope.TeamUserIds(manager.Id).ToListAsync());
    }

    [Fact]
    public async Task Team_CountsAUserOnceWhenBothRelationsApply()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var department = harness.AddDepartment("Eng", manager);
        var both = harness.AddUser("e1", departmentId: department.Id, managerId: manager.Id);

        Assert.Equal([both.Id], await harness.ManagerScope.TeamUserIds(manager.Id).ToListAsync());
    }

    [Fact]
    public async Task Team_ExcludesInactiveUsersAndOtherTeams()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var otherManager = harness.AddUser("m2", SystemRoles.Manager);
        var kept = harness.AddUser("e1", managerId: manager.Id);
        harness.AddUser("e2", managerId: manager.Id, isActive: false);
        harness.AddUser("e3", managerId: otherManager.Id);

        Assert.Equal([kept.Id], await harness.ManagerScope.TeamUserIds(manager.Id).ToListAsync());
    }

    [Fact]
    public async Task Team_ExcludesTheManagerThemselves()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        harness.AddDepartment("Eng", manager);
        harness.AddTask(manager);

        Assert.Empty(await harness.ManagerScope.TeamUserIds(manager.Id).ToListAsync());
        Assert.Empty(await TaskIdsAsync(harness, manager.Id));
    }

    [Fact]
    public async Task Tasks_AreTheTeamsAssignedAndSharedTasksOnly()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var member = harness.AddUser("e1", managerId: manager.Id);
        var outsider = harness.AddUser("e2");
        var assigned = harness.AddTask(member);
        var shared = harness.AddTask(outsider);
        harness.AddRecipient(shared, member);
        harness.AddTask(outsider);
        harness.AddTask(member, TaskStatus.Cancelled);
        harness.AddTask(member, isDeleted: true);

        Assert.Equal([assigned.Id, shared.Id], await TaskIdsAsync(harness, manager.Id));
    }

    [Fact]
    public async Task EmptyTeam_YieldsNoTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var outsider = harness.AddUser("e1");
        harness.AddTask(outsider);

        Assert.Empty(await harness.ManagerScope.TeamUserIds(manager.Id).ToListAsync());
        Assert.Empty(await TaskIdsAsync(harness, manager.Id));
    }

    [Fact]
    public async Task Projects_AreAssignedOnesPlusThoseOfTeamTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var member = harness.AddUser("e1", managerId: manager.Id);
        var outsider = harness.AddUser("e2");
        var assignedOnly = harness.AddProject("Assigned");
        var viaTeam = harness.AddProject("ViaTeam");
        var foreign = harness.AddProject("Foreign");
        harness.AddManagerProject(manager, assignedOnly);
        harness.AddTask(member, projectId: viaTeam.Id);
        harness.AddTask(outsider, projectId: foreign.Id);

        var ids = await harness.ManagerScope.ProjectIds(manager.Id).OrderBy(id => id).ToListAsync();

        Assert.Equal([assignedOnly.Id, viaTeam.Id], ids);
    }
}

public class ManagerScopeFilterTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EmployeeFilter_InTeam_IsAccepted()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var member = harness.AddUser("e1", managerId: manager.Id);
        var task = harness.AddTask(member);
        harness.AddTask(harness.AddUser("e2", managerId: manager.Id));

        var scoped = await harness.ManagerScope.ScopeAsync(manager.Id, new AnalyticsFilter(EmployeeId: member.Id), CancellationToken.None);

        Assert.Equal([task.Id], await scoped.Tasks.Select(t => t.Id).ToListAsync());
    }

    [Fact]
    public async Task EmployeeFilter_OnAnotherManagersTeam_IsForbidden()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var otherManager = harness.AddUser("m2", SystemRoles.Manager);
        var foreign = harness.AddUser("e1", managerId: otherManager.Id);

        await Assert.ThrowsAsync<AnalyticsForbiddenException>(() =>
            harness.ManagerScope.ScopeAsync(manager.Id, new AnalyticsFilter(EmployeeId: foreign.Id), CancellationToken.None));
    }

    [Fact]
    public async Task EmployeeFilter_Unknown_IsNotFound()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);

        await Assert.ThrowsAsync<AnalyticsEntityNotFoundException>(() =>
            harness.ManagerScope.ScopeAsync(manager.Id, new AnalyticsFilter(EmployeeId: 99999), CancellationToken.None));
    }

    [Fact]
    public async Task ProjectFilter_InScope_IsAccepted()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var member = harness.AddUser("e1", managerId: manager.Id);
        var project = harness.AddProject("P");
        var inProject = harness.AddTask(member, projectId: project.Id);
        harness.AddTask(member);

        var scoped = await harness.ManagerScope.ScopeAsync(manager.Id, new AnalyticsFilter(ProjectId: project.Id), CancellationToken.None);

        Assert.Equal([inProject.Id], await scoped.Tasks.Select(t => t.Id).ToListAsync());
    }

    [Fact]
    public async Task ProjectFilter_OfOnlyOtherTeams_IsForbidden()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var outsider = harness.AddUser("e1");
        var foreign = harness.AddProject("Foreign");
        harness.AddTask(outsider, projectId: foreign.Id);

        await Assert.ThrowsAsync<AnalyticsForbiddenException>(() =>
            harness.ManagerScope.ScopeAsync(manager.Id, new AnalyticsFilter(ProjectId: foreign.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ProjectFilter_Unknown_IsNotFound()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);

        await Assert.ThrowsAsync<AnalyticsEntityNotFoundException>(() =>
            harness.ManagerScope.ScopeAsync(manager.Id, new AnalyticsFilter(ProjectId: 99999), CancellationToken.None));
    }

    [Fact]
    public async Task DepartmentAndManagerFilters_AreIgnored()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var other = harness.AddDepartment("Other");
        var member = harness.AddUser("e1", managerId: manager.Id);
        var outsider = harness.AddUser("e2", departmentId: other.Id);
        var kept = harness.AddTask(member);
        harness.AddTask(outsider);

        var scoped = await harness.ManagerScope.ScopeAsync(
            manager.Id,
            new AnalyticsFilter(DepartmentId: other.Id, ManagerId: outsider.Id),
            CancellationToken.None);

        Assert.Equal([kept.Id], await scoped.Tasks.Select(t => t.Id).ToListAsync());
        Assert.Null(scoped.Filter.DepartmentId);
        Assert.Null(scoped.Filter.ManagerId);
    }
}
