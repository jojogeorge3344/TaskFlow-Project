using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using TaskFlow.Api.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.UnitTests.Monitoring.TaskManagement;

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

        await Assert.ThrowsAsync<ForbiddenException>(() =>
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

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            harness.TaskService.LogHoursAsync(task.Id, new LogHoursRequest(hours), assignee.Id, SystemRoles.Employee, CancellationToken.None));
    }

    [Fact]
    public async Task LogHours_WhenTheHoursChangedMeanwhile_AsksToTryAgain()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("lh-mgr6", SystemRoles.Manager);
        var assignee = harness.AddUser("lh-emp6");
        var task = harness.AddTask(assignee.Id, manager.Id);

        // Another request logs hours after this one loaded the task: the tracked copy still holds the old total.
        await harness.Db.Database.ExecuteSqlRawAsync("UPDATE \"Tasks\" SET \"ActualHours\" = 5 WHERE \"Id\" = {0}", task.Id);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            harness.TaskService.LogHoursAsync(task.Id, new LogHoursRequest(1m), assignee.Id, SystemRoles.Employee, CancellationToken.None));

        Assert.Contains("worked hours changed", error.Message);
    }

    [Fact]
    public async Task LogHours_ForAMissingTask_ReturnsNull()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var assignee = harness.AddUser("lh-emp5");

        Assert.Null(await harness.TaskService.LogHoursAsync(9999, new LogHoursRequest(1m), assignee.Id, SystemRoles.Employee, CancellationToken.None));
    }
}
