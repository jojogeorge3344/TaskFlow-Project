using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

public class AuthenticationWiringTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public AuthenticationWiringTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsOk_WithValidToken()
    {
        var client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new User
        {
            Id = 1,
            Username = "admin",
            FullName = "Admin User",
            Role = Role.Administrator
        };

        var accessToken = tokenService.CreateAccessToken(user);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);

        var response = await client.GetAsync("/weatherforecast");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
