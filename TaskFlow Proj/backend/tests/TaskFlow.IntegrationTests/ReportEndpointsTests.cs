using System.Net;
using TaskFlow.Domain.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using User = TaskFlow.Domain.Entities.User;
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests;

public class ReportEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public ReportEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetReport_AsManager_ReturnsTotalsForTheFilter()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "rep-mgr-1", SystemRoles.Manager);
        var employee = await SeedEmployeeAsync("rep-emp-1");

        await SeedTaskAsync(employee.Id, "Pending one", TaskStatus.ToDo, TaskPriority.High, DateTime.UtcNow.AddDays(5));
        await SeedTaskAsync(employee.Id, "Overdue one", TaskStatus.ToDo, TaskPriority.Medium, DateTime.UtcNow.AddDays(-1));
        await SeedTaskAsync(employee.Id, "Done one", TaskStatus.Completed, TaskPriority.Low, DateTime.UtcNow.AddDays(-2));

        var response = await GetWithTokenAsync(client, $"/api/v1/reports?assignedToUserId={employee.Id}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(3, document.RootElement.GetProperty("totalTasks").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("pendingTasks").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("completedTasks").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("overdueTasks").GetInt32());
    }

    [Fact]
    public async Task GetReport_AsEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "rep-emp-2", SystemRoles.Employee);

        var response = await GetWithTokenAsync(client, "/api/v1/reports", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetReport_AsAdmin_IsAllowed()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "rep-admin-1", SystemRoles.Administrator);

        var response = await GetWithTokenAsync(client, "/api/v1/reports", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReport_CombinedFilters_ReturnOnlyMatchingRows()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "rep-mgr-2", SystemRoles.Manager);
        var employee = await SeedEmployeeAsync("rep-emp-3");

        await SeedTaskAsync(employee.Id, "High pending", TaskStatus.ToDo, TaskPriority.High, DateTime.UtcNow.AddDays(5));
        await SeedTaskAsync(employee.Id, "High done", TaskStatus.Completed, TaskPriority.High, DateTime.UtcNow.AddDays(-2));
        await SeedTaskAsync(employee.Id, "Low pending", TaskStatus.ToDo, TaskPriority.Low, DateTime.UtcNow.AddDays(5));

        var response = await GetWithTokenAsync(client,
            $"/api/v1/reports?assignedToUserId={employee.Id}&status=Completed&priority=High", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(1, document.RootElement.GetProperty("totalTasks").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("completedTasks").GetInt32());
    }

    [Fact]
    public async Task GetReport_InvalidDeadlineRange_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "rep-mgr-3", SystemRoles.Manager);

        var response = await GetWithTokenAsync(client,
            "/api/v1/reports?deadlineFrom=2030-05-01&deadlineTo=2030-04-01", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Export_AsManager_ReturnsCsvAttachmentWithHeader()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "rep-mgr-4", SystemRoles.Manager);
        var employee = await SeedEmployeeAsync("rep-emp-4");
        await SeedTaskAsync(employee.Id, "Exportable task", TaskStatus.ToDo, TaskPriority.Medium, DateTime.UtcNow.AddDays(5));

        var response = await GetWithTokenAsync(client, $"/api/v1/reports/export?assignedToUserId={employee.Id}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("taskflow-report.csv", response.Content.Headers.ContentDisposition?.FileName ?? string.Empty);

        var body = await response.Content.ReadAsStringAsync();
        var header = body.Split("\r\n")[0];
        Assert.Equal(
            "TaskCode,Title,Assignee,Department,Priority,Status,StartDate,Deadline,EstimatedHours,CompletedAt",
            header);
        Assert.Contains("Exportable task", body);
    }
    public async Task Export_EscapesFormulaInjectionInTitles()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "rep-mgr-5", SystemRoles.Manager);
        var employee = await SeedEmployeeAsync("rep-emp-5");
        await SeedTaskAsync(employee.Id, "=1+1", TaskStatus.ToDo, TaskPriority.Medium, DateTime.UtcNow.AddDays(5));

        var response = await GetWithTokenAsync(client, $"/api/v1/reports/export?assignedToUserId={employee.Id}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("'=1+1", body);
    }

    [Fact]
    public async Task Export_HonoursTheSameFilterAsTheTotals()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "rep-mgr-6", SystemRoles.Manager);
        var employee = await SeedEmployeeAsync("rep-emp-6");
        var other = await SeedEmployeeAsync("rep-emp-7");

        await SeedTaskAsync(employee.Id, "Mine one", TaskStatus.ToDo, TaskPriority.Medium, DateTime.UtcNow.AddDays(5));
        await SeedTaskAsync(employee.Id, "Mine two", TaskStatus.ToDo, TaskPriority.Medium, DateTime.UtcNow.AddDays(6));
        await SeedTaskAsync(other.Id, "Theirs", TaskStatus.ToDo, TaskPriority.Medium, DateTime.UtcNow.AddDays(5));

        var summary = await GetWithTokenAsync(client, $"/api/v1/reports?assignedToUserId={employee.Id}", token);
        using var summaryDocument = await JsonDocument.ParseAsync(await summary.Content.ReadAsStreamAsync());
        var total = summaryDocument.RootElement.GetProperty("totalTasks").GetInt32();

        var export = await GetWithTokenAsync(client, $"/api/v1/reports/export?assignedToUserId={employee.Id}", token);
        var body = await export.Content.ReadAsStringAsync();
        var rows = body.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, total);
        Assert.Equal(total + 1, rows.Length); // header + matching rows
        Assert.Contains(rows, line => line.Contains("Mine one"));
        Assert.DoesNotContain(rows, line => line.Contains("Theirs"));
    }

    [Fact]
    public async Task Export_AsEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "rep-emp-8", SystemRoles.Employee);

        var response = await GetWithTokenAsync(client, "/api/v1/reports/export", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<string> LoginAsync(HttpClient client, string username, string role)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.local", Password, role);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = username,
            password = Password
        });

        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<User> SeedEmployeeAsync(string username)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.local", Password, SystemRoles.Employee);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.SingleAsync(candidate => candidate.Username == username);
    }

    private async Task SeedTaskAsync(
        int assignedToUserId,
        string title,
        TaskStatus status,
        TaskPriority priority,
        DateTime deadline)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Tasks.Add(new TaskEntity
        {
            TaskCode = $"RP-{Guid.NewGuid():N}"[..24],
            Title = title,
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
