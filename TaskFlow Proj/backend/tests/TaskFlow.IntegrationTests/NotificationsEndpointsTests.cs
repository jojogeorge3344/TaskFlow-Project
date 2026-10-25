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
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.IntegrationTests;

public class NotificationsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public NotificationsEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_ReturnsPagedShape_OnlyCallersRows_NewestFirst()
    {
        var client = _factory.CreateClient();
        var owner = await SeedAndLoginAsync(client, "ntf-list-owner", SystemRoles.Employee);
        var other = await SeedUserAsync("ntf-list-other");
        var task = await CreateTaskAsync("Notification task", owner.Id, owner.Id);

        var oldest = DateTime.UtcNow.AddMinutes(-10);
        await SeedNotificationAsync(owner.Id, task.Id, NotificationType.TaskAssigned, "oldest", createdAt: oldest);
        await SeedNotificationAsync(owner.Id, task.Id, NotificationType.Reassigned, "middle", createdAt: oldest.AddMinutes(5));
        await SeedNotificationAsync(owner.Id, task.Id, NotificationType.System, "newest", createdAt: oldest.AddMinutes(9));
        await SeedNotificationAsync(other.Id, task.Id, NotificationType.TaskAssigned, "other user", createdAt: oldest.AddMinutes(9));

        var response = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/notifications?page=1&pageSize=2", owner.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, root.GetProperty("total").GetInt32());

        var items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("newest", items[0].GetProperty("message").GetString());
        Assert.Equal("middle", items[1].GetProperty("message").GetString());
        Assert.Equal("System", items[0].GetProperty("type").GetString());
        Assert.Equal("Reassigned", items[1].GetProperty("type").GetString());
        Assert.Equal(task.Id, items[0].GetProperty("taskId").GetInt32());
        Assert.Equal(task.TaskCode, items[0].GetProperty("taskCode").GetString());
    }

    [Fact]
    public async Task Get_AppliesTypeAndReadFilters()
    {
        var client = _factory.CreateClient();
        var owner = await SeedAndLoginAsync(client, "ntf-filter-owner", SystemRoles.Employee);

        await SeedNotificationAsync(owner.Id, null, NotificationType.TaskAssigned, "assigned");
        await SeedNotificationAsync(owner.Id, null, NotificationType.Reassigned, "reassigned", isRead: true);

        var byType = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/notifications?type=Reassigned", owner.Token);
        using (var document = await JsonDocument.ParseAsync(await byType.Content.ReadAsStreamAsync()))
        {
            var items = document.RootElement.GetProperty("items");
            Assert.Equal(1, items.GetArrayLength());
            Assert.Equal("reassigned", items[0].GetProperty("message").GetString());
        }

        var byRead = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/notifications?isRead=true", owner.Token);
        using (var document = await JsonDocument.ParseAsync(await byRead.Content.ReadAsStreamAsync()))
        {
            var items = document.RootElement.GetProperty("items");
            Assert.Equal(1, items.GetArrayLength());
            Assert.Equal("reassigned", items[0].GetProperty("message").GetString());
        }
    }

    [Fact]
    public async Task UnreadCount_CountsOnlyOwnedUnread()
    {
        var client = _factory.CreateClient();
        var owner = await SeedAndLoginAsync(client, "ntf-count-owner", SystemRoles.Employee);
        var other = await SeedUserAsync("ntf-count-other");

        await SeedNotificationAsync(owner.Id, null, NotificationType.System, "one");
        await SeedNotificationAsync(owner.Id, null, NotificationType.System, "two");
        await SeedNotificationAsync(owner.Id, null, NotificationType.System, "read one", isRead: true);
        await SeedNotificationAsync(other.Id, null, NotificationType.System, "other");

        var response = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/notifications/unread-count", owner.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(2, document.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task MarkRead_PersistsAndIsScopedToOwner()
    {
        var client = _factory.CreateClient();
        var owner = await SeedAndLoginAsync(client, "ntf-mark-owner", SystemRoles.Employee);
        var otherUser = await SeedAndLoginAsync(client, "ntf-mark-other", SystemRoles.Employee);

        var notificationId = await SeedNotificationAsync(owner.Id, null, NotificationType.System, "mine");

        var crossUser = await SendWithTokenAsync(
            client, HttpMethod.Post, $"/api/v1/notifications/{notificationId}/read", otherUser.Token);
        Assert.Equal(HttpStatusCode.NotFound, crossUser.StatusCode);
        Assert.Equal(1, await GetUnreadCountAsync(client, owner.Token));

        var own = await SendWithTokenAsync(
            client, HttpMethod.Post, $"/api/v1/notifications/{notificationId}/read", owner.Token);
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
        Assert.Equal(0, await GetUnreadCountAsync(client, owner.Token));
    }

    [Fact]
    public async Task MarkAllRead_ZeroesOwnedCountOnly()
    {
        var client = _factory.CreateClient();
        var owner = await SeedAndLoginAsync(client, "ntf-all-owner", SystemRoles.Employee);
        var other = await SeedAndLoginAsync(client, "ntf-all-other", SystemRoles.Employee);

        await SeedNotificationAsync(owner.Id, null, NotificationType.System, "a");
        await SeedNotificationAsync(owner.Id, null, NotificationType.System, "b");
        await SeedNotificationAsync(other.Id, null, NotificationType.System, "c");

        var response = await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/notifications/read-all", owner.Token);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(0, await GetUnreadCountAsync(client, owner.Token));
        Assert.Equal(1, await GetUnreadCountAsync(client, other.Token));
    }

    [Fact]
    public async Task Endpoints_RejectUnauthenticatedCalls()
    {
        var client = _factory.CreateClient();

        var list = await client.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);

        var count = await client.GetAsync("/api/v1/notifications/unread-count");
        Assert.Equal(HttpStatusCode.Unauthorized, count.StatusCode);

        var readAll = await client.PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.Unauthorized, readAll.StatusCode);

        var markRead = await client.PostAsync("/api/v1/notifications/1/read", null);
        Assert.Equal(HttpStatusCode.Unauthorized, markRead.StatusCode);
    }

    private async Task<int> GetUnreadCountAsync(HttpClient client, string token)
    {
        var response = await SendWithTokenAsync(client, HttpMethod.Get, "/api/v1/notifications/unread-count", token);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("count").GetInt32();
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

    private async Task<Domain.Entities.User> SeedUserAsync(string username)
    {
        const string password = "P@ssw0rd!";
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", password, SystemRoles.Employee);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.SingleAsync(user => user.Username == username);
    }

    private async Task<int> GetUserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();
    }

    private async Task<int> SeedNotificationAsync(
        int userId,
        int? taskId,
        NotificationType type,
        string message,
        bool isRead = false,
        DateTime? createdAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var notification = new Domain.Entities.Notification
        {
            UserId = userId,
            TaskId = taskId,
            Type = type,
            Message = message,
            IsRead = isRead,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        return notification.Id;
    }

    private async Task<TaskEntity> CreateTaskAsync(string title, int assignedToUserId, int createdByUserId)
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
            Deadline = DateTime.UtcNow.AddDays(5),
            EstimatedHours = 8,
            Status = TaskStatus.ToDo,
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
