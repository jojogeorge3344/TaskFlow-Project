using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Assistant;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Assistant;

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
        await SeedModelsAsync(cancellationToken);
        await SeedProvidersAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
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

        foreach (var kind in AiProviderCatalog.All.Where(kind => !present.Contains(kind)))
        {
            var local = !AiProviderCatalog.RequiresKey(kind);
            _db.AiProviderConfigs.Add(new AiProviderConfig
            {
                Kind = kind,
                // Ollama needs no key and runs on this machine, so TFA can work as soon as it is installed.
                IsEnabled = local,
                ModelName = AiProviderCatalog.DefaultModel(kind),
                BaseUrl = local ? AiProviderCatalog.DefaultOllamaBaseUrl : null,
                UpdatedAt = now
            });
        }
    }
}
