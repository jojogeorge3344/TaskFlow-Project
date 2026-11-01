using TaskFlow.Api.Persistence.Seeders;
using TaskFlow.Api.Common.Constants;
using Task = System.Threading.Tasks.Task;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.KeyProtection;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.UnitTests.Settings.TfaSettings.ProviderConfig;

/// <summary>The first run on any machine puts the AI defaults into the database; later runs never overwrite what an
/// administrator saved.</summary>
public class AiProviderSeederTests : IAsyncLifetime
{
    private readonly TaskTrackingServiceHarness _harness = TaskTrackingServiceHarness.Create();
    private readonly EphemeralDataProtectionProvider _provider = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private const string ConfiguredKey = "AIzaSyConfiguredKey0000WXYZ";

    private AiProviderSeeder Seeder(string? configuredGeminiKey = null) =>
        new(_harness.Db, TimeProvider.System, new AiKeyProtector(_provider),
            Microsoft.Extensions.Options.Options.Create(new TaskFlow.Api.Config.AiProviderOptions { GeminiApiKey = configuredGeminiKey }));

    /// <summary>A built-in provider row as an older installation had it saved.</summary>
    private static AiProviderConfig BuiltIn(AiApiType apiType) => new()
    {
        Name = AiProviderCatalog.DisplayName(apiType),
        ApiType = apiType,
        Locality = AiProviderCatalog.Locality(apiType),
        IsBuiltIn = true,
        UpdatedAt = DateTime.UtcNow
    };

    private AiProviderConfigService Service() => new(new AiProviderConfigRepository(_harness.Db), new AiModelRepository(_harness.Db), new UnitOfWork(_harness.Db), new AiKeyProtector(_provider), TimeProvider.System);

    [Fact]
    public async Task AFreshDatabaseGetsEveryBuiltInModel()
    {
        await Seeder().SeedAsync();

        foreach (var kind in AiProviderCatalog.All)
        {
            var stored = await _harness.Db.AiModels.Where(model => model.Provider!.ApiType == kind).Select(model => model.Name).ToListAsync();
            Assert.Equal(AiProviderCatalog.BuiltInModels(kind).OrderBy(name => name), stored.OrderBy(name => name));
        }

        Assert.All(await _harness.Db.AiModels.ToListAsync(), model => Assert.True(model.IsBuiltIn));
    }

    [Fact]
    public async Task AFreshDatabaseWithoutAConfiguredKeyGetsEveryProviderOffAndNoKey()
    {
        await Seeder().SeedAsync();

        var rows = await _harness.Db.AiProviderConfigs.ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.False(row.IsEnabled));
        Assert.All(rows, row => Assert.Null(row.EncryptedApiKey));
        Assert.Empty(await Service().GetAvailableAsync());

        var ollama = rows.Single(row => row.ApiType == AiApiType.Ollama);
        Assert.Equal("llama3.2:3b", ollama.ModelName);
        Assert.Equal("http://localhost:11434", ollama.BaseUrl);
        foreach (var cloud in rows.Where(row => row.ApiType != AiApiType.Ollama))
        {
            Assert.Null(cloud.BaseUrl);
            Assert.Equal(AiProviderCatalog.DefaultModel(cloud.ApiType), cloud.ModelName);
        }
    }

    [Fact]
    public async Task AFreshDatabaseWithAConfiguredKeyGetsGeminiOnWithTheKeyEncrypted()
    {
        await Seeder(ConfiguredKey).SeedAsync();

        var rows = await _harness.Db.AiProviderConfigs.ToListAsync();
        var gemini = rows.Single(row => row.ApiType == AiApiType.Gemini);
        Assert.True(gemini.IsEnabled);
        Assert.Equal(AiProviderCatalog.DefaultModel(AiApiType.Gemini), gemini.ModelName);
        Assert.NotNull(gemini.EncryptedApiKey);
        Assert.DoesNotContain(ConfiguredKey, gemini.EncryptedApiKey);
        Assert.Equal(ConfiguredKey, new AiKeyProtector(_provider).Unprotect(gemini.EncryptedApiKey!));
        Assert.Equal("WXYZ", gemini.KeyLast4);
        Assert.All(rows.Where(row => row.ApiType != AiApiType.Gemini), row => Assert.False(row.IsEnabled));
    }

    [Fact]
    public async Task TfaIsAvailableOnTheFirstRunWithGeminiWhenAKeyIsConfigured()
    {
        await Seeder(ConfiguredKey).SeedAsync();

        var available = Assert.Single(await Service().GetAvailableAsync());
        Assert.Equal("Google Gemini", available.DisplayName);
        Assert.Equal(AiProviderCatalog.DefaultModel(AiApiType.Gemini), available.ModelName);
    }

    [Fact]
    public void GeminiIsListedFirst()
    {
        Assert.Equal(AiApiType.Gemini, AiProviderCatalog.All[0]);
    }

    [Fact]
    public async Task AnUpgradedInstallationGetsEveryProviderOffSoTfaStaysOffUntilAnAdministratorTurnsItOn()
    {
        _harness.AddUser("existing-admin");

        await Seeder().SeedAsync();

        var rows = await _harness.Db.AiProviderConfigs.ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.False(row.IsEnabled));
        Assert.Empty(await Service().GetAvailableAsync());
    }

    [Fact]
    public async Task AnExistingOllamaInstallationGetsTheKeyButKeepsOllama()
    {
        _harness.AddUser("existing-admin");
        var ollama = BuiltIn(AiApiType.Ollama);
        ollama.IsEnabled = true;
        ollama.ModelName = "llama3.2:3b";
        ollama.BaseUrl = "http://localhost:11434";
        _harness.Db.AiProviderConfigs.Add(ollama);
        await _harness.Db.SaveChangesAsync();

        await Seeder(ConfiguredKey).SeedAsync();

        var rows = await _harness.Db.AiProviderConfigs.ToListAsync();
        Assert.True(rows.Single(row => row.ApiType == AiApiType.Ollama).IsEnabled);
        var gemini = rows.Single(row => row.ApiType == AiApiType.Gemini);
        Assert.False(gemini.IsEnabled);
        Assert.NotNull(gemini.EncryptedApiKey);
    }

    [Fact]
    public async Task AKeySavedInSettingsIsNeverReplacedByTheConfiguredOne()
    {
        await Seeder().SeedAsync();
        var gemini = await _harness.Db.AiProviderConfigs.SingleAsync(row => row.ApiType == AiApiType.Gemini);
        var saved = new AiKeyProtector(_provider).Protect("AIzaSySavedInSettings1234");
        gemini.EncryptedApiKey = saved;
        gemini.KeyLast4 = "1234";
        await _harness.Db.SaveChangesAsync();

        await Seeder(ConfiguredKey).SeedAsync();

        var after = await _harness.Db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.ApiType == AiApiType.Gemini);
        Assert.Equal((saved, "1234"), (after.EncryptedApiKey, after.KeyLast4));
    }

    [Fact]
    public async Task GeminiSwitchedOffByAnAdministratorStaysOff()
    {
        await Seeder(ConfiguredKey).SeedAsync();
        _harness.AddUser("admin");
        var gemini = await _harness.Db.AiProviderConfigs.SingleAsync(row => row.ApiType == AiApiType.Gemini);
        gemini.IsEnabled = false;
        await _harness.Db.SaveChangesAsync();

        await Seeder(ConfiguredKey).SeedAsync();

        Assert.False((await _harness.Db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.ApiType == AiApiType.Gemini)).IsEnabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankConfiguredKeyIsIgnored(string? configured)
    {
        await Seeder(configured).SeedAsync();

        var gemini = await _harness.Db.AiProviderConfigs.SingleAsync(row => row.ApiType == AiApiType.Gemini);
        Assert.Null(gemini.EncryptedApiKey);
        Assert.False(gemini.IsEnabled);
    }

    [Fact]
    public void TheSeederCannotLogTheKeyBecauseItHasNoLogger()
    {
        Assert.All(
            typeof(AiProviderSeeder).GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => Assert.False(typeof(Microsoft.Extensions.Logging.ILogger).IsAssignableFrom(parameter.ParameterType)));
    }

    [Fact]
    public async Task RunningItAgainAddsNothing()
    {
        await Seeder().SeedAsync();
        var models = await _harness.Db.AiModels.CountAsync();
        var providers = await _harness.Db.AiProviderConfigs.CountAsync();

        await Seeder().SeedAsync();
        await Seeder().SeedAsync();

        Assert.Equal(models, await _harness.Db.AiModels.CountAsync());
        Assert.Equal(providers, await _harness.Db.AiProviderConfigs.CountAsync());
    }

    [Fact]
    public async Task WhatAnAdministratorSavedIsNeverOverwritten()
    {
        var saved = BuiltIn(AiApiType.Ollama);
        saved.IsEnabled = false;
        saved.ModelName = "my-own-model:7b";
        saved.BaseUrl = "http://gpu-box:11434";
        _harness.Db.AiProviderConfigs.Add(saved);
        await _harness.Db.SaveChangesAsync();

        await Seeder().SeedAsync();

        var ollama = await _harness.Db.AiProviderConfigs.SingleAsync(row => row.ApiType == AiApiType.Ollama);
        Assert.False(ollama.IsEnabled);
        Assert.Equal("my-own-model:7b", ollama.ModelName);
        Assert.Equal("http://gpu-box:11434", ollama.BaseUrl);

        // The model that was typed in before the list existed is offered from now on, as a custom one.
        var custom = await _harness.Db.AiModels.SingleAsync(model => model.Name == "my-own-model:7b");
        Assert.False(custom.IsBuiltIn);
        Assert.Equal(ollama.Id, custom.ProviderId);
    }

    /// <summary>A database seeded before Google retired gemini-2.0-flash, gemini-2.5-flash and gemini-2.5-pro.</summary>
    private async Task SeedAnOldDatabaseAsync(bool geminiSetUp)
    {
        var gemini = BuiltIn(AiApiType.Gemini);
        gemini.IsEnabled = geminiSetUp;
        gemini.ModelName = "gemini-2.0-flash";
        gemini.EncryptedApiKey = geminiSetUp ? "protected-key" : null;
        gemini.KeyLast4 = geminiSetUp ? "1234" : null;
        _harness.Db.AiProviderConfigs.Add(gemini);
        foreach (var name in new[] { "gemini-2.0-flash", "gemini-2.5-flash", "gemini-2.5-pro" })
        {
            _harness.Db.AiModels.Add(new AiModel { Provider = gemini, Name = name, IsBuiltIn = true });
        }

        await _harness.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task RetiredBuiltInModelsBecomeRemovableAndCurrentOnesStayBuiltIn()
    {
        await SeedAnOldDatabaseAsync(geminiSetUp: false);

        await Seeder().SeedAsync();

        var gemini = await _harness.Db.AiModels.Where(model => model.Provider!.ApiType == AiApiType.Gemini).ToDictionaryAsync(model => model.Name, model => model.IsBuiltIn);
        Assert.False(gemini["gemini-2.0-flash"]);
        Assert.False(gemini["gemini-2.5-flash"]);
        Assert.False(gemini["gemini-2.5-pro"]);
        foreach (var name in AiProviderCatalog.BuiltInModels(AiApiType.Gemini))
        {
            Assert.True(gemini[name]);
        }

        var geminiId = (await _harness.Db.AiProviderConfigs.SingleAsync(row => row.ApiType == AiApiType.Gemini)).Id;
        await Service().RemoveModelAsync(geminiId, "gemini-2.5-pro");
        Assert.False(await _harness.Db.AiModels.AnyAsync(model => model.Name == "gemini-2.5-pro"));
    }

    [Fact]
    public async Task AGeminiRowNobodySetUpMovesToTheNewDefault()
    {
        await SeedAnOldDatabaseAsync(geminiSetUp: false);

        await Seeder().SeedAsync();

        var gemini = await _harness.Db.AiProviderConfigs.SingleAsync(row => row.ApiType == AiApiType.Gemini);
        Assert.Equal("gemini-3.8-flash", gemini.ModelName);
        Assert.False(gemini.IsEnabled);
        Assert.Null(gemini.EncryptedApiKey);
    }

    [Fact]
    public async Task AGeminiRowAnAdministratorSetUpKeepsItsRetiredModel()
    {
        await SeedAnOldDatabaseAsync(geminiSetUp: true);

        await Seeder().SeedAsync();

        var gemini = await _harness.Db.AiProviderConfigs.SingleAsync(row => row.ApiType == AiApiType.Gemini);
        Assert.Equal("gemini-2.0-flash", gemini.ModelName);
        Assert.True(gemini.IsEnabled);
        Assert.Equal("protected-key", gemini.EncryptedApiKey);
        Assert.False((await _harness.Db.AiModels.SingleAsync(model => model.Name == "gemini-2.0-flash")).IsBuiltIn);
    }

    [Fact]
    public async Task RetiringOldModelsTwiceChangesNothingMore()
    {
        await SeedAnOldDatabaseAsync(geminiSetUp: false);
        await Seeder().SeedAsync();
        var models = await _harness.Db.AiModels.AsNoTracking().OrderBy(model => model.Id).Select(model => new { model.Name, model.IsBuiltIn }).ToListAsync();
        var gemini = await _harness.Db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.ApiType == AiApiType.Gemini);

        await Seeder().SeedAsync();

        Assert.Equal(models, await _harness.Db.AiModels.AsNoTracking().OrderBy(model => model.Id).Select(model => new { model.Name, model.IsBuiltIn }).ToListAsync());
        var again = await _harness.Db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.ApiType == AiApiType.Gemini);
        Assert.Equal(gemini.ModelName, again.ModelName);
        Assert.Equal(gemini.UpdatedAt, again.UpdatedAt);
    }

    [Fact]
    public async Task ASeededModelRemovedByNobodyIsNotDuplicatedWhenTheListGrows()
    {
        await Seeder().SeedAsync();
        var geminiId = (await _harness.Db.AiProviderConfigs.SingleAsync(row => row.ApiType == AiApiType.Gemini)).Id;
        _harness.Db.AiModels.Add(new AiModel { ProviderId = geminiId, Name = "gemini-custom", IsBuiltIn = false });
        await _harness.Db.SaveChangesAsync();

        await Seeder().SeedAsync();

        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Name == "gemini-custom"));
        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Provider!.ApiType == AiApiType.Gemini && model.Name == AiProviderCatalog.DefaultModel(AiApiType.Gemini)));
    }
}
