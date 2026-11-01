using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.KeyProtection;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Integrations.Ai.Registry;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Services.Assistant.Chat;
using TaskFlow.Api.Services.Assistant.ModelGateway;
using TaskFlow.Api.Services.Assistant.RateLimits;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.UnitTests.Assistant.ModelGateway;

public class ProviderFailureHandlingTests
{
    [Theory]
    [InlineData(AiApiType.Gemini, ProviderFailureKind.RateLimited, "Google Gemini", "limit")]
    [InlineData(AiApiType.OpenAI, ProviderFailureKind.InvalidKey, "OpenAI GPT", "API key")]
    [InlineData(AiApiType.Gemini, ProviderFailureKind.Timeout, "Google Gemini", "too long")]
    [InlineData(AiApiType.Ollama, ProviderFailureKind.Unreachable, "Ollama", "not running")]
    [InlineData(AiApiType.OpenAI, ProviderFailureKind.Unreachable, "OpenAI GPT", "cannot be reached")]
    [InlineData(AiApiType.Ollama, ProviderFailureKind.ModelUnavailable, "Ollama", "install")]
    [InlineData(AiApiType.Gemini, ProviderFailureKind.ModelUnavailable, "Google Gemini", "model name")]
    [InlineData(AiApiType.OpenAI, ProviderFailureKind.Unexpected, "OpenAI GPT", "unexpected")]
    public void EachKindHasItsOwnMessageNamingTheProvider(AiApiType provider, ProviderFailureKind kind, string name, string phrase)
    {
        var message = ProviderFailureMessages.For(provider, kind);

        Assert.Contains(name, message);
        Assert.Contains(phrase, message);
    }

    [Fact]
    public void AMalformedModelNameSaysItIsNotAModelIdAndGivesAnExample() =>
        Assert.Equal(
            "Google Gemini did not accept the configured model name as a model ID. Ask an administrator to set a valid model ID, for example gemini-3.8-flash, or choose another model.",
            ProviderFailureMessages.For(AiApiType.Gemini, ProviderFailureKind.InvalidModelName));

    [Fact]
    public void EveryKindSuggestsAnotherModel()
    {
        foreach (var kind in Enum.GetValues<ProviderFailureKind>())
        {
            Assert.Contains("another model", ProviderFailureMessages.For(AiApiType.Gemini, kind));
        }
    }

    [Fact]
    public void RateLimitMessageUsesTheRetryTimeWhenKnown()
    {
        Assert.Contains("42 seconds", ProviderFailureMessages.For(AiApiType.Gemini, ProviderFailureKind.RateLimited, TimeSpan.FromSeconds(42)));
        Assert.Contains("about a minute", ProviderFailureMessages.For(AiApiType.Gemini, ProviderFailureKind.RateLimited));
        Assert.Contains("3 minutes", ProviderFailureMessages.For(AiApiType.Gemini, ProviderFailureKind.RateLimited, TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public void MessagesNeverContainRawProviderText()
    {
        foreach (var provider in Enum.GetValues<AiApiType>())
        foreach (var kind in Enum.GetValues<ProviderFailureKind>())
        {
            var message = ProviderFailureMessages.For(provider, kind);
            Assert.DoesNotContain("RESOURCE_EXHAUSTED", message);
            Assert.DoesNotContain("HTTP", message);
            Assert.DoesNotContain("429", message);
        }
    }

    [Fact]
    public async Task AFailureOnOneProviderNeverSendsToAnother()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var protector = new AiKeyProtector(new EphemeralDataProtectionProvider());
        var config = new AiProviderConfigService(new AiProviderConfigRepository(harness.Db), new AiModelRepository(harness.Db), new UnitOfWork(harness.Db), protector, TimeProvider.System);
        var ids = await AiTestProviders.SeedBuiltInsAsync(harness.Db);
        await config.SaveAsync(1, new UpdateAiProvidersRequest(
        [
            new UpdateAiProviderRequest(ids[AiApiType.Ollama], true, "llama3.1:8b", "http://localhost:11434", null, null),
            new UpdateAiProviderRequest(ids[AiApiType.Gemini], true, "gemini-2.0-flash", null, "AIzaSyExampleKey1234", null)
        ]));

        var ollamaHandler = new FakeHttpHandler().Throw(new HttpRequestException("refused"));
        var geminiHandler = new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"hi"}]}}]}""");
        var registry = new ChatModelProviderRegistry(
            [new OllamaProvider(new HttpClient(ollamaHandler)), new GeminiProvider(new HttpClient(geminiHandler))]);
        var gateway = new ChatModelGateway(config, registry, new CapturingLogger<ChatModelGateway>());
        var request = new ChatModelRequest("sys", [new ChatMessage(AssistantMessageRole.User, "hello")], [], "x");

        await Assert.ThrowsAsync<ProviderFailureException>(() => gateway.CompleteAsync(ids[AiApiType.Ollama], request));

        Assert.Single(ollamaHandler.Sent);
        Assert.Empty(geminiHandler.Sent);

        // Choosing Gemini explicitly is what sends the request there.
        var result = await gateway.CompleteAsync(ids[AiApiType.Gemini], request);
        Assert.Equal("hi", result.Text);
        Assert.Single(geminiHandler.Sent);
    }

    [Fact]
    public void TheTwentyFirstRequestInAMinuteIsRefusedWithARetryTime()
    {
        var clock = new FixedTimeProvider(new DateTime(2026, 10, 18, 9, 0, 0));
        var limiter = new AssistantRateLimiter(clock);

        for (var i = 0; i < AssistantRateLimiter.RequestsPerMinute; i++)
        {
            Assert.Null(limiter.TryAcquire(7));
            clock.Set(clock.GetUtcNow().UtcDateTime.AddSeconds(1));
        }

        var wait = limiter.TryAcquire(7);

        Assert.NotNull(wait);
        Assert.InRange(wait!.Value.TotalSeconds, 1, 60);
    }

    [Fact]
    public void TheLimitIsPerUserAndRecoversAfterAMinute()
    {
        var clock = new FixedTimeProvider(new DateTime(2026, 10, 18, 9, 0, 0));
        var limiter = new AssistantRateLimiter(clock);
        for (var i = 0; i < AssistantRateLimiter.RequestsPerMinute; i++)
        {
            limiter.TryAcquire(7);
        }

        Assert.NotNull(limiter.TryAcquire(7));
        Assert.Null(limiter.TryAcquire(8));

        clock.Set(clock.GetUtcNow().UtcDateTime.AddSeconds(61));
        Assert.Null(limiter.TryAcquire(7));
    }
}
