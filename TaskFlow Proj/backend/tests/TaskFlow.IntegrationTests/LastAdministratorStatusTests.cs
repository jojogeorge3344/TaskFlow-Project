using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.IntegrationTests;

/// <summary>Needs a database with exactly one Administrator, so it has its own fixture.</summary>
public class LastAdministratorStatusTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public LastAdministratorStatusTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TheOnlyActiveAdministratorCannotBeDeactivatedButCanOnceAnotherExists()
    {
        await _factory.SeedUserAsync("status-admin", "status-admin@taskflow.local", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "status-admin");
        var adminId = await UserIdAsync("status-admin");

        var blocked = await PatchStatusAsync(client, token, adminId, isActive: false);
        await _factory.SeedUserAsync("status-admin-2", "status-admin-2@taskflow.local", Password, SystemRoles.Administrator);
        var allowed = await PatchStatusAsync(client, token, await UserIdAsync("status-admin-2"), isActive: false);

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("only active Administrator", await blocked.Content.ReadAsStringAsync());
        Assert.True(await IsActiveAsync(adminId));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private async Task<HttpResponseMessage> PatchStatusAsync(HttpClient client, string token, int id, bool isActive)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{id}/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { isActive });
        return await client.SendAsync(request);
    }

    private async Task<bool> IsActiveAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(user => user.Id == userId).Select(user => user.IsActive).SingleAsync();
    }

    private async Task<int> UserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();
    }

    private static async Task<string> LoginAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }
}
