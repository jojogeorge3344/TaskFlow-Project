using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests;

/// <summary>Deleting employees and tasks is Administrator-only. Both are soft deletes that keep history.</summary>
public class AdminDeleteEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public AdminDeleteEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------------------ employees

    [Fact]
    public async Task AnAdministratorDeletesAnEmployeeWhoDisappearsFromTheListAndCanNoLongerSignIn()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "del-admin-1", SystemRoles.Administrator);
        var employee = await SeedAsync("del-emp-1");
        var employeeToken = await TokenAsync(client, "del-emp-1");

        var response = await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{employee.Id}", admin);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var list = await SendAsync(client, HttpMethod.Get, "/api/v1/users", admin);
        Assert.DoesNotContain("del-emp-1", await list.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"/api/v1/users/{employee.Id}", admin)).StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "del-emp-1", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var oldSession = await SendAsync(client, HttpMethod.Get, "/api/v1/auth/me", employeeToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == employee.Id);
        Assert.True(row.IsDeleted);
        Assert.Equal("del-emp-1@taskflow.com", row.Email);
    }

    [Theory]
    [InlineData(SystemRoles.Manager)]
    [InlineData(SystemRoles.Employee)]
    public async Task NonAdministratorsCannotDeleteAnEmployee(string role)
    {
        var client = _factory.CreateClient();
        var token = await SignInAsync(client, $"del-nonadmin-{role.ToLowerInvariant()}", role);
        var employee = await SeedAsync($"del-target-{role.ToLowerInvariant()}");

        var response = await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{employee.Id}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await IsDeletedAsync(employee.Id));
    }

    [Fact]
    public async Task ARoleThatCanManageEmployeesCannotDeleteThem()
    {
        var client = _factory.CreateClient();
        var roleId = await AddRoleAsync(PermissionKeys.EmployeesCreate, PermissionKeys.EmployeesEdit, PermissionKeys.EmployeesStatus, PermissionKeys.EmployeesView);
        var token = await SignInInRoleAsync(client, "del-people-mgr", roleId);
        var employee = await SeedAsync("del-target-people");

        var response = await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{employee.Id}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await IsDeletedAsync(employee.Id));
    }

    [Fact]
    public async Task DeletingAnUnknownOrAlreadyDeletedEmployeeIsNotFound()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "del-admin-2", SystemRoles.Administrator);
        var employee = await SeedAsync("del-emp-2");

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Delete, "/api/v1/users/999999", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{employee.Id}", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{employee.Id}", admin)).StatusCode);
    }

    [Fact]
    public async Task AnAdministratorCannotDeleteTheirOwnAccount()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "del-admin-3", SystemRoles.Administrator);
        await SeedAsync("del-admin-3b", SystemRoles.Administrator);
        var self = await UserAsync("del-admin-3");

        var response = await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{self.Id}", admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("own account", await response.Content.ReadAsStringAsync());
        Assert.False(await IsDeletedAsync(self.Id));
    }

    [Fact]
    public async Task AnEmployeeWithUnfinishedTasksCannotBeDeletedUntilTheyAreDone()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "del-admin-4", SystemRoles.Administrator);
        var adminUser = await UserAsync("del-admin-4");
        var employee = await SeedAsync("del-emp-4");
        var task = await AddTaskAsync("Open work", employee.Id, adminUser.Id, TaskStatus.InProgress);

        var blocked = await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{employee.Id}", admin);
        await SetStatusAsync(task.Id, TaskStatus.Completed);
        var allowed = await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{employee.Id}", admin);

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("unfinished", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    [Fact]
    public async Task ADeletedEmployeeLeavesPickersProjectsAndWorkloadButHistoryKeepsTheirName()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "del-admin-5", SystemRoles.Administrator);
        var adminUser = await UserAsync("del-admin-5");
        var employee = await SeedAsync("del-emp-5");
        var projectId = await AddProjectAsync("Delete Dept 5", employee.Id);
        var finished = await AddTaskAsync("Finished by leaver", employee.Id, adminUser.Id, TaskStatus.Completed);
        var created = await AddTaskAsync("Created by leaver", adminUser.Id, employee.Id, TaskStatus.Completed);
        await AddCommentAsync(created.Id, employee.Id, "My last word");

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{employee.Id}", admin)).StatusCode);

        var members = await (await SendAsync(client, HttpMethod.Get, $"/api/v1/projects/{projectId}/employees", admin)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("del-emp-5", members);

        var workload = await (await SendAsync(client, HttpMethod.Get, "/api/v1/workload", admin)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("del-emp-5 full name", workload);

        // History still names them.
        var detail = await (await SendAsync(client, HttpMethod.Get, $"/api/v1/tasks/{finished.Id}", admin)).Content.ReadAsStringAsync();
        Assert.Contains("del-emp-5 full name", detail);

        var list = await (await SendAsync(client, HttpMethod.Get, "/api/v1/tasks?pageSize=100", admin)).Content.ReadAsStringAsync();
        Assert.Contains("Finished by leaver", list);
        Assert.Contains("Created by leaver", list);
        Assert.Contains("del-emp-5 full name", list);

        var board = await (await SendAsync(client, HttpMethod.Get, "/api/v1/tasks/board", admin)).Content.ReadAsStringAsync();
        Assert.Contains("Finished by leaver", board);

        var comments = await (await SendAsync(client, HttpMethod.Get, $"/api/v1/tasks/{created.Id}/comments", admin)).Content.ReadAsStringAsync();
        Assert.Contains("My last word", comments);
        Assert.Contains("del-emp-5 full name", comments);

        var assignees = await (await SendAsync(client, HttpMethod.Get, "/api/v1/tasks/assignees", admin)).Content.ReadAsStringAsync();
        Assert.Contains("del-emp-5 full name", assignees);

        var report = await (await SendAsync(client, HttpMethod.Get, "/api/v1/reports/export", admin)).Content.ReadAsStringAsync();
        Assert.Contains("Finished by leaver", report);
        Assert.Contains("del-emp-5 full name", report);
    }

    [Fact]
    public async Task DeletingAManagerClearsTheirReportsAndProjects()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "del-admin-6", SystemRoles.Administrator);
        var manager = await SeedAsync("del-mgr-6", SystemRoles.Manager);
        var report = await SeedAsync("del-report-6");
        var projectId = await AddProjectAsync("Delete Dept 6", null);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var team = new Department { Name = "Delete Team 6", CreatedAt = DateTime.UtcNow };
            db.Departments.Add(team);
            await db.SaveChangesAsync();
            db.DepartmentManagers.Add(new DepartmentManager { ManagerUserId = manager.Id, DepartmentId = team.Id });
            db.UserDepartments.Add(new UserDepartment { UserId = report.Id, DepartmentId = team.Id });
            db.ManagerProjects.Add(new ManagerProject { ManagerUserId = manager.Id, ProjectId = projectId });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{manager.Id}", admin)).StatusCode);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await checkDb.DepartmentManagers.AnyAsync(link => link.ManagerUserId == manager.Id));
        Assert.False(await checkDb.ManagerProjects.AnyAsync(link => link.ManagerUserId == manager.Id));
    }

    // ------------------------------------------------------------------ tasks

    [Fact]
    public async Task AnAdministratorDeletesATaskAndItIsGoneEverywhere()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "xerase-admin-1", SystemRoles.Administrator);
        var adminUser = await UserAsync("xerase-admin-1");
        var employee = await SeedAsync("xerase-emp-1");
        var employeeToken = await TokenAsync(client, "xerase-emp-1");
        var keep = await AddTaskAsync("Task to keep", employee.Id, adminUser.Id, TaskStatus.InProgress);
        var remove = await AddTaskAsync("Task to remove", employee.Id, adminUser.Id, TaskStatus.InProgress);
        var url = $"/api/v1/tasks/{remove.Id}";

        var response = await SendAsync(client, HttpMethod.Delete, url, admin);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // The API answers a missing or deleted task the same way: it is not available to anyone.
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, url, admin)).StatusCode);

        foreach (var token in new[] { admin, employeeToken })
        {
            var list = await (await SendAsync(client, HttpMethod.Get, "/api/v1/tasks?pageSize=100", token)).Content.ReadAsStringAsync();
            Assert.Contains("Task to keep", list);
            Assert.DoesNotContain("Task to remove", list);

            var board = await (await SendAsync(client, HttpMethod.Get, "/api/v1/tasks/board", token)).Content.ReadAsStringAsync();
            Assert.DoesNotContain("Task to remove", board);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True((await db.Tasks.SingleAsync(task => task.Id == remove.Id)).IsDeleted);
        Assert.True(await db.TaskActivityLogs.AnyAsync(log => log.TaskId == remove.Id && log.EventType == TaskActivityEventType.Deleted && log.ActorUserId == adminUser.Id));
        Assert.False((await db.Tasks.SingleAsync(task => task.Id == keep.Id)).IsDeleted);
    }

    [Theory]
    [InlineData(SystemRoles.Manager)]
    [InlineData(SystemRoles.Employee)]
    public async Task NonAdministratorsCannotDeleteATask(string role)
    {
        var client = _factory.CreateClient();
        var token = await SignInAsync(client, $"xerase-nonadmin-{role.ToLowerInvariant()}", role);
        var owner = await UserAsync($"xerase-nonadmin-{role.ToLowerInvariant()}");
        var task = await AddTaskAsync($"Protected {role}", owner.Id, owner.Id, TaskStatus.ToDo);

        var response = await SendAsync(client, HttpMethod.Delete, $"/api/v1/tasks/{task.Id}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await TaskIsDeletedAsync(task.Id));
    }

    [Fact]
    public async Task ARoleThatCanEditAndCreateTasksCannotDeleteThem()
    {
        var client = _factory.CreateClient();
        var roleId = await AddRoleAsync(PermissionKeys.TasksViewAll, PermissionKeys.TasksCreate, PermissionKeys.TasksUpdate);
        var token = await SignInInRoleAsync(client, "xerase-task-mgr", roleId);
        var owner = await UserAsync("xerase-task-mgr");
        var task = await AddTaskAsync("Protected from editors", owner.Id, owner.Id, TaskStatus.ToDo);

        var response = await SendAsync(client, HttpMethod.Delete, $"/api/v1/tasks/{task.Id}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await TaskIsDeletedAsync(task.Id));
    }

    [Fact]
    public async Task DeletingAnUnknownOrAlreadyDeletedTaskIsNotFound()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "xerase-admin-2", SystemRoles.Administrator);
        var adminUser = await UserAsync("xerase-admin-2");
        var task = await AddTaskAsync("Delete twice", adminUser.Id, adminUser.Id, TaskStatus.ToDo);

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Delete, "/api/v1/tasks/999999", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/tasks/{task.Id}", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/tasks/{task.Id}", admin)).StatusCode);
    }

    [Fact]
    public async Task ADeletedTaskLeavesReportsWorkloadAndDashboardCounts()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "xerase-admin-3", SystemRoles.Administrator);
        var adminUser = await UserAsync("xerase-admin-3");
        var employee = await SeedAsync("xerase-emp-3");
        await AddTaskAsync("Counted", employee.Id, adminUser.Id, TaskStatus.InProgress);
        var removed = await AddTaskAsync("Not counted", employee.Id, adminUser.Id, TaskStatus.InProgress);

        var pendingBefore = await DashboardPendingAsync(client, admin);
        Assert.Equal(2, await ReportTotalAsync(client, admin, employee.Id));
        Assert.Equal(2, await WorkloadActiveAsync(client, admin, employee.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/tasks/{removed.Id}", admin)).StatusCode);

        Assert.Equal(1, await ReportTotalAsync(client, admin, employee.Id));
        Assert.Equal(1, await WorkloadActiveAsync(client, admin, employee.Id));
        Assert.Equal(pendingBefore - 1, await DashboardPendingAsync(client, admin));
        var export = await (await SendAsync(client, HttpMethod.Get, $"/api/v1/reports/export?assignedToUserId={employee.Id}", admin)).Content.ReadAsStringAsync();
        Assert.Contains("Counted", export);
        Assert.DoesNotContain("Not counted", export);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<bool> TaskIsDeletedAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tasks.Where(task => task.Id == id).Select(task => task.IsDeleted).SingleAsync();
    }

    private static async Task<JsonDocument> JsonAsync(HttpClient client, string url, string token)
    {
        var response = await SendAsync(client, HttpMethod.Get, url, token);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private static async Task<int> ReportTotalAsync(HttpClient client, string token, int employeeId)
    {
        using var document = await JsonAsync(client, $"/api/v1/reports?assignedToUserId={employeeId}", token);
        return document.RootElement.GetProperty("totalTasks").GetInt32();
    }

    private static async Task<int> WorkloadActiveAsync(HttpClient client, string token, int employeeId)
    {
        using var document = await JsonAsync(client, "/api/v1/workload", token);
        return document.RootElement.EnumerateArray()
            .Single(row => row.GetProperty("userId").GetInt32() == employeeId)
            .GetProperty("activeTasks").GetInt32();
    }

    private static async Task<int> DashboardPendingAsync(HttpClient client, string token)
    {
        using var document = await JsonAsync(client, "/api/v1/dashboard/admin", token);
        return document.RootElement.GetProperty("pendingTasks").GetInt32();
    }

    private async Task<User> SeedAsync(string username, string role = SystemRoles.Employee)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);
        return await UserAsync(username);
    }

    private async Task<User> UserAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(user => user.Username == username);
    }

    private async Task<bool> IsDeletedAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.IgnoreQueryFilters().Where(user => user.Id == id).Select(user => user.IsDeleted).SingleAsync();
    }

    private async Task<int> AddRoleAsync(params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var role = new Role { Name = $"Custom {Guid.NewGuid():N}"[..30], IsSystem = false, CreatedAt = DateTime.UtcNow };
        foreach (var permission in await db.Permissions.Where(permission => permissionKeys.Contains(permission.Key)).ToListAsync())
        {
            role.Permissions.Add(new RolePermission { Permission = permission });
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<string> SignInInRoleAsync(HttpClient client, string username, int roleId)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync(user => user.Username == username)).RoleId = roleId;
            await db.SaveChangesAsync();
        }

        return await TokenAsync(client, username);
    }

    private async Task<string> SignInAsync(HttpClient client, string username, string role)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);
        return await TokenAsync(client, username);
    }

    private static async Task<string> TokenAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<int> AddProjectAsync(string name, int? memberId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = new Project { Name = name, CreatedAt = DateTime.UtcNow };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        if (memberId is int id)
        {
            db.UserProjectAllocations.Add(new UserProjectAllocation { UserId = id, ProjectId = project.Id, AllocationPercent = 100, PositionId = 1 });
            await db.SaveChangesAsync();
        }

        return project.Id;
    }

    private async Task<TaskEntity> AddTaskAsync(string title, int assignedTo, int createdBy, TaskStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = new TaskEntity
        {
            TaskCode = $"DEL-{Guid.NewGuid():N}"[..20],
            Title = title,
            AssignedToUserId = assignedTo,
            CreatedByUserId = createdBy,
            Priority = TaskPriority.Medium,
            StartDate = DateTime.UtcNow.AddDays(-2),
            Deadline = DateTime.UtcNow.AddDays(5),
            EstimatedHours = 4,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    private async Task SetStatusAsync(int taskId, TaskStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Tasks.SingleAsync(task => task.Id == taskId)).Status = status;
        await db.SaveChangesAsync();
    }

    private async Task AddCommentAsync(int taskId, int authorId, string body)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TaskComments.Add(new TaskComment { TaskId = taskId, AuthorUserId = authorId, Body = body, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }
}
