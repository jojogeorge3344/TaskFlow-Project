using TaskFlow.Api.Integrations.Ai;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Common.Exceptions;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Settings.TfaSettings;

namespace TaskFlow.Api.Services.Settings.TfaSettings;

public class AiProviderConfigService : IAiProviderConfigService
{
    /// <summary>The longest model name that is stored (the length of the AiModels.Name column).</summary>
    private const int MaxModelNameLength = 100;

    private readonly IAiProviderConfigRepository _configs;
    private readonly IAiModelRepository _models;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAiKeyProtector _keyProtector;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AiProviderConfigService>? _logger;

    public AiProviderConfigService(
        IAiProviderConfigRepository configs,
        IAiModelRepository models,
        IUnitOfWork unitOfWork,
        IAiKeyProtector keyProtector,
        TimeProvider timeProvider,
        ILogger<AiProviderConfigService>? logger = null)
    {
        _logger = logger;
        _configs = configs;
        _models = models;
        _unitOfWork = unitOfWork;
        _keyProtector = keyProtector;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<AiProviderOptionDto>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var configs = await GetAdminConfigAsync(cancellationToken);
        return configs
            .Where(config => config.IsConfigured)
            .Select(config => new AiProviderOptionDto(config.Kind, config.DisplayName, config.ModelName, config.Locality))
            .ToList();
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> GetAdminConfigAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _configs.GetAllAsync(cancellationToken);
        var models = await LoadModelsAsync(cancellationToken);
        return AiProviderCatalog.All.Select(kind => ToDto(kind, rows.FirstOrDefault(row => row.Kind == kind), models)).ToList();
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> SaveAsync(
        int userId,
        UpdateAiProvidersRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rows = await _configs.GetAllForUpdateAsync(cancellationToken);

        foreach (var update in request.Providers ?? [])
        {
            if (update.Kind is not { } kind || update.IsEnabled is not { } enabled || update.ModelName is null)
            {
                throw new RequestValidationException("providers", "Every provider setting must be provided.");
            }

            var modelName = update.ModelName.Trim();
            if (modelName.Length == 0 || modelName.Length > MaxModelNameLength)
            {
                throw new RequestValidationException("providers", $"The {AiProviderCatalog.DisplayName(kind)} model name must be 1 to {MaxModelNameLength} characters.");
            }

            var row = rows.FirstOrDefault(existing => existing.Kind == kind);
            if (row is null)
            {
                row = new AiProviderConfig { Kind = kind };
                _configs.Add(row);
                rows.Add(row);
            }

            row.IsEnabled = enabled;
            row.ModelName = modelName;
            if (enabled)
            {
                // Only the model in use is added to the list, so a disabled server's leftover text never is.
                await RememberModelAsync(kind, modelName, cancellationToken);
            }

            row.BaseUrl = kind == AiProviderKind.Ollama
                ? NormalizeBaseUrl(update.BaseUrl) ?? AiProviderCatalog.DefaultOllamaBaseUrl
                : null;

            if (!string.IsNullOrEmpty(update.ApiKey))
            {
                if (!AiProviderCatalog.RequiresKey(kind))
                {
                    throw new RequestValidationException("providers", $"{AiProviderCatalog.DisplayName(kind)} does not use an API key.");
                }

                var key = update.ApiKey.Trim();
                row.EncryptedApiKey = _keyProtector.Protect(key);
                row.KeyLast4 = key.Length <= 4 ? key : key[^4..];
            }
            else if (update.ClearKey == true)
            {
                row.EncryptedApiKey = null;
                row.KeyLast4 = null;
            }

            row.UpdatedByUserId = userId;
            row.UpdatedAt = now;
        }

        await SaveKeepingAnyDuplicateModelAsync(cancellationToken);
        var models = await LoadModelsAsync(cancellationToken);
        return AiProviderCatalog.All.Select(kind => ToDto(kind, rows.FirstOrDefault(row => row.Kind == kind), models)).ToList();
    }

    /// <summary>The models offered for each provider: the built-in ones first, then those administrators added.</summary>
    private async Task<ModelLists> LoadModelsAsync(CancellationToken cancellationToken)
    {
        var models = await _models.GetAllAsync(cancellationToken);
        return new ModelLists(
            models.ToLookup(model => model.Kind, model => model.Name),
            models.Where(model => !model.IsBuiltIn).ToLookup(model => model.Kind, model => model.Name));
    }

    /// <summary>A model name typed in Settings that is not in the list yet is added to it, so it is offered from then on.</summary>
    private async System.Threading.Tasks.Task RememberModelAsync(AiProviderKind kind, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!await _models.IsKnownAsync(kind, name, cancellationToken))
        {
            _models.Add(new AiModel { Kind = kind, Name = name, IsBuiltIn = false });
        }
    }

    /// <summary>Every model of each provider, and the part of it that an administrator added (and so can remove).</summary>
    private sealed record ModelLists(ILookup<AiProviderKind, string> All, ILookup<AiProviderKind, string> Custom);

    /// <summary>Saves. If another administrator added the same new model at the same moment, the unique index rejects our
    /// copy: drop it, keep theirs, and save the rest instead of failing the whole request.</summary>
    private async System.Threading.Tasks.Task SaveKeepingAnyDuplicateModelAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            if (!await _models.DropAddedModelsStoredElsewhereAsync(cancellationToken))
            {
                throw;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> RemoveModelAsync(
        AiProviderKind kind,
        string name,
        CancellationToken cancellationToken = default)
    {
        var model = await _models.GetForUpdateAsync(kind, name, cancellationToken)
            ?? throw new NotFoundException("That model is not in the list.");

        if (model.IsBuiltIn)
        {
            throw new RequestValidationException("name", "A built-in model cannot be removed.");
        }

        if (await _configs.UsesModelAsync(kind, name, cancellationToken))
        {
            throw new RequestValidationException("name", "That model is the one saved for its server. Choose another model and save before removing it.");
        }

        _models.Remove(model);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAdminConfigAsync(cancellationToken);
    }

    public async Task<AiProviderRuntime?> GetRuntimeAsync(AiProviderKind kind, CancellationToken cancellationToken = default)
    {
        var row = await _configs.GetAsync(kind, cancellationToken);
        if (row is null || !IsConfigured(row))
        {
            if (row is { IsEnabled: true } && !string.IsNullOrEmpty(row.EncryptedApiKey) && _keyProtector.Unprotect(row.EncryptedApiKey) is null)
            {
                _logger?.LogWarning("The saved API key for {Provider} cannot be decrypted (key ring lost or changed); it must be entered again", kind);
            }

            return null;
        }

        var apiKey = AiProviderCatalog.RequiresKey(kind) ? _keyProtector.Unprotect(row.EncryptedApiKey!) : null;
        return new AiProviderRuntime(kind, row.ModelName, row.BaseUrl, apiKey);
    }

    private AiProviderConfigDto ToDto(AiProviderKind kind, AiProviderConfig? row, ModelLists models)
    {
        var offered = models.All[kind].ToList();
        var chosen = string.IsNullOrWhiteSpace(row?.ModelName) ? AiProviderCatalog.DefaultModel(kind) : row!.ModelName;
        if (!offered.Contains(chosen))
        {
            offered.Add(chosen);
        }

        var baseUrl = kind == AiProviderKind.Ollama ? row?.BaseUrl ?? AiProviderCatalog.DefaultOllamaBaseUrl : null;
        return new AiProviderConfigDto(
            kind,
            AiProviderCatalog.DisplayName(kind),
            AiProviderCatalog.Locality(kind),
            AiProviderCatalog.RequiresKey(kind),
            row?.IsEnabled ?? false,
            string.IsNullOrWhiteSpace(row?.ModelName) ? AiProviderCatalog.DefaultModel(kind) : row!.ModelName,
            baseUrl,
            !string.IsNullOrEmpty(row?.EncryptedApiKey),
            row?.KeyLast4,
            row is not null && IsConfigured(row),
            // A key that is stored but cannot be decrypted (for example the key ring was lost or restored under another
            // account) has to be entered again; the administrator is told so.
            string.IsNullOrEmpty(row?.EncryptedApiKey) || _keyProtector.Unprotect(row.EncryptedApiKey) is not null,
            offered,
            models.Custom[kind].ToList());
    }

    /// <summary>Enabled, has a model, and either is local with an address or has a key that can still be decrypted.</summary>
    private bool IsConfigured(AiProviderConfig row)
    {
        if (!row.IsEnabled || string.IsNullOrWhiteSpace(row.ModelName))
        {
            return false;
        }

        if (!AiProviderCatalog.RequiresKey(row.Kind))
        {
            return !string.IsNullOrWhiteSpace(row.BaseUrl);
        }

        return !string.IsNullOrEmpty(row.EncryptedApiKey) && _keyProtector.Unprotect(row.EncryptedApiKey) is not null;
    }

    private static string? NormalizeBaseUrl(string? baseUrl) =>
        string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim().TrimEnd('/');
}
