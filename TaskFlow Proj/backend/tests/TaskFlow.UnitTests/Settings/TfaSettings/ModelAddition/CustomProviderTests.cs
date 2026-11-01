using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Integrations.Ai.KeyProtection;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Integrations.Ai.Registry;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence.Seeders;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Services.Assistant.ModelGateway;
using TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest;
using TaskFlow.Api.Services.Settings.TfaSettings.ModelAddition;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.UnitTests.Settings.TfaSettings.ModelAddition;

/// <summary>Custom providers: created from Add AI model only when the test passes, removed when their last model goes,
/// and models moved between providers from the Edit screen.</summary>
public class CustomProviderTests : IAsyncLifetime
{
    private const string OpenAiOk = """{"choices":[{"message":{"role":"assistant","content":"OK"}}]}""";
    private const string GeminiOk = """{"candidates":[{"content":{"parts":[{"text":"OK"}]}}]}""";

    private readonly TaskTrackingServiceHarness _harness = TaskTrackingServiceHarness.Create();
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly FakeHttpHandler _http = new();
    private IReadOnlyDictionary<AiApiType, int> _ids = new Dictionary<AiApiType, int>();

    /// <summary>The built-in providers with their built-in models (llama3.2:3b and so on), all switched off.</summary>
    public async Task InitializeAsync()
    {
        _ids = await AiTestProviders.SeedBuiltInsAsync(_harness.Db);
        await new AiProviderSeeder(_harness.Db, TimeProvider.System).SeedAsync();
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private int Id(AiApiType apiType) => _ids[apiType];

    private AiProviderConfigService Config() =>
        new(new AiProviderConfigRepository(_harness.Db), new AiModelRepository(_harness.Db), new UnitOfWork(_harness.Db), new AiKeyProtector(_protection), TimeProvider.System);

    private AiModelAdditionService Service()
    {
        var client = new HttpClient(_http);
        var registry = new ChatModelProviderRegistry([new OllamaProvider(client), new GeminiProvider(client), new OpenAiProvider(client)]);
        var config = Config();
        var gateway = new ChatModelGateway(config, registry, new CapturingLogger<ChatModelGateway>());
        return new AiModelAdditionService(config, new AiProviderConnectionTester(gateway, registry, new CapturingLogger<AiProviderConnectionTester>()));
    }

    private static NewAiProviderRequest Groq(string name = "Groq") =>
        new(name, AiApiType.OpenAI, "https://api.groq.com/openai/v1", "Cloud");

    private Task<AddAiModelResultDto> AddGroqAsync(string model = "llama-3.3-70b-versatile", string? key = "gsk-groq-key-4321") =>
        Service().AddAndActivateAsync(1, new AddAiModelRequest(model, key, null, NewProvider: Groq()));

    private async Task<int> GroqIdAsync() => (await _harness.Db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.Name == "Groq")).Id;

    [Fact]
    public async Task APassingTestCreatesTheProviderAndMakesItsModelTheOneInUse()
    {
        _http.Reply(HttpStatusCode.OK, OpenAiOk);

        var result = await AddGroqAsync();

        Assert.True(result.Ok);
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", _http.Sent.Single().Request.RequestUri!.ToString());
        var groq = result.Providers!.Single(provider => provider.DisplayName == "Groq");
        Assert.False(groq.IsBuiltIn);
        Assert.Equal(AiApiType.OpenAI, groq.ApiType);
        Assert.Equal("Cloud", groq.Locality);
        Assert.True(groq.IsEnabled);
        Assert.True(groq.IsConfigured);
        Assert.Equal("4321", groq.KeyLast4);
        Assert.Equal(["llama-3.3-70b-versatile"], groq.Models);
        Assert.Equal(["llama-3.3-70b-versatile"], groq.CustomModels);
        Assert.All(result.Providers!.Where(provider => provider.IsBuiltIn), provider => Assert.False(provider.IsEnabled));
        Assert.Equal("Groq", result.Providers!.Last().DisplayName);
    }

    [Fact]
    public async Task AFailedTestCreatesNoProviderAndNoModel()
    {
        _http.Reply(HttpStatusCode.Unauthorized, """{"error":{"code":"invalid_api_key","message":"bad key"}}""");

        var result = await AddGroqAsync();

        Assert.False(result.Ok);
        Assert.Equal(ProviderFailureKind.InvalidKey, result.FailureKind);
        Assert.StartsWith("Groq did not accept the API key.", result.Message);
        Assert.False(await _harness.Db.AiProviderConfigs.AnyAsync(row => row.Name == "Groq"));
        Assert.False(await _harness.Db.AiModels.AnyAsync(model => model.Name == "llama-3.3-70b-versatile"));
    }

    [Fact]
    public async Task ALocalServerNeedsNoKeyAndSendsNoAuthorizationHeader()
    {
        _http.Reply(HttpStatusCode.OK, OpenAiOk);

        var result = await Service().AddAndActivateAsync(1, new AddAiModelRequest(
            "qwen2.5-7b-instruct", null, null, NewProvider: new NewAiProviderRequest("LM Studio", AiApiType.OpenAI, "http://localhost:1234/v1", "Local")));

        Assert.True(result.Ok);
        Assert.Null(_http.Sent.Single().Request.Headers.Authorization);
        var lmStudio = result.Providers!.Single(provider => provider.DisplayName == "LM Studio");
        Assert.Equal("Local", lmStudio.Locality);
        Assert.False(lmStudio.KeySet);
        Assert.False(lmStudio.RequiresKey);
        Assert.True(lmStudio.IsConfigured);
    }

    [Theory]
    [InlineData("Groq", AiApiType.OpenAI, "https://api.groq.com/openai/v1", "A provider with that name already exists.")]
    [InlineData("GROQ", AiApiType.OpenAI, "https://api.groq.com/openai/v1", "A provider with that name already exists.")]
    [InlineData("google gemini", AiApiType.OpenAI, "https://example.com/v1", "A provider with that name already exists.")]
    [InlineData("X", AiApiType.OpenAI, "https://example.com/v1", "The provider name must be 2 to 40 characters.")]
    [InlineData("My Gemini", AiApiType.Gemini, "https://example.com/v1", "A custom provider can use the OpenAI-compatible or Ollama API.")]
    [InlineData("Metadata", AiApiType.OpenAI, "http://169.254.169.254/v1", "The server address must be a valid http or https URL, and not a link-local or cloud metadata address.")]
    [InlineData("No address", AiApiType.OpenAI, "", "Enter the server address.")]
    public async Task AnInvalidNewProviderIsRefusedWithoutSendingAnything(string name, AiApiType apiType, string baseUrl, string message)
    {
        _http.Reply(HttpStatusCode.OK, OpenAiOk);
        await AddGroqAsync();
        _http.Sent.Clear();

        var failure = await Assert.ThrowsAsync<RequestValidationException>(() => Service().AddAndActivateAsync(1,
            new AddAiModelRequest("some-model", null, null, NewProvider: new NewAiProviderRequest(name, apiType, baseUrl, "Cloud"))));

        Assert.Equal(message, failure.Message);
        Assert.Empty(_http.Sent);
    }

    [Fact]
    public async Task ModelIdsWithSlashesAndUppercaseAreAcceptedForACustomProvider()
    {
        _http.Reply(HttpStatusCode.OK, OpenAiOk);

        var result = await AddGroqAsync("meta-llama/Llama-3.3-70B-Instruct");

        Assert.True(result.Ok);
        Assert.Contains("meta-llama/Llama-3.3-70B-Instruct", result.Providers!.Single(provider => provider.DisplayName == "Groq").Models);
    }

    [Fact]
    public async Task DeletingTheLastModelOfACustomProviderRemovesTheProviderAndItsKey()
    {
        _http.Reply(HttpStatusCode.OK, OpenAiOk);
        await AddGroqAsync();
        var groqId = await GroqIdAsync();
        await Config().UseModelAsync(1, Id(AiApiType.Ollama), "llama3.2:3b");

        var providers = await Config().RemoveModelAsync(groqId, "llama-3.3-70b-versatile");

        Assert.DoesNotContain(providers, provider => provider.DisplayName == "Groq");
        Assert.False(await _harness.Db.AiProviderConfigs.AnyAsync(row => row.Id == groqId));
        Assert.Equal(3, await _harness.Db.AiProviderConfigs.CountAsync(row => row.IsBuiltIn));
    }

    [Fact]
    public async Task ABuiltInProviderIsNeverRemovedWhenItsLastAddedModelGoes()
    {
        _http.Reply(HttpStatusCode.OK, GeminiOk);
        await Service().AddAndActivateAsync(1, new AddAiModelRequest("gemini-2.5-pro", "AIza-gemini-key", null, ProviderId: Id(AiApiType.Gemini)));
        await Config().UseModelAsync(1, Id(AiApiType.Ollama), "llama3.2:3b");

        await Config().RemoveModelAsync(Id(AiApiType.Gemini), "gemini-2.5-pro");

        var gemini = await _harness.Db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.Id == Id(AiApiType.Gemini));
        Assert.Equal("gemini-3.8-flash", gemini.ModelName);
        Assert.NotNull(gemini.EncryptedApiKey);
    }

    [Fact]
    public async Task MovingAModelToAnotherExistingProviderTestsItThereAndMovesIt()
    {
        _http.Reply(HttpStatusCode.OK, OpenAiOk);
        await AddGroqAsync();
        _http.Reply(HttpStatusCode.OK, GeminiOk);
        await Service().AddAndActivateAsync(1, new AddAiModelRequest("gemini-2.0-flash", "AIza-gemini-key", null, ProviderId: Id(AiApiType.Gemini)));
        await Config().UseModelAsync(1, await GroqIdAsync(), "llama-3.3-70b-versatile");
        _http.Sent.Clear();
        _http.Reply(HttpStatusCode.OK, OpenAiOk);

        var result = await Service().EditAsync(1, Id(AiApiType.Gemini), "gemini-2.0-flash",
            new AddAiModelRequest("google/gemini-2.0-flash-001", null, null, ProviderId: await GroqIdAsync()));

        Assert.True(result.Ok);
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", _http.Sent.Single().Request.RequestUri!.ToString());
        Assert.Contains("google/gemini-2.0-flash-001", result.Providers!.Single(provider => provider.DisplayName == "Groq").Models);
        Assert.DoesNotContain("gemini-2.0-flash", result.Providers!.Single(provider => provider.DisplayName == "Google Gemini").Models);
        Assert.Equal("llama-3.3-70b-versatile", result.Providers!.Single(provider => provider.IsEnabled).ModelName);
    }

    [Fact]
    public async Task MovingAModelToANewProviderCreatesItAndAnEmptiedCustomProviderGoes()
    {
        _http.Reply(HttpStatusCode.OK, OpenAiOk);
        await AddGroqAsync();
        await Config().UseModelAsync(1, Id(AiApiType.Ollama), "llama3.2:3b");
        _http.Reply(HttpStatusCode.OK, OpenAiOk);

        var result = await Service().EditAsync(1, await GroqIdAsync(), "llama-3.3-70b-versatile",
            new AddAiModelRequest("meta-llama/llama-3.3-70b-instruct", "or-key-7777", null,
                NewProvider: new NewAiProviderRequest("OpenRouter", AiApiType.OpenAI, "https://openrouter.ai/api/v1", "Cloud")));

        Assert.True(result.Ok);
        Assert.DoesNotContain(result.Providers!, provider => provider.DisplayName == "Groq");
        var openRouter = result.Providers!.Single(provider => provider.DisplayName == "OpenRouter");
        Assert.Equal(["meta-llama/llama-3.3-70b-instruct"], openRouter.Models);
        Assert.Equal("7777", openRouter.KeyLast4);
        Assert.False(openRouter.IsEnabled);
    }

    [Fact]
    public async Task AMoveIsRefusedForADuplicateOnTheTargetAndAFailedTestLeavesTheModelWhereItWas()
    {
        _http.Reply(HttpStatusCode.OK, OpenAiOk);
        await AddGroqAsync();
        _http.Reply(HttpStatusCode.OK, OpenAiOk);
        await Service().AddAndActivateAsync(1, new AddAiModelRequest("mixtral-8x7b", null, null, ProviderId: await GroqIdAsync()));
        await Config().UseModelAsync(1, Id(AiApiType.Ollama), "llama3.2:3b");
        _http.Reply(HttpStatusCode.OK, """{"message":{"role":"assistant","content":"OK"}}""");
        await Service().AddAndActivateAsync(1, new AddAiModelRequest("mixtral-8x7b", null, null, ProviderId: Id(AiApiType.Ollama)));
        await Config().UseModelAsync(1, Id(AiApiType.Ollama), "llama3.2:3b");
        _http.Sent.Clear();

        var duplicate = await Assert.ThrowsAsync<RequestValidationException>(async () => await
            Service().EditAsync(1, await GroqIdAsync(), "mixtral-8x7b", new AddAiModelRequest("mixtral-8x7b", null, null, ProviderId: Id(AiApiType.Ollama))));
        Assert.Equal("That model is already in the list.", duplicate.Message);
        Assert.Empty(_http.Sent);

        _http.Reply(HttpStatusCode.NotFound, """{"error":"model 'qwen' not found"}""");
        var failed = await Service().EditAsync(1, await GroqIdAsync(), "mixtral-8x7b", new AddAiModelRequest("qwen", null, null, ProviderId: Id(AiApiType.Ollama)));

        Assert.False(failed.Ok);
        var groqId = await GroqIdAsync();
        Assert.True(await _harness.Db.AiModels.AnyAsync(model => model.ProviderId == groqId && model.Name == "mixtral-8x7b"));
        Assert.False(await _harness.Db.AiModels.AnyAsync(model => model.Name == "qwen"));
    }

    [Fact]
    public async Task TheModelInUseCannotBeMovedAndClearKeyCannotBeCombinedWithAMove()
    {
        _http.Reply(HttpStatusCode.OK, OpenAiOk);
        await AddGroqAsync();
        _http.Reply(HttpStatusCode.OK, OpenAiOk);
        await Service().AddAndActivateAsync(1, new AddAiModelRequest("mixtral-8x7b", null, null, ProviderId: await GroqIdAsync()));

        var inUse = await Assert.ThrowsAsync<RequestValidationException>(async () => await
            Service().EditAsync(1, await GroqIdAsync(), "mixtral-8x7b", new AddAiModelRequest("mixtral-8x7b", null, null, ProviderId: Id(AiApiType.Ollama))));
        Assert.Equal("mixtral-8x7b is in use by TFA. Switch TFA to another model first, then edit it.", inUse.Message);

        var clearAndMove = await Assert.ThrowsAsync<RequestValidationException>(async () => await
            Service().EditAsync(1, await GroqIdAsync(), "llama-3.3-70b-versatile",
                new AddAiModelRequest("llama-3.3-70b-versatile", null, null, ClearKey: true, ProviderId: Id(AiApiType.OpenAI))));
        Assert.Equal("Remove the key on its own, without other changes.", clearAndMove.Message);
    }
}
