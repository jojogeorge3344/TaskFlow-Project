using TaskFlow.Api.Persistence.Seeders;
using TaskFlow.Api.Dtos.Settings.TfaSettings.Validators;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Common.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.KeyProtection;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.UnitTests.Settings.TfaSettings.ProviderConfig;

public class AiProviderConfigServiceTests : IAsyncLifetime
{
    private readonly TaskTrackingServiceHarness _harness = TaskTrackingServiceHarness.Create();
    private readonly EphemeralDataProtectionProvider _provider = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private AiProviderConfigService Service() =>
        new(new AiProviderConfigRepository(_harness.Db), new AiModelRepository(_harness.Db), new UnitOfWork(_harness.Db), new AiKeyProtector(_provider), TimeProvider.System);

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
    public async Task EveryProviderListsTheModelsToChooseFromAndAlwaysIncludesTheChosenOne()
    {
        var seeder = new AiProviderSeeder(_harness.Db, TimeProvider.System);
        await seeder.SeedAsync();

        var config = await Service().GetAdminConfigAsync();

        foreach (var provider in config)
        {
            Assert.Equal(AiProviderCatalog.BuiltInModels(provider.Kind), provider.Models);
            Assert.Contains(provider.ModelName, provider.Models);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyModelNameIsRejected(string name)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Save(Update(AiProviderKind.Ollama, model: name)));
        Assert.Empty(await _harness.Db.AiModels.ToListAsync());
    }

    [Fact]
    public async Task AModelNameLongerThanTheStoredLengthIsRejected()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Save(Update(AiProviderKind.Ollama, model: new string('m', 101))));
        Assert.Empty(await _harness.Db.AiModels.ToListAsync());

        await Save(Update(AiProviderKind.Ollama, model: new string('m', 100)));
        Assert.Equal(1, await _harness.Db.AiModels.CountAsync());
    }

    [Fact]
    public async Task OnlyTheModelOfTheEnabledServerIsAddedToTheList()
    {
        await Save(
            Update(AiProviderKind.Ollama, enabled: true, model: "phi3:mini"),
            Update(AiProviderKind.Gemini, enabled: false, model: "leftover-typo"));

        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Name == "phi3:mini"));
        Assert.Equal(0, await _harness.Db.AiModels.CountAsync(model => model.Name == "leftover-typo"));
    }

    [Fact]
    public async Task ACustomModelIsListedAsRemovableAndCanBeRemovedOnceNoServerUsesIt()
    {
        await new AiProviderSeeder(_harness.Db, TimeProvider.System).SeedAsync();
        await Save(Update(AiProviderKind.Ollama, model: "typo-model"));

        var config = await Service().GetAdminConfigAsync();
        Assert.Equal(["typo-model"], config.Single(provider => provider.Kind == AiProviderKind.Ollama).CustomModels);
        Assert.All(config.Where(provider => provider.Kind != AiProviderKind.Ollama), provider => Assert.Empty(provider.CustomModels));

        // It is the saved model, so it stays until another one is saved.
        await Assert.ThrowsAsync<RequestValidationException>(() => Service().RemoveModelAsync(AiProviderKind.Ollama, "typo-model"));
        await Save(Update(AiProviderKind.Ollama, model: "llama3.2:3b"));

        var after = await Service().RemoveModelAsync(AiProviderKind.Ollama, "typo-model");

        var ollama = after.Single(provider => provider.Kind == AiProviderKind.Ollama);
        Assert.DoesNotContain("typo-model", ollama.Models);
        Assert.Empty(ollama.CustomModels);
        Assert.Equal(0, await _harness.Db.AiModels.CountAsync(model => model.Name == "typo-model"));
    }

    [Fact]
    public async Task ABuiltInModelCannotBeRemovedAndAnUnknownOneIsNotFound()
    {
        await new AiProviderSeeder(_harness.Db, TimeProvider.System).SeedAsync();

        await Assert.ThrowsAsync<RequestValidationException>(() => Service().RemoveModelAsync(AiProviderKind.Ollama, "llama3.1:8b"));
        await Assert.ThrowsAsync<NotFoundException>(() => Service().RemoveModelAsync(AiProviderKind.Ollama, "never-added"));
        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Name == "llama3.1:8b"));
    }

    [Fact]
    public async Task TwoAdministratorsAddingTheSameNewModelAtOnceDoNotFailTheSecondSave()
    {
        await new AiProviderSeeder(_harness.Db, TimeProvider.System).SeedAsync();

        // Just before our save reaches the database, another administrator's save adds the same model.
        var raced = false;
        _harness.Db.SavingChanges += (_, _) =>
        {
            if (raced)
            {
                return;
            }

            raced = true;
            var options = new DbContextOptionsBuilder<TaskFlow.Api.Persistence.AppDbContext>()
                .UseSqlite(_harness.Db.Database.GetDbConnection())
                .Options;
            using var other = new TaskFlow.Api.Persistence.AppDbContext(options);
            other.AiModels.Add(new TaskFlow.Api.Models.Entities.AiModel { Kind = AiProviderKind.Ollama, Name = "phi3:mini", IsBuiltIn = false });
            other.SaveChanges();
        };

        var config = await Save(Update(AiProviderKind.Ollama, model: "phi3:mini"));

        Assert.True(raced);
        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Name == "phi3:mini"));
        Assert.Equal("phi3:mini", config.Single(provider => provider.Kind == AiProviderKind.Ollama).ModelName);
    }

    [Fact]
    public async Task AModelTypedInSettingsIsAddedToTheListForEveryone()
    {
        await new AiProviderSeeder(_harness.Db, TimeProvider.System).SeedAsync();

        var config = await Save(Update(AiProviderKind.Ollama, model: "phi3:mini", baseUrl: "http://localhost:11434"));

        var ollama = config.Single(provider => provider.Kind == AiProviderKind.Ollama);
        Assert.Equal("phi3:mini", ollama.ModelName);
        Assert.Equal(AiProviderCatalog.BuiltInModels(AiProviderKind.Ollama).Append("phi3:mini"), ollama.Models);
        var stored = await _harness.Db.AiModels.SingleAsync(model => model.Name == "phi3:mini");
        Assert.False(stored.IsBuiltIn);

        // Saving it again does not add it twice, and the other providers are unaffected.
        var again = await Save(Update(AiProviderKind.Ollama, model: "phi3:mini", baseUrl: "http://localhost:11434"));
        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Name == "phi3:mini"));
        Assert.DoesNotContain("phi3:mini", again.Single(provider => provider.Kind == AiProviderKind.Gemini).Models);
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

        var withNewKeyRing = new AiProviderConfigService(new AiProviderConfigRepository(_harness.Db), new AiModelRepository(_harness.Db), new UnitOfWork(_harness.Db), new AiKeyProtector(new EphemeralDataProtectionProvider()), TimeProvider.System);

        Assert.Empty(await withNewKeyRing.GetAvailableAsync());
        Assert.Null(await withNewKeyRing.GetRuntimeAsync(AiProviderKind.Gemini));
    }

    [Fact]
    public async Task AKeyThatCannotBeReadIsReportedSoTheAdministratorKnowsToEnterItAgain()
    {
        await Save(Update(AiProviderKind.Gemini, key: "AIzaSyExampleKey1234"));
        var withNewKeyRing = new AiProviderConfigService(new AiProviderConfigRepository(_harness.Db), new AiModelRepository(_harness.Db), new UnitOfWork(_harness.Db), new AiKeyProtector(new EphemeralDataProtectionProvider()), TimeProvider.System);

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
        var withNewKeyRing = new AiProviderConfigService(new AiProviderConfigRepository(_harness.Db), new AiModelRepository(_harness.Db), new UnitOfWork(_harness.Db), new AiKeyProtector(new EphemeralDataProtectionProvider()), TimeProvider.System, logger);

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
