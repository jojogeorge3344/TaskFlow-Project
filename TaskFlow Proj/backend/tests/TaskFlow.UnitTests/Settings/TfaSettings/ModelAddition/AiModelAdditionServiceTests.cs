using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Integrations.Ai.KeyProtection;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Integrations.Ai.Registry;
using AiModel = TaskFlow.Api.Models.Entities.AiModel;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Services.Assistant.ModelGateway;
using TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest;
using TaskFlow.Api.Services.Settings.TfaSettings.ModelAddition;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.UnitTests.Settings.TfaSettings.ModelAddition;

/// <summary>The Add AI model page: test with the typed values, and save and activate only when the test passes.</summary>
public class AiModelAdditionServiceTests : IAsyncLifetime
{
    private const string GeminiOk = """{"candidates":[{"content":{"parts":[{"text":"OK"}]}}]}""";
    private const string OllamaOk = """{"message":{"role":"assistant","content":"OK"}}""";

    private readonly TaskTrackingServiceHarness _harness = TaskTrackingServiceHarness.Create();
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly FakeHttpHandler _http = new();

    private IReadOnlyDictionary<AiApiType, int> _ids = new Dictionary<AiApiType, int>();

    public async Task InitializeAsync() => _ids = await AiTestProviders.SeedBuiltInsAsync(_harness.Db);

    private int Id(AiApiType apiType) => _ids[apiType];

    public async Task DisposeAsync() => await _harness.DisposeAsync();

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

    /// <summary>OpenAI saved and in use with its own key; Gemini has a saved key but is switched off.</summary>
    private async Task SaveStartingSetupAsync() =>
        await Config().SaveAsync(1, new UpdateAiProvidersRequest(
        [
            new UpdateAiProviderRequest(Id(AiApiType.OpenAI), true, "gpt-4o-mini", null, "sk-openai-key-1111", null),
            new UpdateAiProviderRequest(Id(AiApiType.Gemini), false, "gemini-2.0-flash", null, "AIza-saved-gemini-2222", null),
            new UpdateAiProviderRequest(Id(AiApiType.Ollama), false, "llama3.2:3b", null, null, null)
        ]));

    [Fact]
    public async Task APassingTestAddsTheModelSavesTheKeyAndMakesItTheOneInUse()
    {
        await SaveStartingSetupAsync();
        _http.Reply(HttpStatusCode.OK, GeminiOk);

        var result = await Service().AddAndActivateAsync(1, new AddAiModelRequest(" gemini-2.5-flash-lite ", "AIza-new-gemini-9876", null, ProviderId: Id(AiApiType.Gemini)));

        Assert.True(result.Ok);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<AiProviderConfigDto>>(result.Providers);
        var gemini = providers.Single(provider => provider.ApiType == AiApiType.Gemini);
        Assert.True(gemini.IsEnabled);
        Assert.True(gemini.IsConfigured);
        Assert.Equal("gemini-2.5-flash-lite", gemini.ModelName);
        Assert.Contains("gemini-2.5-flash-lite", gemini.Models);
        Assert.Equal("9876", gemini.KeyLast4);
        Assert.Equal("AIza-new-gemini-9876", _http.Sent.Single().Request.Headers.GetValues("x-goog-api-key").Single());

        // The others are switched off, and keep their keys and models.
        var openAi = providers.Single(provider => provider.ApiType == AiApiType.OpenAI);
        Assert.False(openAi.IsEnabled);
        Assert.True(openAi.KeySet);
        Assert.Equal("gpt-4o-mini", openAi.ModelName);
        Assert.False(providers.Single(provider => provider.ApiType == AiApiType.Ollama).IsEnabled);
    }

    [Fact]
    public async Task AFailedTestSavesNothing()
    {
        await SaveStartingSetupAsync();
        var before = await SnapshotAsync();
        _http.Reply(HttpStatusCode.BadRequest, """{"error":{"status":"INVALID_ARGUMENT","message":"API key not valid"}}""");

        var result = await Service().AddAndActivateAsync(1, new AddAiModelRequest("gemini-2.5-flash-lite", "AIza-wrong-key", null, ProviderId: Id(AiApiType.Gemini)));

        Assert.False(result.Ok);
        Assert.NotNull(result.FailureKind);
        Assert.Null(result.Providers);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task AModelAlreadyInTheListIsNotAddedTwice()
    {
        await SaveStartingSetupAsync();
        _http.Reply(HttpStatusCode.OK, GeminiOk);

        var result = await Service().AddAndActivateAsync(1, new AddAiModelRequest("gemini-2.0-flash", null, null, ProviderId: Id(AiApiType.Gemini)));

        Assert.True(result.Ok);
        Assert.Single(await _harness.Db.AiModels.Where(model => model.ProviderId == Id(AiApiType.Gemini) && model.Name == "gemini-2.0-flash").ToListAsync());
        Assert.Equal("gemini-2.0-flash", result.Providers!.Single(provider => provider.ApiType == AiApiType.Gemini).ModelName);
    }

    [Fact]
    public async Task AnEmptyKeyUsesAndKeepsTheSavedKey()
    {
        await SaveStartingSetupAsync();
        _http.Reply(HttpStatusCode.OK, GeminiOk);

        var result = await Service().AddAndActivateAsync(1, new AddAiModelRequest("gemini-2.5-pro", "", null, ProviderId: Id(AiApiType.Gemini)));

        Assert.True(result.Ok);
        Assert.Equal("AIza-saved-gemini-2222", _http.Sent.Single().Request.Headers.GetValues("x-goog-api-key").Single());
        Assert.Equal("2222", result.Providers!.Single(provider => provider.ApiType == AiApiType.Gemini).KeyLast4);
    }

    [Fact]
    public async Task ACloudProviderWithNoKeyIsRefusedWithoutSendingAnything()
    {
        var failure = await Assert.ThrowsAsync<RequestValidationException>(() =>
            Service().AddAndActivateAsync(1, new AddAiModelRequest("gpt-4.1-mini", null, null, ProviderId: Id(AiApiType.OpenAI))));

        Assert.Contains("API key", failure.Message);
        Assert.Empty(_http.Sent);
    }

    [Fact]
    public async Task AKeySentForOllamaIsRefused()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            Service().AddAndActivateAsync(1, new AddAiModelRequest("phi3:mini", "some-key", null, ProviderId: Id(AiApiType.Ollama))));

        Assert.Empty(_http.Sent);
    }

    [Fact]
    public async Task OllamaWithoutAnAddressUsesTheDefaultAndSavesIt()
    {
        _http.Reply(HttpStatusCode.OK, OllamaOk);

        var result = await Service().AddAndActivateAsync(1, new AddAiModelRequest("phi3:mini", null, null, ProviderId: Id(AiApiType.Ollama)));

        Assert.True(result.Ok);
        Assert.StartsWith("http://localhost:11434/", _http.Sent.Single().Request.RequestUri!.ToString());
        var ollama = result.Providers!.Single(provider => provider.ApiType == AiApiType.Ollama);
        Assert.True(ollama.IsEnabled);
        Assert.Equal("phi3:mini", ollama.ModelName);
        Assert.Equal("http://localhost:11434", ollama.BaseUrl);
    }

    [Theory]
    [InlineData("Gemini_TF_AI")]
    [InlineData("Gemini-3.8-Flash")]
    public async Task AGeminiNameThatIsNotAModelIdIsRefusedWithoutSendingAnything(string name)
    {
        await SaveStartingSetupAsync();
        var before = await SnapshotAsync();

        var failure = await Assert.ThrowsAsync<RequestValidationException>(() =>
            Service().AddAndActivateAsync(1, new AddAiModelRequest(name, "AIza-new-gemini-9876", null, ProviderId: Id(AiApiType.Gemini))));

        Assert.Contains("gemini-3.8-flash", failure.Message);
        Assert.Contains("Google AI Studio", failure.Message);
        Assert.Empty(_http.Sent);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task AGeminiNameCopiedWithTheModelsPrefixIsSavedWithoutIt()
    {
        await SaveStartingSetupAsync();
        _http.Reply(HttpStatusCode.OK, GeminiOk);

        var result = await Service().AddAndActivateAsync(1, new AddAiModelRequest("models/gemini-3.8-flash", null, null, ProviderId: Id(AiApiType.Gemini)));

        Assert.True(result.Ok);
        Assert.EndsWith("/v1beta/models/gemini-3.8-flash:generateContent", _http.Sent.Single().Request.RequestUri!.AbsolutePath);
        Assert.Equal("gemini-3.8-flash", result.Providers!.Single(provider => provider.ApiType == AiApiType.Gemini).ModelName);
        Assert.Equal(0, await _harness.Db.AiModels.CountAsync(model => model.Name.StartsWith("models/")));
    }

    /// <summary>The starting setup plus two Gemini models an administrator added, and the built-in gemini-3.8-flash.</summary>
    private async Task SaveSetupWithAddedGeminiModelsAsync()
    {
        await SaveStartingSetupAsync();
        _harness.Db.AiModels.AddRange(
            new AiModel { ProviderId = Id(AiApiType.Gemini), Name = "gemini-2.0-flash", IsBuiltIn = false },
            new AiModel { ProviderId = Id(AiApiType.Gemini), Name = "gemini-2.5-pro", IsBuiltIn = false },
            new AiModel { ProviderId = Id(AiApiType.Gemini), Name = "gemini-3.8-flash", IsBuiltIn = true });
        await _harness.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task EditingWithAPassingTestRenamesTheModelAndKeepsTheModelInUse()
    {
        await SaveSetupWithAddedGeminiModelsAsync();
        _http.Reply(HttpStatusCode.OK, GeminiOk);

        var result = await Service().EditAsync(1, Id(AiApiType.Gemini), "gemini-2.0-flash", new AddAiModelRequest("gemini-2.5-flash", null, null));

        Assert.True(result.Ok);
        Assert.EndsWith("/models/gemini-2.5-flash:generateContent", _http.Sent.Single().Request.RequestUri!.AbsolutePath);
        var gemini = result.Providers!.Single(provider => provider.ApiType == AiApiType.Gemini);
        Assert.Contains("gemini-2.5-flash", gemini.Models);
        Assert.DoesNotContain("gemini-2.0-flash", gemini.Models);

        // Gemini was switched off with gemini-2.0-flash saved; the saved name follows the rename, and OpenAI stays in use.
        Assert.Equal("gemini-2.5-flash", gemini.ModelName);
        Assert.False(gemini.IsEnabled);
        Assert.True(result.Providers!.Single(provider => provider.ApiType == AiApiType.OpenAI).IsEnabled);
    }

    [Fact]
    public async Task EditingWithANewKeyReplacesTheProviderKey()
    {
        await SaveSetupWithAddedGeminiModelsAsync();
        _http.Reply(HttpStatusCode.OK, GeminiOk);

        var result = await Service().EditAsync(1, Id(AiApiType.Gemini), "gemini-2.5-pro", new AddAiModelRequest("gemini-2.5-pro", "AIza-new-gemini-7777", null));

        Assert.True(result.Ok);
        Assert.Equal("AIza-new-gemini-7777", _http.Sent.Single().Request.Headers.GetValues("x-goog-api-key").Single());
        Assert.Equal("7777", result.Providers!.Single(provider => provider.ApiType == AiApiType.Gemini).KeyLast4);
    }

    [Fact]
    public async Task AFailedEditTestSavesNothing()
    {
        await SaveSetupWithAddedGeminiModelsAsync();
        var before = await SnapshotAsync();
        _http.Reply(HttpStatusCode.NotFound, """{"error":{"status":"NOT_FOUND","message":"not found"}}""");

        var result = await Service().EditAsync(1, Id(AiApiType.Gemini), "gemini-2.5-pro", new AddAiModelRequest("gemini-9.9-pro", "AIza-new-gemini-7777", null));

        Assert.False(result.Ok);
        Assert.Equal(ProviderFailureKind.ModelUnavailable, result.FailureKind);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task ABuiltInTheModelInUseOrADuplicateNameIsRefusedWithoutCallingTheProvider()
    {
        await SaveSetupWithAddedGeminiModelsAsync();
        await Config().UseModelAsync(1, Id(AiApiType.Gemini), "gemini-2.5-pro");

        var builtIn = await Assert.ThrowsAsync<RequestValidationException>(() =>
            Service().EditAsync(1, Id(AiApiType.Gemini), "gemini-3.8-flash", new AddAiModelRequest("gemini-3.9-flash", null, null)));
        var inUse = await Assert.ThrowsAsync<RequestValidationException>(() =>
            Service().EditAsync(1, Id(AiApiType.Gemini), "gemini-2.5-pro", new AddAiModelRequest("gemini-2.5-flash", null, null)));
        await Config().UseModelAsync(1, Id(AiApiType.OpenAI), "gpt-4o-mini");
        var duplicate = await Assert.ThrowsAsync<RequestValidationException>(() =>
            Service().EditAsync(1, Id(AiApiType.Gemini), "gemini-2.5-pro", new AddAiModelRequest("gemini-2.0-flash", null, null)));

        Assert.Equal("A built-in model cannot be changed or removed.", builtIn.Message);
        Assert.Equal("gemini-2.5-pro is in use by TFA. Switch TFA to another model first, then edit it.", inUse.Message);
        Assert.Equal("That model is already in the list.", duplicate.Message);
        Assert.Empty(_http.Sent);
    }

    [Fact]
    public async Task ClearingTheKeyRemovesItWithoutCallingTheProvider()
    {
        await SaveSetupWithAddedGeminiModelsAsync();

        var result = await Service().EditAsync(1, Id(AiApiType.Gemini), "gemini-2.5-pro", new AddAiModelRequest("gemini-2.5-pro", null, null, ClearKey: true));

        Assert.True(result.Ok);
        var gemini = result.Providers!.Single(provider => provider.ApiType == AiApiType.Gemini);
        Assert.False(gemini.KeySet);
        Assert.Null(gemini.KeyLast4);
        Assert.Empty(_http.Sent);
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            Service().EditAsync(1, Id(AiApiType.Gemini), "gemini-2.5-pro", new AddAiModelRequest("gemini-2.6-pro", null, null, ClearKey: true)));
    }

    [Fact]
    public async Task TestingAListedModelUsesThatModelWithTheSavedKeyAndChangesNothing()
    {
        await SaveSetupWithAddedGeminiModelsAsync();
        var before = await SnapshotAsync();
        _http.Reply(HttpStatusCode.OK, GeminiOk);

        var result = await Service().TestSavedModelAsync(Id(AiApiType.Gemini), "gemini-2.5-pro");

        Assert.True(result.Ok);
        var sent = _http.Sent.Single().Request;
        Assert.EndsWith("/models/gemini-2.5-pro:generateContent", sent.RequestUri!.AbsolutePath);
        Assert.Equal("AIza-saved-gemini-2222", sent.Headers.GetValues("x-goog-api-key").Single());
        Assert.Equal(before, await SnapshotAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => Service().TestSavedModelAsync(Id(AiApiType.Gemini), "never-added"));
    }

    private async Task<string> SnapshotAsync()
    {
        _harness.Db.ChangeTracker.Clear();
        var rows = await _harness.Db.AiProviderConfigs.AsNoTracking().OrderBy(row => row.ApiType).ToListAsync();
        var models = await _harness.Db.AiModels.AsNoTracking().OrderBy(model => model.ProviderId).ThenBy(model => model.Name).ToListAsync();
        return string.Join("|", rows.Select(row => $"{row.ApiType}:{row.IsEnabled}:{row.ModelName}:{row.BaseUrl}:{row.EncryptedApiKey}:{row.KeyLast4}"))
            + "#" + string.Join(",", models.Select(model => $"{model.ProviderId}/{model.Name}"));
    }
}
