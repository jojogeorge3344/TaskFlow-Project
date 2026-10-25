using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Services;
using TaskFlow.Application.Tasks.Validators;
using Task = System.Threading.Tasks.Task;
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class ProjectTaskTests
{
    private static CreateTaskRequest Request(int assigneeId, int projectId) => new(
        "Needs a project", null, assigneeId, TaskPriority.Low,
        DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 1m, ProjectId: projectId);

    [Fact]
    public void Validator_RejectsMissingProject()
    {
        var result = new CreateTaskRequestValidator().Validate(Request(1, 0));

        Assert.Contains(result.Errors, error => error.ErrorMessage == "A project is required.");
    }

    [Fact]
    public async Task CreateAsync_UnknownOrDeletedProject_IsRejected()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("pt-mgr", SystemRoles.Manager);
        var assignee = harness.AddUser("pt-assignee");
        var deleted = harness.AddProject();
        deleted.IsDeleted = true;
        await harness.Db.SaveChangesAsync();

        foreach (var projectId in new[] { 0, 9999, deleted.Id })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.TaskService.CreateAsync(Request(assignee.Id, projectId), manager.Id, SystemRoles.Manager, CancellationToken.None));
        }

        Assert.Equal(0, await harness.Db.Tasks.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ValidProject_MapsTheTask()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("pt-mgr2", SystemRoles.Manager);
        var assignee = harness.AddUser("pt-assignee2");
        var project = harness.AddProject();

        var details = await harness.TaskService.CreateAsync(Request(assignee.Id, project.Id), manager.Id, SystemRoles.Manager, CancellationToken.None);

        Assert.Equal(project.Id, (await harness.Db.Tasks.SingleAsync(task => task.Id == details.Id)).ProjectId);
    }

[Fact]
    public async Task DetailsAndList_ExposeProjectName_AndNullForLegacyTasks()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("pn-mgr", SystemRoles.Manager);
        var assignee = harness.AddUser("pn-assignee");
        var project = harness.AddProject("Apollo");

        var created = await harness.TaskService.CreateAsync(
            Request(assignee.Id, project.Id) with { FirstReminderDays = 5, SecondReminderDays = 3, OverdueReminderFrequency = ReminderFrequency.Weekly },
            manager.Id, SystemRoles.Manager, CancellationToken.None);
        var legacy = harness.AddTask(assignee.Id, manager.Id, "Legacy");

        var details = await harness.TaskService.GetDetailsAsync(created.Id, manager.Id, SystemRoles.Manager, CancellationToken.None);
        var list = await harness.TaskService.ListAsync(new TaskListFilter(), manager.Id, SystemRoles.Manager, CancellationToken.None);

        Assert.Equal(project.Id, details!.ProjectId);
        Assert.Equal("Apollo", details.ProjectName);
        Assert.Equal(5, details.FirstReminderDays);
        Assert.Equal(3, details.SecondReminderDays);
        Assert.Equal(ReminderFrequency.Weekly, details.OverdueReminderFrequency);
        Assert.Equal("Apollo", list.Items.Single(item => item.Id == created.Id).ProjectName);
        Assert.Null(list.Items.Single(item => item.Id == legacy.Id).ProjectName);
    }

    [Fact]
    public async Task GetTasksAsync_FiltersAndReportsAssigneeRole()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("pt-mgr3", SystemRoles.Manager);
        var dev = harness.AddUser("pt-dev");
        var qa = harness.AddUser("pt-qa");
        var project = harness.AddProject();
        var other = harness.AddProject();
        var devRole = await harness.Db.Positions.SingleAsync(p => p.Name == "Developer");
        var qaRole = await harness.Db.Positions.SingleAsync(p => p.Name == "QA");
        await harness.Db.SaveChangesAsync();
        harness.Db.UserProjectAllocations.AddRange(
            new UserProjectAllocation { UserId = dev.Id, ProjectId = project.Id, AllocationPercent = 50, PositionId = devRole.Id },
            new UserProjectAllocation { UserId = qa.Id, ProjectId = project.Id, AllocationPercent = 50, PositionId = qaRole.Id });

        var devTask = harness.AddTask(dev.Id, manager.Id, "Dev work", priority: TaskPriority.High);
        var qaTask = harness.AddTask(qa.Id, manager.Id, "QA work", TaskStatus.InProgress);
        var elsewhere = harness.AddTask(dev.Id, manager.Id, "Other project");
        devTask.ProjectId = project.Id;
        qaTask.ProjectId = project.Id;
        elsewhere.ProjectId = other.Id;
        await harness.Db.SaveChangesAsync();

        var service = new ProjectService(harness.Db, new UserAllocationService(harness.Db));

        var all = await service.GetTasksAsync(project.Id, new ProjectTaskFilter(), CancellationToken.None);
        Assert.Equal(2, all!.Count);
        Assert.Equal("Developer", all.Single(t => t.Id == devTask.Id).AssigneePositionName);

        var byRole = await service.GetTasksAsync(project.Id, new ProjectTaskFilter(PositionId: qaRole.Id), CancellationToken.None);
        Assert.Equal(qaTask.Id, Assert.Single(byRole!).Id);

        var combined = await service.GetTasksAsync(project.Id, new ProjectTaskFilter(Priority: TaskPriority.High, AssignedToUserId: dev.Id), CancellationToken.None);
        Assert.Equal(devTask.Id, Assert.Single(combined!).Id);

        var none = await service.GetTasksAsync(project.Id, new ProjectTaskFilter(Status: TaskStatus.InProgress, PositionId: devRole.Id), CancellationToken.None);
        Assert.Empty(none!);

        Assert.Null(await service.GetTasksAsync(9999, new ProjectTaskFilter(), CancellationToken.None));
    }
}
