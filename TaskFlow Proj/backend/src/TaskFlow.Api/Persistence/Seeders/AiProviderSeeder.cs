using TaskFlow.Api.Common.Constants;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Persistence.Seeders;

/// <summary>Puts the AI defaults into the database the first time the application runs on any machine: the list of
/// models offered in Settings, and a row for each provider (Ollama on, the cloud providers off and without a key).
/// It only adds what is missing, so it is safe to run at every start and never overwrites what an administrator saved.
/// API keys are never part of this: they are entered in Settings and kept encrypted in the database only.</summary>
public class AiProviderSeeder
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AiProviderSeeder(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async System.Threading.Tasks.Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await RetireOldBuiltInModelsAsync(cancellationToken);
        await SeedModelsAsync(cancellationToken);
        await SeedProvidersAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A model that was built in once but is no longer in <see cref="AiProviderCatalog.BuiltInModels"/> (Google
    /// retires Gemini models, for example) becomes an administrator-added one, so it can be removed in Settings. A provider
    /// nobody has set up yet (switched off, no key) that still names such a model moves to the current default; a provider
    /// an administrator set up keeps its model.</summary>
    private async System.Threading.Tasks.Task RetireOldBuiltInModelsAsync(CancellationToken cancellationToken)
    {
        var retired = (await _db.AiModels.Where(model => model.IsBuiltIn).ToListAsync(cancellationToken))
            .Where(model => !AiProviderCatalog.BuiltInModels(model.Kind).Contains(model.Name))
            .ToList();
        if (retired.Count == 0)
        {
            return;
        }

        foreach (var model in retired)
        {
            model.IsBuiltIn = false;
        }

        var retiredNames = retired.Select(model => (model.Kind, model.Name)).ToHashSet();
        var untouched = await _db.AiProviderConfigs
            .Where(config => !config.IsEnabled && config.EncryptedApiKey == null)
            .ToListAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var config in untouched.Where(config => retiredNames.Contains((config.Kind, config.ModelName))))
        {
            config.ModelName = AiProviderCatalog.DefaultModel(config.Kind);
            config.UpdatedAt = now;
        }
    }

    private async System.Threading.Tasks.Task SeedModelsAsync(CancellationToken cancellationToken)
    {
        var known = (await _db.AiModels.AsNoTracking().Select(model => new { model.Kind, model.Name }).ToListAsync(cancellationToken))
            .Select(model => (model.Kind, model.Name))
            .ToHashSet();

        foreach (var kind in AiProviderCatalog.All)
        {
            foreach (var name in AiProviderCatalog.BuiltInModels(kind))
            {
                if (known.Add((kind, name)))
                {
                    _db.AiModels.Add(new AiModel { Kind = kind, Name = name, IsBuiltIn = true });
                }
            }
        }

        // A model an administrator typed in before this list existed stays selectable.
        var saved = await _db.AiProviderConfigs.AsNoTracking().Select(config => new { config.Kind, config.ModelName }).ToListAsync(cancellationToken);
        foreach (var config in saved)
        {
            if (!string.IsNullOrWhiteSpace(config.ModelName) && known.Add((config.Kind, config.ModelName)))
            {
                _db.AiModels.Add(new AiModel { Kind = config.Kind, Name = config.ModelName, IsBuiltIn = false });
            }
        }
    }

    private async System.Threading.Tasks.Task SeedProvidersAsync(CancellationToken cancellationToken)
    {
        var present = await _db.AiProviderConfigs.AsNoTracking().Select(config => config.Kind).ToListAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Ollama is switched on only for a brand-new installation: no user exists yet, because this runs before the users
        // are seeded. An installation that is being upgraded keeps TFA off until an administrator turns it on.
        var freshInstall = !await _db.Users.AnyAsync(cancellationToken);

        foreach (var kind in AiProviderCatalog.All.Where(kind => !present.Contains(kind)))
        {
            var local = !AiProviderCatalog.RequiresKey(kind);
            _db.AiProviderConfigs.Add(new AiProviderConfig
            {
                Kind = kind,
                // Ollama needs no key and runs on this machine, so TFA can work as soon as it is installed.
                IsEnabled = local && freshInstall,
                ModelName = AiProviderCatalog.DefaultModel(kind),
                BaseUrl = local ? AiProviderCatalog.DefaultOllamaBaseUrl : null,
                UpdatedAt = now
            });
        }
    }
}
