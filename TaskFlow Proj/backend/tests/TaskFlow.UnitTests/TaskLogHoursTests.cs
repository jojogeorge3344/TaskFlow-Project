using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;

namespace TaskFlow.UnitTests;

public class TaskLogHoursTests
{
    [Fact]
    public async Task LogHours_ByTheAssigneeWithoutEditRights_AddsToTheTotalAndRecordsHistory()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("lh-mgr", SystemRoles.Manager);
        var assignee = harness.AddUser("lh-emp");
        var task = harness.AddTask(assignee.Id, manager.Id);

        var result = await harness.TaskService.LogHoursAsync(task.Id, new LogHoursRequest(2.5m), assignee.Id, SystemRoles.Employee, CancellationToken.None);
        await harness.TaskService.LogHoursAsync(task.Id, new LogHoursRequest(1m), assignee.Id, SystemRoles.Employee, CancellationToken.None);

        Assert.Equal(2.5m, result!.ActualHours);
        Assert.Equal(3.5m, (await harness.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == task.Id)).ActualHours);
        Assert.Equal(2, await harness.Db.TaskActivityLogs.CountAsync(a => a.TaskId == task.Id && a.EventType == TaskActivityEventType.HoursLogged));
    }

    [Fact]
    public async Task LogHours_NotifiesTheOtherAssignedMembers()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("lh-mgr2", SystemRoles.Manager);
        var assignee = harness.AddUser("lh-emp2");
        var task = harness.AddTask(assignee.Id, manager.Id);
        harness.AddRecipient(task.Id, manager.Id);

        await harness.TaskService.LogHoursAsync(task.Id, new LogHoursRequest(2m), assignee.Id, SystemRoles.Employee, CancellationToken.None);

        Assert.Contains(await harness.Db.Notifications.ToListAsync(), n => n.UserId == manager.Id && n.Message.Contains("added 2h"));
    }

    [Fact]
    public async Task LogHours_ByAnotherEmployee_IsRejected()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("lh-mgr3", SystemRoles.Manager);
        var assignee = harness.AddUser("lh-emp3");
        var other = harness.AddUser("lh-other");
        var task = harness.AddTask(assignee.Id, manager.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.LogHoursAsync(task.Id, new LogHoursRequest(1m), other.Id, SystemRoles.Employee, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25)]
    public async Task LogHours_RejectsOutOfRangeHours(int hours)
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("lh-mgr4", SystemRoles.Manager);
        var assignee = harness.AddUser("lh-emp4");
        var task = harness.AddTask(assignee.Id, manager.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.TaskService.LogHoursAsync(task.Id, new LogHoursRequest(hours), assignee.Id, SystemRoles.Employee, CancellationToken.None));
    }

    [Fact]
    public async Task LogHours_ForAMissingTask_ReturnsNull()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var assignee = harness.AddUser("lh-emp5");

        Assert.Null(await harness.TaskService.LogHoursAsync(9999, new LogHoursRequest(1m), assignee.Id, SystemRoles.Employee, CancellationToken.None));
    }
}
