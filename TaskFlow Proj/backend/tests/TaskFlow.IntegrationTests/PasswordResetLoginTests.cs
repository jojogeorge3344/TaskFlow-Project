using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

/// <summary>Section 4 of the password-reset change: the anonymous Login Screen notice read, its
/// clearing by a successful sign-in only, and the forced password change after a temporary password.</summary>
public class PasswordResetLoginTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";
    private const string NoticeTrue = "{\"noticeAvailable\":true}";
    private const string NoticeFalse = "{\"noticeAvailable\":false}";

    private readonly TaskFlowApiFactory _factory;

    public PasswordResetLoginTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    // ---- 4.1 The notice read -----------------------------------------------------------------

    [Fact]
    public async Task TheNotice_IsAvailableOnlyAfterACompletedLoginScreenReset_InTheSameShapeEitherWay()
    {
        var client = _factory.CreateClient();
        var user = Unique("notice-user");
        var other = Unique("notice-other");
        var admin = Unique("notice-admin");
        await SeedAsync(user, SystemRoles.Employee);
        await SeedAsync(other, SystemRoles.Employee);
        await SeedAsync(admin, SystemRoles.Administrator);

        Assert.Equal(NoticeFalse, await ReadNoticeAsync(client, user));

        await RequestAsync(client, user);
        // A pending request is not a completed reset.
        Assert.Equal(NoticeFalse, await ReadNoticeAsync(client, user));

        await ProcessPendingAsync(client, admin, user);

        Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, user));
        Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, $"{user}@taskflow.com"));

        // Another account's identifier and an unknown one never see this notice, in the same shape.
        Assert.Equal(NoticeFalse, await ReadNoticeAsync(client, other));
        Assert.Equal(NoticeFalse, await ReadNoticeAsync(client, Unique("nobody")));
    }

    // ---- 4.2 Clearing ------------------------------------------------------------------------

    [Fact]
    public async Task OnlyASuccessfulSignIn_ClearsTheNotice_AndALaterResetRebindsIt()
    {
        var client = _factory.CreateClient();
        var user = Unique("clear-user");
        var admin = Unique("clear-admin");
        await SeedAsync(user, SystemRoles.Employee);
        await SeedAsync(admin, SystemRoles.Administrator);

        await RequestAsync(client, user);
        var firstPassword = await ProcessPendingAsync(client, admin, user);

        // A failed sign-in (the old password) leaves the notice.
        var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = user, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, user));

        // A successful one with the new password clears it.
        await LoginAsync(client, user, firstPassword);
        Assert.Equal(NoticeFalse, await ReadNoticeAsync(client, user));

        // A later completed reset binds the notice to the latest event; the cleared one stays gone.
        await RequestAsync(client, user);
        await ProcessPendingAsync(client, admin, user);
        Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, user));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notice = Assert.Single(await db.LoginPasswordResetNotices.AsNoTracking()
            .Where(candidate => candidate.User!.Username == user)
            .ToListAsync());
        var latest = await db.PasswordResetRequests.AsNoTracking()
            .Where(request => request.User!.Username == user)
            .OrderByDescending(request => request.Id)
            .FirstAsync();
        Assert.Equal(latest.Id, notice.PasswordResetRequestId);
    }

    [Fact]
    public async Task AProfileScreenReset_ShowsTheNoticeToo_UntilTheNextSuccessfulSignIn()
    {
        var client = _factory.CreateClient();
        var user = Unique("profile-notice");
        var admin = Unique("profile-notice-admin");
        await SeedAsync(user, SystemRoles.Employee);
        await SeedAsync(admin, SystemRoles.Administrator);

        // The request is raised while signed in, from the Profile Screen.
        var userToken = await LoginAsync(client, user, Password);
        var raised = await SendAsync(client, HttpMethod.Post, "/api/v1/users/me/password-reset-request", userToken);
        Assert.Equal(HttpStatusCode.OK, raised.StatusCode);
        Assert.Equal(NoticeFalse, await ReadNoticeAsync(client, user));

        var issued = await ProcessPendingAsync(client, admin, user);

        // The reset ended the session; back on the Login Screen the notice is waiting.
        Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, user));

        await LoginAsync(client, user, issued);
        Assert.Equal(NoticeFalse, await ReadNoticeAsync(client, user));
    }

    // ---- 14.1 The notice stays until a sign-in or an update clears it ---------------------------------

    [Fact]
    public async Task TheNotice_HasNoTimeLimit_AndStaysThroughAnyNumberOfLookups()
    {
        var client = _factory.CreateClient();
        var user = Unique("stay-user");
        var admin = Unique("stay-admin");
        await SeedAsync(user, SystemRoles.Employee);
        await SeedAsync(admin, SystemRoles.Administrator);

        await RequestAsync(client, user);
        await ProcessPendingAsync(client, admin, user);

        // Polling every couple of seconds, a reload, the other identifier spelling: always there.
        for (var lookup = 0; lookup < 8; lookup++)
        {
            Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, user));
        }

        Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, $"{user}@taskflow.com"));

        // Even when the notice is old, nothing but a sign-in or an update removes it.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notice = await db.LoginPasswordResetNotices.SingleAsync(candidate => candidate.User!.Username == user);
            notice.CreatedAt = DateTime.UtcNow.AddDays(-3);
            await db.SaveChangesAsync();
        }

        Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, user));

        // The Inbox mail and the in-app notification are there too.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userId = await db.Users.Where(u => u.Username == user).Select(u => u.Id).SingleAsync();
            Assert.True(await db.Notifications.AnyAsync(
                n => n.UserId == userId && n.Type == NotificationType.PasswordReset));
            Assert.True(await db.MailboxItems.AnyAsync(
                item => item.OwnerUserId == userId && item.Folder == MailFolder.Inbox));
        }
    }

    [Fact]
    public async Task ALaterReset_RebindsTheNoticeToTheNewestEvent()
    {
        var client = _factory.CreateClient();
        var user = Unique("rebind-user");
        var admin = Unique("rebind-admin");
        await SeedAsync(user, SystemRoles.Employee);
        await SeedAsync(admin, SystemRoles.Administrator);

        await RequestAsync(client, user);
        var first = await ProcessPendingAsync(client, admin, user);
        Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, user));
        await LoginAsync(client, user, first);
        Assert.Equal(NoticeFalse, await ReadNoticeAsync(client, user));

        await RequestAsync(client, user);
        await ProcessPendingAsync(client, admin, user);

        Assert.Equal(NoticeTrue, await ReadNoticeAsync(client, user));
    }

    // ---- 11.3 The mandatory screen asks for New and Confirm only -----------------------------------

    [Fact]
    public async Task AFlaggedUser_CanChangeWithoutTheCurrentPassword_AndTheTemporaryPasswordStopsWorking()
    {
        var client = _factory.CreateClient();
        var user = Unique("mandatory-user");
        var admin = Unique("mandatory-admin");
        await SeedAsync(user, SystemRoles.Employee);
        await SeedAsync(admin, SystemRoles.Administrator);

        await RequestAsync(client, user);
        var temporary = await ProcessPendingAsync(client, admin, user);
        var token = await LoginAsync(client, user, temporary);

        // A wrong current password, if one is supplied, is still refused.
        var wrong = await ChangeAsync(client, token, new { currentPassword = "not-the-password", newPassword = "Fresh-P@ss1" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        // A too-short new password is refused with a field error, flag untouched.
        var weak = await ChangeAsync(client, token, new { newPassword = "abc" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        // New password only: accepted.
        const string chosen = "Fresh-P@ss1";
        Assert.Equal(HttpStatusCode.NoContent, (await ChangeAsync(client, token, new { newPassword = chosen })).StatusCode);

        // The temporary password is dead, the new one works, the flag is lifted and the session ended.
        var old = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = user, password = temporary });
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        var freshToken = await LoginAsync(client, user, chosen);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/auth/me", freshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/v1/auth/me", token)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await db.Users.AsNoTracking().SingleAsync(u => u.Username == user)).MustChangePassword);
    }

    [Fact]
    public async Task AnUnflaggedUser_StillNeedsTheCurrentPassword()
    {
        var client = _factory.CreateClient();
        var user = Unique("normal-user");
        await SeedAsync(user, SystemRoles.Employee);
        var token = await LoginAsync(client, user, Password);

        var missing = await ChangeAsync(client, token, new { newPassword = "Fresh-P@ss1" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var wrong = await ChangeAsync(client, token, new { currentPassword = "nope-nope", newPassword = "Fresh-P@ss1" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        // Nothing changed.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = user, password = Password })).StatusCode);
    }

    // ---- 4.3 Forced password change ---------------------------------------------------------

    [Fact]
    public async Task ATemporaryPassword_ForcesAChangeBeforeAnyOtherRoute_AndTheChangeLiftsIt()
    {
        var client = _factory.CreateClient();
        var user = Unique("force-user");
        var admin = Unique("force-admin");
        await SeedAsync(user, SystemRoles.Employee);
        await SeedAsync(admin, SystemRoles.Administrator);

        await RequestAsync(client, user);
        var temporary = await ProcessPendingAsync(client, admin, user);
        var token = await LoginAsync(client, user, temporary);

        // Every other authenticated route is refused with the dedicated problem.
        foreach (var url in new[] { "/api/v1/auth/me", "/api/v1/users", "/api/v1/notifications" })
        {
            var refused = await SendAsync(client, HttpMethod.Get, url, token);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            using var problem = await JsonDocument.ParseAsync(await refused.Content.ReadAsStreamAsync());
            Assert.Equal("Password change required", problem.RootElement.GetProperty("title").GetString());
        }

        // The current-user route stays open.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/users/me", token)).StatusCode);

        // So does the change itself, which lifts the obligation.
        const string newPassword = "Fresh-P@ss1";
        using var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/me/password")
        {
            Content = JsonContent.Create(new { currentPassword = temporary, newPassword })
        };
        change.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(change)).StatusCode);

        var freshToken = await LoginAsync(client, user, newPassword);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/auth/me", freshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_StaysReachable_ForAUserWhoMustStillChangeTheirPassword()
    {
        var client = _factory.CreateClient();
        var user = Unique("logout-user");
        var admin = Unique("logout-admin");
        await SeedAsync(user, SystemRoles.Employee);
        await SeedAsync(admin, SystemRoles.Administrator);

        await RequestAsync(client, user);
        var temporary = await ProcessPendingAsync(client, admin, user);
        var token = await LoginAsync(client, user, temporary);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post, "/api/v1/auth/logout", token)).StatusCode);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private Task SeedAsync(string username, string role) =>
        _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);

    private static async Task<HttpResponseMessage> ChangeAsync(HttpClient client, string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/me/password")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, string identifier) =>
        client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { identifier });

    private static async Task<string> ReadNoticeAsync(HttpClient client, string identifier)
    {
        var response = await client.GetAsync($"/api/v1/auth/password-reset-notice?identifier={Uri.EscapeDataString(identifier)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Processes the user's newest pending request as the administrator, typing the password the
    /// administrator would relay out of band, and returns it.</summary>
    private async Task<string> ProcessPendingAsync(HttpClient client, string admin, string user)
    {
        var adminToken = await LoginAsync(client, admin, Password);
        var issued = $"Issued-{Guid.NewGuid():N}"[..20];

        Guid reference;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            reference = await db.PasswordResetRequests.AsNoTracking()
                .Where(request => request.User!.Username == user && request.Status == PasswordResetStatus.Pending)
                .OrderByDescending(request => request.Id)
                .Select(request => request.ReferenceId)
                .FirstAsync();
        }

        using var process = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/password-reset-requests/{reference}/process")
        {
            Content = JsonContent.Create(new Dictionary<string, string?> { ["password"] = issued })
        };
        process.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(process)).StatusCode);
        return issued;
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string token)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
