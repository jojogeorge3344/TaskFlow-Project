using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Application.Mfa;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.IntegrationTests;

/// <summary>Real time plus an offset the test can move forward, so each authenticator code can come from a new
/// 30-second step (a code can only be used once) without waiting.</summary>
internal sealed class SteppingClock : TimeProvider
{
    private long _offsetSeconds;

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddSeconds(Interlocked.Read(ref _offsetSeconds));

    public void Advance(int seconds) => Interlocked.Add(ref _offsetSeconds, seconds);
}

/// <summary>Shared plumbing for the MFA endpoint tests.</summary>
internal sealed class MfaTestHarness
{
    public const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _baseFactory;

    /// <param name="requireMfa">Turns the "set up two-step verification first" gate on. Off by default so most tests can sign in with just a password.</param>
    public MfaTestHarness(TaskFlowApiFactory baseFactory, bool requireMfa = false)
    {
        _baseFactory = baseFactory;
        Clock = new SteppingClock();
        Factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Mfa:Required"] = requireMfa ? "true" : "false" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
            });
        });
    }

    public SteppingClock Clock { get; }

    public WebApplicationFactory<Program> Factory { get; }

    /// <summary>A code for a time step the server has not seen yet.</summary>
    public string NextCode(byte[] secret)
    {
        Clock.Advance(31);
        return Totp.CodeFor(secret, Totp.StepFor(Clock.GetUtcNow()));
    }

    public async Task<string> SeedAndLoginAsync(HttpClient client, string username, string role = SystemRoles.Employee)
    {
        await _baseFactory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string url, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    /// <summary>Runs setup and enable for a signed-in user and returns the key and backup codes.</summary>
    public async Task<(byte[] Secret, string[] BackupCodes)> EnrolAsync(HttpClient client, string token)
    {
        var setup = await SendAsync(client, HttpMethod.Post, "/api/v1/users/me/mfa/setup", token, new { currentPassword = Password });
        setup.EnsureSuccessStatusCode();
        using var setupDoc = JsonDocument.Parse(await setup.Content.ReadAsStringAsync());
        var secret = Base32.Decode(setupDoc.RootElement.GetProperty("key").GetString())!;

        var enable = await SendAsync(client, HttpMethod.Post, "/api/v1/users/me/mfa/enable", token, new { code = NextCode(secret) });
        enable.EnsureSuccessStatusCode();
        using var enableDoc = JsonDocument.Parse(await enable.Content.ReadAsStringAsync());
        var codes = enableDoc.RootElement.GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        return (secret, codes);
    }
}
