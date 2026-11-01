using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.KeyProtection;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Integrations.Ai.Registry;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Services.Assistant.ModelGateway;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.UnitTests.Assistant.ModelGateway;

public class ChatModelGatewayTests : IAsyncLifetime
{
    private const string Key = "sk-test-SECRETKEY-9999";
    private const string Prompt = "my confidential question about TF-102";

    private readonly TaskTrackingServiceHarness _harness = TaskTrackingServiceHarness.Create();
    private readonly EphemeralDataProtectionProvider _dataProtection = new();
    private readonly CapturingLogger<ChatModelGateway> _logger = new();

    private IReadOnlyDictionary<AiApiType, int> _ids = new Dictionary<AiApiType, int>();

    public async Task InitializeAsync() => _ids = await AiTestProviders.SeedBuiltInsAsync(_harness.Db);

    private int Id(AiApiType apiType) => _ids[apiType];

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private AiProviderConfigService Config() => new(new AiProviderConfigRepository(_harness.Db), new AiModelRepository(_harness.Db), new UnitOfWork(_harness.Db), new AiKeyProtector(_dataProtection), TimeProvider.System);

    private ChatModelGateway Gateway(FakeHttpHandler handler) =>
        new(Config(), new ChatModelProviderRegistry([new OpenAiProvider(new HttpClient(handler))]), _logger);

    private static ChatModelRequest Request() =>
        new("You are TFA.", [new ChatMessage(AssistantMessageRole.User, Prompt)], [], "ignored");

    private Task SaveOpenAi(string key = Key, string model = "gpt-4o-mini") =>
        Config().SaveAsync(1, new UpdateAiProvidersRequest(
            [new UpdateAiProviderRequest(Id(AiApiType.OpenAI), true, model, null, key, null)]));

    [Fact]
    public async Task ANotConfiguredProviderSendsNothing()
    {
        var handler = new FakeHttpHandler();

        await Assert.ThrowsAsync<ProviderNotConfiguredException>(() => Gateway(handler).CompleteAsync(Id(AiApiType.OpenAI), Request()));

        Assert.Empty(handler.Sent);
    }

    [Fact]
    public async Task AFailingCallLogsNeitherTheKeyNorTheRequestText()
    {
        await SaveOpenAi();
        var handler = new FakeHttpHandler().Reply(
            HttpStatusCode.Unauthorized,
            "{\"error\":{\"code\":\"invalid_api_key\",\"message\":\"Incorrect API key provided: " + Key + ". You asked: " + Prompt + "\"}}");

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => Gateway(handler).CompleteAsync(Id(AiApiType.OpenAI), Request()));

        Assert.Equal(ProviderFailureKind.InvalidKey, failure.Kind);
        var log = Assert.Single(_logger.Messages);
        Assert.Contains("OpenAI", log);
        Assert.Contains("InvalidKey", log);
        Assert.DoesNotContain("SECRETKEY", log);
        Assert.DoesNotContain("confidential", log);
        Assert.DoesNotContain("SECRETKEY", failure.ToString());
        Assert.DoesNotContain("confidential", failure.ToString());
    }

    [Fact]
    public async Task TheKeyIsReadFromConfigurationAtCallTime()
    {
        await SaveOpenAi("sk-first-key-1111");
        var handler = new FakeHttpHandler()
            .Reply(HttpStatusCode.OK, """{"choices":[{"message":{"content":"one"}}]}""")
            .Reply(HttpStatusCode.OK, """{"choices":[{"message":{"content":"two"}}]}""");
        var gateway = Gateway(handler);

        await gateway.CompleteAsync(Id(AiApiType.OpenAI), Request());
        await SaveOpenAi("sk-second-key-2222");
        await gateway.CompleteAsync(Id(AiApiType.OpenAI), Request());

        Assert.Equal("sk-first-key-1111", handler.Sent[0].Request.Headers.Authorization!.Parameter);
        Assert.Equal("sk-second-key-2222", handler.Sent[1].Request.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task TheConfiguredModelNameIsUsed()
    {
        await SaveOpenAi(model: "gpt-4o");
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}""");

        await Gateway(handler).CompleteAsync(Id(AiApiType.OpenAI), Request());

        Assert.Contains("\"model\":\"gpt-4o\"", handler.Sent[0].Body);
    }

    [Fact]
    public async Task AFailureOfACustomProviderNamesItAndGoesToItsAddress()
    {
        _harness.Db.AiProviderConfigs.Add(new TaskFlow.Api.Models.Entities.AiProviderConfig
        {
            Name = "Groq", ApiType = AiApiType.OpenAI, Locality = "Cloud", IsEnabled = true, ModelName = "llama-3.3-70b-versatile",
            BaseUrl = "https://api.groq.com/openai/v1", UpdatedAt = DateTime.UtcNow
        });
        await _harness.Db.SaveChangesAsync();
        var groqId = _harness.Db.AiProviderConfigs.Single(row => row.Name == "Groq").Id;
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.TooManyRequests, """{"error":{"code":"rate_limit_exceeded","message":"slow down"}}""");

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => Gateway(handler).CompleteAsync(groqId, Request()));

        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", handler.Sent.Single().Request.RequestUri!.ToString());
        Assert.Equal(groqId, failure.ProviderId);
        Assert.Equal("Groq", failure.ProviderName);
        Assert.StartsWith("Groq's free limit or rate limit has been reached.", TaskFlow.Api.Services.Assistant.Chat.ProviderFailureMessages.For(failure));
        Assert.Contains("Groq", Assert.Single(_logger.Messages));
    }
}
