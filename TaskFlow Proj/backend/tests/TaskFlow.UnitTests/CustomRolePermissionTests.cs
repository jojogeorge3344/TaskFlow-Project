using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using Task = System.Threading.Tasks.Task;
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;

namespace TaskFlow.UnitTests;

/// <summary>Task scoping and task rules follow the permissions a role holds, not the name of the role, so a custom role
/// behaves according to the boxes ticked for it.</summary>
public class CustomRolePermissionTests
{
    private static User InRole(TaskTrackingServiceHarness harness, string username, Role role)
    {
        var user = harness.AddUser(username);
        user.RoleId = role.Id;
        harness.Db.SaveChanges();
        return user;
    }

    private static CreateTaskRequest NewTask(int assigneeId) => new(
        "Custom role task",
        "Body",
        assigneeId,
        TaskPriority.Medium,
        DateTime.UtcNow.AddDays(1),
        DateTime.UtcNow.AddDays(3),
        4m);

    [Fact]
    public async Task ARoleWithTasksViewAllSeesEveryTaskAndARoleWithoutItSeesOnlyItsOwn()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var orgRole = TestAccessControl.AddCustomRole(harness.Db, "Org Viewer", PermissionKeys.TasksViewAll);
        var ownRole = TestAccessControl.AddCustomRole(harness.Db, "Own Only", PermissionKeys.TasksCreate);
        var manager = harness.AddUser("cr-mgr", SystemRoles.Manager);
        var alice = harness.AddUser("cr-alice");
        var bob = harness.AddUser("cr-bob");
        var viewer = InRole(harness, "cr-viewer", orgRole);
        var limited = InRole(harness, "cr-limited", ownRole);
        var forAlice = harness.AddTask(alice.Id, manager.Id, "For Alice");
        harness.AddTask(bob.Id, manager.Id, "For Bob");
        harness.AddTask(limited.Id, manager.Id, "For the limited user");

        var seenByViewer = await harness.TaskService.ListAsync(new TaskListFilter(), viewer.Id, orgRole.Name, CancellationToken.None);
        var seenByLimited = await harness.TaskService.ListAsync(new TaskListFilter(), limited.Id, ownRole.Name, CancellationToken.None);

        Assert.Equal(3, seenByViewer.Total);
        Assert.Equal(["For the limited user"], seenByLimited.Items.Select(task => task.Title));
        Assert.NotNull(await harness.TaskService.GetDetailsAsync(forAlice.Id, viewer.Id, orgRole.Name, CancellationToken.None));
        Assert.Null(await harness.TaskService.GetDetailsAsync(forAlice.Id, limited.Id, ownRole.Name, CancellationToken.None));
    }

    [Fact]
    public async Task CreatingAndEditingTasksNeedTheirOwnPermissions()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var creatorRole = TestAccessControl.AddCustomRole(harness.Db, "Task Creator", PermissionKeys.TasksCreate);
        var editorRole = TestAccessControl.AddCustomRole(harness.Db, "Task Editor", PermissionKeys.TasksCreate, PermissionKeys.TasksUpdate, PermissionKeys.TasksViewAll);
        var nothingRole = TestAccessControl.AddCustomRole(harness.Db, "Nothing");
        var assignee = harness.AddUser("cr-assignee");
        var creator = InRole(harness, "cr-creator", creatorRole);
        var editor = InRole(harness, "cr-editor", editorRole);
        var nobody = InRole(harness, "cr-nobody", nothingRole);

        var created = await harness.TaskService.CreateAsync(NewTask(assignee.Id), creator.Id, creatorRole.Name, CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.CreateAsync(NewTask(assignee.Id), nobody.Id, nothingRole.Name, CancellationToken.None));
        var update = new UpdateTaskRequest("Renamed", "Body", TaskPriority.High, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(4), 5m);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.UpdateAsync(created.Id, update, creator.Id, creatorRole.Name, CancellationToken.None));
        var edited = await harness.TaskService.UpdateAsync(created.Id, update, editor.Id, editorRole.Name, CancellationToken.None);

        Assert.Equal("Custom role task", created.Title);
        Assert.Equal("Renamed", edited!.Title);
    }

    [Fact]
    public async Task ReassigningAndManagingRecipientsNeedTheirOwnPermissions()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var viewOnly = TestAccessControl.AddCustomRole(harness.Db, "View Only", PermissionKeys.TasksViewAll);
        var manager = harness.AddUser("cr2-mgr", SystemRoles.Manager);
        var alice = harness.AddUser("cr2-alice");
        var bob = harness.AddUser("cr2-bob");
        var viewer = InRole(harness, "cr2-viewer", viewOnly);
        var task = harness.AddTask(alice.Id, manager.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.ReassignAsync(task.Id, new ReassignTaskRequest(bob.Id, "Rebalancing"), viewer.Id, viewOnly.Name, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.SetRecipientsAsync(task.Id, new SetRecipientsRequest([bob.Id]), viewer.Id, viewOnly.Name, CancellationToken.None));
        var reassigned = await harness.TaskService.ReassignAsync(task.Id, new ReassignTaskRequest(bob.Id, "Rebalancing"), manager.Id, SystemRoles.Manager, CancellationToken.None);

        Assert.Equal(bob.Id, reassigned!.AssignedToUserId);
    }

    [Fact]
    public async Task APermissionTakenAwayFromARoleStopsWorkingOnTheNextCall()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var role = TestAccessControl.AddCustomRole(harness.Db, "Temporary Creator", PermissionKeys.TasksCreate);
        var assignee = harness.AddUser("cr3-assignee");
        var user = InRole(harness, "cr3-user", role);

        await harness.TaskService.CreateAsync(NewTask(assignee.Id), user.Id, role.Name, CancellationToken.None);
        harness.Db.RolePermissions.RemoveRange(harness.Db.RolePermissions.Where(grant => grant.RoleId == role.Id));
        harness.Db.SaveChanges();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.CreateAsync(NewTask(assignee.Id), user.Id, role.Name, CancellationToken.None));
    }
}
