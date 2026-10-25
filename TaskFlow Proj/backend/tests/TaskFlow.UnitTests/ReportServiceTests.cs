using TaskFlow.Application.Reports;
using TaskFlow.Domain.Enums;
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class ReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Future = new(2026, 6, 10);
    private static readonly DateTime Past = new(2026, 5, 20);

    [Fact]
    public async Task GetSummaryAsync_NoFilters_CountsEveryTaskInScope()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice");
        var bob = harness.AddUser("bob");

        harness.AddTask(alice.Id, Future, TaskStatus.ToDo);
        harness.AddTask(alice.Id, Past, TaskStatus.Completed);
        harness.AddTask(bob.Id, Past, TaskStatus.ToDo);          // overdue

        var summary = await harness.Reports.GetSummaryAsync(new ReportFilter(), CancellationToken.None);

        Assert.Equal(3, summary.TotalTasks);
        Assert.Equal(2, summary.PendingTasks);
        Assert.Equal(1, summary.CompletedTasks);
        Assert.Equal(1, summary.OverdueTasks);
    }

    [Fact]
    public async Task GetSummaryAsync_CombinedFilters_ReturnMatchingTotals()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice");
        var bob = harness.AddUser("bob");

        harness.AddTask(alice.Id, Future, TaskStatus.ToDo);
        harness.AddTask(alice.Id, Past, TaskStatus.Completed);
        harness.AddTask(bob.Id, Past, TaskStatus.Completed);

        var summary = await harness.Reports.GetSummaryAsync(
            new ReportFilter(AssignedToUserId: alice.Id, Status: TaskStatus.Completed),
            CancellationToken.None);

        Assert.Equal(1, summary.TotalTasks);
        Assert.Equal(0, summary.PendingTasks);
        Assert.Equal(1, summary.CompletedTasks);
        Assert.Equal(0, summary.OverdueTasks);
    }

    [Fact]
    public async Task GetSummaryAsync_EmptyScope_ReturnsZeros()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice");
        harness.AddTask(alice.Id, Future, TaskStatus.ToDo);

        var summary = await harness.Reports.GetSummaryAsync(
            new ReportFilter(AssignedToUserId: 987654),
            CancellationToken.None);

        Assert.Equal(0, summary.TotalTasks);
        Assert.Equal(0, summary.PendingTasks);
        Assert.Equal(0, summary.CompletedTasks);
        Assert.Equal(0, summary.OverdueTasks);
    }

    [Fact]
    public async Task GetSummaryAsync_DeadlineRange_IsInclusiveOfBoundaries()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice");

        harness.AddTask(alice.Id, new DateTime(2026, 6, 1), TaskStatus.ToDo);   // on the lower bound
        harness.AddTask(alice.Id, new DateTime(2026, 6, 2), TaskStatus.ToDo);   // on the upper bound
        harness.AddTask(alice.Id, new DateTime(2026, 6, 3), TaskStatus.ToDo);   // outside

        var summary = await harness.Reports.GetSummaryAsync(
            new ReportFilter(DeadlineFrom: new DateTime(2026, 6, 1), DeadlineTo: new DateTime(2026, 6, 2)),
            CancellationToken.None);

        Assert.Equal(2, summary.TotalTasks);
    }

    [Fact]
    public async Task GetSummaryAsync_LocalKindDeadlineBound_MatchesTheSameUtcInstant()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice");
        harness.AddTask(alice.Id, new DateTime(2026, 6, 1), TaskStatus.ToDo);
        harness.AddTask(alice.Id, new DateTime(2026, 6, 2), TaskStatus.ToDo);

        var utcBound = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        // A `Z`-suffixed query value binds to a local-kind DateTime holding the same instant.
        var localBound = utcBound.ToLocalTime();

        var fromUtc = await harness.Reports.GetSummaryAsync(
            new ReportFilter(DeadlineFrom: utcBound), CancellationToken.None);
        var fromLocal = await harness.Reports.GetSummaryAsync(
            new ReportFilter(DeadlineFrom: localBound), CancellationToken.None);

        Assert.Equal(2, fromUtc.TotalTasks);
        Assert.Equal(fromUtc.TotalTasks, fromLocal.TotalTasks);
    }

    [Fact]
    public async Task GetSummaryAsync_OverdueMatchesTheDashboardRule()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice");

        harness.AddTask(alice.Id, Past, TaskStatus.ToDo);          // overdue
        harness.AddTask(alice.Id, Past, TaskStatus.Completed);     // completed, not overdue
        harness.AddTask(alice.Id, Past, TaskStatus.Cancelled);     // cancelled, not overdue
        harness.AddTask(alice.Id, Future, TaskStatus.ToDo);        // future, not overdue

        var summary = await harness.Reports.GetSummaryAsync(new ReportFilter(), CancellationToken.None);

        Assert.Equal(1, summary.OverdueTasks);
        Assert.Equal(4, summary.TotalTasks);
        Assert.Equal(2, summary.PendingTasks);
    }

    [Fact]
    public async Task GetSummaryAsync_PriorityFilter_NarrowsToThatPriority()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice");

        harness.AddTask(alice.Id, Future, TaskStatus.ToDo, TaskPriority.High);
        harness.AddTask(alice.Id, Future, TaskStatus.ToDo, TaskPriority.Medium);

        var summary = await harness.Reports.GetSummaryAsync(
            new ReportFilter(Priority: TaskPriority.High),
            CancellationToken.None);

        Assert.Equal(1, summary.TotalTasks);
    }

    [Fact]
    public async Task GetSummaryAsync_ProjectFilter_UsesTheAssigneeProject()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var engineering = harness.AddProject("Engineering");
        var operations = harness.AddProject("Operations");

        var alice = harness.AddUser("alice", projectId: engineering.Id);
        var bob = harness.AddUser("bob", projectId: operations.Id);
        harness.AddTask(alice.Id, Future, TaskStatus.ToDo);
        harness.AddTask(bob.Id, Future, TaskStatus.ToDo);

        var summary = await harness.Reports.GetSummaryAsync(
            new ReportFilter(ProjectId: engineering.Id),
            CancellationToken.None);

        Assert.Equal(1, summary.TotalTasks);
    }

    [Fact]
    public async Task GetRowsAsync_HonoursTheSameFilterAsTheTotals()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice", fullName: "Alice Adams");
        var bob = harness.AddUser("bob", fullName: "Bob Brown");

        var mine = harness.AddTask(alice.Id, Future, TaskStatus.ToDo, TaskPriority.High);
        mine.Title = "Visible";
        harness.Db.SaveChanges();
        harness.AddTask(bob.Id, Future, TaskStatus.ToDo, TaskPriority.High);

        var filter = new ReportFilter(AssignedToUserId: alice.Id, Priority: TaskPriority.High);
        var summary = await harness.Reports.GetSummaryAsync(filter, CancellationToken.None);
        var rows = await harness.Reports.GetRowsAsync(filter, CancellationToken.None);

        var only = Assert.Single(rows);
        Assert.Equal(summary.TotalTasks, rows.Count);
        Assert.Equal("Visible", only.Title);
        Assert.Equal("Alice Adams", only.Assignee);
    }

    [Fact]
    public async Task GetRowsAsync_ProjectsAssigneeAndProject()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var engineering = harness.AddProject("Engineering");
        var alice = harness.AddUser("alice", projectId: engineering.Id, fullName: "Alice Adams");
        harness.AddTask(alice.Id, Future, TaskStatus.Completed, TaskPriority.Critical);

        var rows = await harness.Reports.GetRowsAsync(new ReportFilter(), CancellationToken.None);

        var only = Assert.Single(rows);
        Assert.Equal("Alice Adams", only.Assignee);
        Assert.Equal("Engineering", only.Project);
        Assert.Equal(TaskPriority.Critical, only.Priority);
        Assert.Equal(TaskStatus.Completed, only.Status);
    }
}
