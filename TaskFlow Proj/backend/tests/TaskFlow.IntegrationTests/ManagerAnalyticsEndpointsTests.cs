using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using Project = TaskFlow.Api.Models.Entities.Project;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests;

public class ManagerAnalyticsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string AnalyticsUrl = "/api/v1/dashboard/manager/analytics";

    private readonly TaskFlowApiFactory _factory;
    private readonly string _u = Guid.NewGuid().ToString("N")[..8]; // a new instance per test, so every test owns its users, tasks and projects

    public ManagerAnalyticsEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Analytics_AsManager_ReturnsOnlyTheTeamsDataAndNoDepartmentSection()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-a-{_u}", SystemRoles.Manager);
        await SeedTeamAsync($"mgr-a-{_u}", $"mgr-a-member-{_u}", tasks: 2);
        await SeedTeamAsync($"mgr-b-{_u}", $"mgr-b-member-{_u}", tasks: 3);

        var response = await GetAsync(client, AnalyticsUrl, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        foreach (var section in new[] { "filter", "summary", "tasksByStatus", "tasksByPriority", "byEmployee", "byProject", "attention" })
        {
            Assert.True(root.TryGetProperty(section, out _), section);
        }

        Assert.False(root.TryGetProperty("byDepartment", out _));
        Assert.Equal(2, root.GetProperty("tasksByStatus").GetProperty("total").GetInt32());
        var employees = root.GetProperty("byEmployee").EnumerateArray().Select(row => row.GetProperty("name").GetString()).ToList();
        Assert.Equal([$"mgr-a-member-{_u} full name"], employees);
    }

    [Fact]
    public async Task Analytics_WithoutToken_IsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync(AnalyticsUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analytics_AsEmployee_IsForbiddenWithNoData()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-employee-{_u}", SystemRoles.Employee);

        var response = await GetAsync(client, AnalyticsUrl, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("summary", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Analytics_EmployeeOnAnotherManagersTeam_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-a-{_u}", SystemRoles.Manager);
        await SeedTeamAsync($"mgr-a-{_u}", $"mgr-a-member-{_u}", tasks: 1);
        var foreignId = await SeedTeamAsync($"mgr-b-{_u}", $"mgr-b-member-{_u}", tasks: 1);

        var response = await GetAsync(client, $"{AnalyticsUrl}?employeeId={foreignId}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("summary", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Analytics_ProjectOfAnotherTeam_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-a-{_u}", SystemRoles.Manager);
        await SeedTeamAsync($"mgr-a-{_u}", $"mgr-a-member-{_u}", tasks: 1);
        await SeedTeamAsync($"mgr-b-{_u}", $"mgr-b-member-{_u}", tasks: 1);
        var foreignProjectId = await SeedProjectTaskAsync($"mgr-b-member-{_u}", $"Foreign project {_u}");

        var response = await GetAsync(client, $"{AnalyticsUrl}?projectId={foreignProjectId}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Analytics_OwnEmployeeAndProject_AreAccepted()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-a-{_u}", SystemRoles.Manager);
        var memberId = await SeedTeamAsync($"mgr-a-{_u}", $"mgr-a-member-{_u}", tasks: 1);
        var projectId = await SeedProjectTaskAsync($"mgr-a-member-{_u}", $"Own project {_u}");

        var response = await GetAsync(client, $"{AnalyticsUrl}?employeeId={memberId}&projectId={projectId}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("employeeId")]
    [InlineData("projectId")]
    public async Task Analytics_UnknownEntityId_IsNotFound(string parameter)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-a-{_u}", SystemRoles.Manager);

        var response = await GetAsync(client, $"{AnalyticsUrl}?{parameter}=999999", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Analytics_DepartmentParameter_IsIgnored()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-a-{_u}", SystemRoles.Manager);

        var response = await GetAsync(client, $"{AnalyticsUrl}?departmentId=999999&managerId=999999", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var applied = document.RootElement.GetProperty("filter");
        Assert.Equal(JsonValueKind.Null, applied.GetProperty("departmentId").ValueKind);
        Assert.Equal(JsonValueKind.Null, applied.GetProperty("managerId").ValueKind);
    }

    [Fact]
    public async Task Analytics_EndBeforeStart_IsBadRequest()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-a-{_u}", SystemRoles.Manager);

        var response = await GetAsync(client, $"{AnalyticsUrl}?from=2026-02-01&to=2026-01-01", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Filters_AsManager_ListOnlyInScopeEmployeesAndProjects()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-a-{_u}", SystemRoles.Manager);
        await SeedTeamAsync($"mgr-a-{_u}", $"mgr-a-member-{_u}", tasks: 1);
        await SeedTeamAsync($"mgr-b-{_u}", $"mgr-b-member-{_u}", tasks: 1);
        await SeedProjectTaskAsync($"mgr-a-member-{_u}", $"Own project {_u}");
        await SeedProjectTaskAsync($"mgr-b-member-{_u}", $"Foreign project {_u}");

        var response = await GetAsync(client, $"{AnalyticsUrl}/filters", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("departments", out _));
        Assert.Equal(
            [$"mgr-a-member-{_u} full name"],
            root.GetProperty("employees").EnumerateArray().Select(option => option.GetProperty("name").GetString()));
        Assert.Equal(
            [$"Own project {_u}"],
            root.GetProperty("projects").EnumerateArray().Select(option => option.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Filters_AsEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"mgr-employee-{_u}", SystemRoles.Employee);

        var response = await GetAsync(client, $"{AnalyticsUrl}/filters", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Filters_WithoutToken_IsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync($"{AnalyticsUrl}/filters");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Makes <paramref name="memberUsername"/> report to the manager and gives them <paramref name="tasks"/> tasks
    /// created today (idempotent). Returns the member's id.</summary>
    private async Task<int> SeedTeamAsync(string managerUsername, string memberUsername, int tasks)
    {
        await _factory.SeedUserAsync(managerUsername, $"{managerUsername}@taskflow.com", "P@ssw0rd!", SystemRoles.Manager);
        await _factory.SeedUserAsync(memberUsername, $"{memberUsername}@taskflow.com", "P@ssw0rd!", SystemRoles.Employee);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var manager = await db.Users.SingleAsync(user => user.Username == managerUsername);
        var member = await db.Users.SingleAsync(user => user.Username == memberUsername);
        var teamName = $"Team of {managerUsername}";
        var team = await db.Departments.FirstOrDefaultAsync(d => d.Name == teamName);
        if (team is null)
        {
            team = new TaskFlow.Api.Models.Entities.Department { Name = teamName, CreatedAt = DateTime.UtcNow };
            db.Departments.Add(team);
            await db.SaveChangesAsync();
            db.DepartmentManagers.Add(new TaskFlow.Api.Models.Entities.DepartmentManager { ManagerUserId = manager.Id, DepartmentId = team.Id });
            db.UserDepartments.Add(new TaskFlow.Api.Models.Entities.UserDepartment { UserId = member.Id, DepartmentId = team.Id });
        }

        var existing = await db.Tasks.CountAsync(task => task.AssignedToUserId == member.Id && task.Title == "Team seeded");
        var now = DateTime.UtcNow;
        for (var i = existing; i < tasks; i++)
        {
            db.Tasks.Add(NewTask(member.Id, "Team seeded", null, now));
        }

        await db.SaveChangesAsync();
        return member.Id;
    }

    private async Task<int> SeedProjectTaskAsync(string memberUsername, string projectName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Users.SingleAsync(user => user.Username == memberUsername);
        var project = await db.Projects.FirstOrDefaultAsync(candidate => candidate.Name == projectName);
        if (project is null)
        {
            project = new Project { Name = projectName, CreatedAt = DateTime.UtcNow };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            db.Tasks.Add(NewTask(member.Id, "Project seeded", project.Id, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        return project.Id;
    }

    private static TaskEntity NewTask(int assigneeId, string title, int? projectId, DateTime now) => new()
    {
        TaskCode = $"T{Guid.NewGuid():N}"[..8],
        Title = title,
        AssignedToUserId = assigneeId,
        CreatedByUserId = assigneeId,
        ProjectId = projectId,
        StartDate = now,
        Deadline = now.AddDays(3),
        EstimatedHours = 1,
        Status = TaskStatus.ToDo,
        CreatedAt = now,
        UpdatedAt = now
    };

    private async Task<string> LoginAsync(HttpClient client, string username, string role)
    {
        const string password = "P@ssw0rd!";
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", password, role);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = username,
            password
        });

        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
