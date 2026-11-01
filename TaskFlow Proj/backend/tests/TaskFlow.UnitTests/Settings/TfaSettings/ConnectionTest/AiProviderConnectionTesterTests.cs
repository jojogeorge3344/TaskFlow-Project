using System.Net;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Integrations.Ai.Registry;
using TaskFlow.Api.Services.Assistant.ModelGateway;
using TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest;

namespace TaskFlow.UnitTests.Settings.TfaSettings.ConnectionTest;

/// <summary>The Add AI model page's test: values that are not saved go straight to the provider.</summary>
public class AiProviderConnectionTesterTests
{
    private const string Key = "AIzaSyCandidateKey9876";

    private static (AiProviderConnectionTester Tester, FakeHttpHandler Handler, CapturingLogger<AiProviderConnectionTester> Logger) Create(FakeHttpHandler handler)
    {
        var registry = new ChatModelProviderRegistry([new GeminiProvider(new HttpClient(handler)), new OpenAiProvider(new HttpClient(handler))]);
        var logger = new CapturingLogger<AiProviderConnectionTester>();
        return (new AiProviderConnectionTester(new UnusedGateway(), registry, logger), handler, logger);
    }

    [Fact]
    public async Task AWorkingCandidatePassesAndUsesTheTypedModelAndKey()
    {
        var (tester, handler, _) = Create(new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"OK"}]}}]}"""));

        var result = await tester.TestCandidateAsync(new AiProviderRuntime(AiProviderKind.Gemini, "gemini-2.5-flash-lite", null, Key));

        Assert.True(result.Ok);
        Assert.Null(result.FailureKind);
        var sent = Assert.Single(handler.Sent);
        Assert.Contains("gemini-2.5-flash-lite", sent.Request.RequestUri!.ToString());
        Assert.Equal(Key, sent.Request.Headers.GetValues("x-goog-api-key").Single());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"code":"invalid_api_key"}}""", ProviderFailureKind.InvalidKey)]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"code":"model_not_found"}}""", ProviderFailureKind.ModelUnavailable)]
    public async Task AProviderRefusalIsAFailedResultWithTheFixedMessage(HttpStatusCode status, string body, ProviderFailureKind expected)
    {
        var (tester, _, _) = Create(new FakeHttpHandler().Reply(status, body));

        var result = await tester.TestCandidateAsync(new AiProviderRuntime(AiProviderKind.OpenAI, "gpt-x", null, Key));

        Assert.False(result.Ok);
        Assert.Equal(expected, result.FailureKind);
        Assert.Contains("OpenAI GPT", result.Message);
    }

    [Fact]
    public async Task ATimeoutIsAFailedResult()
    {
        var (tester, _, _) = Create(new FakeHttpHandler().Throw(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")));

        var result = await tester.TestCandidateAsync(new AiProviderRuntime(AiProviderKind.OpenAI, "gpt-4o-mini", null, Key));

        Assert.False(result.Ok);
        Assert.Equal(ProviderFailureKind.Timeout, result.FailureKind);
    }

    [Fact]
    public async Task TheKeyIsNeverLogged()
    {
        var (tester, _, logger) = Create(new FakeHttpHandler().Reply(HttpStatusCode.Unauthorized, """{"error":{"message":"bad key AIzaSyCandidateKey9876"}}"""));

        await tester.TestCandidateAsync(new AiProviderRuntime(AiProviderKind.Gemini, "gemini-2.5-flash", null, Key));

        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, message => Assert.DoesNotContain(Key, message));
    }

    [Fact]
    public async Task AnUnclassifiedRefusalShowsTheProvidersStatusAndReason()
    {
        var (tester, _, logger) = Create(new FakeHttpHandler().Reply(HttpStatusCode.BadRequest,
            """{"error":{"code":400,"status":"FAILED_PRECONDITION","message":"User location is not supported for the API use."}}"""));

        var result = await tester.TestCandidateAsync(new AiProviderRuntime(AiProviderKind.Gemini, "gemini-3.8-flash", null, Key));

        Assert.False(result.Ok);
        Assert.Equal(ProviderFailureKind.Unexpected, result.FailureKind);
        Assert.StartsWith("Google Gemini returned an unexpected error.", result.Message);
        Assert.EndsWith("Google Gemini said: HTTP 400 - FAILED_PRECONDITION: User location is not supported for the API use.", result.Message);
        Assert.Contains(logger.Messages, message => message.Contains("User location is not supported"));
    }

    [Fact]
    public async Task ARetiredModelShowsWhichModelToUseInstead()
    {
        var (tester, _, _) = Create(new FakeHttpHandler().Reply(HttpStatusCode.NotFound,
            """{"error":{"code":404,"status":"NOT_FOUND","message":"This model models/gemini-2.0-flash is no longer available. Please update your code to use models/gemini-3.8-flash."}}"""));

        var result = await tester.TestCandidateAsync(new AiProviderRuntime(AiProviderKind.Gemini, "gemini-2.0-flash", null, Key));

        Assert.Equal(ProviderFailureKind.ModelUnavailable, result.FailureKind);
        Assert.Contains("not available on Google Gemini", result.Message);
        Assert.Contains("use models/gemini-3.8-flash", result.Message);
    }

    [Fact]
    public async Task AKeyLabelTypedAsTheModelIsReportedAsNotAModelIdWithGooglesReason()
    {
        var (tester, _, _) = Create(new FakeHttpHandler().Reply(HttpStatusCode.BadRequest,
            """{"error":{"code":400,"status":"INVALID_ARGUMENT","message":"* GenerateContentRequest.model: unexpected model name format\n"}}"""));

        var result = await tester.TestCandidateAsync(new AiProviderRuntime(AiProviderKind.Gemini, "Gemini_TF_AI", null, Key));

        Assert.False(result.Ok);
        Assert.Equal(ProviderFailureKind.InvalidModelName, result.FailureKind);
        Assert.StartsWith("Google Gemini did not accept the configured model name as a model ID.", result.Message);
        Assert.EndsWith("Google Gemini said: HTTP 400 - INVALID_ARGUMENT: * GenerateContentRequest.model: unexpected model name format", result.Message);
    }

    [Fact]
    public async Task TheSentKeyIsReplacedInTheShownReason()
    {
        var (tester, _, logger) = Create(new FakeHttpHandler().Reply(HttpStatusCode.BadRequest,
            $$$"""{"error":{"status":"FAILED_PRECONDITION","message":"Project for {{{Key}}} is suspended"}}"""));

        var result = await tester.TestCandidateAsync(new AiProviderRuntime(AiProviderKind.Gemini, "gemini-3.8-flash", null, Key));

        Assert.Contains("Project for [key] is suspended", result.Message);
        Assert.DoesNotContain(Key, result.Message);
        Assert.All(logger.Messages, message => Assert.DoesNotContain(Key, message));
    }

    [Fact]
    public async Task AKeyProblemKeepsTheFixedMessage()
    {
        var (tester, _, _) = Create(new FakeHttpHandler().Reply(HttpStatusCode.Forbidden,
            """{"error":{"status":"PERMISSION_DENIED","message":"denied"}}"""));

        var result = await tester.TestCandidateAsync(new AiProviderRuntime(AiProviderKind.Gemini, "gemini-3.8-flash", null, Key));

        Assert.Equal(ProviderFailureKind.InvalidKey, result.FailureKind);
        Assert.DoesNotContain("said:", result.Message);
    }

    private sealed class UnusedGateway : IChatModelGateway
    {
        public Task<ChatModelResult> CompleteAsync(AiProviderKind kind, ChatModelRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A candidate test must not use the saved settings.");
    }
}
