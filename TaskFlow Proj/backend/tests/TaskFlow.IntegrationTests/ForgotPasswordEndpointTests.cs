using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.IntegrationTests;

/// <summary>Endpoint-level coverage for the reset surfaces: the authenticated (Profile-only) forgot-password request and the
/// Administrator-only validate/consume endpoints.</summary>
public class ForgotPasswordEndpointTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";
    private const string ForgotPassword = "/api/v1/auth/forgot-password";
    private const string ResetPassword = "/api/v1/auth/reset-password";

    private readonly TaskFlowApiFactory _factory;

    public ForgotPasswordEndpointTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ForgotPassword_Anonymous_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(ForgotPassword, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_SignedInEmployee_ReturnsGenericOkAndSendsMailToAdmin()
    {
        await _factory.SeedUserAsync("endpoint-emp", "endpoint-emp@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync("endpoint-admin", "endpoint-admin@taskflow.com", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();
        var token = await LoginForTokenAsync(client, "endpoint-emp@taskflow.com", Password);

        var response = await SendAsync(client, HttpMethod.Post, ForgotPassword, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await DbAsync(db => db.MailMessages.AnyAsync(message =>
            message.Subject == "TaskFlow - Password reset requested")));
    }

    [Fact]
    public async Task ForgotPassword_SignedInAdministrator_ReturnsGenericOkAndGeneratesPassword()
    {
        await _factory.SeedUserAsync("endpoint-admin-self", "endpoint-admin-self@taskflow.com", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();
        var token = await LoginForTokenAsync(client, "endpoint-admin-self@taskflow.com", Password);

        var response = await SendAsync(client, HttpMethod.Post, ForgotPassword, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var adminId = await DbAsync(db => db.Users
            .Where(user => user.Username == "endpoint-admin-self")
            .Select(user => user.Id)
            .SingleAsync());
        Assert.False(await DbAsync(db => db.PasswordResetTokens.AnyAsync(row => row.UserId == adminId)));
        Assert.True(await DbAsync(db => db.MailMessages.AnyAsync(message =>
            message.Subject == "TaskFlow - Your password has been reset" &&
            message.Recipients.Any(recipient => recipient.UserId == adminId))));
    }

    [Fact]
    public async Task ForgotPassword_ActsOnTheCaller_AndIgnoresAnyIdentifierInTheBody()
    {
        await _factory.SeedUserAsync("endpoint-caller", "endpoint-caller@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync("endpoint-victim", "endpoint-victim@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync("endpoint-known-admin", "endpoint-known-admin@taskflow.com", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();
        var token = await LoginForTokenAsync(client, "endpoint-caller@taskflow.com", Password);

        var response = await SendAsync(
            client, HttpMethod.Post, ForgotPassword, token, new { identifier = "endpoint-victim@taskflow.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await DbAsync(db => db.PasswordResetTokens.AnyAsync(row => row.User!.Username == "endpoint-caller")));
        Assert.False(await DbAsync(db => db.PasswordResetTokens.AnyAsync(row => row.User!.Username == "endpoint-victim")));
    }

    [Fact]
    public async Task ResetPassword_ValidToken_SetsPasswordAndReturnsNoContent()
    {
        await _factory.SeedUserAsync("endpoint-emp2", "endpoint-emp2@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync("endpoint-admin2", "endpoint-admin2@taskflow.com", Password, SystemRoles.Administrator);

        var userId = await DbAsync(db => db.Users
            .Where(user => user.Email == "endpoint-emp2@taskflow.com")
            .Select(user => user.Id)
            .SingleAsync());
        var token = await CreateTokenAsync(userId);
        var client = _factory.CreateClient();
        var adminToken = await LoginForTokenAsync(client, "endpoint-admin2@taskflow.com", Password);

        var response = await SendAsync(client, HttpMethod.Post, ResetPassword, adminToken, new { token });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var usedToken = await DbAsync(db => db.PasswordResetTokens
            .OrderByDescending(row => row.Id)
            .FirstAsync(row => row.UserId == userId));
        Assert.True(usedToken.Used);
        Assert.NotNull(usedToken.ConsumedAt);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsError()
    {
        await _factory.SeedUserAsync("endpoint-admin3", "endpoint-admin3@taskflow.com", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();
        var adminToken = await LoginForTokenAsync(client, "endpoint-admin3@taskflow.com", Password);

        var response = await SendAsync(client, HttpMethod.Post, ResetPassword, adminToken, new { token = "invalid-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_RequiresAdministrator()
    {
        await _factory.SeedUserAsync("endpoint-emp3", "endpoint-emp3@taskflow.com", Password, SystemRoles.Employee);
        var client = _factory.CreateClient();
        var employeeToken = await LoginForTokenAsync(client, "endpoint-emp3@taskflow.com", Password);

        var response = await SendAsync(client, HttpMethod.Post, ResetPassword, employeeToken, new { token = "any-token" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidateResetToken_ValidToken_ReturnsUserDetails()
    {
        await _factory.SeedUserAsync("endpoint-emp4", "endpoint-emp4@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync("endpoint-admin4", "endpoint-admin4@taskflow.com", Password, SystemRoles.Administrator);

        var userId = await DbAsync(db => db.Users
            .Where(user => user.Email == "endpoint-emp4@taskflow.com")
            .Select(user => user.Id)
            .SingleAsync());
        var token = await CreateTokenAsync(userId);
        var client = _factory.CreateClient();
        var adminToken = await LoginForTokenAsync(client, "endpoint-admin4@taskflow.com", Password);

        var response = await SendAsync(
            client,
            HttpMethod.Get,
            $"{ResetPassword}/{Uri.EscapeDataString(token)}",
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DetailsBody>();
        Assert.NotNull(body);
        Assert.Equal(userId, body!.User.Id);
        Assert.Equal(1, body.RequestCount);
        Assert.Equal("Pending", Assert.Single(body.Requests).Status);
    }

    [Fact]
    public async Task ValidateResetToken_InvalidToken_ReturnsError()
    {
        await _factory.SeedUserAsync("endpoint-admin5", "endpoint-admin5@taskflow.com", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();
        var adminToken = await LoginForTokenAsync(client, "endpoint-admin5@taskflow.com", Password);

        var response = await SendAsync(client, HttpMethod.Get, $"{ResetPassword}/invalid-token", adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<string> CreateTokenAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IPasswordResetTokenService>();
        return await tokenService.RequestResetTokenAsync(userId);
    }

    private async Task<string> LoginForTokenAsync(HttpClient client, string identifier, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = identifier, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseBody>();
        return body!.AccessToken;
    }

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

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    private sealed record AuthResponseBody(string AccessToken, DateTime ExpiresAt);

    private sealed record UserBody(int Id, string FullName, string Username, string Email, string Role);

    private sealed record RequestEntryBody(int Id, string Status);

    private sealed record DetailsBody(UserBody User, int RequestCount, List<RequestEntryBody> Requests);
}
