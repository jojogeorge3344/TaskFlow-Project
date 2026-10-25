using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using ProjectEntity = TaskFlow.Domain.Entities.Project;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests;

public class EmployeeAnalyticsIsolationTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Analytics = "/api/v1/dashboard/employee/analytics";
    private readonly TaskFlowApiFactory _factory;
    private readonly string _u = Guid.NewGuid().ToString("N")[..8];

    public EmployeeAnalyticsIsolationTests(TaskFlowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DrillDownCountsMatchTheDashboard_AndATamperedUrlReturnsNoOtherUsersTasks()
    {
        var (aId, bId, projectId) = await SeedAsync();
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"iso-emp-a-{_u}");

        var analytics = await JsonAsync(client, Analytics, token);
        var status = analytics.GetProperty("tasksByStatus");

        foreach (var (bucket, property) in new[]
                 {
                     ("Overdue", "overdue"), ("Pending", "pending"), ("InProgress", "inProgress"), ("Completed", "completed")
                 })
        {
            var list = await JsonAsync(client, $"/api/v1/tasks?bucket={bucket}&employeeId={aId}", token);
            Assert.Equal(status.GetProperty(property).GetInt32(), list.GetProperty("total").GetInt32());
        }

        Assert.True(status.GetProperty("overdue").GetInt32() > 0);

        var all = await JsonAsync(client, $"/api/v1/tasks?bucket=All&employeeId={aId}", token);
        Assert.Equal(analytics.GetProperty("summary").GetProperty("totalTasks").GetInt32(), all.GetProperty("total").GetInt32());

        var project = analytics.GetProperty("projects").EnumerateArray().Single(row => row.GetProperty("projectId").GetInt32() == projectId);
        var inProject = await JsonAsync(client, $"/api/v1/tasks?bucket=All&employeeId={aId}&projectId={projectId}", token);
        Assert.Equal(project.GetProperty("totalTasks").GetInt32(), inProject.GetProperty("total").GetInt32());
        Assert.Equal(2, project.GetProperty("totalTasks").GetInt32());

        var foreign = await JsonAsync(client, $"/api/v1/tasks?bucket=All&employeeId={bId}", token);
        Assert.Equal(0, foreign.GetProperty("total").GetInt32());
        var foreignProject = await JsonAsync(client, $"/api/v1/tasks?employeeId={bId}&projectId={projectId}", token);
        Assert.Equal(0, foreignProject.GetProperty("total").GetInt32());
    }

    private async Task<(int A, int B, int ProjectId)> SeedAsync()
    {
        await _factory.SeedUserAsync($"iso-emp-a-{_u}", $"iso-emp-a-{_u}@taskflow.com", "P@ssw0rd!", SystemRoles.Employee);
        await _factory.SeedUserAsync($"iso-emp-b-{_u}", $"iso-emp-b-{_u}@taskflow.com", "P@ssw0rd!", SystemRoles.Employee);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await db.Users.SingleAsync(user => user.Username == $"iso-emp-a-{_u}");
        var b = await db.Users.SingleAsync(user => user.Username == $"iso-emp-b-{_u}");
        var project = new ProjectEntity { Name = $"Iso project {_u}", CreatedAt = DateTime.UtcNow };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        db.Tasks.AddRange(
            NewTask(a.Id, TaskStatus.ToDo, now.AddDays(-3), project.Id),
            NewTask(a.Id, TaskStatus.ToDo, now.AddDays(2), project.Id),
            NewTask(a.Id, TaskStatus.InProgress, now.AddDays(4), null),
            NewTask(a.Id, TaskStatus.Completed, now.AddDays(1), null),
            NewTask(b.Id, TaskStatus.ToDo, now.AddDays(-5), project.Id),
            NewTask(b.Id, TaskStatus.InProgress, now.AddDays(3), project.Id));
        await db.SaveChangesAsync();
        return (a.Id, b.Id, project.Id);
    }

    private static TaskEntity NewTask(int assigneeId, TaskStatus status, DateTime deadline, int? projectId)
    {
        var now = DateTime.UtcNow;
        return new TaskEntity
        {
            TaskCode = $"T{Guid.NewGuid():N}"[..8],
            Title = "Isolation seeded",
            AssignedToUserId = assigneeId,
            CreatedByUserId = assigneeId,
            ProjectId = projectId,
            StartDate = now,
            Deadline = deadline,
            EstimatedHours = 1,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = status == TaskStatus.Completed ? now : null
        };
    }

    private async Task<string> LoginAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = "P@ssw0rd!" });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> JsonAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }
}
