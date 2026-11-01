using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Common.Exceptions;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.KeyProtection;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

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

            var modelName = AiProviderCatalog.NormalizeModelName(kind, update.ModelName);
            if (modelName.Length == 0 || modelName.Length > MaxModelNameLength)
            {
                throw new RequestValidationException("providers", $"The {AiProviderCatalog.DisplayName(kind)} model name must be 1 to {MaxModelNameLength} characters.");
            }

            var row = rows.FirstOrDefault(existing => existing.Kind == kind);

            // A name already stored for a switched-off provider is left alone, so an older entry never blocks the save;
            // it is checked again once that provider is switched on.
            if ((enabled || modelName != row?.ModelName) && !AiProviderCatalog.IsValidModelId(kind, modelName))
            {
                throw new RequestValidationException("providers", $"{AiProviderCatalog.DisplayName(kind)}: {AiProviderCatalog.ModelIdMessage(kind)}");
            }

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
            throw new RequestValidationException("name", BuiltInMessage);
        }

        if (await IsInUseAsync(kind, name, cancellationToken))
        {
            throw new RequestValidationException("name", InUseMessage(name, "delete"));
        }

        // A switched-off provider that still has this model saved goes back to its default model.
        foreach (var row in (await _configs.GetAllForUpdateAsync(cancellationToken)).Where(row => row.Kind == kind && row.ModelName == name))
        {
            row.ModelName = AiProviderCatalog.DefaultModel(kind);
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

    public async Task<AiProviderRuntime> ResolveCandidateRuntimeAsync(
        AiProviderKind kind,
        AddAiModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var modelName = AiProviderCatalog.NormalizeModelName(kind, request.ModelName);
        if (!AiProviderCatalog.IsValidModelId(kind, modelName))
        {
            throw new RequestValidationException("modelName", AiProviderCatalog.ModelIdMessage(kind));
        }

        var typedKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim();
        var row = await _configs.GetAsync(kind, cancellationToken);

        if (!AiProviderCatalog.RequiresKey(kind))
        {
            if (typedKey is not null)
            {
                throw new RequestValidationException("apiKey", $"{AiProviderCatalog.DisplayName(kind)} does not use an API key.");
            }

            var baseUrl = NormalizeBaseUrl(request.BaseUrl) ?? NormalizeBaseUrl(row?.BaseUrl) ?? AiProviderCatalog.DefaultOllamaBaseUrl;
            return new AiProviderRuntime(kind, modelName, baseUrl, null);
        }

        var apiKey = typedKey
            ?? (string.IsNullOrEmpty(row?.EncryptedApiKey) ? null : _keyProtector.Unprotect(row.EncryptedApiKey))
            ?? throw new RequestValidationException("apiKey", "Enter the API key.");
        return new AiProviderRuntime(kind, modelName, null, apiKey);
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> ActivateModelAsync(
        int userId,
        AiProviderRuntime runtime,
        bool saveKey,
        CancellationToken cancellationToken = default)
    {
        var rows = await _configs.GetAllForUpdateAsync(cancellationToken);

        var row = rows.FirstOrDefault(existing => existing.Kind == runtime.Kind);
        if (row is null)
        {
            row = new AiProviderConfig { Kind = runtime.Kind };
            _configs.Add(row);
            rows.Add(row);
        }

        await RememberModelAsync(runtime.Kind, runtime.ModelName, cancellationToken);
        row.ModelName = runtime.ModelName;
        if (runtime.Kind == AiProviderKind.Ollama)
        {
            row.BaseUrl = runtime.BaseUrl ?? AiProviderCatalog.DefaultOllamaBaseUrl;
        }
        else if (saveKey && runtime.ApiKey is { } key)
        {
            row.EncryptedApiKey = _keyProtector.Protect(key);
            row.KeyLast4 = key.Length <= 4 ? key : key[^4..];
        }

        SwitchTo(rows, row, userId);
        await SaveKeepingAnyDuplicateModelAsync(cancellationToken);
        var models = await LoadModelsAsync(cancellationToken);
        return AiProviderCatalog.All.Select(kind => ToDto(kind, rows.FirstOrDefault(existing => existing.Kind == kind), models)).ToList();
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> UseModelAsync(
        int userId,
        AiProviderKind kind,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        var name = AiProviderCatalog.NormalizeModelName(kind, modelName);
        if (!await _models.IsKnownAsync(kind, name, cancellationToken))
        {
            throw new NotFoundException("That model is not in the list.");
        }

        var rows = await _configs.GetAllForUpdateAsync(cancellationToken);
        var row = rows.FirstOrDefault(existing => existing.Kind == kind);
        if (AiProviderCatalog.RequiresKey(kind) && ReadableKey(row) is null)
        {
            throw new RequestValidationException("modelName", NoKeyMessage(kind));
        }

        if (row is null)
        {
            row = new AiProviderConfig { Kind = kind };
            _configs.Add(row);
            rows.Add(row);
        }

        row.ModelName = name;
        if (kind == AiProviderKind.Ollama)
        {
            row.BaseUrl ??= AiProviderCatalog.DefaultOllamaBaseUrl;
        }

        SwitchTo(rows, row, userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAdminConfigAsync(cancellationToken);
    }

    public async Task<AiProviderRuntime?> GetSavedRuntimeForModelAsync(
        AiProviderKind kind,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        var name = AiProviderCatalog.NormalizeModelName(kind, modelName);
        if (!await _models.IsKnownAsync(kind, name, cancellationToken))
        {
            throw new NotFoundException("That model is not in the list.");
        }

        var row = await _configs.GetAsync(kind, cancellationToken);
        if (!AiProviderCatalog.RequiresKey(kind))
        {
            return new AiProviderRuntime(kind, name, NormalizeBaseUrl(row?.BaseUrl) ?? AiProviderCatalog.DefaultOllamaBaseUrl, null);
        }

        return ReadableKey(row) is { } key ? new AiProviderRuntime(kind, name, null, key) : null;
    }

    public async System.Threading.Tasks.Task EnsureEditableAsync(
        AiProviderKind kind,
        string name,
        string action,
        CancellationToken cancellationToken = default)
    {
        var model = await _models.GetForUpdateAsync(kind, name, cancellationToken)
            ?? throw new NotFoundException("That model is not in the list.");

        if (model.IsBuiltIn)
        {
            throw new RequestValidationException("name", BuiltInMessage);
        }

        if (await IsInUseAsync(kind, name, cancellationToken))
        {
            throw new RequestValidationException("name", InUseMessage(name, action));
        }
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> UpdateModelAsync(
        int userId,
        AiProviderKind kind,
        string currentName,
        AiProviderRuntime runtime,
        bool saveKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureEditableAsync(kind, currentName, "edit", cancellationToken);
        var model = (await _models.GetForUpdateAsync(kind, currentName, cancellationToken))!;
        var rows = await _configs.GetAllForUpdateAsync(cancellationToken);
        var row = rows.FirstOrDefault(existing => existing.Kind == kind);
        if (row is null)
        {
            row = new AiProviderConfig { Kind = kind, ModelName = AiProviderCatalog.DefaultModel(kind) };
            _configs.Add(row);
        }

        if (runtime.ModelName != currentName)
        {
            model.Name = runtime.ModelName;

            // A switched-off provider that still has the old name saved follows the rename.
            if (row.ModelName == currentName)
            {
                row.ModelName = runtime.ModelName;
            }
        }

        if (kind == AiProviderKind.Ollama)
        {
            row.BaseUrl = runtime.BaseUrl ?? AiProviderCatalog.DefaultOllamaBaseUrl;
        }
        else if (saveKey && runtime.ApiKey is { } key)
        {
            row.EncryptedApiKey = _keyProtector.Protect(key);
            row.KeyLast4 = key.Length <= 4 ? key : key[^4..];
        }

        row.UpdatedByUserId = userId;
        row.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            throw new RequestValidationException("modelName", DuplicateMessage);
        }

        return await GetAdminConfigAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> ClearKeyAsync(
        int userId,
        AiProviderKind kind,
        CancellationToken cancellationToken = default)
    {
        if (!AiProviderCatalog.RequiresKey(kind))
        {
            throw new RequestValidationException("apiKey", $"{AiProviderCatalog.DisplayName(kind)} does not use an API key.");
        }

        var row = (await _configs.GetAllForUpdateAsync(cancellationToken)).FirstOrDefault(existing => existing.Kind == kind);
        if (row is not null)
        {
            row.EncryptedApiKey = null;
            row.KeyLast4 = null;
            row.UpdatedByUserId = userId;
            row.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAdminConfigAsync(cancellationToken);
    }

    public Task<bool> IsKnownModelAsync(AiProviderKind kind, string name, CancellationToken cancellationToken = default) =>
        _models.IsKnownAsync(kind, name, cancellationToken);

    internal const string BuiltInMessage = "A built-in model cannot be changed or removed.";
    internal const string DuplicateMessage = "That model is already in the list.";

    internal static string InUseMessage(string name, string action) =>
        $"{name} is in use by TFA. Switch TFA to another model first, then {action} it.";

    internal static string NoKeyMessage(AiProviderKind kind) =>
        $"No key saved for {AiProviderCatalog.DisplayName(kind)} yet. Add it on the Add AI model screen.";

    /// <summary>TFA's active model: the one saved on the enabled provider. A name saved only on a switched-off provider is
    /// not in use.</summary>
    private async Task<bool> IsInUseAsync(AiProviderKind kind, string name, CancellationToken cancellationToken)
    {
        var row = await _configs.GetAsync(kind, cancellationToken);
        return row is { IsEnabled: true } && row.ModelName == name;
    }

    private string? ReadableKey(AiProviderConfig? row) =>
        string.IsNullOrEmpty(row?.EncryptedApiKey) ? null : _keyProtector.Unprotect(row.EncryptedApiKey);

    /// <summary>TFA uses one provider: <paramref name="row"/>'s. The others are switched off; their keys and models stay stored.</summary>
    private void SwitchTo(IEnumerable<AiProviderConfig> rows, AiProviderConfig row, int userId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var other in rows)
        {
            if (other == row || other.IsEnabled)
            {
                other.IsEnabled = other == row;
                other.UpdatedByUserId = userId;
                other.UpdatedAt = now;
            }
        }
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
