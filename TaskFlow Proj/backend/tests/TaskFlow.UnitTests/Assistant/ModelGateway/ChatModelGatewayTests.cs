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

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private AiProviderConfigService Config() => new(new AiProviderConfigRepository(_harness.Db), new AiModelRepository(_harness.Db), new UnitOfWork(_harness.Db), new AiKeyProtector(_dataProtection), TimeProvider.System);

    private ChatModelGateway Gateway(FakeHttpHandler handler) =>
        new(Config(), new ChatModelProviderRegistry([new OpenAiProvider(new HttpClient(handler))]), _logger);

    private static ChatModelRequest Request() =>
        new("You are TFA.", [new ChatMessage(AssistantMessageRole.User, Prompt)], [], "ignored");

    private Task SaveOpenAi(string key = Key, string model = "gpt-4o-mini") =>
        Config().SaveAsync(1, new UpdateAiProvidersRequest(
            [new UpdateAiProviderRequest(AiProviderKind.OpenAI, true, model, null, key, null)]));

    [Fact]
    public async Task ANotConfiguredProviderSendsNothing()
    {
        var handler = new FakeHttpHandler();

        await Assert.ThrowsAsync<ProviderNotConfiguredException>(() => Gateway(handler).CompleteAsync(AiProviderKind.OpenAI, Request()));

        Assert.Empty(handler.Sent);
    }

    [Fact]
    public async Task AFailingCallLogsNeitherTheKeyNorTheRequestText()
    {
        await SaveOpenAi();
        var handler = new FakeHttpHandler().Reply(
            HttpStatusCode.Unauthorized,
            "{\"error\":{\"code\":\"invalid_api_key\",\"message\":\"Incorrect API key provided: " + Key + ". You asked: " + Prompt + "\"}}");

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => Gateway(handler).CompleteAsync(AiProviderKind.OpenAI, Request()));

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

        await gateway.CompleteAsync(AiProviderKind.OpenAI, Request());
        await SaveOpenAi("sk-second-key-2222");
        await gateway.CompleteAsync(AiProviderKind.OpenAI, Request());

        Assert.Equal("sk-first-key-1111", handler.Sent[0].Request.Headers.Authorization!.Parameter);
        Assert.Equal("sk-second-key-2222", handler.Sent[1].Request.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task TheConfiguredModelNameIsUsed()
    {
        await SaveOpenAi(model: "gpt-4o");
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}""");

        await Gateway(handler).CompleteAsync(AiProviderKind.OpenAI, Request());

        Assert.Contains("\"model\":\"gpt-4o\"", handler.Sent[0].Body);
    }
}
