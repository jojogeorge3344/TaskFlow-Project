using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

/// <summary>End-to-end coverage of both reset flows: a user-initiated request that emails an Administrator a
/// one-time link, and an Administrator-initiated request from the login page that generates a password directly.</summary>
public class PasswordResetEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";
    private const string ForgotPassword = "/api/v1/auth/forgot-password";
    private const string ResetPassword = "/api/v1/auth/reset-password";

    private readonly TaskFlowApiFactory _factory;

    public PasswordResetEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UserInitiatedReset_EmailsTheAdministrator_WhoGeneratesAndDeliversANewPassword()
    {
        await _factory.SeedUserAsync("pr-user", "pr-user@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync("pr-admin", "pr-admin@taskflow.com", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();

        var userToken = await LoginForTokenAsync(client, "pr-user", Password);
        var request = await SendAsync(client, HttpMethod.Post, ForgotPassword, userToken);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        var adminId = await DbAsync(db => db.Users
            .Where(user => user.Username == "pr-admin")
            .Select(user => user.Id)
            .SingleAsync());
        var requestMail = await DbAsync(db => db.MailMessages
            .Where(message =>
                message.IsSystem &&
                message.Subject == "TaskFlow - Password reset requested" &&
                message.Recipients.Any(recipient => recipient.UserId == adminId))
            .OrderByDescending(message => message.Id)
            .FirstOrDefaultAsync());

        Assert.NotNull(requestMail);
        var link = Regex.Match(requestMail!.Body, @"reset-password\?token=([^&\s]+)");
        Assert.True(link.Success, "The reset request mail must contain a one-time link.");
        var token = link.Groups[1].Value;
        Assert.Contains("valid for 5 minutes", requestMail.Body);

        var userId = await DbAsync(db => db.Users
            .Where(user => user.Username == "pr-user")
            .Select(user => user.Id)
            .SingleAsync());

        // Only an Administrator may look at or consume the link.
        var employeeToken = await LoginForTokenAsync(client, "pr-user", Password);
        var employeeAttempt = await SendAsync(client, HttpMethod.Get, $"{ResetPassword}/{Escape(token)}", employeeToken);
        Assert.Equal(HttpStatusCode.Forbidden, employeeAttempt.StatusCode);

        var adminToken = await LoginForTokenAsync(client, "pr-admin", Password);

        var validation = await SendAsync(client, HttpMethod.Get, $"{ResetPassword}/{Escape(token)}", adminToken);
        Assert.Equal(HttpStatusCode.OK, validation.StatusCode);
        var validated = await validation.Content.ReadFromJsonAsync<DetailsBody>();
        Assert.NotNull(validated);
        Assert.Equal(userId, validated!.User.Id);
        Assert.Equal("pr-user", validated.User.Username);
        Assert.Equal(1, validated.RequestCount);

        // The request mail is filed under Password Request, links to the app (not the API) and notifies the admin.
        Assert.True(requestMail.IsPasswordRequest);
        Assert.Contains("http://localhost:4200/reset-password?token=", requestMail.Body);
        Assert.True(await DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == adminId && n.Message.Contains("requested a password reset"))));

        var reset = await SendAsync(client, HttpMethod.Post, ResetPassword, adminToken, new { token });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(string.Empty, await reset.Content.ReadAsStringAsync());

        var newPassword = await NewPasswordForAsync(userId);
        Assert.NotEqual(Password, newPassword);

        // The old password is gone, the emailed one works, and the user is notified in-app.
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "pr-user", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "pr-user", newPassword)).StatusCode);
        Assert.True(await DbAsync(db => db.Notifications.AnyAsync(notification =>
            notification.UserId == userId &&
            notification.Type == TaskFlow.Domain.Enums.NotificationType.System)));

        // The link is one-time: both reading and consuming it again fail with the generic "link not valid".
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(client, HttpMethod.Get, $"{ResetPassword}/{Escape(token)}", adminToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(client, HttpMethod.Post, ResetPassword, adminToken, new { token })).StatusCode);
    }

    [Fact]
    public async Task AdminInitiatedReset_FromTheProfileScreen_GeneratesAPasswordWithoutSendingAnAdminLink()
    {
        await _factory.SeedUserAsync("pr-admin-self", "pr-admin-self@taskflow.com", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();

        var selfToken = await LoginForTokenAsync(client, "pr-admin-self", Password);
        var request = await SendAsync(client, HttpMethod.Post, ForgotPassword, selfToken);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        var adminId = await DbAsync(db => db.Users
            .Where(user => user.Username == "pr-admin-self")
            .Select(user => user.Id)
            .SingleAsync());

        // No reset link was minted for the Administrator; the password was generated straight away.
        Assert.False(await DbAsync(db => db.PasswordResetTokens.AnyAsync(row => row.UserId == adminId)));

        var newPassword = await NewPasswordForAsync(adminId);
        Assert.NotEqual(Password, newPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "pr-admin-self", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "pr-admin-self", newPassword)).StatusCode);

        var notification = await DbAsync(db => db.Notifications
            .Where(row => row.UserId == adminId)
            .OrderByDescending(row => row.Id)
            .FirstAsync());
        Assert.Contains("password was regenerated", notification.Message);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<string> NewPasswordForAsync(int userId)
    {
        var mail = await DbAsync(db => db.MailMessages
            .Where(message =>
                message.IsSystem &&
                message.Subject == "TaskFlow - Your password has been reset" &&
                message.Recipients.Any(recipient => recipient.UserId == userId))
            .OrderByDescending(message => message.Id)
            .FirstAsync());

        var match = Regex.Match(mail.Body, @"Your new password is: (\S+)");
        Assert.True(match.Success, "The new-password mail must carry the generated password.");
        return match.Groups[1].Value;
    }

    private async Task<string> LoginForTokenAsync(HttpClient client, string identifier, string password)
    {
        var response = await LoginAsync(client, identifier, password);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AuthResponseBody>();
        return body!.AccessToken;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string identifier, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = identifier, password });

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string token,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request);
    }

    private static string Escape(string token) => Uri.EscapeDataString(token);

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    private sealed record UserBody(int Id, string FullName, string Username, string Email, string Role);

    private sealed record RequestEntryBody(int Id, string Status);

    private sealed record DetailsBody(UserBody User, int RequestCount, List<RequestEntryBody> Requests);

    private sealed record AuthResponseBody(string AccessToken, DateTime ExpiresAt);
}
