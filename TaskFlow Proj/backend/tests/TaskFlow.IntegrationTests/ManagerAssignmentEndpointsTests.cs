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
    public async Task AFlaggedProjectManagerIsTheManagerOfEveryoneOnTheProject()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "ma-admin-1", SystemRoles.Administrator);
        var manager = await SeedAsync("ma-mgr-1", SystemRoles.Manager);
        var employee = await SeedAsync("ma-emp-1");
        var finance = await AddProjectAsync("MA Finance 1");

        var saved = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{finance}/members", admin, new
        {
            members = new object[]
            {
                new { userId = manager.Id, positionId = 2, percent = 20, isManager = true },
                new { userId = employee.Id, positionId = 4, percent = 60, isManager = false }
            }
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var managerView = await JsonAsync(client, $"/api/v1/users/{manager.Id}", admin);
        Assert.Equal(
            ["MA Finance 1"],
            managerView.RootElement.GetProperty("managerProjects").EnumerateArray().Select(project => project.GetProperty("name").GetString()));
        Assert.Equal(
            [employee.Id],
            managerView.RootElement.GetProperty("reports").EnumerateArray().Select(report => report.GetProperty("id").GetInt32()));

        using var employeeView = await JsonAsync(client, $"/api/v1/users/{employee.Id}", admin);
        Assert.Equal(manager.Id, Assert.Single(employeeView.RootElement.GetProperty("managers").EnumerateArray()).GetProperty("id").GetInt32());

        using var list = await JsonAsync(client, "/api/v1/users", admin);
        Assert.Contains(list.RootElement.EnumerateArray(), user => user.GetProperty("id").GetInt32() == employee.Id
            && user.GetProperty("managers").EnumerateArray().Any(m => m.GetProperty("fullName").GetString() == "ma-mgr-1 full name"));
    }

    [Fact]
    public async Task TheDirectManagerProjectsEndpointNoLongerExists()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "ma-admin-gone", SystemRoles.Administrator);
        var manager = await SeedAsync("ma-mgr-gone", SystemRoles.Manager);

        var response = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{manager.Id}/assignments", admin,
            new { managerProjectIds = Array.Empty<int>() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnEmployeeSeesTheirOwnManagerOnTheirProfile()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "ma-admin-2", SystemRoles.Administrator);
        var manager = await SeedAsync("ma-mgr-2", SystemRoles.Manager);
        var employee = await SeedAsync("ma-emp-2");
        await AddTeamAsync("MA Team 2", manager.Id, employee.Id);
        var employeeToken = await TokenAsync(client, "ma-emp-2");

        using var profile = await JsonAsync(client, "/api/v1/users/me", employeeToken);

        Assert.Equal("ma-mgr-2 full name", Assert.Single(profile.RootElement.GetProperty("managers").EnumerateArray()).GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Assignments_DefineWhichTasksAManagerSees_ButNotWorkloadOrReports()
    {
        var client = _factory.CreateClient();
        await SignInAsync(client, "ma-admin-4", SystemRoles.Administrator);
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
        await AddTeamAsync("MA Team 4a", manager.Id, mine.Id);
        await AddTeamAsync("MA Team 4b", otherManager.Id, theirs.Id);
        var after = await SnapshotAsync();

        // The Tasks screen follows the manager's team: nobody was assigned before, only "mine" is on the team after.
        Assert.DoesNotContain("Visible mine", before.Tasks);
        Assert.Contains("Visible mine", after.Tasks);
        Assert.DoesNotContain("Visible theirs", after.Tasks);
        Assert.DoesNotContain("Visible unassigned", after.Tasks);

        // Workload and reports are outside this scoping and stay organisation-wide.
        Assert.Equal(before.Workload, after.Workload);
        Assert.Equal(before.ReportTotal, after.ReportTotal);
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

    /// <summary>A department the manager manages and the employees belong to: that is what makes them the employees' manager.</summary>
    private async Task AddTeamAsync(string departmentName, int managerId, params int[] employeeIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var department = new Department { Name = departmentName, CreatedAt = DateTime.UtcNow };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        db.DepartmentManagers.Add(new DepartmentManager { ManagerUserId = managerId, DepartmentId = department.Id });
        foreach (var employeeId in employeeIds)
        {
            db.UserDepartments.Add(new UserDepartment { UserId = employeeId, DepartmentId = department.Id });
        }

        await db.SaveChangesAsync();
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
