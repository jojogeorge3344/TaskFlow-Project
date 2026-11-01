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

    private AiProviderSeeder Seeder() => new(_harness.Db, TimeProvider.System);

    private AiProviderConfigService Service() => new(new AiProviderConfigRepository(_harness.Db), new AiModelRepository(_harness.Db), new UnitOfWork(_harness.Db), new AiKeyProtector(_provider), TimeProvider.System);

    [Fact]
    public async Task AFreshDatabaseGetsEveryBuiltInModel()
    {
        await Seeder().SeedAsync();

        foreach (var kind in AiProviderCatalog.All)
        {
            var stored = await _harness.Db.AiModels.Where(model => model.Kind == kind).Select(model => model.Name).ToListAsync();
            Assert.Equal(AiProviderCatalog.BuiltInModels(kind).OrderBy(name => name), stored.OrderBy(name => name));
        }

        Assert.All(await _harness.Db.AiModels.ToListAsync(), model => Assert.True(model.IsBuiltIn));
    }

    [Fact]
    public async Task AFreshDatabaseGetsOllamaOnAndTheCloudProvidersOffWithoutAKey()
    {
        await Seeder().SeedAsync();

        var rows = await _harness.Db.AiProviderConfigs.ToListAsync();
        Assert.Equal(3, rows.Count);

        var ollama = rows.Single(row => row.Kind == AiProviderKind.Ollama);
        Assert.True(ollama.IsEnabled);
        Assert.Equal("llama3.2:3b", ollama.ModelName);
        Assert.Equal("http://localhost:11434", ollama.BaseUrl);

        foreach (var cloud in rows.Where(row => row.Kind != AiProviderKind.Ollama))
        {
            Assert.False(cloud.IsEnabled);
            Assert.Null(cloud.EncryptedApiKey);
            Assert.Null(cloud.BaseUrl);
            Assert.Equal(AiProviderCatalog.DefaultModel(cloud.Kind), cloud.ModelName);
        }
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
    public async Task TfaIsAvailableOnTheFirstRunBecauseOllamaIsSeededOn()
    {
        await Seeder().SeedAsync();

        var available = await Service().GetAvailableAsync();

        var ollama = Assert.Single(available);
        Assert.Equal(AiProviderKind.Ollama, ollama.Kind);
        Assert.Equal("llama3.2:3b", ollama.ModelName);
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
        _harness.Db.AiProviderConfigs.Add(new AiProviderConfig
        {
            Kind = AiProviderKind.Ollama,
            IsEnabled = false,
            ModelName = "my-own-model:7b",
            BaseUrl = "http://gpu-box:11434",
            UpdatedAt = DateTime.UtcNow
        });
        await _harness.Db.SaveChangesAsync();

        await Seeder().SeedAsync();

        var ollama = await _harness.Db.AiProviderConfigs.SingleAsync(row => row.Kind == AiProviderKind.Ollama);
        Assert.False(ollama.IsEnabled);
        Assert.Equal("my-own-model:7b", ollama.ModelName);
        Assert.Equal("http://gpu-box:11434", ollama.BaseUrl);

        // The model that was typed in before the list existed is offered from now on, as a custom one.
        var custom = await _harness.Db.AiModels.SingleAsync(model => model.Name == "my-own-model:7b");
        Assert.False(custom.IsBuiltIn);
        Assert.Equal(AiProviderKind.Ollama, custom.Kind);
    }

    /// <summary>A database seeded before Google retired gemini-2.0-flash, gemini-2.5-flash and gemini-2.5-pro.</summary>
    private async Task SeedAnOldDatabaseAsync(bool geminiSetUp)
    {
        foreach (var name in new[] { "gemini-2.0-flash", "gemini-2.5-flash", "gemini-2.5-pro" })
        {
            _harness.Db.AiModels.Add(new AiModel { Kind = AiProviderKind.Gemini, Name = name, IsBuiltIn = true });
        }

        _harness.Db.AiProviderConfigs.Add(new AiProviderConfig
        {
            Kind = AiProviderKind.Gemini,
            IsEnabled = geminiSetUp,
            ModelName = "gemini-2.0-flash",
            EncryptedApiKey = geminiSetUp ? "protected-key" : null,
            KeyLast4 = geminiSetUp ? "1234" : null,
            UpdatedAt = DateTime.UtcNow
        });
        await _harness.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task RetiredBuiltInModelsBecomeRemovableAndCurrentOnesStayBuiltIn()
    {
        await SeedAnOldDatabaseAsync(geminiSetUp: false);

        await Seeder().SeedAsync();

        var gemini = await _harness.Db.AiModels.Where(model => model.Kind == AiProviderKind.Gemini).ToDictionaryAsync(model => model.Name, model => model.IsBuiltIn);
        Assert.False(gemini["gemini-2.0-flash"]);
        Assert.False(gemini["gemini-2.5-flash"]);
        Assert.False(gemini["gemini-2.5-pro"]);
        foreach (var name in AiProviderCatalog.BuiltInModels(AiProviderKind.Gemini))
        {
            Assert.True(gemini[name]);
        }

        await Service().RemoveModelAsync(AiProviderKind.Gemini, "gemini-2.5-pro");
        Assert.False(await _harness.Db.AiModels.AnyAsync(model => model.Name == "gemini-2.5-pro"));
    }

    [Fact]
    public async Task AGeminiRowNobodySetUpMovesToTheNewDefault()
    {
        await SeedAnOldDatabaseAsync(geminiSetUp: false);

        await Seeder().SeedAsync();

        var gemini = await _harness.Db.AiProviderConfigs.SingleAsync(row => row.Kind == AiProviderKind.Gemini);
        Assert.Equal("gemini-3.8-flash", gemini.ModelName);
        Assert.False(gemini.IsEnabled);
        Assert.Null(gemini.EncryptedApiKey);
    }

    [Fact]
    public async Task AGeminiRowAnAdministratorSetUpKeepsItsRetiredModel()
    {
        await SeedAnOldDatabaseAsync(geminiSetUp: true);

        await Seeder().SeedAsync();

        var gemini = await _harness.Db.AiProviderConfigs.SingleAsync(row => row.Kind == AiProviderKind.Gemini);
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
        var gemini = await _harness.Db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.Kind == AiProviderKind.Gemini);

        await Seeder().SeedAsync();

        Assert.Equal(models, await _harness.Db.AiModels.AsNoTracking().OrderBy(model => model.Id).Select(model => new { model.Name, model.IsBuiltIn }).ToListAsync());
        var again = await _harness.Db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.Kind == AiProviderKind.Gemini);
        Assert.Equal(gemini.ModelName, again.ModelName);
        Assert.Equal(gemini.UpdatedAt, again.UpdatedAt);
    }

    [Fact]
    public async Task ASeededModelRemovedByNobodyIsNotDuplicatedWhenTheListGrows()
    {
        await Seeder().SeedAsync();
        _harness.Db.AiModels.Add(new AiModel { Kind = AiProviderKind.Gemini, Name = "gemini-custom", IsBuiltIn = false });
        await _harness.Db.SaveChangesAsync();

        await Seeder().SeedAsync();

        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Name == "gemini-custom"));
        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Kind == AiProviderKind.Gemini && model.Name == AiProviderCatalog.DefaultModel(AiProviderKind.Gemini)));
    }
}
