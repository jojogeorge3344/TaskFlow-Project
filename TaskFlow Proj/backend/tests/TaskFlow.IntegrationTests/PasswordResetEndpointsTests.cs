using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Application.Mail;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

/// <summary>The request-creation half of Requirement 18: uniform anonymous submission from the Login
/// Screen, session-bound submission from the Profile Screen, the administrator notification mail and
/// the permission-gated list endpoint.</summary>
public class PasswordResetEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public PasswordResetEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task KnownAndUnknownIdentifier_GetTheSameConfirmation_ButOnlyTheKnownOneWritesARequest()
    {
        var client = _factory.CreateClient();
        var username = Unique("pwd-known");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);

        var before = await CountRequestsAsync();

        var known = await SubmitAsync(client, username);
        var unknown = await SubmitAsync(client, $"{username}-nope");

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);

        // Byte-identical answers: the response never discloses whether an account matched.
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());

        Assert.Equal(before + 1, await CountRequestsAsync());

        var request = await GetRequestAsync(username);
        Assert.NotNull(request);
        Assert.Equal(PasswordResetOrigin.LoginScreen, request.Origin);
        Assert.Equal(PasswordResetStatus.Pending, request.Status);
        Assert.NotEqual(Guid.Empty, request.ReferenceId);
        Assert.Null(request.CompletedAt);
    }

    [Fact]
    public async Task InactiveAndDeletedIdentifiers_WriteNoRequest_ButAnswerTheSame()
    {
        var client = _factory.CreateClient();

        var inactive = Unique("pwd-inactive");
        await _factory.SeedUserAsync(inactive, $"{inactive}@taskflow.com", Password, SystemRoles.Employee, isActive: false);

        var deleted = Unique("pwd-deleted");
        await _factory.SeedUserAsync(deleted, $"{deleted}@taskflow.com", Password, SystemRoles.Employee);
        await SetDeletedAsync(deleted, true);

        var before = await CountRequestsAsync();

        var unknown = await SubmitAsync(client, Unique("pwd-missing"));
        var inactiveResponse = await SubmitAsync(client, inactive);
        var deletedResponse = await SubmitAsync(client, deleted);

        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deletedResponse.StatusCode);

        var unknownBody = await unknown.Content.ReadAsStringAsync();
        Assert.Equal(unknownBody, await inactiveResponse.Content.ReadAsStringAsync());
        Assert.Equal(unknownBody, await deletedResponse.Content.ReadAsStringAsync());

        Assert.Equal(before, await CountRequestsAsync());
    }

    [Fact]
    public async Task BlankIdentifier_IsRejected_AndWritesNoRequest()
    {
        var client = _factory.CreateClient();
        var before = await CountRequestsAsync();

        var blank = await SubmitAsync(client, "   ");
        var missing = await client.PostAsJsonAsync(
            "/api/v1/auth/password-reset-requests",
            new Dictionary<string, string?> { ["identifier"] = null });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(before, await CountRequestsAsync());
    }

    [Fact]
    public async Task ProfileScreenRequest_UsesTheSessionAccount_AndRequiresASession()
    {
        var client = _factory.CreateClient();
        var username = Unique("pwd-profile");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        var userId = await GetUserIdAsync(username);
        var token = await LoginAsync(client, username);

        var before = await CountRequestsAsync();

        // Without a session nothing is created and the call is refused as unauthorized.
        var anonymous = await client.PostAsync("/api/v1/users/me/password-reset-request", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(before, await CountRequestsAsync());

        var signedIn = await SendAsync(client, HttpMethod.Post, "/api/v1/users/me/password-reset-request", token);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(before + 1, await CountRequestsAsync());

        var request = await GetRequestAsync(username);
        Assert.NotNull(request);
        Assert.Equal(PasswordResetOrigin.ProfileScreen, request.Origin);
        Assert.Equal(userId, request.UserId);
        Assert.Equal(PasswordResetStatus.Pending, request.Status);
    }

    [Fact]
    public async Task AdminNotificationMail_LinksStraightToTheRequest_WhenAnApplicationAddressIsConfigured()
    {
        var requester = Unique("link-user");
        var admin = Unique("link-admin");
        await _factory.SeedUserAsync(requester, $"{requester}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(admin, $"{admin}@taskflow.com", Password, SystemRoles.Administrator);

        using var configured = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Mail:AppBaseUrl"] = "https://tasks.example.test/" })));
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(configured.CreateClient(), requester)).StatusCode);

        var request = await GetRequestAsync(requester);
        var body = await GetAdminMailBodyAsync(requester);

        Assert.Contains(
            $"https://tasks.example.test/employees?resetRequest={request!.ReferenceId}",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Password, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminNotificationMail_HasNoLink_WhenNoApplicationAddressIsConfigured()
    {
        var requester = Unique("nolink-user");
        var admin = Unique("nolink-admin");
        await _factory.SeedUserAsync(requester, $"{requester}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(admin, $"{admin}@taskflow.com", Password, SystemRoles.Administrator);

        using var unconfigured = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Mail:AppBaseUrl"] = "" })));
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(unconfigured.CreateClient(), requester)).StatusCode);

        var body = await GetAdminMailBodyAsync(requester);

        Assert.DoesNotContain("resetRequest=", body, StringComparison.Ordinal);
        Assert.DoesNotContain("http", body, StringComparison.OrdinalIgnoreCase);
        // The mail is still delivered with the request details.
        Assert.Contains(requester, body, StringComparison.Ordinal);
    }

    private async Task<string> GetAdminMailBodyAsync(string requester)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.MailMessages
            .Where(mail => mail.IsSystem && mail.Subject == $"Password reset request: {requester}")
            .Select(mail => mail.Body)
            .SingleAsync();
    }

    [Fact]
    public async Task AdminNotificationMail_ReachesEveryOtherAdministrator_AndNotTheRequester()
    {
        var client = _factory.CreateClient();

        var requester = Unique("pwd-admin1");
        var secondAdmin = Unique("pwd-admin2");
        var thirdAdmin = Unique("pwd-admin3");
        var employee = Unique("pwd-employee");
        await _factory.SeedUserAsync(requester, $"{requester}@taskflow.com", Password, SystemRoles.Administrator);
        await _factory.SeedUserAsync(secondAdmin, $"{secondAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        await _factory.SeedUserAsync(thirdAdmin, $"{thirdAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        await _factory.SeedUserAsync(employee, $"{employee}@taskflow.com", Password, SystemRoles.Employee);

        var requesterUserId = await GetUserIdAsync(requester);
        var secondAdminId = await GetUserIdAsync(secondAdmin);
        var thirdAdminId = await GetUserIdAsync(thirdAdmin);

        var response = await SubmitAsync(client, requester);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var request = await GetRequestAsync(requester);
        Assert.NotNull(request);
        Assert.NotNull(request.AdminNotifiedAt);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subject = $"Password reset request: {requester}";
        var message = await db.MailMessages.SingleAsync(
            mail => mail.IsSystem && mail.Subject == subject);

        // One mail, one Inbox copy per eligible administrator, and never to the requester.
        var inboxOwners = await db.MailboxItems
            .Where(item => item.MessageId == message.Id && item.Folder == MailFolder.Inbox)
            .Select(item => item.OwnerUserId)
            .OrderBy(id => id)
            .ToListAsync();
        Assert.Equal(new[] { secondAdminId, thirdAdminId }.OrderBy(id => id), inboxOwners);

        var recipientIds = await db.MailRecipients
            .Where(recipient => recipient.MessageId == message.Id)
            .Select(recipient => recipient.UserId)
            .ToListAsync();
        Assert.Equal(inboxOwners, recipientIds.OrderBy(id => id));

        // The body carries the requester's identification and the request reference - and no password.
        Assert.Contains(requester, message.Body, StringComparison.Ordinal);
        Assert.Contains($"{requester}@taskflow.com", message.Body, StringComparison.Ordinal);
        Assert.Contains(SystemRoles.Administrator, message.Body, StringComparison.Ordinal);
        Assert.Contains(request!.ReferenceId.ToString(), message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, message.Subject, StringComparison.Ordinal);

        // The requester-admin gets no copy of the notification.
        var requesterCopies = await db.MailboxItems.CountAsync(
            item => item.MessageId == message.Id && item.OwnerUserId == requesterUserId);
        Assert.Equal(0, requesterCopies);
    }

    [Fact]
    public async Task MailWriteFailure_StillCreatesTheRequest_WithNoNotificationStamp()
    {
        var username = Unique("pwd-mailfail");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);

        using var changed = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemMailWriter>();
            services.AddSingleton<ISystemMailWriter>(new ThrowingMailWriter());
        }));
        var failingClient = changed.CreateClient();

        var before = await CountRequestsAsync();

        var response = await SubmitAsync(failingClient, username);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(before + 1, await CountRequestsAsync());

        var request = await GetRequestAsync(username);
        Assert.NotNull(request);
        Assert.Null(request.AdminNotifiedAt);
        Assert.Equal(PasswordResetStatus.Pending, request.Status);
    }

    [Fact]
    public async Task ListEndpoint_OnlyAnswersCallersHoldingEmployeesManage()
    {
        var client = _factory.CreateClient();

        var manager = Unique("pwd-mgr");
        var employee = Unique("pwd-listemp");
        var administrator = Unique("pwd-listadmin");
        await _factory.SeedUserAsync(manager, $"{manager}@taskflow.com", Password, SystemRoles.Manager);
        await _factory.SeedUserAsync(employee, $"{employee}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(administrator, $"{administrator}@taskflow.com", Password, SystemRoles.Administrator);

        var managerToken = await LoginAsync(client, manager);
        var employeeToken = await LoginAsync(client, employee);
        var administratorToken = await LoginAsync(client, administrator);

        const string url = "/api/v1/password-reset-requests";

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(url)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Get, url, managerToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Get, url, employeeToken)).StatusCode);

        var allowed = await SendAsync(client, HttpMethod.Get, url, administratorToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        using var document = await JsonDocument.ParseAsync(await allowed.Content.ReadAsStreamAsync());
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    private Task<HttpResponseMessage> SubmitAsync(HttpClient client, string? identifier) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/password-reset-requests",
            new Dictionary<string, string?> { ["identifier"] = identifier });

    private async Task<string> LoginAsync(HttpClient client, string username)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = username,
            password = Password
        });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string token)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private async Task<int> CountRequestsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PasswordResetRequests.CountAsync();
    }

    private async Task<PasswordResetRequest?> GetRequestAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PasswordResetRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(request => request.User!.Username == username);
    }

    private async Task<int> GetUserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();
    }

    private async Task SetDeletedAsync(string username, bool isDeleted)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Username == username);
        user.IsDeleted = isDeleted;
        await db.SaveChangesAsync();
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>Fails every system mail write, to prove a mail failure cannot take the request with it.</summary>
    private sealed class ThrowingMailWriter : ISystemMailWriter
    {
        public Task WriteAsync(
            IReadOnlyCollection<int> recipientUserIds,
            string subject,
            string body,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated system mail write failure.");
    }
}
