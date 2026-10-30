using System.Net;
using Microsoft.AspNetCore.DataProtection;
using TaskFlow.Application.Assistant;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Infrastructure.Assistant;

namespace TaskFlow.UnitTests;

public class ProviderFailureHandlingTests
{
    [Theory]
    [InlineData(AiProviderKind.Gemini, ProviderFailureKind.RateLimited, "Google Gemini", "limit")]
    [InlineData(AiProviderKind.OpenAI, ProviderFailureKind.InvalidKey, "OpenAI GPT", "API key")]
    [InlineData(AiProviderKind.Gemini, ProviderFailureKind.Timeout, "Google Gemini", "too long")]
    [InlineData(AiProviderKind.Ollama, ProviderFailureKind.Unreachable, "Ollama", "not running")]
    [InlineData(AiProviderKind.OpenAI, ProviderFailureKind.Unreachable, "OpenAI GPT", "cannot be reached")]
    [InlineData(AiProviderKind.Ollama, ProviderFailureKind.ModelUnavailable, "Ollama", "install")]
    [InlineData(AiProviderKind.Gemini, ProviderFailureKind.ModelUnavailable, "Google Gemini", "model name")]
    [InlineData(AiProviderKind.OpenAI, ProviderFailureKind.Unexpected, "OpenAI GPT", "unexpected")]
    public void EachKindHasItsOwnMessageNamingTheProvider(AiProviderKind provider, ProviderFailureKind kind, string name, string phrase)
    {
        var message = ProviderFailureMessages.For(provider, kind);

        Assert.Contains(name, message);
        Assert.Contains(phrase, message);
    }

    [Fact]
    public void EveryKindSuggestsAnotherModel()
    {
        foreach (var kind in Enum.GetValues<ProviderFailureKind>())
        {
            Assert.Contains("another model", ProviderFailureMessages.For(AiProviderKind.Gemini, kind));
        }
    }

    [Fact]
    public void RateLimitMessageUsesTheRetryTimeWhenKnown()
    {
        Assert.Contains("42 seconds", ProviderFailureMessages.For(AiProviderKind.Gemini, ProviderFailureKind.RateLimited, TimeSpan.FromSeconds(42)));
        Assert.Contains("about a minute", ProviderFailureMessages.For(AiProviderKind.Gemini, ProviderFailureKind.RateLimited));
        Assert.Contains("3 minutes", ProviderFailureMessages.For(AiProviderKind.Gemini, ProviderFailureKind.RateLimited, TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public void MessagesNeverContainRawProviderText()
    {
        foreach (var provider in Enum.GetValues<AiProviderKind>())
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
        var config = new AiProviderConfigService(harness.Db, protector, TimeProvider.System);
        await config.SaveAsync(1, new UpdateAiProvidersRequest(
        [
            new UpdateAiProviderRequest(AiProviderKind.Ollama, true, "llama3.1:8b", "http://localhost:11434", null, null),
            new UpdateAiProviderRequest(AiProviderKind.Gemini, true, "gemini-2.0-flash", null, "AIzaSyExampleKey1234", null)
        ]));

        var ollamaHandler = new FakeHttpHandler().Throw(new HttpRequestException("refused"));
        var geminiHandler = new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"hi"}]}}]}""");
        var registry = new ChatModelProviderRegistry(
            [new OllamaProvider(new HttpClient(ollamaHandler)), new GeminiProvider(new HttpClient(geminiHandler))]);
        var gateway = new ChatModelGateway(config, registry, new CapturingLogger<ChatModelGateway>());
        var request = new ChatModelRequest("sys", [new ChatMessage(AssistantMessageRole.User, "hello")], [], "x");

        await Assert.ThrowsAsync<ProviderFailureException>(() => gateway.CompleteAsync(AiProviderKind.Ollama, request));

        Assert.Single(ollamaHandler.Sent);
        Assert.Empty(geminiHandler.Sent);

        // Choosing Gemini explicitly is what sends the request there.
        var result = await gateway.CompleteAsync(AiProviderKind.Gemini, request);
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
