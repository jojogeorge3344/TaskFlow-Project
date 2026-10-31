using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Services.Assistant;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Middleware;

namespace TaskFlow.IntegrationTests.Common;

/// <summary>The api-error-handling contract: one problem-details shape, a fixed status per failure kind, and no internal
/// detail in a 500.</summary>
public class GlobalExceptionHandlerTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public GlobalExceptionHandlerTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<Exception, int> KnownFailures => new()
    {
        { new BusinessRuleException("You cannot delete your own account."), StatusCodes.Status400BadRequest },
        { new ForbiddenException("You do not have access to this task."), StatusCodes.Status403Forbidden },
        { new NotFoundException("That model is not in the list."), StatusCodes.Status404NotFound },
        { new ConflictException("Move the mail to Trash before deleting it forever."), StatusCodes.Status409Conflict },
        { new AssistantRateLimitedException(TimeSpan.FromSeconds(30)), StatusCodes.Status429TooManyRequests },
    };

    [Theory]
    [MemberData(nameof(KnownFailures))]
    public async Task AKnownFailureBecomesAProblemWithItsStatusAndMessage(Exception exception, int status)
    {
        var (context, body) = await HandleAsync(exception);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.Equal(exception.Message, body.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.False(body.TryGetProperty("error", out _));
        Assert.False(body.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task AFailureKindKeepsItsTitleAndRetryAfter()
    {
        var (context, body) = await HandleAsync(new AssistantRateLimitedException(TimeSpan.FromSeconds(30)));

        Assert.Equal("Too many requests", body.GetProperty("title").GetString());
        Assert.Equal("30", context.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task AValidationFailureListsTheFieldErrors()
    {
        var (context, body) = await HandleAsync(new RequestValidationException("name", "A built-in model cannot be removed."));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("A built-in model cannot be removed.", body.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    [Fact]
    public async Task ALostSaveRaceBecomesARetryableConflict()
    {
        var (context, body) = await HandleAsync(new DbUpdateConcurrencyException("race"));

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("The record was changed at the same time.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task AnUnexpectedFailureNeverShowsItsMessage()
    {
        var (context, body) = await HandleAsync(new InvalidOperationException("Sequence contains no elements"));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(GlobalExceptionHandler.GenericErrorDetail, body.GetProperty("detail").GetString());
        Assert.DoesNotContain("Sequence contains no elements", body.GetRawText());
    }

    [Fact]
    public async Task AnUnexpectedFailureOverHttpIsLoggedUnderTheResponsesTraceId()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/diagnostics/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain("Diagnostic failure.", body.GetRawText());
        var traceId = body.GetProperty("traceId").GetString()!;
        Assert.Contains(_factory.Logs.Lines, line => line.Contains("Unhandled exception") && line.Contains(traceId));
        Assert.Contains(_factory.Logs.Lines, line => line.Contains("Diagnostic failure."));
    }

    [Fact]
    public async Task AnInvalidRequestIsRejectedByTheValidationFilterBeforeTheEndpointRuns()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("EmailOrUsername", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task EveryRequestIsLoggedOnceWithoutItsBody()
    {
        var client = _factory.CreateClient();
        var path = $"/api/v1/request-log-probe-{Guid.NewGuid():N}";
        const string secret = "never-log-this-password";

        await client.PostAsJsonAsync(path, new { password = secret });

        var entry = Assert.Single(_factory.Logs.Lines, line => line.StartsWith($"HTTP POST {path} responded"));
        Assert.Contains("responded 404", entry);
        Assert.Contains(" ms for user anonymous (trace ", entry);
        Assert.DoesNotContain(_factory.Logs.Lines, line => line.Contains(secret));
    }

    private async Task<(HttpContext Context, JsonElement Body)> HandleAsync(Exception exception)
    {
        using var scope = _factory.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        var handler = ActivatorUtilities.CreateInstance<GlobalExceptionHandler>(scope.ServiceProvider, NullLogger<GlobalExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        context.Response.Body.Position = 0;
        return (context, await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body));
    }
}
