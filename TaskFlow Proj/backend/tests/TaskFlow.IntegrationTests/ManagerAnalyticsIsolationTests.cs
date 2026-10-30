using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using DepartmentEntity = TaskFlow.Api.Models.Entities.Department;
using DepartmentManagerEntity = TaskFlow.Api.Models.Entities.DepartmentManager;
using UserDepartmentEntity = TaskFlow.Api.Models.Entities.UserDepartment;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests;

public class ManagerAnalyticsIsolationTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Analytics = "/api/v1/dashboard/manager/analytics";
    private readonly TaskFlowApiFactory _factory;

    public ManagerAnalyticsIsolationTests(TaskFlowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task TwoManagersWithOverlappingDepartments_EachSeeOnlyTheirTeamAndCountsMatchTheDrillDown()
    {
        // Shared department managed by both; each manager also has one direct report outside it.
        var ids = await SeedAsync();
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();
        var tokenA = await LoginAsync(clientA, "iso-mgr-a", SystemRoles.Manager);
        var tokenB = await LoginAsync(clientB, "iso-mgr-b", SystemRoles.Manager);

        var teamA = await TeamAsync(clientA, tokenA);
        var teamB = await TeamAsync(clientB, tokenB);

        Assert.Equal(new[] { "iso-a-report", "iso-shared" }, teamA.names);
        Assert.Equal(new[] { "iso-b-report", "iso-shared" }, teamB.names);
        Assert.Equal(4, teamA.total);
        Assert.Equal(4, teamB.total);

        foreach (var (client, token, own) in new[] { (clientA, tokenA, ids.ReportA), (clientB, tokenB, ids.ReportB) })
        {
            var analytics = await JsonAsync(client, $"{Analytics}?employeeId={own}", token);
            var overdue = analytics.GetProperty("tasksByStatus").GetProperty("overdue").GetInt32();
            var list = await JsonAsync(client, $"/api/v1/tasks?bucket=Overdue&employeeId={own}", token);
            Assert.Equal(overdue, list.GetProperty("total").GetInt32());
            Assert.True(overdue > 0);
        }

        var foreignList = await JsonAsync(clientA, $"/api/v1/tasks?bucket=All&employeeId={ids.ReportB}", tokenA);
        Assert.Equal(0, foreignList.GetProperty("total").GetInt32());
        var forbidden = await GetAsync(clientA, $"{Analytics}?employeeId={ids.ReportB}", tokenA);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/filters")]
    public async Task EmployeeCaller_IsForbiddenOnTheManagerAnalyticsEndpoints(string suffix)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "iso-employee", SystemRoles.Employee);

        var response = await GetAsync(client, Analytics + suffix, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedCaller_IsRejectedOnTheManagerAnalyticsEndpoint()
    {
        var response = await _factory.CreateClient().GetAsync(Analytics);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(int total, string[] names)> TeamAsync(HttpClient client, string token)
    {
        var json = await JsonAsync(client, Analytics, token);
        var names = json.GetProperty("byEmployee").EnumerateArray()
            .Select(row => row.GetProperty("name").GetString()!.Replace(" full name", ""))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return (json.GetProperty("tasksByStatus").GetProperty("total").GetInt32(), names);
    }

    private async Task<(int ReportA, int ReportB)> SeedAsync()
    {
        foreach (var (name, role) in new[]
                 {
                     ("iso-mgr-a", SystemRoles.Manager), ("iso-mgr-b", SystemRoles.Manager), ("iso-a-report", SystemRoles.Employee),
                     ("iso-b-report", SystemRoles.Employee), ("iso-shared", SystemRoles.Employee)
                 })
        {
            await _factory.SeedUserAsync(name, $"{name}@taskflow.com", "P@ssw0rd!", role);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Users.Where(user => user.Username.StartsWith("iso-")).ToDictionaryAsync(user => user.Username);
        var department = await db.Departments.FirstOrDefaultAsync(candidate => candidate.Name == "Iso shared");
        if (department is null)
        {
            department = new DepartmentEntity { Name = "Iso shared", CreatedAt = DateTime.UtcNow };
            db.Departments.Add(department);
            await db.SaveChangesAsync();
            db.DepartmentManagers.Add(new DepartmentManagerEntity { ManagerUserId = users["iso-mgr-a"].Id, DepartmentId = department.Id });
            db.DepartmentManagers.Add(new DepartmentManagerEntity { ManagerUserId = users["iso-mgr-b"].Id, DepartmentId = department.Id });
            db.UserDepartments.Add(new UserDepartmentEntity { UserId = users["iso-shared"].Id, DepartmentId = department.Id });
            var teamA = new DepartmentEntity { Name = "Iso team A", CreatedAt = DateTime.UtcNow };
            var teamB = new DepartmentEntity { Name = "Iso team B", CreatedAt = DateTime.UtcNow };
            db.Departments.AddRange(teamA, teamB);
            await db.SaveChangesAsync();
            db.DepartmentManagers.Add(new DepartmentManagerEntity { ManagerUserId = users["iso-mgr-a"].Id, DepartmentId = teamA.Id });
            db.DepartmentManagers.Add(new DepartmentManagerEntity { ManagerUserId = users["iso-mgr-b"].Id, DepartmentId = teamB.Id });
            db.UserDepartments.Add(new UserDepartmentEntity { UserId = users["iso-a-report"].Id, DepartmentId = teamA.Id });
            db.UserDepartments.Add(new UserDepartmentEntity { UserId = users["iso-b-report"].Id, DepartmentId = teamB.Id });
            var now = DateTime.UtcNow;
            foreach (var name in new[] { "iso-a-report", "iso-b-report", "iso-shared" })
            {
                db.Tasks.Add(NewTask(users[name].Id, TaskStatus.ToDo, now.AddDays(3), now));
                db.Tasks.Add(NewTask(users[name].Id, TaskStatus.ToDo, now.AddDays(-2), now));
            }

            await db.SaveChangesAsync();
        }

        return (users["iso-a-report"].Id, users["iso-b-report"].Id);
    }

    private static TaskEntity NewTask(int assignee, TaskStatus status, DateTime deadline, DateTime now) => new()
    {
        TaskCode = $"T{Guid.NewGuid():N}"[..8],
        Title = "Isolation",
        AssignedToUserId = assignee,
        CreatedByUserId = assignee,
        StartDate = now,
        Deadline = deadline,
        EstimatedHours = 1,
        Status = status,
        CreatedAt = now,
        UpdatedAt = now
    };

    private async Task<string> LoginAsync(HttpClient client, string username, string role)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", "P@ssw0rd!", role);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = "P@ssw0rd!" });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> JsonAsync(HttpClient client, string url, string token)
    {
        var response = await GetAsync(client, url, token);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
