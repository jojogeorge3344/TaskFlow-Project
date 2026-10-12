using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

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

        var response = await client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProtectedEndpoint_ReturnsOk_WithValidToken()
    {
        var client = _factory.CreateClient();

        // Tokens are only honoured for users that exist and whose security stamp matches.
        await _factory.SeedUserAsync("wiring-admin", "wiring-admin@taskflow.local", "P@ssw0rd!", Role.Administrator);

        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Username == "wiring-admin");

        var accessToken = tokenService.CreateAccessToken(user);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);

        var response = await client.GetAsync("/weatherforecast");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
