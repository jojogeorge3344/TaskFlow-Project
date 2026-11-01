using System.Net;
using TaskFlow.Api.Common.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests.Monitoring.TaskManagement;

public class TasksEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public TasksEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBoard_AsManager_ReturnsAllTasks()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-board-emp");

        await CreateTaskAsync("Board task one", employee.Id, manager.Id);
        await CreateTaskAsync("Board task two", manager.Id, manager.Id);

        var response = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/tasks/board", manager.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var tasks = document.RootElement.EnumerateArray().ToList();
        Assert.Contains(tasks, task => task.GetProperty("title").GetString() == "Board task one");
        Assert.Contains(tasks, task => task.GetProperty("title").GetString() == "Board task two");
    }

    [Fact]
    public async Task GetBoard_AsEmployee_ReturnsOnlyScopedTasks()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr2", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-board-emp2");
        var other = await SeedUserAsync("tek-board-other");

        await CreateTaskAsync("Mine", employee.Id, manager.Id);
        await CreateTaskAsync("Not mine", other.Id, manager.Id);

        var token = await LoginAsync(client, "tek-board-emp2", SystemRoles.Employee);

        var response = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/tasks/board", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var titles = document.RootElement.EnumerateArray()
            .Select(task => task.GetProperty("title").GetString())
            .ToList();

        Assert.Single(titles);
        Assert.Equal("Mine", titles[0]);
    }

    [Fact]
    public async Task GetBoard_WithoutToken_IsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/tasks/board");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDetails_AsAssignee_Returns200()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr3", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-detail-emp");
        var task = await CreateTaskAsync("Detail task", employee.Id, manager.Id);

        var token = await LoginAsync(client, "tek-detail-emp", SystemRoles.Employee);

        var response = await SendWithTokenAsync(client, HttpMethod.Get, $"/api/v1/tasks/{task.Id}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Detail task", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("Upcoming", document.RootElement.GetProperty("stage").GetString());
    }

    [Fact]
    public async Task GetDetails_AsUnrelatedEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr4", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-detail-emp2");
        var unrelated = await SeedUserAsync("tek-unrelated");
        var task = await CreateTaskAsync("Private task", employee.Id, manager.Id);

        var token = await LoginAsync(client, "tek-unrelated", SystemRoles.Employee);

        var response = await SendWithTokenAsync(client, HttpMethod.Get, $"/api/v1/tasks/{task.Id}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDetails_MissingOrDeletedTask_IsForbidden()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr5", SystemRoles.Manager);

        var response = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/tasks/999999", manager.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsManager_ReturnsUpdatedAndWritesActivity()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr6", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-upd-emp");
        var task = await CreateTaskAsync("Before update", employee.Id, manager.Id);

        var response = await SendWithTokenAsync(client, HttpMethod.Put, $"/api/v1/tasks/{task.Id}", manager.Token,
            JsonContent.Create(new
            {
                title = "After update",
                description = "Updated body",
                priority = "High",
                startDate = task.StartDate,
                deadline = task.Deadline.AddDays(2),
                estimatedHours = 12
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("After update", document.RootElement.GetProperty("title").GetString());

        var activity = await SendWithTokenAsync(client, HttpMethod.Get, $"/api/v1/tasks/{task.Id}/activity", manager.Token);
        using var activityDocument = await JsonDocument.ParseAsync(await activity.Content.ReadAsStreamAsync());
        var eventTypes = activityDocument.RootElement.EnumerateArray()
            .Select(entry => entry.GetProperty("eventType").GetString())
            .ToList();
        Assert.Contains("DeadlineChanged", eventTypes);
    }

    [Fact]
    public async Task Update_AsEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr7", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-upd-emp2");
        var task = await CreateTaskAsync("Locked task", employee.Id, manager.Id);

        var token = await LoginAsync(client, "tek-upd-emp2", SystemRoles.Employee);

        var response = await SendWithTokenAsync(client, HttpMethod.Put, $"/api/v1/tasks/{task.Id}", token,
            JsonContent.Create(new
            {
                title = "Nope",
                priority = "Low",
                startDate = task.StartDate,
                deadline = task.Deadline,
                estimatedHours = 8
            }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_InvalidBody_Returns400()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr8", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-upd-emp3");
        var task = await CreateTaskAsync("Strict task", employee.Id, manager.Id);

        var response = await SendWithTokenAsync(client, HttpMethod.Put, $"/api/v1/tasks/{task.Id}", manager.Token,
            JsonContent.Create(new
            {
                title = "Deadline before start",
                priority = "Low",
                startDate = task.Deadline,
                deadline = task.StartDate,
                estimatedHours = 8
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AllowedTransition_Returns200()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr9", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-status-emp");
        var task = await CreateTaskAsync("Move me", employee.Id, manager.Id, status: TaskStatus.ToDo);

        var token = await LoginAsync(client, "tek-status-emp", SystemRoles.Employee);

        var response = await SendWithTokenAsync(client, HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/status", token,
            JsonContent.Create(new { status = "InProgress" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("InProgress", document.RootElement.GetProperty("status").GetString());

        var activity = await SendWithTokenAsync(client, HttpMethod.Get, $"/api/v1/tasks/{task.Id}/activity", token);
        using var activityDocument = await JsonDocument.ParseAsync(await activity.Content.ReadAsStreamAsync());
        var eventTypes = activityDocument.RootElement.EnumerateArray()
            .Select(entry => entry.GetProperty("eventType").GetString())
            .ToList();
        Assert.Contains("StatusChanged", eventTypes);
    }

    [Fact]
    public async Task ChangeStatus_DisallowedTransition_Returns400()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr10", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-status-emp2");
        var task = await CreateTaskAsync("Stuck in ToDo", employee.Id, manager.Id, status: TaskStatus.ToDo);

        var token = await LoginAsync(client, "tek-status-emp2", SystemRoles.Employee);

        var response = await SendWithTokenAsync(client, HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/status", token,
            JsonContent.Create(new { status = "Completed" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reassign_AsManager_PublishesNotificationForNewAssignee()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr11", SystemRoles.Manager);
        var oldAssignee = await SeedUserAsync("tek-reassign-old");
        var newAssignee = await SeedUserAsync("tek-reassign-new");
        var task = await CreateTaskAsync("Handover task", oldAssignee.Id, manager.Id);

        var response = await SendWithTokenAsync(client, HttpMethod.Post, $"/api/v1/tasks/{task.Id}/reassign", manager.Token,
            JsonContent.Create(new { newAssigneeUserId = newAssignee.Id, reason = "Role change" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(newAssignee.Id, document.RootElement.GetProperty("assignedToUserId").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await db.Notifications
            .SingleAsync(candidate => candidate.UserId == newAssignee.Id && candidate.TaskId == task.Id);
        Assert.Equal("Reassigned", notification.Type.ToString());
    }

    [Fact]
    public async Task Reassign_AsEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr12", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-reassign-old2");
        var other = await SeedUserAsync("tek-reassign-target");
        var task = await CreateTaskAsync("Locked handover", employee.Id, manager.Id);

        var token = await LoginAsync(client, "tek-reassign-old2", SystemRoles.Employee);

        var response = await SendWithTokenAsync(client, HttpMethod.Post, $"/api/v1/tasks/{task.Id}/reassign", token,
            JsonContent.Create(new { newAssigneeUserId = other.Id, reason = "Nope" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Comments_AddAndList_Return200()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr13", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-comment-emp");
        var task = await CreateTaskAsync("Commented task", employee.Id, manager.Id);

        var token = await LoginAsync(client, "tek-comment-emp", SystemRoles.Employee);

        var add = await SendWithTokenAsync(client, HttpMethod.Post, $"/api/v1/tasks/{task.Id}/comments", token,
            JsonContent.Create(new { body = "First handoff note" }));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var list = await SendWithTokenAsync(client, HttpMethod.Get, $"/api/v1/tasks/{task.Id}/comments", token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using var document = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        var comments = document.RootElement.EnumerateArray().ToList();
        var comment = Assert.Single(comments);
        Assert.Equal("First handoff note", comment.GetProperty("body").GetString());

        var activity = await SendWithTokenAsync(client, HttpMethod.Get, $"/api/v1/tasks/{task.Id}/activity", token);
        using var activityDocument = await JsonDocument.ParseAsync(await activity.Content.ReadAsStreamAsync());
        Assert.Contains(activityDocument.RootElement.EnumerateArray(),
            entry => entry.GetProperty("eventType").GetString() == "CommentAdded");
    }

    [Fact]
    public async Task Comments_EmptyBody_Returns400()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr14", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-comment-emp2");
        var task = await CreateTaskAsync("Strict comments", employee.Id, manager.Id);

        var token = await LoginAsync(client, "tek-comment-emp2", SystemRoles.Employee);

        var response = await SendWithTokenAsync(client, HttpMethod.Post, $"/api/v1/tasks/{task.Id}/comments", token,
            JsonContent.Create(new { body = "  " }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Activity_WithFilters_ReturnsFilteredEntries()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr15", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-activity-emp");
        var task = await CreateTaskAsync("Filtered activity", employee.Id, manager.Id);

        var token = await LoginAsync(client, "tek-activity-emp", SystemRoles.Employee);

        await SendWithTokenAsync(client, HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/status", token,
            JsonContent.Create(new { status = "InProgress" }));

        var response = await SendWithTokenAsync(client, HttpMethod.Get,
            $"/api/v1/tasks/{task.Id}/activity?eventType=StatusChanged", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var entries = document.RootElement.EnumerateArray().ToList();
        var entry = Assert.Single(entries);
        Assert.Equal("StatusChanged", entry.GetProperty("eventType").GetString());
        Assert.Equal("ToDo", entry.GetProperty("oldValue").GetString());
        Assert.Equal("InProgress", entry.GetProperty("newValue").GetString());
    }

    [Fact]
    public async Task Recipients_GetAndUpdate_WorkForManager()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-mgr16", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-recip-emp");
        var extra = await SeedUserAsync("tek-recip-extra");
        var task = await CreateTaskAsync("Recipient task", employee.Id, manager.Id);

        var set = await SendWithTokenAsync(client, HttpMethod.Put, $"/api/v1/tasks/{task.Id}/recipients", manager.Token,
            JsonContent.Create(new { recipientUserIds = new[] { employee.Id, extra.Id } }));
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var get = await SendWithTokenAsync(client, HttpMethod.Get, $"/api/v1/tasks/{task.Id}/recipients", manager.Token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        using var document = await JsonDocument.ParseAsync(await get.Content.ReadAsStreamAsync());
        var userIds = document.RootElement.EnumerateArray()
            .Select(recipient => recipient.GetProperty("userId").GetInt32())
            .ToList();
        Assert.Contains(employee.Id, userIds);
        Assert.Contains(extra.Id, userIds);
    }

    [Fact]
    public async Task Create_AsManager_Returns201AndPersistsTask()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-create-mgr", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-create-emp");
        var extra = await SeedUserAsync("tek-create-extra");
        var projectId = await SeedProjectAsync("tek-create-project");

        var response = await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/tasks", manager.Token,
            JsonContent.Create(new
            {
                title = "Draft launch plan",
                description = "Screen 8 create flow",
                assignedToUserId = employee.Id,
                projectId,
                priority = "High",
                startDate = DateTime.UtcNow.AddDays(1),
                deadline = DateTime.UtcNow.AddDays(4),
                estimatedHours = 12,
                additionalRecipientUserIds = new[] { extra.Id },
                firstReminderDays = 3,
                secondReminderDays = 1,
                overdueReminderFrequency = "Weekly"
            }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var created = document.RootElement;
        Assert.StartsWith("TSK-", created.GetProperty("taskCode").GetString());
        Assert.Equal("Draft launch plan", created.GetProperty("title").GetString());
        Assert.Equal(employee.Id, created.GetProperty("assignedToUserId").GetInt32());
        Assert.Equal($"{employee.Username} full name", created.GetProperty("assignedToName").GetString());
        Assert.Equal(2, created.GetProperty("recipients").GetArrayLength());

        var id = created.GetProperty("id").GetInt32();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Tasks.SingleAsync(task => task.Id == id);
        Assert.Equal(projectId, stored.ProjectId);
        Assert.Equal(3, stored.FirstReminderDays);
        Assert.Equal(ReminderFrequency.Weekly, stored.OverdueReminderFrequency);
        Assert.Equal(2, await db.TaskRecipients.CountAsync(recipient => recipient.TaskId == id));
    }

    [Fact]
    public async Task Create_WithoutReminderFields_StoresConfiguredDefaults()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-defaults-mgr", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-defaults-emp");
        var projectId = await SeedProjectAsync("tek-defaults-project");

        var configure = await SendWithTokenAsync(client, HttpMethod.Put, "/api/v1/settings/reminders", manager.Token,
            JsonContent.Create(new
            {
                enabled = true,
                deadlineDayEnabled = true,
                defaultFirstDays = 3,
                defaultSecondDays = 1,
                defaultOverdueFrequency = "Weekly",
                mailEnabled = false
            }));
        Assert.Equal(HttpStatusCode.OK, configure.StatusCode);

        try
        {
            var response = await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/tasks", manager.Token,
                JsonContent.Create(new
                {
                    title = "Uses configured reminders",
                    assignedToUserId = employee.Id,
                    projectId,
                    priority = "Medium",
                    startDate = DateTime.UtcNow.AddDays(1),
                    deadline = DateTime.UtcNow.AddDays(6),
                    estimatedHours = 4
                }));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var id = document.RootElement.GetProperty("id").GetInt32();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Tasks.SingleAsync(task => task.Id == id);
            Assert.Equal(3, stored.FirstReminderDays);
            Assert.Equal(1, stored.SecondReminderDays);
            Assert.Equal(ReminderFrequency.Weekly, stored.OverdueReminderFrequency);
        }
        finally
        {
            await SendWithTokenAsync(client, HttpMethod.Put, "/api/v1/settings/reminders", manager.Token,
                JsonContent.Create(new
                {
                    enabled = true,
                    deadlineDayEnabled = true,
                    defaultFirstDays = 2,
                    defaultSecondDays = 1,
                    defaultOverdueFrequency = "Daily",
                    mailEnabled = false
                }));
        }
    }

    [Fact]
    public async Task Create_AsEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        await SeedUserAsync("tek-create-mgr2");
        var assignee = await SeedUserAsync("tek-create-assignee2");
        var token = await LoginAsync(client, "tek-create-mgr2", SystemRoles.Employee);

        var response = await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/tasks", token,
            JsonContent.Create(new
            {
                title = "Should not exist",
                assignedToUserId = assignee.Id,
                priority = "Low",
                startDate = DateTime.UtcNow.AddDays(1),
                deadline = DateTime.UtcNow.AddDays(2),
                estimatedHours = 2
            }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-create-mgr3", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-create-emp3");

        var response = await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/tasks", manager.Token,
            JsonContent.Create(new
            {
                title = "   ",
                assignedToUserId = employee.Id,
                priority = "Low",
                startDate = DateTime.UtcNow.AddDays(2),
                deadline = DateTime.UtcNow.AddDays(1),
                estimatedHours = 0
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_AsEmployee_ReturnsOnlyAccessibleTasks()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-list-mgr", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-list-emp");
        var other = await SeedUserAsync("tek-list-other");
        await CreateTaskAsync("Mine to see", employee.Id, manager.Id);
        await CreateTaskAsync("Hidden from me", other.Id, manager.Id);

        var token = await LoginAsync(client, "tek-list-emp", SystemRoles.Employee);
        var response = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/tasks", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(items);
        Assert.Equal("Mine to see", item.GetProperty("title").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task List_SearchAndFilters_ReturnMatches()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-list-mgr2", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-list-emp2");
        await CreateTaskAsync("Zeta milestone zz9", employee.Id, manager.Id, TaskStatus.InProgress);
        await CreateTaskAsync("Unrelated chore", employee.Id, manager.Id, TaskStatus.ToDo);

        var search = await SendWithTokenAsync(client, HttpMethod.Get,
            "/api/v1/tasks?search=zz9", manager.Token);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        using var searchDocument = await JsonDocument.ParseAsync(await search.Content.ReadAsStreamAsync());
        var matches = searchDocument.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(matches, item => item.GetProperty("title").GetString() == "Zeta milestone zz9");

        var byStatus = await SendWithTokenAsync(client, HttpMethod.Get,
            "/api/v1/tasks?status=InProgress&assignedToUserId=" + employee.Id, manager.Token);
        Assert.Equal(HttpStatusCode.OK, byStatus.StatusCode);

        using var statusDocument = await JsonDocument.ParseAsync(await byStatus.Content.ReadAsStreamAsync());
        var statusItems = statusDocument.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.All(statusItems, item => Assert.Equal("InProgress", item.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task List_DeadlineRange_ReturnsOnlyTasksInRange()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-list-mgr4", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-list-emp4");
        await CreateTaskAsync("Range inside", employee.Id, manager.Id, deadline: new DateTime(2031, 3, 10, 0, 0, 0, DateTimeKind.Utc));
        await CreateTaskAsync("Range outside", employee.Id, manager.Id, deadline: new DateTime(2031, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        var response = await SendWithTokenAsync(client, HttpMethod.Get,
            $"/api/v1/tasks?assignedToUserId={employee.Id}&deadlineFrom=2031-03-01&deadlineTo=2031-03-31", manager.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("Range inside", item.GetProperty("title").GetString());
    }

    [Fact]
    public async Task List_ReturnsPagingMetadata()
    {
        var client = _factory.CreateClient();
        var manager = await SeedAndLoginAsync(client, "tek-list-mgr3", SystemRoles.Manager);
        var employee = await SeedUserAsync("tek-list-emp3");
        await CreateTaskAsync("Page one", employee.Id, manager.Id);
        await CreateTaskAsync("Page two", employee.Id, manager.Id);

        var response = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/tasks?page=1&pageSize=1", manager.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(1, document.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("pageSize").GetInt32());
        Assert.True(document.RootElement.GetProperty("total").GetInt32() >= 2);
        Assert.Equal(1, document.RootElement.GetProperty("items").GetArrayLength());
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

    private async Task<(string Token, int Id)> SeedAndLoginAsync(HttpClient client, string username, string role)
    {
        var token = await LoginAsync(client, username, role);
        var id = await GetUserIdAsync(username);
        return (token, id);
    }

    private async Task<TaskFlow.Api.Models.Entities.User> SeedUserAsync(string username)
    {
        const string password = "P@ssw0rd!";
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", password, SystemRoles.Employee);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.SingleAsync(user => user.Username == username);
    }

    /// <summary>A task must belong to an active project, so creating one through the API needs a project first.</summary>
    private async Task<int> SeedProjectAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = new TaskFlow.Api.Models.Entities.Project { Name = name, CreatedAt = DateTime.UtcNow };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task<int> GetUserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();
    }

    private async Task<TaskEntity> CreateTaskAsync(
        string title,
        int assignedToUserId,
        int createdByUserId,
        TaskStatus status = TaskStatus.ToDo,
        DateTime? deadline = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = new TaskEntity
        {
            TaskCode = $"SEED-{Guid.NewGuid():N}"[..24],
            Title = title,
            AssignedToUserId = assignedToUserId,
            CreatedByUserId = createdByUserId,
            Priority = TaskPriority.Medium,
            StartDate = DateTime.UtcNow.AddDays(-1),
            Deadline = deadline ?? DateTime.UtcNow.AddDays(5),
            EstimatedHours = 8,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        return task;
    }

    private static async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string token,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}