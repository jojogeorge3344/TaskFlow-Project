using TaskFlow.Api.Common.Security;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests.Monitoring.TaskManagement.AssigneeCandidates;

public class AssigneeCandidateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Future = new(2026, 6, 10);
    private static readonly DateTime Past = new(2026, 5, 20);

    [Fact]
    public async Task GetCandidatesAsync_UnknownOrDeletedProject_ReturnsNull()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var deleted = harness.AddProject("Old");
        deleted.IsDeleted = true;
        harness.Db.SaveChanges();

        Assert.Null(await harness.AssigneeCandidates.GetCandidatesAsync(9999, CancellationToken.None));
        Assert.Null(await harness.AssigneeCandidates.GetCandidatesAsync(deleted.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetCandidatesAsync_ProjectWithoutTeam_ReturnsEmpty()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Empty");
        harness.AddUser("elsewhere", fullName: "Elsewhere Ed");

        var candidates = await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None);

        Assert.NotNull(candidates);
        Assert.Empty(candidates);
    }

    [Fact]
    public async Task GetCandidatesAsync_ListsOnlyActiveAssignableTeamMembers()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Apollo");
        var other = harness.AddProject("Zeus");
        var member = harness.AddUser("member", projectId: project.Id, fullName: "Mia Member");
        member.EmployeeCode = "EMP001";
        harness.Db.SaveChanges();
        harness.AddUser("inactive", isActive: false, projectId: project.Id, fullName: "Ina Inactive");
        harness.AddUser("boss", role: SystemRoles.Manager, projectId: project.Id, fullName: "Bo Boss");
        harness.AddUser("outsider", projectId: other.Id, fullName: "Otto Outsider");

        var candidates = await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None);

        var only = Assert.Single(candidates!);
        Assert.Equal(member.Id, only.UserId);
        Assert.Equal("Mia Member", only.FullName);
        Assert.Equal("EMP001", only.EmployeeCode);
    }

    [Fact]
    public async Task GetCandidatesAsync_CountsActiveAndOverdueAcrossAllProjectsAndIgnoresCancelledAndDeleted()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Apollo");
        var alice = harness.AddUser("alice", projectId: project.Id, fullName: "Alice Adams");

        harness.AddTask(alice.Id, Future, TaskStatus.ToDo);                    // active
        harness.AddTask(alice.Id, Past, TaskStatus.InProgress);                // active + overdue
        harness.AddTask(alice.Id, Future, TaskStatus.InQA);                    // active
        harness.AddTask(alice.Id, Past, TaskStatus.Cancelled);                 // ignored
        harness.AddTask(alice.Id, Future, TaskStatus.ToDo, isDeleted: true);   // ignored

        var row = Assert.Single((await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None))!);

        Assert.Equal(3, row.ActiveTasks);
        Assert.Equal(1, row.OverdueTasks);
        Assert.Null(row.OnTimeRate);
    }

    [Fact]
    public async Task GetCandidatesAsync_OnTimeRateIsCompletedByDeadlineOverAllCompleted()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Apollo");
        var alice = harness.AddUser("alice", projectId: project.Id, fullName: "Alice Adams");

        harness.AddTask(alice.Id, Past, TaskStatus.Completed, completedAt: Past.AddDays(-1));  // early: on time
        harness.AddTask(alice.Id, Past, TaskStatus.Completed, completedAt: Past);              // at the deadline: on time
        harness.AddTask(alice.Id, Past, TaskStatus.Completed, completedAt: Past.AddDays(1));   // late
        harness.AddTask(alice.Id, Past, TaskStatus.Completed);                                 // no completion time: not on time

        var row = Assert.Single((await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None))!);

        Assert.Equal(0.5, row.OnTimeRate);
        Assert.Equal(0, row.ActiveTasks);
    }

    [Fact]
    public async Task GetCandidatesAsync_FewestActiveTasksComesFirst()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Apollo");
        var a = harness.AddUser("a", projectId: project.Id, fullName: "A");
        var b = harness.AddUser("b", projectId: project.Id, fullName: "B");
        var c = harness.AddUser("c", projectId: project.Id, fullName: "C");
        AddActive(harness, a.Id, 5);
        AddActive(harness, b.Id, 2);
        AddActive(harness, c.Id, 4);

        var candidates = await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None);

        Assert.Equal(new[] { b.Id, c.Id, a.Id }, candidates!.Select(candidate => candidate.UserId));
    }

    [Fact]
    public async Task GetCandidatesAsync_AllocationPercentHasNoEffect()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Apollo");
        var full = harness.AddUser("full", projectId: project.Id, fullName: "Full Fiona", allocationPercent: 100);
        var light = harness.AddUser("light", projectId: project.Id, fullName: "Light Liam", allocationPercent: 20);
        AddActive(harness, full.Id, 1);
        AddActive(harness, light.Id, 4);

        var candidates = await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None);

        Assert.Equal(full.Id, candidates![0].UserId);
    }

    [Fact]
    public async Task GetCandidatesAsync_TieOnActive_FewerOverdueWins()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Apollo");
        var a = harness.AddUser("a", projectId: project.Id, fullName: "A");
        var b = harness.AddUser("b", projectId: project.Id, fullName: "B");
        harness.AddTask(a.Id, Past, TaskStatus.ToDo);
        harness.AddTask(a.Id, Future, TaskStatus.ToDo);
        AddActive(harness, b.Id, 2);

        var candidates = await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None);

        Assert.Equal(b.Id, candidates![0].UserId);
    }

    [Fact]
    public async Task GetCandidatesAsync_TieOnActiveAndOverdue_HigherOnTimeRateWins()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Apollo");
        var a = harness.AddUser("a", projectId: project.Id, fullName: "A");
        var b = harness.AddUser("b", projectId: project.Id, fullName: "B");
        harness.AddTask(a.Id, Past, TaskStatus.Completed, completedAt: Past.AddDays(1));   // A: 0% on time
        harness.AddTask(b.Id, Past, TaskStatus.Completed, completedAt: Past.AddDays(-1));  // B: 100% on time

        var candidates = await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None);

        Assert.Equal(new[] { b.Id, a.Id }, candidates!.Select(candidate => candidate.UserId));
    }

    [Fact]
    public async Task GetCandidatesAsync_NoCompletedHistoryRanksAfterAnyRate()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Apollo");
        var fresh = harness.AddUser("fresh", projectId: project.Id, fullName: "Aaron Fresh");
        var late = harness.AddUser("late", projectId: project.Id, fullName: "Zara Late");
        harness.AddTask(late.Id, Past, TaskStatus.Completed, completedAt: Past.AddDays(1));   // 0%, but a rate

        var candidates = await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None);

        Assert.Equal(new[] { late.Id, fresh.Id }, candidates!.Select(candidate => candidate.UserId));
    }

    [Fact]
    public async Task GetCandidatesAsync_FullTie_FallsBackToNameThenId()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var project = harness.AddProject("Apollo");
        var bala = harness.AddUser("bala", projectId: project.Id, fullName: "Bala");
        var anita2 = harness.AddUser("anita2", projectId: project.Id, fullName: "anita");
        var anita1 = harness.AddUser("anita1", projectId: project.Id, fullName: "Anita");

        var candidates = await harness.AssigneeCandidates.GetCandidatesAsync(project.Id, CancellationToken.None);

        // Name compares without case, so the two Anitas tie and the lower id goes first.
        Assert.Equal(new[] { anita2.Id, anita1.Id, bala.Id }, candidates!.Select(candidate => candidate.UserId));
    }

    private static void AddActive(WorkloadReportHarness harness, int userId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            harness.AddTask(userId, Future, TaskStatus.ToDo);
        }
    }
}
