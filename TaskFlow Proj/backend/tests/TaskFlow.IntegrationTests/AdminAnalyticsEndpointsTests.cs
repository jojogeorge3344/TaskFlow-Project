using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests;

public class AdminAnalyticsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string AnalyticsUrl = "/api/v1/dashboard/admin/analytics";

    private readonly TaskFlowApiFactory _factory;

    public AdminAnalyticsEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Analytics_AsAdmin_ReturnsEverySection()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "ana-admin", SystemRoles.Administrator);
        await SeedTasksAsync("ana-admin");

        var response = await GetAsync(client, AnalyticsUrl, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        foreach (var section in new[]
                 {
                     "filter", "summary", "tasksByStatus", "tasksByPriority", "deadlineBuckets",
                     "trend", "byDepartment", "byEmployee", "byManager", "byProject"
                 })
        {
            Assert.True(root.TryGetProperty(section, out _), section);
        }

        var status = root.GetProperty("tasksByStatus");
        Assert.Equal(
            status.GetProperty("total").GetInt32(),
            status.GetProperty("pending").GetInt32()
            + status.GetProperty("inProgress").GetInt32()
            + status.GetProperty("completed").GetInt32()
            + status.GetProperty("overdue").GetInt32());
        Assert.True(status.GetProperty("total").GetInt32() >= 2);
    }

    [Fact]
    public async Task Analytics_AcceptsFiltersAndEchoesThem()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "ana-admin", SystemRoles.Administrator);

        var response = await GetAsync(client, $"{AnalyticsUrl}?from=2026-01-01&to=2026-01-31&basis=Deadline", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var applied = document.RootElement.GetProperty("filter");
        Assert.Equal("2026-01-01", applied.GetProperty("from").GetString());
        Assert.Equal("2026-01-31", applied.GetProperty("to").GetString());
        Assert.Equal("Deadline", applied.GetProperty("basis").GetString());
    }

    [Fact]
    public async Task Analytics_WithoutToken_IsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync(AnalyticsUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(SystemRoles.Manager)]
    [InlineData(SystemRoles.Employee)]
    public async Task Analytics_AsNonAdmin_IsForbiddenWithNoData(string role)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"ana-{role.ToLowerInvariant()}", role);

        var response = await GetAsync(client, AnalyticsUrl, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("summary", body);
    }

    [Theory]
    [InlineData(SystemRoles.Manager)]
    [InlineData(SystemRoles.Employee)]
    public async Task AnalyticsFilters_AsNonAdmin_IsForbidden(string role)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"ana-{role.ToLowerInvariant()}", role);

        var response = await GetAsync(client, $"{AnalyticsUrl}/filters", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnalyticsFilters_AsAdmin_ReturnsOptions()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "ana-admin", SystemRoles.Administrator);

        var response = await GetAsync(client, $"{AnalyticsUrl}/filters", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        foreach (var name in new[] { "today", "timezone", "departments", "projects", "employees", "managers" })
        {
            Assert.True(document.RootElement.TryGetProperty(name, out _), name);
        }
    }

    [Fact]
    public async Task Analytics_EndBeforeStart_IsBadRequest()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "ana-admin", SystemRoles.Administrator);

        var response = await GetAsync(client, $"{AnalyticsUrl}?from=2026-02-01&to=2026-01-01", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analytics_UnknownBasis_IsBadRequest()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "ana-admin", SystemRoles.Administrator);

        var response = await GetAsync(client, $"{AnalyticsUrl}?basis=Nonsense", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("departmentId")]
    [InlineData("projectId")]
    [InlineData("employeeId")]
    [InlineData("managerId")]
    public async Task Analytics_UnknownEntityId_IsNotFound(string parameter)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "ana-admin", SystemRoles.Administrator);

        var response = await GetAsync(client, $"{AnalyticsUrl}?{parameter}=999999", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("summary", body);
    }

    [Fact]
    public async Task TaskList_AcceptsTheDrillDownFiltersAndMatchesTheAnalyticsValue()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "ana-admin", SystemRoles.Administrator);
        await SeedTasksAsync("ana-admin");
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var query = $"from={today}&to={today}&basis=Created";

        var analytics = await GetAsync(client, $"{AnalyticsUrl}?{query}", token);
        using var analyticsDocument = await JsonDocument.ParseAsync(await analytics.Content.ReadAsStreamAsync());
        var completed = analyticsDocument.RootElement.GetProperty("tasksByStatus").GetProperty("completed").GetInt32();

        var list = await GetAsync(client, $"/api/v1/tasks?bucket=Completed&{query}&departmentId=0", token);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listDocument = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        Assert.True(completed > 0);
        Assert.Equal(completed, listDocument.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task TaskList_InvertedDateRange_IsBadRequest()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "ana-admin", SystemRoles.Administrator);

        var response = await GetAsync(client, "/api/v1/tasks?from=2026-02-01&to=2026-01-01", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task SeedTasksAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(candidate => candidate.Username == username);
        var now = DateTime.UtcNow;

        foreach (var (status, deadline) in new[]
                 {
                     (TaskStatus.ToDo, now.AddDays(3)),
                     (TaskStatus.Completed, now.AddDays(-1))
                 })
        {
            db.Tasks.Add(new TaskEntity
            {
                TaskCode = $"T{Guid.NewGuid():N}"[..8],
                Title = "Seeded",
                AssignedToUserId = user.Id,
                CreatedByUserId = user.Id,
                StartDate = now,
                Deadline = deadline,
                EstimatedHours = 1,
                Status = status,
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = status == TaskStatus.Completed ? now : null
            });
        }

        await db.SaveChangesAsync();
    }

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
