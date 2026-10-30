using TaskFlow.Api.Integrations.Interfaces.Security;
using System.Net;
using TaskFlow.Api.Common.Security;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.IntegrationTests;

public class AuthenticationWiringTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public AuthenticationWiringTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async System.Threading.Tasks.Task ProtectedEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/notifications/unread-count");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProtectedEndpoint_ReturnsOk_WithValidToken()
    {
        var client = _factory.CreateClient();

        // Tokens are only honoured for users that exist and whose security stamp matches.
        await _factory.SeedUserAsync("wiring-admin", "wiring-admin@taskflow.com", "P@ssw0rd!", SystemRoles.Administrator);

        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.Include(candidate => candidate.Role).SingleAsync(candidate => candidate.Username == "wiring-admin");

        var accessToken = tokenService.CreateAccessToken(user);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);

        var response = await client.GetAsync("/api/v1/notifications/unread-count");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
