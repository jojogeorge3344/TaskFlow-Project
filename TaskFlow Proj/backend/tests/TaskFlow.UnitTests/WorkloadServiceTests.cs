using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class WorkloadServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Future = new(2026, 6, 10);
    private static readonly DateTime Past = new(2026, 5, 20);

    [Fact]
    public async Task GetWorkloadAsync_ReturnsPerEmployeeCounts()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var engineering = harness.AddDepartment("Engineering");

        var alice = harness.AddUser("alice", departmentId: engineering.Id, fullName: "Alice Adams");
        var bob = harness.AddUser("bob", fullName: "Bob Brown");

        harness.AddTask(alice.Id, Future, TaskStatus.ToDo, TaskPriority.High);        // active + high-priority
        harness.AddTask(alice.Id, Past, TaskStatus.ToDo, TaskPriority.Critical);      // active + high-priority + overdue
        harness.AddTask(alice.Id, Past, TaskStatus.Completed, TaskPriority.Medium);   // completed only
        harness.AddTask(alice.Id, Past, TaskStatus.Cancelled, TaskPriority.High);     // ignored entirely

        harness.AddTask(bob.Id, Future, TaskStatus.InProgress, TaskPriority.Low);     // active only

        var workload = await harness.Workload.GetWorkloadAsync(null, CancellationToken.None);
        var byName = workload.ToDictionary(entry => entry.FullName);

        var aliceRow = byName["Alice Adams"];
        Assert.Equal(engineering.Id, aliceRow.DepartmentId);
        Assert.Equal("Engineering", aliceRow.DepartmentName);
        Assert.Equal(2, aliceRow.ActiveTasks);
        Assert.Equal(2, aliceRow.HighPriorityTasks);
        Assert.Equal(1, aliceRow.OverdueTasks);
        Assert.Equal(1, aliceRow.CompletedTasks);

        var bobRow = byName["Bob Brown"];
        Assert.Null(bobRow.DepartmentId);
        Assert.Null(bobRow.DepartmentName);
        Assert.Equal(1, bobRow.ActiveTasks);
        Assert.Equal(0, bobRow.HighPriorityTasks);
        Assert.Equal(0, bobRow.OverdueTasks);
        Assert.Equal(0, bobRow.CompletedTasks);
    }

    [Fact]
    public async Task GetWorkloadAsync_InQaTaskCountsAsActiveNotCompleted()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var qa = harness.AddUser("qa", fullName: "QA Quinn");

        harness.AddTask(qa.Id, Future, TaskStatus.InQA, TaskPriority.Medium);

        var workload = await harness.Workload.GetWorkloadAsync(null, CancellationToken.None);
        var row = Assert.Single(workload);

        Assert.Equal(1, row.ActiveTasks);
        Assert.Equal(0, row.OverdueTasks);
        Assert.Equal(0, row.CompletedTasks);
    }

    [Fact]
    public async Task GetWorkloadAsync_DepartmentFilter_NarrowsToThatDepartment()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var engineering = harness.AddDepartment("Engineering");
        var operations = harness.AddDepartment("Operations");

        var alice = harness.AddUser("alice", departmentId: engineering.Id, fullName: "Alice Adams");
        var bob = harness.AddUser("bob", departmentId: operations.Id, fullName: "Bob Brown");
        harness.AddTask(alice.Id, Future, TaskStatus.ToDo);
        harness.AddTask(bob.Id, Future, TaskStatus.ToDo);

        var filtered = await harness.Workload.GetWorkloadAsync(engineering.Id, CancellationToken.None);

        var only = Assert.Single(filtered);
        Assert.Equal("Alice Adams", only.FullName);

        var all = await harness.Workload.GetWorkloadAsync(null, CancellationToken.None);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetWorkloadAsync_IncludesZeroLoadActiveEmployeesOnly()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var idle = harness.AddUser("idle", fullName: "Idle Ian");
        var inactive = harness.AddUser("gone", isActive: false, fullName: "Gone Greg");
        harness.AddTask(inactive.Id, Future, TaskStatus.ToDo);

        var workload = await harness.Workload.GetWorkloadAsync(null, CancellationToken.None);

        var only = Assert.Single(workload);
        Assert.Equal(idle.Id, only.UserId);
        Assert.Equal(0, only.ActiveTasks);
        Assert.Equal(0, only.HighPriorityTasks);
        Assert.Equal(0, only.OverdueTasks);
        Assert.Equal(0, only.CompletedTasks);
    }

    [Fact]
    public async Task GetWorkloadAsync_ExcludesNonEmployeeRoles()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var employee = harness.AddUser("emp", fullName: "Emma Employee");
        harness.AddUser("boss", role: SystemRoles.Manager, fullName: "Boss Manager");
        harness.AddUser("admin", role: SystemRoles.Administrator, fullName: "Admin User");
        harness.AddTask(employee.Id, Future, TaskStatus.ToDo);

        var workload = await harness.Workload.GetWorkloadAsync(null, CancellationToken.None);

        var only = Assert.Single(workload);
        Assert.Equal(employee.Id, only.UserId);
    }

    [Fact]
    public async Task GetWorkloadAsync_ExcludesDeletedTasksFromCounts()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice", fullName: "Alice Adams");
        harness.AddTask(alice.Id, Future, TaskStatus.ToDo);
        harness.AddTask(alice.Id, Future, TaskStatus.ToDo, isDeleted: true);

        var workload = await harness.Workload.GetWorkloadAsync(null, CancellationToken.None);

        var only = Assert.Single(workload);
        Assert.Equal(1, only.ActiveTasks);
    }

    [Fact]
    public async Task GetWorkloadAsync_OverdueMatchesTheDashboardRule()
    {
        await using var harness = WorkloadReportHarness.Create(Now);
        var alice = harness.AddUser("alice", fullName: "Alice Adams");

        harness.AddTask(alice.Id, Past, TaskStatus.ToDo);                                   // overdue
        harness.AddTask(alice.Id, Past, TaskStatus.Completed);                              // completed, not overdue
        harness.AddTask(alice.Id, Past, TaskStatus.Cancelled);                              // cancelled, not overdue
        harness.AddTask(alice.Id, Future, TaskStatus.ToDo);                                 // future, not overdue

        var workload = await harness.Workload.GetWorkloadAsync(null, CancellationToken.None);

        var only = Assert.Single(workload);
        Assert.Equal(1, only.OverdueTasks);
    }
}
