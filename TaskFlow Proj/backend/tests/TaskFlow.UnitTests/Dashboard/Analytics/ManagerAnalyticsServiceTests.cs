using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Security;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using User = TaskFlow.Api.Models.Entities.User;

namespace TaskFlow.UnitTests.Dashboard.Analytics;

public class ManagerAnalyticsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Past = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Task<ManagerAnalyticsResponseDto> GetAsync(AnalyticsHarness harness, User manager, AnalyticsFilter? filter = null) =>
        harness.ManagerService.GetAsync(manager.Id, filter ?? new AnalyticsFilter(), CancellationToken.None);

    private static (User Manager, User[] Members) TeamOf(AnalyticsHarness harness, int size)
    {
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        var members = Enumerable.Range(1, size)
            .Select(index => harness.AddUser($"e{index}", managerId: manager.Id))
            .ToArray();
        return (manager, members);
    }

    private static void AddOpen(AnalyticsHarness harness, User member, int count)
    {
        for (var i = 0; i < count; i++)
        {
            harness.AddTask(member);
        }
    }

    [Fact]
    public async Task Tasks_ReconcileAcrossSummaryStatusAndPriority()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 2);
        harness.AddTask(members[0], TaskStatus.ToDo, priority: TaskPriority.High);
        harness.AddTask(members[0], TaskStatus.InProgress, priority: TaskPriority.Low);
        harness.AddTask(members[1], TaskStatus.Completed);
        harness.AddTask(members[1], TaskStatus.ToDo, deadline: Past, priority: TaskPriority.High);
        harness.AddTask(harness.AddUser("outsider"));

        var result = await GetAsync(harness, manager);

        Assert.Equal(new BucketCountsDto(1, 1, 1, 1), result.TasksByStatus);
        Assert.Equal(4, result.Summary.TotalTasks);
        Assert.Equal(25, result.Summary.CompletionPercent);
        Assert.Equal(result.Summary.TotalTasks, result.TasksByPriority.Sum(priority => priority.Count));
        Assert.Equal(2, result.Summary.TeamSize);
        Assert.Null(result.Filter.DepartmentId);
    }

    [Fact]
    public async Task EmptyTeam_ReturnsZerosAndNoRows()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        harness.AddTask(harness.AddUser("outsider"));

        var result = await GetAsync(harness, manager);

        Assert.Equal(0, result.Summary.TotalTasks);
        Assert.Equal(0, result.Summary.TeamSize);
        Assert.Empty(result.ByEmployee);
        Assert.Empty(result.ByProject);
        Assert.Empty(result.Attention.DelayedTasks);
    }

    [Fact]
    public async Task HighWorkload_FlagsAtExactlyOnePointFiveTimesTheMean()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 2);
        AddOpen(harness, members[0], 6);
        AddOpen(harness, members[1], 2);

        var result = await GetAsync(harness, manager);

        Assert.True(result.ByEmployee.Single(row => row.UserId == members[0].Id).HighWorkload);
        Assert.False(result.ByEmployee.Single(row => row.UserId == members[1].Id).HighWorkload);
    }

    [Fact]
    public async Task HighWorkload_NotFlaggedJustBelowOnePointFiveTimesTheMean()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 2);
        AddOpen(harness, members[0], 5);
        AddOpen(harness, members[1], 2);

        var result = await GetAsync(harness, manager);

        Assert.DoesNotContain(result.ByEmployee, row => row.HighWorkload);
    }

    [Fact]
    public async Task HighWorkload_RequiresAtLeastFiveOpenTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 2);
        AddOpen(harness, members[0], ManagerTeamThresholds.HighWorkloadMinimumOpen - 1);

        var result = await GetAsync(harness, manager);

        Assert.DoesNotContain(result.ByEmployee, row => row.HighWorkload);
    }

    [Fact]
    public async Task HighWorkload_UsesTheWholeTeamMeanEvenWhenFilteredToOneEmployee()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 2);
        AddOpen(harness, members[0], 6);
        AddOpen(harness, members[1], 2);

        var result = await GetAsync(harness, manager, new AnalyticsFilter(EmployeeId: members[0].Id));

        var row = Assert.Single(result.ByEmployee);
        Assert.True(row.HighWorkload);
        Assert.Equal(1, result.Summary.TeamSize);
    }

    [Fact]
    public async Task OverdueFlag_SetWhenAnyTaskIsOverdue()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 2);
        harness.AddTask(members[0], TaskStatus.ToDo, deadline: Past);
        harness.AddTask(members[1]);

        var result = await GetAsync(harness, manager);

        var late = result.ByEmployee.Single(row => row.UserId == members[0].Id);
        Assert.True(late.HasOverdue);
        Assert.Equal(1, late.Tasks.Overdue);
        Assert.False(result.ByEmployee.Single(row => row.UserId == members[1].Id).HasOverdue);
    }

    [Fact]
    public async Task Employees_ListsOnlyTeamMembersIncludingThoseWithNoTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 2);
        var outsider = harness.AddUser("outsider");
        var shared = harness.AddTask(outsider);
        harness.AddRecipient(shared, members[0]);

        var result = await GetAsync(harness, manager);

        Assert.Equal(
            new[] { members[0].Id, members[1].Id }.OrderBy(id => id),
            result.ByEmployee.Select(row => row.UserId).OrderBy(id => id));
        Assert.DoesNotContain(result.ByEmployee, row => row.UserId == outsider.Id);
        Assert.Equal(1, result.ByEmployee.Single(row => row.UserId == members[0].Id).Tasks.Total);
        Assert.Equal(0, result.ByEmployee.Single(row => row.UserId == members[1].Id).Tasks.Total);
    }

    [Fact]
    public async Task Projects_CountOnlyTeamTasksAndListAssignedProjectsWithZeroTasks()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 1);
        var outsider = harness.AddUser("outsider");
        var shared = harness.AddProject("Shared");
        var assignedOnly = harness.AddProject("AssignedOnly");
        harness.AddManagerProject(manager, assignedOnly);
        harness.AddTask(members[0], projectId: shared.Id);
        harness.AddTask(outsider, projectId: shared.Id);
        harness.AddTask(outsider, projectId: shared.Id);

        var result = await GetAsync(harness, manager);

        Assert.Equal(1, result.ByProject.Single(project => project.ProjectId == shared.Id).Tasks.Total);
        var empty = result.ByProject.Single(project => project.ProjectId == assignedOnly.Id);
        Assert.Equal(0, empty.Tasks.Total);
        Assert.Equal(0, empty.Tasks.CompletionPercent);
    }

    [Fact]
    public async Task Projects_GroupTeamTasksWithoutAProjectUnderNoProject()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 1);
        harness.AddTask(members[0]);

        var result = await GetAsync(harness, manager);

        var row = Assert.Single(result.ByProject);
        Assert.Null(row.ProjectId);
        Assert.Equal(1, row.Tasks.Total);
    }

    [Fact]
    public async Task Attention_ListsMostOverdueFirstCappedAtFiveAndProjectsWithOverdue()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 1);
        var project = harness.AddProject("Apollo");
        for (var day = 1; day <= 7; day++)
        {
            harness.AddTask(members[0], TaskStatus.ToDo, deadline: Now.UtcDateTime.AddDays(-day), projectId: project.Id);
        }

        var result = await GetAsync(harness, manager);

        Assert.Equal(ManagerTeamThresholds.AttentionListSize, result.Attention.DelayedTasks.Count);
        Assert.Equal([7, 6, 5, 4, 3], result.Attention.DelayedTasks.Select(task => task.DaysOverdue));
        Assert.All(result.Attention.DelayedTasks, task => Assert.Equal("Apollo", task.ProjectName));
        var attention = Assert.Single(result.Attention.ProjectsNeedingAttention);
        Assert.Equal(7, attention.OverdueTasks);
    }

    [Fact]
    public async Task FilterOptions_ListEmployeesInTheDatabaseNameOrder()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("m1", SystemRoles.Manager);
        harness.AddUser("anna", managerId: manager.Id);
        harness.AddUser("Bob", managerId: manager.Id);

        var options = await harness.ManagerService.GetFilterOptionsAsync(manager.Id, CancellationToken.None);

        // SQLite compares names byte by byte, so upper case sorts before lower case, as the other name lists do.
        Assert.Equal(["Bob full name", "anna full name"], options.Employees.Select(employee => employee.Name));
    }

    [Fact]
    public async Task FilterOptions_ListOnlyInScopeEmployeesAndProjects()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var (manager, members) = TeamOf(harness, 1);
        var outsider = harness.AddUser("outsider");
        var mine = harness.AddProject("Mine");
        var foreign = harness.AddProject("Foreign");
        harness.AddTask(members[0], projectId: mine.Id);
        harness.AddTask(outsider, projectId: foreign.Id);

        var options = await harness.ManagerService.GetFilterOptionsAsync(manager.Id, CancellationToken.None);

        Assert.Equal([members[0].Id], options.Employees.Select(option => option.Id));
        Assert.Equal([mine.Id], options.Projects.Select(option => option.Id));
    }
}
