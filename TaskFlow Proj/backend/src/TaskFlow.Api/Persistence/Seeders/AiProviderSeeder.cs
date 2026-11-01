using TaskFlow.Api.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.KeyProtection;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Persistence.Seeders;

/// <summary>Puts the AI defaults into the database the first time the application runs on any machine: a built-in
/// provider row for Google Gemini, OpenAI GPT and Ollama (all off), and the models offered for each. A Gemini API key
/// supplied in configuration (<c>AiProviders:GeminiApiKey</c>, from user secrets or an environment variable) is stored
/// encrypted on the Gemini row when that row has no key, and on a new installation Gemini is then switched on, so TFA
/// works on the first run. It only adds what is missing, so it is safe to run at every start and never overwrites what an
/// administrator saved. Custom providers are never touched. The key is never logged.</summary>
public class AiProviderSeeder
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IAiKeyProtector? _keyProtector;
    private readonly string? _configuredGeminiKey;

    public AiProviderSeeder(AppDbContext db, TimeProvider timeProvider, IAiKeyProtector? keyProtector = null, IOptions<AiProviderOptions>? options = null)
    {
        _db = db;
        _timeProvider = timeProvider;
        _keyProtector = keyProtector;
        _configuredGeminiKey = options?.Value.GeminiApiKey?.Trim();
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

        // A brand-new installation has no user yet, because this runs before the users are seeded. Only there may Gemini be
        // switched on; an installation that is being upgraded keeps whichever provider an administrator chose.
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
                IsEnabled = false,
                ModelName = AiProviderCatalog.DefaultModel(apiType),
                BaseUrl = local ? AiProviderCatalog.DefaultOllamaBaseUrl : null,
                UpdatedAt = now
            };
            _db.AiProviderConfigs.Add(row);
            builtIns.Add(row);
        }

        ApplyConfiguredGeminiKey(builtIns, freshInstall, now);
        return builtIns;
    }

    /// <summary>Stores the configured Gemini key on a Gemini row that has none (design D1). A key saved in TFA Settings is
    /// never replaced, and on an existing installation no provider is switched on or off.</summary>
    private void ApplyConfiguredGeminiKey(List<AiProviderConfig> builtIns, bool freshInstall, DateTime now)
    {
        if (string.IsNullOrEmpty(_configuredGeminiKey) || _keyProtector is null
            || builtIns.FirstOrDefault(row => row.ApiType == AiApiType.Gemini) is not { } gemini
            || !string.IsNullOrEmpty(gemini.EncryptedApiKey))
        {
            return;
        }

        gemini.EncryptedApiKey = _keyProtector.Protect(_configuredGeminiKey);
        gemini.KeyLast4 = _configuredGeminiKey.Length <= 4 ? _configuredGeminiKey : _configuredGeminiKey[^4..];
        gemini.UpdatedAt = now;
        if (freshInstall && builtIns.All(row => !row.IsEnabled))
        {
            gemini.IsEnabled = true;
        }
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
