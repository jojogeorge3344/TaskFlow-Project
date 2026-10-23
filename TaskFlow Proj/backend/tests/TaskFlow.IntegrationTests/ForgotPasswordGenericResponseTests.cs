using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.IntegrationTests;

/// <summary>Task 7.2: the Profile forgot-password response is generic. It never reveals who the Administrator
/// recipient is, and an Administrator caller gets exactly the response an employee does.</summary>
public class ForgotPasswordGenericResponseTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";
    private const string ForgotPassword = "/api/v1/auth/forgot-password";

    private readonly TaskFlowApiFactory _factory;

    public ForgotPasswordGenericResponseTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EmployeeAndAdministratorCallersGetTheSameGenericResponse()
    {
        await _factory.SeedUserAsync("generic-admin", "generic-admin@taskflow.com", Password, SystemRoles.Administrator);
        await _factory.SeedUserAsync("generic-known", "generic-known@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync("generic-self-admin", "generic-self-admin@taskflow.com", Password, SystemRoles.Administrator);
        var client = _factory.CreateClient();

        var employee = await PostAsync(client, "generic-known@taskflow.com");
        var administrator = await PostAsync(client, "generic-self-admin@taskflow.com");

        var employeeBody = await employee.Content.ReadAsStringAsync();
        var administratorBody = await administrator.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, employee.StatusCode);
        Assert.Equal(HttpStatusCode.OK, administrator.StatusCode);
        Assert.Equal(employeeBody, administratorBody);

        foreach (var body in new[] { employeeBody, administratorBody })
        {
            Assert.DoesNotContain("generic-admin", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("generic-known", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("administrator", body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(await DbAsync(db => db.PasswordResetTokens.AnyAsync(row => row.User!.Username == "generic-known")));
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = email, password = Password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthBody>();

        using var request = new HttpRequestMessage(HttpMethod.Post, ForgotPassword);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
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
