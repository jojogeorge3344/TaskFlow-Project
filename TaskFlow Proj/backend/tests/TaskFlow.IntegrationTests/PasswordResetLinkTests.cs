using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

/// <summary>Design D14: the encrypted login link an administrator copies, how the Login Screen resolves it, the
/// password update behind the bell, and the rate limit on the endpoints that check a password.</summary>
public class PasswordResetLinkTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public PasswordResetLinkTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    // ---- 13.1 The administrator's login link ----------------------------------------------------------

    [Fact]
    public async Task TheAdministratorGetsAnEncryptedLink_ThatHoldsNeitherTheEmailNorThePassword()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("link-ok");
        await RequestAsync(client, user);
        var issued = await ProcessAsync(client, admin, user);
        var reference = await LatestReferenceAsync(user);

        var response = await GetLinkAsync(client, await LoginAsync(client, admin, Password), reference);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var path = document.RootElement.GetProperty("path").GetString()!;

        Assert.StartsWith("/login/", path, StringComparison.Ordinal);
        var token = path["/login/".Length..];
        Assert.True(token.Length > 40, "The token is an encrypted payload, not a short readable value.");
        Assert.DoesNotContain(user, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", token, StringComparison.Ordinal);
        Assert.DoesNotContain(issued, body, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, body, StringComparison.Ordinal);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public async Task OnlyAnAdministratorCanGetTheLink_AndOnlyForTheLatestCompletedRequest()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("link-rules");
        var other = Unique("link-rules-employee");
        await SeedAsync(other, SystemRoles.Employee);
        var adminToken = await LoginAsync(client, admin, Password);

        // A pending request has no link yet.
        await RequestAsync(client, user);
        var pending = await LatestReferenceAsync(user);
        Assert.Equal(HttpStatusCode.Conflict, (await GetLinkAsync(client, adminToken, pending)).StatusCode);

        // Completed: the administrator gets it, an employee does not.
        await ProcessAsync(client, admin, user);
        Assert.Equal(HttpStatusCode.OK, (await GetLinkAsync(client, adminToken, pending)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetLinkAsync(client, await LoginAsync(client, other, Password), pending)).StatusCode);

        // Unknown reference.
        Assert.Equal(HttpStatusCode.NotFound, (await GetLinkAsync(client, adminToken, Guid.NewGuid())).StatusCode);

        // A later reset replaces the event: the earlier request can no longer yield a link.
        await RequestAsync(client, user);
        await ProcessAsync(client, admin, user);
        Assert.Equal(HttpStatusCode.Conflict, (await GetLinkAsync(client, adminToken, pending)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetLinkAsync(client, adminToken, await LatestReferenceAsync(user))).StatusCode);
    }

    // ---- 13.2 Resolving a link on the Login Screen ----------------------------------------------------

    [Fact]
    public async Task AValidLinkReturnsTheEmailAndTheNotice_AndBadOnesLookLikeAnUnknownAccount()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("resolve");
        await RequestAsync(client, user);
        await ProcessAsync(client, admin, user);
        var token = await TokenAsync(client, admin, user);

        var valid = await ResolveAsync(client, token);
        Assert.Equal($"{user}@taskflow.com", valid.GetProperty("email").GetString());
        Assert.True(valid.GetProperty("noticeAvailable").GetBoolean());

        // Tampered, truncated, empty and made-up tokens all answer the same uniform empty shape.
        var empty = """{"email":null,"noticeAvailable":false}""";
        foreach (var bad in new[] { token[..^3] + "AAA", token[..20], "", "not-a-token", Convert.ToBase64String(new byte[64]) })
        {
            var response = await client.GetAsync($"/api/v1/auth/password-reset-link?token={Uri.EscapeDataString(bad)}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(empty, await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task ALaterResetRetiresAnEarlierLink()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("supersede");
        await RequestAsync(client, user);
        await ProcessAsync(client, admin, user);
        var first = await TokenAsync(client, admin, user);

        await RequestAsync(client, user);
        await ProcessAsync(client, admin, user);
        var second = await TokenAsync(client, admin, user);

        var old = await ResolveAsync(client, first);
        Assert.Equal(JsonValueKind.Null, old.GetProperty("email").ValueKind);
        Assert.False(old.GetProperty("noticeAvailable").GetBoolean());
        Assert.Equal($"{user}@taskflow.com", (await ResolveAsync(client, second)).GetProperty("email").GetString());
    }

    // ---- 13.3 The password update behind the bell -----------------------------------------------------

    [Fact]
    public async Task UpdatingFromTheLoginPage_ReplacesThePassword_AndClearsTheNotice()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("complete");
        await RequestAsync(client, user);
        var issued = await ProcessAsync(client, admin, user);
        var token = await TokenAsync(client, admin, user);

        const string chosen = "Fresh-P@ss1";
        // The link is the proof: no temporary password is sent or asked for.
        var response = await CompleteAsync(client, new { token, newPassword = chosen });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The temporary password is dead, the new one signs in, nothing is owed and the notice is gone.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = user, password = issued })).StatusCode);
        var freshToken = await LoginAsync(client, user, chosen);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "/api/v1/auth/me", freshToken)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await db.Users.AsNoTracking().SingleAsync(u => u.Username == user)).MustChangePassword);
        var notice = await db.LoginPasswordResetNotices.AsNoTracking().SingleAsync(n => n.User!.Username == user);
        Assert.NotNull(notice.ClearedAt);

        // The lookup now says there is nothing waiting.
        Assert.False((await ResolveAsync(client, token)).GetProperty("noticeAvailable").GetBoolean());
    }

    [Fact]
    public async Task WithoutALink_NothingCanBeUpdated_EvenWithAnIdentifierAndTheTemporaryPassword()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("nolink");
        await RequestAsync(client, user);
        var issued = await ProcessAsync(client, admin, user);

        // The old identifier-plus-temporary-password body is no longer a way in: it names no link.
        foreach (var body in new object[]
        {
            new { identifier = user, temporaryPassword = issued, newPassword = "Fresh-P@ss1" },
            new { newPassword = "Fresh-P@ss1" }
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(client, body)).StatusCode);
        }

        // Nothing moved: the issued password is still the credential.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = user, password = issued })).StatusCode);
    }

    [Fact]
    public async Task EveryRefusal_IsTheSameGeneric401_AndChangesNothing()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("refuse");
        await RequestAsync(client, user);
        await ProcessAsync(client, admin, user);
        var superseded = await TokenAsync(client, admin, user);

        // A later reset retires the first link; the current one is the only live one.
        await RequestAsync(client, user);
        var issued = await ProcessAsync(client, admin, user);
        var current = await TokenAsync(client, admin, user);

        var cases = new[]
        {
            "garbage-token",
            current[..^3] + "AAA",
            current[..30],
            superseded,
            Convert.ToBase64String(new byte[64])
        };

        string? firstBody = null;
        foreach (var token in cases)
        {
            var response = await CompleteAsync(client, new { token, newPassword = "Fresh-P@ss1" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            firstBody ??= text;
            Assert.Equal(firstBody, text);
        }

        // Nothing moved: the issued password still works.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = user, password = issued })).StatusCode);
    }

    [Fact]
    public async Task AWeakNewPasswordOrMissingFields_AreRefusedWithAFieldError()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("weak");
        await RequestAsync(client, user);
        var issued = await ProcessAsync(client, admin, user);
        var token = await TokenAsync(client, admin, user);

        foreach (var body in new object[]
        {
            new { token, newPassword = "abc" },
            new { token, newPassword = "" },
            new { token },
            new { newPassword = "Fresh-P@ss1" }
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(client, body)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = user, password = issued })).StatusCode);
    }

    [Fact]
    public async Task TheLinkWorksOnce_AndIsDeadAfterItIsUsed()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("once");
        await RequestAsync(client, user);
        await ProcessAsync(client, admin, user);
        var token = await TokenAsync(client, admin, user);
        var reference = await LatestReferenceAsync(user);

        Assert.Equal(HttpStatusCode.NoContent, (await CompleteAsync(client, new { token, newPassword = "Fresh-P@ss1" })).StatusCode);

        // Used: it no longer resolves, no longer updates, and the administrator can not get another for it.
        var resolved = await ResolveAsync(client, token);
        Assert.Equal(JsonValueKind.Null, resolved.GetProperty("email").ValueKind);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteAsync(client, new { token, newPassword = "Other-P@ss2" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await GetLinkAsync(client, await LoginAsync(client, admin, Password), reference)).StatusCode);

        // The first update stands.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = user, password = "Fresh-P@ss1" })).StatusCode);
    }

    [Fact]
    public async Task ASuccessfulSignIn_RetiresTheLink()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("signin-retires");
        await RequestAsync(client, user);
        var issued = await ProcessAsync(client, admin, user);
        var token = await TokenAsync(client, admin, user);

        await LoginAsync(client, user, issued);

        Assert.Equal(JsonValueKind.Null, (await ResolveAsync(client, token)).GetProperty("email").ValueKind);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteAsync(client, new { token, newPassword = "Fresh-P@ss1" })).StatusCode);
    }

    [Fact]
    public async Task TheAdministratorsLinkResponse_NeverContainsAPassword()
    {
        var client = _factory.CreateClient();
        var (user, admin) = await SeedPairAsync("idpath");
        await RequestAsync(client, user);
        var issued = await ProcessAsync(client, admin, user);


        var link = await GetLinkAsync(client, await LoginAsync(client, admin, Password), await LatestReferenceAsync(user));
        var body = await link.Content.ReadAsStringAsync();
        Assert.DoesNotContain(issued, body, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, body, StringComparison.Ordinal);
    }

    // ---- 13.4 Rate limiting ----------------------------------------------------------------------------

    [Fact]
    public async Task RepeatedPasswordAttempts_AreRefusedWithARetryAfter_ButOtherEndpointsAreNot()
    {
        using var limited = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:PasswordAttemptsPerMinute"] = "3" })));
        var client = limited.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "nobody", password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var refused = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "nobody", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.True(int.Parse(retryAfter!.Single()) > 0);

        // The update endpoint shares the policy, and unrelated endpoints are untouched.
        var update = await client.PostAsJsonAsync("/api/v1/auth/complete-password-reset", new { token = "x", newPassword = "Fresh-P@ss1" });
        Assert.Equal(HttpStatusCode.TooManyRequests, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/password-reset-notice?identifier=nobody")).StatusCode);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private async Task<(string User, string Admin)> SeedPairAsync(string prefix)
    {
        var user = Unique($"{prefix}-u");
        var admin = Unique($"{prefix}-a");
        await SeedAsync(user, SystemRoles.Employee);
        await SeedAsync(admin, SystemRoles.Administrator);
        return (user, admin);
    }

    private Task SeedAsync(string username, string role) =>
        _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, string identifier) =>
        client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { identifier });

    private static Task<HttpResponseMessage> CompleteAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/v1/auth/complete-password-reset", body);

    private static async Task<JsonElement> ResolveAsync(HttpClient client, string token)
    {
        var response = await client.GetAsync($"/api/v1/auth/password-reset-link?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task<Guid> LatestReferenceAsync(string user)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PasswordResetRequests.AsNoTracking()
            .Where(request => request.User!.Username == user)
            .OrderByDescending(request => request.Id)
            .Select(request => request.ReferenceId)
            .FirstAsync();
    }

    /// <summary>Has the administrator process the user's newest pending request with a typed password and
    /// returns that password (the one the administrator would relay).</summary>
    private async Task<string> ProcessAsync(HttpClient client, string admin, string user)
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

    private async Task<string> TokenAsync(HttpClient client, string admin, string user)
    {
        var response = await GetLinkAsync(client, await LoginAsync(client, admin, Password), await LatestReferenceAsync(user));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("path").GetString()!["/login/".Length..];
    }

    private static async Task<HttpResponseMessage> GetLinkAsync(HttpClient client, string adminToken, Guid reference)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/password-reset-requests/{reference}/login-link");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        return await client.SendAsync(request);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
