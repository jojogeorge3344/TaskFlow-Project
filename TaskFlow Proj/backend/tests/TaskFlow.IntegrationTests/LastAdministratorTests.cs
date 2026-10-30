using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;

namespace TaskFlow.IntegrationTests;

/// <summary>Needs a database with exactly one Administrator, so it has its own fixture.</summary>
public class LastAdministratorTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public LastAdministratorTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TheOnlyActiveAdministratorCannotBeMovedToAnotherRoleButCanOnceAnotherExists()
    {
        await _factory.SeedUserAsync("last-admin", "last-admin@taskflow.com", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "last-admin");
        var adminId = await UserIdAsync("last-admin");

        var blocked = await PutUserAsync(client, token, adminId, "last-admin", SystemRoles.EmployeeId);
        await _factory.SeedUserAsync("second-admin", "second-admin@taskflow.com", Password, SystemRoles.Administrator);
        var allowed = await PutUserAsync(client, token, await UserIdAsync("second-admin"), "second-admin", SystemRoles.EmployeeId);

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("only active Administrator", (await blocked.Content.ReadAsStringAsync()));
        Assert.Equal(SystemRoles.AdministratorId, await RoleIdAsync(adminId));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private async Task<HttpResponseMessage> PutUserAsync(HttpClient client, string token, int id, string username, int roleId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/users/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            employeeCode = (string?)null,
            fullName = "Updated Name",
            username,
            email = $"{username}@taskflow.com",
            roleId,
            projectId = (int?)null,
            password = (string?)null
        });
        return await client.SendAsync(request);
    }

    private async Task<int> UserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();
    }

    private async Task<int> RoleIdAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(user => user.Id == userId).Select(user => user.RoleId).SingleAsync();
    }

    private static async Task<string> LoginAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }
}
