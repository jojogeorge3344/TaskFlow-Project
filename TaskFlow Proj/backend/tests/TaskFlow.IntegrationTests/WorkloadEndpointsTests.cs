using System.Net;
using TaskFlow.Domain.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using Project = TaskFlow.Domain.Entities.Project;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using User = TaskFlow.Domain.Entities.User;
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests;

public class WorkloadEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public WorkloadEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWorkload_AsManager_ReturnsPerEmployeeCounts()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "wl-mgr-1", SystemRoles.Manager);
        var employee = await SeedEmployeeAsync("wl-emp-1");

        await SeedTaskAsync(employee.Id, TaskStatus.ToDo, TaskPriority.High, DateTime.UtcNow.AddDays(5));
        await SeedTaskAsync(employee.Id, TaskStatus.ToDo, TaskPriority.Critical, DateTime.UtcNow.AddDays(-1));
        await SeedTaskAsync(employee.Id, TaskStatus.Completed, TaskPriority.Medium, DateTime.UtcNow.AddDays(-2));

        var response = await GetWithTokenAsync(client, "/api/v1/workload", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var row = document.RootElement.EnumerateArray()
            .Single(entry => entry.GetProperty("userId").GetInt32() == employee.Id);

        Assert.Equal(2, row.GetProperty("activeTasks").GetInt32());
        Assert.Equal(2, row.GetProperty("highPriorityTasks").GetInt32());
        Assert.Equal(1, row.GetProperty("overdueTasks").GetInt32());
        Assert.Equal(1, row.GetProperty("completedTasks").GetInt32());
    }

    [Fact]
    public async Task GetWorkload_AsAdmin_IsAllowed()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "wl-admin-1", SystemRoles.Administrator);

        var response = await GetWithTokenAsync(client, "/api/v1/workload", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkload_AsEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "wl-emp-2", SystemRoles.Employee);

        var response = await GetWithTokenAsync(client, "/api/v1/workload", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkload_WithoutToken_IsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/workload");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkload_ProjectFilter_NarrowsTheResult()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "wl-mgr-2", SystemRoles.Manager);

        var engineering = await SeedProjectAsync("WL Engineering");
        var operations = await SeedProjectAsync("WL Operations");

        var engineer = await SeedEmployeeAsync("wl-emp-eng", engineering.Id);
        var operatorUser = await SeedEmployeeAsync("wl-emp-ops", operations.Id);

        var response = await GetWithTokenAsync(client, $"/api/v1/workload?projectId={engineering.Id}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var userIds = document.RootElement.EnumerateArray()
            .Select(entry => entry.GetProperty("userId").GetInt32())
            .ToList();

        Assert.Contains(engineer.Id, userIds);
        Assert.DoesNotContain(operatorUser.Id, userIds);
    }

    private async Task<string> LoginAsync(HttpClient client, string username, string role)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = username,
            password = Password
        });

        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<User> SeedEmployeeAsync(string username, int? projectId = null)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Username == username);

        if (projectId is not null)
        {
            db.UserProjectAllocations.Add(new TaskFlow.Domain.Entities.UserProjectAllocation { UserId = user.Id, ProjectId = projectId.Value, AllocationPercent = 100, PositionId = 1 });
            await db.SaveChangesAsync();
        }

        return user;
    }

    private async Task<Project> SeedProjectAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Projects.SingleOrDefaultAsync(project => project.Name == name);
        if (existing is not null)
        {
            return existing;
        }

        var project = new Project { Name = name, CreatedAt = DateTime.UtcNow };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private async Task SeedTaskAsync(int assignedToUserId, TaskStatus status, TaskPriority priority, DateTime deadline)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Tasks.Add(new TaskEntity
        {
            TaskCode = $"WL-{Guid.NewGuid():N}"[..24],
            Title = "Workload task",
            AssignedToUserId = assignedToUserId,
            CreatedByUserId = assignedToUserId,
            Priority = priority,
            StartDate = DateTime.UtcNow.AddDays(-3),
            Deadline = deadline,
            EstimatedHours = 4,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> GetWithTokenAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
