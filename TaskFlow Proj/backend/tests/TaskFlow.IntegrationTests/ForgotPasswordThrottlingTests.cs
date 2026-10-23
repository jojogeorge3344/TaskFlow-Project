using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;

namespace TaskFlow.IntegrationTests;

/// <summary>Task 7.1: reset requests are throttled per requesting account and per Administrator recipient.</summary>
public class ForgotPasswordThrottlingTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";
    private const string ForgotPassword = "/api/v1/auth/forgot-password";

    private readonly TaskFlowApiFactory _factory;

    public ForgotPasswordThrottlingTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequestsOverThePerUserLimitStopMintingLinks_AndKeepTheGenericResponse()
    {
        await _factory.SeedUserAsync("throttle-admin", "throttle-admin@taskflow.com", Password, SystemRoles.Administrator);
        await _factory.SeedUserAsync("throttle-target", "throttle-target@taskflow.com", Password, SystemRoles.Employee);
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { emailOrUsername = "throttle-target", password = Password });
        login.EnsureSuccessStatusCode();
        var accessToken = (await login.Content.ReadFromJsonAsync<AuthBody>())!.AccessToken;

        var bodies = new List<string>();
        for (var request = 0; request < PasswordResetRateLimiter.RequestsPerUser; request++)
        {
            var allowed = await PostForgotAsync(client, accessToken);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            bodies.Add(await allowed.Content.ReadAsStringAsync());
        }

        var userId = await DbAsync(db => db.Users
            .Where(user => user.Username == "throttle-target")
            .Select(user => user.Id)
            .SingleAsync());
        var linksBefore = await DbAsync(db => db.PasswordResetTokens.CountAsync(row => row.UserId == userId));
        Assert.Equal(PasswordResetRateLimiter.RequestsPerUser, linksBefore);

        var throttled = await PostForgotAsync(client, accessToken);

        Assert.Equal(HttpStatusCode.OK, throttled.StatusCode);
        Assert.Equal(bodies[0], await throttled.Content.ReadAsStringAsync());
        Assert.Equal(linksBefore, await DbAsync(db => db.PasswordResetTokens.CountAsync(row => row.UserId == userId)));
    }

    private static async Task<HttpResponseMessage> PostForgotAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ForgotPassword);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    private sealed record AuthBody(string AccessToken);
}
