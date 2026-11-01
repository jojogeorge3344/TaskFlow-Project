using TaskFlow.Api.Common.Constants;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Persistence.Seeders;

/// <summary>Puts the AI defaults into the database the first time the application runs on any machine: a built-in
/// provider row for Ollama, Google Gemini and OpenAI GPT (Ollama on, the cloud providers off and without a key), and the
/// models offered for each. It only adds what is missing, so it is safe to run at every start and never overwrites what an
/// administrator saved. Custom providers are never touched. API keys are never part of this: they are entered in Settings
/// and kept encrypted in the database only.</summary>
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
        var builtIns = await SeedProvidersAsync(cancellationToken);
        await RetireOldBuiltInModelsAsync(builtIns, cancellationToken);
        await SeedModelsAsync(builtIns, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The built-in provider rows, with any missing one added (tracked, so models can refer to it before it is saved).</summary>
    private async Task<List<AiProviderConfig>> SeedProvidersAsync(CancellationToken cancellationToken)
    {
        var builtIns = await _db.AiProviderConfigs.Where(config => config.IsBuiltIn).ToListAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Ollama is switched on only for a brand-new installation: no user exists yet, because this runs before the users
        // are seeded. An installation that is being upgraded keeps TFA off until an administrator turns it on.
        var freshInstall = !await _db.Users.AnyAsync(cancellationToken);

        foreach (var apiType in AiProviderCatalog.All.Where(apiType => builtIns.All(row => row.ApiType != apiType)))
        {
            var local = !AiProviderCatalog.RequiresKey(apiType);
            var row = new AiProviderConfig
            {
                Name = AiProviderCatalog.DisplayName(apiType),
                ApiType = apiType,
                Locality = AiProviderCatalog.Locality(apiType),
                IsBuiltIn = true,
                // Ollama needs no key and runs on this machine, so TFA can work as soon as it is installed.
                IsEnabled = local && freshInstall,
                ModelName = AiProviderCatalog.DefaultModel(apiType),
                BaseUrl = local ? AiProviderCatalog.DefaultOllamaBaseUrl : null,
                UpdatedAt = now
            };
            _db.AiProviderConfigs.Add(row);
            builtIns.Add(row);
        }

        return builtIns;
    }

    /// <summary>A model that was built in once but is no longer in <see cref="AiProviderCatalog.BuiltInModels"/> (Google
    /// retires Gemini models, for example) becomes an administrator-added one, so it can be removed in Settings. A provider
    /// nobody has set up yet (switched off, no key) that still names such a model moves to the current default; a provider
    /// an administrator set up keeps its model.</summary>
    private async System.Threading.Tasks.Task RetireOldBuiltInModelsAsync(List<AiProviderConfig> builtIns, CancellationToken cancellationToken)
    {
        var builtInModels = await _db.AiModels.Where(model => model.IsBuiltIn).ToListAsync(cancellationToken);
        var retired = builtInModels
            .Where(model => builtIns.FirstOrDefault(row => row.Id == model.ProviderId) is not { } row
                || !AiProviderCatalog.BuiltInModels(row.ApiType).Contains(model.Name))
            .ToList();
        if (retired.Count == 0)
        {
            return;
        }

        foreach (var model in retired)
        {
            model.IsBuiltIn = false;
        }

        var retiredNames = retired.Select(model => (model.ProviderId, model.Name)).ToHashSet();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var config in builtIns.Where(config => !config.IsEnabled && config.EncryptedApiKey == null
                     && retiredNames.Contains((config.Id, config.ModelName))))
        {
            config.ModelName = AiProviderCatalog.DefaultModel(config.ApiType);
            config.UpdatedAt = now;
        }
    }

    private async System.Threading.Tasks.Task SeedModelsAsync(List<AiProviderConfig> builtIns, CancellationToken cancellationToken)
    {
        var known = (await _db.AiModels.AsNoTracking().Select(model => new { model.ProviderId, model.Name }).ToListAsync(cancellationToken))
            .Select(model => (model.ProviderId, model.Name))
            .ToHashSet();

        foreach (var row in builtIns)
        {
            foreach (var name in AiProviderCatalog.BuiltInModels(row.ApiType))
            {
                // A provider added just now has id 0 and nothing stored yet.
                if (row.Id == 0 || known.Add((row.Id, name)))
                {
                    _db.AiModels.Add(new AiModel { Provider = row, Name = name, IsBuiltIn = true });
                }
            }
        }

        // A model an administrator typed in before this list existed stays selectable.
        var saved = await _db.AiProviderConfigs.AsNoTracking().Select(config => new { config.Id, config.ModelName }).ToListAsync(cancellationToken);
        foreach (var config in saved)
        {
            if (!string.IsNullOrWhiteSpace(config.ModelName) && known.Add((config.Id, config.ModelName))
                && !_db.AiModels.Local.Any(model => model.ProviderId == config.Id && model.Name == config.ModelName))
            {
                _db.AiModels.Add(new AiModel { ProviderId = config.Id, Name = config.ModelName, IsBuiltIn = false });
            }
        }
    }
}
