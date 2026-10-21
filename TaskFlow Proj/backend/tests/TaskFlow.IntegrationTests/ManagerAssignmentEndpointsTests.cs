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

/// <summary>Manager assignments are recorded data that only Administrators change, and they never change access.</summary>
public class ManagerAssignmentEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public ManagerAssignmentEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnAdministratorAssignsProjectsToAManagerAndAnEmployeeToThem()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "ma-admin-1", SystemRoles.Administrator);
        var manager = await SeedAsync("ma-mgr-1", SystemRoles.Manager);
        var employee = await SeedAsync("ma-emp-1");
        var finance = await AddProjectAsync("MA Finance 1");
        var ops = await AddProjectAsync("MA Operations 1");

        var forManager = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{manager.Id}/assignments", admin,
            new { managerId = (int?)null, managerProjectIds = new[] { finance, ops } });
        var forEmployee = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{employee.Id}/assignments", admin,
            new { managerId = manager.Id, managerProjectIds = Array.Empty<int>() });

        Assert.Equal(HttpStatusCode.OK, forManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, forEmployee.StatusCode);

        using var managerView = await JsonAsync(client, $"/api/v1/users/{manager.Id}", admin);
        var projects = managerView.RootElement.GetProperty("managerProjects").EnumerateArray()
            .Select(project => project.GetProperty("name").GetString()).Order().ToList();
        Assert.Equal(["MA Finance 1", "MA Operations 1"], projects);
        var reports = managerView.RootElement.GetProperty("reports").EnumerateArray()
            .Select(report => report.GetProperty("id").GetInt32()).ToList();
        Assert.Equal([employee.Id], reports);

        using var employeeView = await JsonAsync(client, $"/api/v1/users/{employee.Id}", admin);
        Assert.Equal(manager.Id, employeeView.RootElement.GetProperty("managerId").GetInt32());
        Assert.Equal("ma-mgr-1 full name", employeeView.RootElement.GetProperty("managerName").GetString());

        using var list = await JsonAsync(client, "/api/v1/users", admin);
        Assert.Contains(list.RootElement.EnumerateArray(), user => user.GetProperty("id").GetInt32() == employee.Id
            && user.GetProperty("managerName").GetString() == "ma-mgr-1 full name");
    }

    [Fact]
    public async Task AnEmployeeSeesTheirOwnManagerOnTheirProfile()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "ma-admin-2", SystemRoles.Administrator);
        var manager = await SeedAsync("ma-mgr-2", SystemRoles.Manager);
        var employee = await SeedAsync("ma-emp-2");
        await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{employee.Id}/assignments", admin,
            new { managerId = manager.Id, managerProjectIds = Array.Empty<int>() });
        var employeeToken = await TokenAsync(client, "ma-emp-2");

        using var profile = await JsonAsync(client, "/api/v1/users/me", employeeToken);

        Assert.Equal("ma-mgr-2 full name", profile.RootElement.GetProperty("managerName").GetString());
    }

    [Theory]
    [InlineData(SystemRoles.Manager)]
    [InlineData(SystemRoles.Employee)]
    public async Task NonAdministratorsCannotChangeAssignments(string role)
    {
        var client = _factory.CreateClient();
        var token = await SignInAsync(client, $"ma-nonadmin-{role.ToLowerInvariant()}", role);
        var manager = await SeedAsync($"ma-target-mgr-{role.ToLowerInvariant()}", SystemRoles.Manager);
        var employee = await SeedAsync($"ma-target-emp-{role.ToLowerInvariant()}");

        var response = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{employee.Id}/assignments", token,
            new { managerId = manager.Id, managerProjectIds = Array.Empty<int>() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null((await UserAsync(employee.Username)).ManagerId);
    }

    [Fact]
    public async Task ARoleThatCanManageEmployeesCannotChangeAssignments()
    {
        var client = _factory.CreateClient();
        var roleId = await AddRoleAsync(PermissionKeys.EmployeesView, PermissionKeys.EmployeesManage);
        var token = await SignInInRoleAsync(client, "ma-people-mgr", roleId);
        var manager = await SeedAsync("ma-target-mgr-people", SystemRoles.Manager);
        var employee = await SeedAsync("ma-target-emp-people");

        var response = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{employee.Id}/assignments", token,
            new { managerId = manager.Id, managerProjectIds = Array.Empty<int>() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null((await UserAsync(employee.Username)).ManagerId);
    }

    [Fact]
    public async Task InvalidAssignmentsAreRejectedWithAMessageAndAnUnknownUserIsNotFound()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "ma-admin-3", SystemRoles.Administrator);
        var employee = await SeedAsync("ma-emp-3");
        var otherEmployee = await SeedAsync("ma-emp-3b");
        var project = await AddProjectAsync("MA Dept 3");

        var notAManager = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{employee.Id}/assignments", admin,
            new { managerId = otherEmployee.Id, managerProjectIds = Array.Empty<int>() });
        var projectsForEmployee = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{employee.Id}/assignments", admin,
            new { managerId = (int?)null, managerProjectIds = new[] { project } });
        var unknown = await SendAsync(client, HttpMethod.Put, "/api/v1/users/999999/assignments", admin,
            new { managerId = (int?)null, managerProjectIds = Array.Empty<int>() });

        Assert.Equal(HttpStatusCode.BadRequest, notAManager.StatusCode);
        Assert.Contains("active Manager", await notAManager.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, projectsForEmployee.StatusCode);
        Assert.Contains("Only a Manager", await projectsForEmployee.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task AssignmentsDoNotChangeWhatAManagerCanSee()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "ma-admin-4", SystemRoles.Administrator);
        var adminUser = await UserAsync("ma-admin-4");
        var manager = await SeedAsync("ma-mgr-4", SystemRoles.Manager);
        var otherManager = await SeedAsync("ma-mgr-4b", SystemRoles.Manager);
        var mine = await SeedAsync("ma-emp-4-mine");
        var theirs = await SeedAsync("ma-emp-4-theirs");
        var unassigned = await SeedAsync("ma-emp-4-none");
        var managerToken = await TokenAsync(client, "ma-mgr-4");
        await AddTaskAsync("Visible mine", mine.Id, adminUser.Id);
        await AddTaskAsync("Visible theirs", theirs.Id, adminUser.Id);
        await AddTaskAsync("Visible unassigned", unassigned.Id, adminUser.Id);

        async Task<(string Tasks, string Workload, int ReportTotal)> SnapshotAsync()
        {
            var tasks = await (await SendAsync(client, HttpMethod.Get, "/api/v1/tasks?pageSize=100", managerToken)).Content.ReadAsStringAsync();
            var workload = await (await SendAsync(client, HttpMethod.Get, "/api/v1/workload", managerToken)).Content.ReadAsStringAsync();
            using var report = await JsonAsync(client, "/api/v1/reports", managerToken);
            return (tasks, workload, report.RootElement.GetProperty("totalTasks").GetInt32());
        }

        var before = await SnapshotAsync();
        await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{mine.Id}/assignments", admin,
            new { managerId = manager.Id, managerProjectIds = Array.Empty<int>() });
        await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{theirs.Id}/assignments", admin,
            new { managerId = otherManager.Id, managerProjectIds = Array.Empty<int>() });
        var projectId = await AddProjectAsync("MA Dept 4");
        await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{manager.Id}/assignments", admin,
            new { managerId = (int?)null, managerProjectIds = new[] { projectId } });
        var after = await SnapshotAsync();

        Assert.Equal(before, after);
        Assert.Contains("Visible mine", after.Tasks);
        Assert.Contains("Visible theirs", after.Tasks);
        Assert.Contains("Visible unassigned", after.Tasks);
        Assert.Contains("ma-emp-4-theirs full name", after.Workload);
    }

    // ------------------------------------------------------------------ helpers

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

    private async Task<int> AddProjectAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = new Project { Name = name, CreatedAt = DateTime.UtcNow };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task AddTaskAsync(string title, int assignedTo, int createdBy)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tasks.Add(new TaskEntity
        {
            TaskCode = $"MA-{Guid.NewGuid():N}"[..20],
            Title = title,
            AssignedToUserId = assignedTo,
            CreatedByUserId = createdBy,
            Priority = TaskPriority.Medium,
            StartDate = DateTime.UtcNow.AddDays(-1),
            Deadline = DateTime.UtcNow.AddDays(5),
            EstimatedHours = 2,
            Status = TaskStatus.ToDo,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
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

    private static async Task<JsonDocument> JsonAsync(HttpClient client, string url, string token)
    {
        var response = await SendAsync(client, HttpMethod.Get, url, token);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
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
