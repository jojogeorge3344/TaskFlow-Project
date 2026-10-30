using TaskFlow.Api.Repositories.Settings.TfaSettings;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Settings.TfaSettings;
using TaskFlow.Api.Persistence.Seeders;
using TaskFlow.Api.Integrations.Ai;
using TaskFlow.Api.Common.Constants;
using Task = System.Threading.Tasks.Task;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Infrastructure.Assistant;

namespace TaskFlow.UnitTests;

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

    [Fact]
    public async Task ASeededModelRemovedByNobodyIsNotDuplicatedWhenTheListGrows()
    {
        await Seeder().SeedAsync();
        _harness.Db.AiModels.Add(new AiModel { Kind = AiProviderKind.Gemini, Name = "gemini-custom", IsBuiltIn = false });
        await _harness.Db.SaveChangesAsync();

        await Seeder().SeedAsync();

        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Name == "gemini-custom"));
        Assert.Equal(1, await _harness.Db.AiModels.CountAsync(model => model.Kind == AiProviderKind.Gemini && model.Name == "gemini-2.0-flash"));
    }
}
