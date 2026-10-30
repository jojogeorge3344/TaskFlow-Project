using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests;

public class EmployeeAnalyticsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string AnalyticsUrl = "/api/v1/dashboard/employee/analytics";

    private readonly TaskFlowApiFactory _factory;
    private readonly string _u = Guid.NewGuid().ToString("N")[..8];

    public EmployeeAnalyticsEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Analytics_AsEmployee_ReturnsOwnDataSections()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"emp-a-{_u}");
        await SeedTasksAsync($"emp-a-{_u}", 2);
        await SeedTasksAsync($"emp-b-{_u}", 5);

        var response = await GetAsync(client, AnalyticsUrl, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        foreach (var section in new[] { "summary", "tasksByStatus", "tasksByPriority", "upcomingDeadlines", "projects", "trend", "completedInPeriod" })
        {
            Assert.True(root.TryGetProperty(section, out _), section);
        }

        Assert.Equal(2, root.GetProperty("summary").GetProperty("totalTasks").GetInt32());
    }

    [Fact]
    public async Task Analytics_WithoutToken_IsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync(AnalyticsUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analytics_EmployeeIdParameter_IsIgnored()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"emp-a-{_u}");
        await SeedTasksAsync($"emp-a-{_u}", 1);
        var otherId = await SeedTasksAsync($"emp-b-{_u}", 4);

        var response = await GetAsync(client, $"{AnalyticsUrl}?employeeId={otherId}&userId={otherId}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(1, document.RootElement.GetProperty("summary").GetProperty("totalTasks").GetInt32());
    }

    [Fact]
    public async Task Analytics_EntityFilters_AreIgnored()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"emp-a-{_u}");
        await SeedTasksAsync($"emp-a-{_u}", 3);

        var response = await GetAsync(client, $"{AnalyticsUrl}?departmentId=999&projectId=999&managerId=999&from=2020-01-01", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(3, document.RootElement.GetProperty("summary").GetProperty("totalTasks").GetInt32());
    }

    [Fact]
    public async Task Analytics_ResponseHasNoColleagueNameOrTaskTitle()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"emp-a-{_u}");
        await SeedTasksAsync($"emp-a-{_u}", 1);
        await SeedTasksAsync($"emp-b-{_u}", 2);

        var body = await (await GetAsync(client, AnalyticsUrl, token)).Content.ReadAsStringAsync();

        Assert.DoesNotContain($"emp-b-{_u}", body);
    }

    [Fact]
    public async Task Analytics_UnknownPeriod_IsBadRequest()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"emp-a-{_u}");

        var response = await GetAsync(client, $"{AnalyticsUrl}?period=year", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("7d", 7)]
    [InlineData("30d", 30)]
    public async Task Analytics_Period_SetsTrendLength(string period, int days)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"emp-a-{_u}");

        var response = await GetAsync(client, $"{AnalyticsUrl}?period={period}", token);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(days, document.RootElement.GetProperty("trend").GetArrayLength());
    }

    /// <summary>Seeds the user (employee role) with <paramref name="tasks"/> tasks, idempotently. Returns the user id.</summary>
    private async Task<int> SeedTasksAsync(string username, int tasks)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", "P@ssw0rd!", SystemRoles.Employee);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Username == username);
        var existing = await db.Tasks.CountAsync(task => task.AssignedToUserId == user.Id);
        var now = DateTime.UtcNow;
        for (var i = existing; i < tasks; i++)
        {
            db.Tasks.Add(new TaskEntity
            {
                TaskCode = $"T{Guid.NewGuid():N}"[..8],
                Title = "Employee seeded",
                AssignedToUserId = user.Id,
                CreatedByUserId = user.Id,
                StartDate = now,
                Deadline = now.AddDays(3),
                EstimatedHours = 1,
                Status = TaskStatus.ToDo,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<string> LoginAsync(HttpClient client, string username)
    {
        const string password = "P@ssw0rd!";
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", password, SystemRoles.Employee);

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
