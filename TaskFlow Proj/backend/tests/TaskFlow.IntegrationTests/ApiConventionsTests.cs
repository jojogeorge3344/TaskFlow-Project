using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using TaskFlow.Api.Cookies;

namespace TaskFlow.IntegrationTests;

public class ApiConventionsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public ApiConventionsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CorsPreflight_FromSpaOrigin_IsAllowed()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:4200", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("true", response.Headers.GetValues("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task UnhandledError_ReturnsProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/diagnostics/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal(500, problem!.Status);
    }

    [Fact]
    public void RefreshTokenCookie_HasSecureAttributesScopedToAuthPath()
    {
        var options = RefreshTokenCookie.CreateOptions(DateTime.UtcNow.AddDays(7));

        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
        Assert.Equal("/api/v1/auth", options.Path);
    }

    private sealed class ProblemResponse
    {
        public int Status { get; set; }
    }
}