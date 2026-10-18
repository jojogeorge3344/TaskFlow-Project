using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskFlow.Application.Assistant;
using TaskFlow.Application.Assistant.Validators;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Assistant;

namespace TaskFlow.UnitTests;

public class AiProviderConfigServiceTests : IAsyncLifetime
{
    private readonly TaskTrackingServiceHarness _harness = TaskTrackingServiceHarness.Create();
    private readonly EphemeralDataProtectionProvider _provider = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private AiProviderConfigService Service() =>
        new(_harness.Db, new AiKeyProtector(_provider), TimeProvider.System);

    private static UpdateAiProviderRequest Update(
        AiProviderKind kind, bool enabled = true, string? model = null, string? baseUrl = null, string? key = null, bool? clear = null) =>
        new(kind, enabled, model ?? AiProviderCatalog.DefaultModel(kind), baseUrl, key, clear);

    private Task<IReadOnlyList<AiProviderConfigDto>> Save(params UpdateAiProviderRequest[] updates) =>
        Service().SaveAsync(1, new UpdateAiProvidersRequest(updates));

    [Fact]
    public async Task FreshInstallationReportsEveryProviderAsNotConfigured()
    {
        var config = await Service().GetAdminConfigAsync();
        var available = await Service().GetAvailableAsync();

        Assert.Equal(3, config.Count);
        Assert.All(config, provider => Assert.False(provider.IsConfigured));
        Assert.Empty(available);
    }

    [Fact]
    public async Task CloudProviderEnabledWithoutAKeyIsNotConfigured()
    {
        var config = await Save(Update(AiProviderKind.Gemini));

        var gemini = config.Single(provider => provider.Kind == AiProviderKind.Gemini);
        Assert.True(gemini.IsEnabled);
        Assert.False(gemini.KeySet);
        Assert.False(gemini.IsConfigured);
        Assert.Empty(await Service().GetAvailableAsync());
    }

    [Fact]
    public async Task OllamaWithAnAddressIsConfiguredAndLocal()
    {
        await Save(Update(AiProviderKind.Ollama, baseUrl: "http://localhost:11434/"));

        var available = await Service().GetAvailableAsync();

        var ollama = Assert.Single(available);
        Assert.Equal(AiProviderKind.Ollama, ollama.Kind);
        Assert.Equal("Local", ollama.Locality);
        Assert.Equal("http://localhost:11434", (await Service().GetAdminConfigAsync()).First().BaseUrl);
    }

    [Fact]
    public async Task OnlyConfiguredProvidersAreListedForUsers()
    {
        await Save(
            Update(AiProviderKind.Ollama, baseUrl: "http://localhost:11434"),
            Update(AiProviderKind.Gemini, key: "AIzaSyExampleKey1234"),
            Update(AiProviderKind.OpenAI));

        var kinds = (await Service().GetAvailableAsync()).Select(option => option.Kind).ToList();

        Assert.Equal([AiProviderKind.Ollama, AiProviderKind.Gemini], kinds);
    }

    [Fact]
    public async Task SavedKeyIsReportedOnlyAsLastFourCharacters()
    {
        const string key = "sk-proj-abcdefghijklmnop1234";
        var config = await Save(Update(AiProviderKind.OpenAI, key: key));

        var openAi = config.Single(provider => provider.Kind == AiProviderKind.OpenAI);
        Assert.True(openAi.KeySet);
        Assert.Equal("1234", openAi.KeyLast4);
        Assert.True(openAi.IsConfigured);

        var serialised = JsonSerializer.Serialize(await Service().GetAdminConfigAsync());
        Assert.DoesNotContain(key, serialised);
        Assert.DoesNotContain("abcdefghijklmnop", serialised);
    }

    [Fact]
    public async Task StoredKeyIsEncryptedAtRest()
    {
        const string key = "sk-proj-abcdefghijklmnop1234";
        await Save(Update(AiProviderKind.OpenAI, key: key));

        var stored = await _harness.Db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.Kind == AiProviderKind.OpenAI);

        Assert.DoesNotContain("abcdefghijklmnop", stored.EncryptedApiKey);
        Assert.Equal(key, (await Service().GetRuntimeAsync(AiProviderKind.OpenAI))!.ApiKey);
    }

    [Fact]
    public async Task EmptyKeyOnSaveKeepsTheExistingKey()
    {
        await Save(Update(AiProviderKind.OpenAI, key: "sk-proj-original-key-9999"));

        await Save(Update(AiProviderKind.OpenAI, model: "gpt-4o", key: ""));

        var runtime = await Service().GetRuntimeAsync(AiProviderKind.OpenAI);
        Assert.Equal("sk-proj-original-key-9999", runtime!.ApiKey);
        Assert.Equal("gpt-4o", runtime.ModelName);
    }

    [Fact]
    public async Task NewKeyReplacesTheOldOne()
    {
        await Save(Update(AiProviderKind.OpenAI, key: "sk-proj-original-key-9999"));

        var config = await Save(Update(AiProviderKind.OpenAI, key: "sk-proj-replacement-key-7777"));

        Assert.Equal("7777", config.Single(provider => provider.Kind == AiProviderKind.OpenAI).KeyLast4);
        Assert.Equal("sk-proj-replacement-key-7777", (await Service().GetRuntimeAsync(AiProviderKind.OpenAI))!.ApiKey);
    }

    [Fact]
    public async Task ClearKeyRemovesItAndTheProviderBecomesNotConfigured()
    {
        await Save(Update(AiProviderKind.OpenAI, key: "sk-proj-original-key-9999"));

        var config = await Save(Update(AiProviderKind.OpenAI, clear: true));

        var openAi = config.Single(provider => provider.Kind == AiProviderKind.OpenAI);
        Assert.False(openAi.KeySet);
        Assert.Null(openAi.KeyLast4);
        Assert.False(openAi.IsConfigured);
        Assert.Null(await Service().GetRuntimeAsync(AiProviderKind.OpenAI));
    }

    [Fact]
    public async Task DisabledProviderHasNoRuntime()
    {
        await Save(Update(AiProviderKind.Ollama, baseUrl: "http://localhost:11434"));
        await Save(Update(AiProviderKind.Ollama, enabled: false, baseUrl: "http://localhost:11434"));

        Assert.Null(await Service().GetRuntimeAsync(AiProviderKind.Ollama));
    }

    [Fact]
    public async Task AKeyThatCanNoLongerBeDecryptedMakesTheProviderNotConfigured()
    {
        await Save(Update(AiProviderKind.Gemini, key: "AIzaSyExampleKey1234"));

        var withNewKeyRing = new AiProviderConfigService(_harness.Db, new AiKeyProtector(new EphemeralDataProtectionProvider()), TimeProvider.System);

        Assert.Empty(await withNewKeyRing.GetAvailableAsync());
        Assert.Null(await withNewKeyRing.GetRuntimeAsync(AiProviderKind.Gemini));
    }

    [Fact]
    public async Task AKeyThatCannotBeReadIsReportedSoTheAdministratorKnowsToEnterItAgain()
    {
        await Save(Update(AiProviderKind.Gemini, key: "AIzaSyExampleKey1234"));
        var withNewKeyRing = new AiProviderConfigService(_harness.Db, new AiKeyProtector(new EphemeralDataProtectionProvider()), TimeProvider.System);

        var config = await withNewKeyRing.GetAdminConfigAsync();

        var gemini = config.Single(provider => provider.Kind == AiProviderKind.Gemini);
        Assert.True(gemini.KeySet);
        Assert.Equal("1234", gemini.KeyLast4);
        Assert.False(gemini.KeyReadable);
        Assert.False(gemini.IsConfigured);
        Assert.True(config.Single(provider => provider.Kind == AiProviderKind.Ollama).KeyReadable);
        Assert.True(config.Single(provider => provider.Kind == AiProviderKind.OpenAI).KeyReadable);
    }

    [Fact]
    public async Task AReadableKeyIsReportedAsReadable()
    {
        var config = await Save(Update(AiProviderKind.Gemini, key: "AIzaSyExampleKey1234"));

        Assert.True(config.Single(provider => provider.Kind == AiProviderKind.Gemini).KeyReadable);
    }

    [Fact]
    public async Task UsingAnUnreadableKeyLogsAWarningWithoutAnyKeyMaterial()
    {
        await Save(Update(AiProviderKind.Gemini, key: "AIzaSyExampleKey1234"));
        var logger = new CapturingLogger<AiProviderConfigService>();
        var withNewKeyRing = new AiProviderConfigService(_harness.Db, new AiKeyProtector(new EphemeralDataProtectionProvider()), TimeProvider.System, logger);

        var runtime = await withNewKeyRing.GetRuntimeAsync(AiProviderKind.Gemini);

        Assert.Null(runtime);
        var line = Assert.Single(logger.Messages);
        Assert.Contains("Gemini", line);
        Assert.Contains("cannot be decrypted", line);
        Assert.DoesNotContain("ExampleKey", line);
    }

    [Fact]
    public void NoResponseTypeCanHoldTheFullKey()
    {
        var properties = typeof(AiProviderConfigDto).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain(properties, name => name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("Encrypted", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(AiProviderOptionDto).GetProperties().Select(property => property.Name), name => name.Contains("Key"));
    }

    [Fact]
    public void ValidatorRejectsBadInput()
    {
        var validator = new UpdateAiProvidersRequestValidator();

        Assert.False(validator.Validate(new UpdateAiProvidersRequest(null)).IsValid);
        Assert.False(validator.Validate(new UpdateAiProvidersRequest([Update(AiProviderKind.Ollama, model: "")])).IsValid);
        Assert.False(validator.Validate(new UpdateAiProvidersRequest([Update(AiProviderKind.Ollama, baseUrl: "ftp://host")])).IsValid);
        Assert.False(validator.Validate(new UpdateAiProvidersRequest([Update(AiProviderKind.Ollama, baseUrl: "not a url")])).IsValid);
        Assert.False(validator.Validate(new UpdateAiProvidersRequest([Update(AiProviderKind.OpenAI, key: "has space")])).IsValid);
        Assert.False(validator.Validate(new UpdateAiProvidersRequest([Update(AiProviderKind.OpenAI, key: "sk-x", clear: true)])).IsValid);
        Assert.False(validator.Validate(new UpdateAiProvidersRequest([Update(AiProviderKind.OpenAI), Update(AiProviderKind.OpenAI)])).IsValid);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://169.254.10.20:11434")]
    [InlineData("http://[fe80::1]:11434")]
    [InlineData("http://[::ffff:169.254.169.254]/")]
    [InlineData("http://0.0.0.0:11434")]
    [InlineData("http://metadata.google.internal/computeMetadata/v1")]
    [InlineData("http://METADATA/")]
    public void ValidatorRefusesMetadataAndLinkLocalAddressesForOllama(string address)
    {
        var validator = new UpdateAiProvidersRequestValidator();

        var result = validator.Validate(new UpdateAiProvidersRequest([Update(AiProviderKind.Ollama, baseUrl: address)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("metadata"));
    }

    [Theory]
    [InlineData("http://localhost:11434")]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("http://192.168.1.20:11434")]
    [InlineData("http://10.0.0.5:11434")]
    [InlineData("http://[::1]:11434")]
    [InlineData("https://ollama.internal.example")]
    public void ValidatorStillAllowsLocalAndPrivateAddressesForOllama(string address)
    {
        var validator = new UpdateAiProvidersRequestValidator();

        Assert.True(validator.Validate(new UpdateAiProvidersRequest([Update(AiProviderKind.Ollama, baseUrl: address)])).IsValid);
    }

    [Fact]
    public void ValidatorAcceptsAGoodRequest()
    {
        var validator = new UpdateAiProvidersRequestValidator();

        var result = validator.Validate(new UpdateAiProvidersRequest(
        [
            Update(AiProviderKind.Ollama, baseUrl: "http://localhost:11434"),
            Update(AiProviderKind.Gemini, key: "AIzaSyExampleKey1234"),
            Update(AiProviderKind.OpenAI)
        ]));

        Assert.True(result.IsValid);
    }
}
