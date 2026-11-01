using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Dtos.Settings.TfaSettings.Validators;
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

    private const int MinProviderNameLength = 2;
    private const int MaxProviderNameLength = 40;

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
            .Select(config => new AiProviderOptionDto(config.ProviderId, config.DisplayName, config.ModelName, config.Locality))
            .ToList();
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> GetAdminConfigAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _configs.GetAllAsync(cancellationToken);
        var models = await _models.GetAllAsync(cancellationToken);
        return ToDtos(rows, models);
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
            if (update.ProviderId is not { } providerId || update.IsEnabled is not { } enabled || update.ModelName is null)
            {
                throw new RequestValidationException("providers", "Every provider setting must be provided.");
            }

            var row = rows.FirstOrDefault(existing => existing.Id == providerId)
                ?? throw new NotFoundException("That AI provider does not exist.");

            var modelName = AiProviderCatalog.NormalizeModelName(row.ApiType, update.ModelName);
            if (modelName.Length == 0 || modelName.Length > MaxModelNameLength)
            {
                throw new RequestValidationException("providers", $"The {row.Name} model name must be 1 to {MaxModelNameLength} characters.");
            }

            // A name already stored for a switched-off provider is left alone, so an older entry never blocks the save;
            // it is checked again once that provider is switched on.
            if ((enabled || modelName != row.ModelName) && !AiProviderCatalog.IsValidModelId(row.ApiType, modelName))
            {
                throw new RequestValidationException("providers", $"{row.Name}: {AiProviderCatalog.ModelIdMessage(row.ApiType)}");
            }

            row.IsEnabled = enabled;
            row.ModelName = modelName;
            if (enabled)
            {
                // Only the model in use is added to the list, so a disabled server's leftover text never is.
                await RememberModelAsync(row, modelName, cancellationToken);
            }

            if (UsesAddress(row))
            {
                row.BaseUrl = NormalizeBaseUrl(update.BaseUrl) ?? row.BaseUrl ?? AiProviderCatalog.DefaultOllamaBaseUrl;
            }

            if (!string.IsNullOrEmpty(update.ApiKey))
            {
                if (!TakesKey(row))
                {
                    throw new RequestValidationException("providers", $"{row.Name} does not use an API key.");
                }

                SetKey(row, update.ApiKey.Trim());
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
        return await GetAdminConfigAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> RemoveModelAsync(
        int providerId,
        string name,
        CancellationToken cancellationToken = default)
    {
        await EnsureEditableAsync(providerId, name, "delete", cancellationToken);
        var model = (await _models.GetForUpdateAsync(providerId, name, cancellationToken))!;
        var row = (await _configs.GetAllForUpdateAsync(cancellationToken)).First(existing => existing.Id == providerId);

        _models.Remove(model);
        await ReleaseModelAsync(row, name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAdminConfigAsync(cancellationToken);
    }

    public async Task<AiProviderRuntime?> GetRuntimeAsync(int providerId, CancellationToken cancellationToken = default)
    {
        var row = await _configs.GetAsync(providerId, cancellationToken);
        if (row is null || !IsConfigured(row))
        {
            if (row is { IsEnabled: true } && !string.IsNullOrEmpty(row.EncryptedApiKey) && _keyProtector.Unprotect(row.EncryptedApiKey) is null)
            {
                _logger?.LogWarning("The saved API key for {Provider} cannot be decrypted (key ring lost or changed); it must be entered again", row.Name);
            }

            return null;
        }

        return Runtime(row, row.ModelName, row.BaseUrl, ReadableKey(row));
    }

    public async Task<AiProviderConfig> GetProviderAsync(int providerId, CancellationToken cancellationToken = default) =>
        await _configs.GetAsync(providerId, cancellationToken) ?? throw new NotFoundException("That AI provider does not exist.");

    public async Task<AiProviderRuntime> ResolveCandidateRuntimeAsync(
        AiProviderConfig? provider,
        AddAiModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var typedKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim();

        if (provider is null)
        {
            // A new provider: everything comes from the request, which ValidateNewProviderAsync has checked.
            var created = request.NewProvider!;
            var apiType = created.ApiType!.Value;
            var newName = ModelName(apiType, request.ModelName);
            return new AiProviderRuntime(
                0, apiType, created.Name!.Trim(), IsLocalLocality(created.Locality), newName, NormalizeBaseUrl(created.BaseUrl), typedKey);
        }

        var modelName = ModelName(provider.ApiType, request.ModelName);

        if (!TakesKey(provider) && typedKey is not null)
        {
            throw new RequestValidationException("apiKey", $"{provider.Name} does not use an API key.");
        }

        if (UsesAddress(provider))
        {
            var baseUrl = NormalizeBaseUrl(request.BaseUrl) ?? NormalizeBaseUrl(provider.BaseUrl) ?? AiProviderCatalog.DefaultOllamaBaseUrl;
            return Runtime(provider, modelName, baseUrl, typedKey ?? ReadableKey(provider));
        }

        var apiKey = typedKey ?? ReadableKey(provider);
        if (RequiresKey(provider) && apiKey is null)
        {
            throw new RequestValidationException("apiKey", "Enter the API key.");
        }

        return Runtime(provider, modelName, provider.BaseUrl, apiKey);
    }

    public async System.Threading.Tasks.Task ValidateNewProviderAsync(NewAiProviderRequest newProvider, CancellationToken cancellationToken = default)
    {
        var name = newProvider.Name?.Trim() ?? string.Empty;
        if (name.Length < MinProviderNameLength || name.Length > MaxProviderNameLength)
        {
            throw new RequestValidationException("newProvider.name", $"The provider name must be {MinProviderNameLength} to {MaxProviderNameLength} characters.");
        }

        if (newProvider.ApiType is not (AiApiType.OpenAI or AiApiType.Ollama))
        {
            throw new RequestValidationException("newProvider.apiType", CustomApiTypeMessage);
        }

        if (string.IsNullOrWhiteSpace(newProvider.BaseUrl))
        {
            throw new RequestValidationException("newProvider.baseUrl", "Enter the server address.");
        }

        if (!UpdateAiProviderRequestValidator.BeHttpUrl(newProvider.BaseUrl) || newProvider.BaseUrl.Trim().Length > 300)
        {
            throw new RequestValidationException("newProvider.baseUrl", UnsafeAddressMessage);
        }

        if (newProvider.Locality is { } locality && locality is not (AiProviderCatalog.Local or AiProviderCatalog.Cloud))
        {
            throw new RequestValidationException("newProvider.locality", "Choose Local or Cloud.");
        }

        if (await _configs.GetByNameAsync(name, cancellationToken) is not null)
        {
            throw new RequestValidationException("newProvider.name", "A provider with that name already exists.");
        }
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> ActivateModelAsync(
        int userId,
        AiProviderRuntime runtime,
        bool saveKey,
        CancellationToken cancellationToken = default)
    {
        var rows = await _configs.GetAllForUpdateAsync(cancellationToken);
        var row = runtime.ProviderId == 0 ? CreateProvider(rows, runtime) : rows.First(existing => existing.Id == runtime.ProviderId);

        await RememberModelAsync(row, runtime.ModelName, cancellationToken);
        row.ModelName = runtime.ModelName;
        StoreAccess(row, runtime, saveKey);

        SwitchTo(rows, row, userId);
        await SaveKeepingAnyDuplicateModelAsync(cancellationToken);
        return await GetAdminConfigAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> UseModelAsync(
        int userId,
        int providerId,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        var rows = await _configs.GetAllForUpdateAsync(cancellationToken);
        var row = rows.FirstOrDefault(existing => existing.Id == providerId)
            ?? throw new NotFoundException("That AI provider does not exist.");

        var name = AiProviderCatalog.NormalizeModelName(row.ApiType, modelName);
        if (!await _models.IsKnownAsync(providerId, name, cancellationToken))
        {
            throw new NotFoundException("That model is not in the list.");
        }

        if (RequiresKey(row) && ReadableKey(row) is null)
        {
            throw new RequestValidationException("modelName", NoKeyMessage(row.Name));
        }

        row.ModelName = name;
        if (UsesAddress(row))
        {
            row.BaseUrl ??= AiProviderCatalog.DefaultOllamaBaseUrl;
        }

        SwitchTo(rows, row, userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAdminConfigAsync(cancellationToken);
    }

    public async Task<AiProviderRuntime?> GetSavedRuntimeForModelAsync(
        int providerId,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        var row = await GetProviderAsync(providerId, cancellationToken);
        var name = AiProviderCatalog.NormalizeModelName(row.ApiType, modelName);
        if (!await _models.IsKnownAsync(providerId, name, cancellationToken))
        {
            throw new NotFoundException("That model is not in the list.");
        }

        if (UsesAddress(row))
        {
            return Runtime(row, name, NormalizeBaseUrl(row.BaseUrl) ?? AiProviderCatalog.DefaultOllamaBaseUrl, ReadableKey(row));
        }

        var key = ReadableKey(row);
        return RequiresKey(row) && key is null ? null : Runtime(row, name, row.BaseUrl, key);
    }

    public async System.Threading.Tasks.Task EnsureEditableAsync(
        int providerId,
        string name,
        string action,
        CancellationToken cancellationToken = default)
    {
        var model = await _models.GetForUpdateAsync(providerId, name, cancellationToken)
            ?? throw new NotFoundException("That model is not in the list.");

        if (model.IsBuiltIn)
        {
            throw new RequestValidationException("name", BuiltInMessage);
        }

        var row = await _configs.GetAsync(providerId, cancellationToken);
        if (row is { IsEnabled: true } && row.ModelName == name)
        {
            throw new RequestValidationException("name", InUseMessage(name, action));
        }
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> UpdateModelAsync(
        int userId,
        int sourceProviderId,
        string currentName,
        AiProviderRuntime target,
        bool saveKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureEditableAsync(sourceProviderId, currentName, "edit", cancellationToken);
        var model = (await _models.GetForUpdateAsync(sourceProviderId, currentName, cancellationToken))!;
        var rows = await _configs.GetAllForUpdateAsync(cancellationToken);
        var source = rows.First(existing => existing.Id == sourceProviderId);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (target.ProviderId == sourceProviderId)
        {
            if (target.ModelName != currentName)
            {
                model.Name = target.ModelName;

                // A switched-off provider that still has the old name saved follows the rename.
                if (source.ModelName == currentName)
                {
                    source.ModelName = target.ModelName;
                }
            }

            StoreAccess(source, target, saveKey);
            source.UpdatedByUserId = userId;
            source.UpdatedAt = now;
        }
        else
        {
            // A move: the model leaves its provider (which keeps working with its default, or goes away when it is a
            // custom provider with nothing left) and joins the target, created now when it is new.
            var destination = target.ProviderId == 0 ? CreateProvider(rows, target) : rows.First(existing => existing.Id == target.ProviderId);
            _models.Remove(model);
            _models.Add(new AiModel { Provider = destination, ProviderId = destination.Id, Name = target.ModelName, IsBuiltIn = false });
            StoreAccess(destination, target, saveKey);
            destination.UpdatedByUserId = userId;
            destination.UpdatedAt = now;
            await ReleaseModelAsync(source, currentName, cancellationToken);
        }

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
        int providerId,
        CancellationToken cancellationToken = default)
    {
        var row = (await _configs.GetAllForUpdateAsync(cancellationToken)).FirstOrDefault(existing => existing.Id == providerId)
            ?? throw new NotFoundException("That AI provider does not exist.");

        if (!TakesKey(row))
        {
            throw new RequestValidationException("apiKey", $"{row.Name} does not use an API key.");
        }

        row.EncryptedApiKey = null;
        row.KeyLast4 = null;
        row.UpdatedByUserId = userId;
        row.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAdminConfigAsync(cancellationToken);
    }

    public Task<bool> IsKnownModelAsync(int providerId, string name, CancellationToken cancellationToken = default) =>
        _models.IsKnownAsync(providerId, name, cancellationToken);

    internal const string BuiltInMessage = "A built-in model cannot be changed or removed.";
    internal const string DuplicateMessage = "That model is already in the list.";
    internal const string CustomApiTypeMessage = "A custom provider can use the OpenAI-compatible or Ollama API.";
    internal const string UnsafeAddressMessage = "The server address must be a valid http or https URL, and not a link-local or cloud metadata address.";

    internal static string InUseMessage(string name, string action) =>
        $"{name} is in use by TFA. Switch TFA to another model first, then {action} it.";

    internal static string NoKeyMessage(string providerName) =>
        $"No key saved for {providerName} yet. Add it on the Add AI model screen.";

    /// <summary>Built-in Gemini and OpenAI need a key; Ollama and custom providers (a local server may need none) do not.</summary>
    internal static bool RequiresKey(AiProviderConfig row) => row.IsBuiltIn && row.ApiType != AiApiType.Ollama;

    /// <summary>Whether a key may be stored: every provider except an Ollama one.</summary>
    private static bool TakesKey(AiProviderConfig row) => row.ApiType != AiApiType.Ollama;

    /// <summary>Ollama and custom providers are called at a stored address; built-in Gemini and OpenAI at a fixed one.</summary>
    private static bool UsesAddress(AiProviderConfig row) => row.ApiType == AiApiType.Ollama || !row.IsBuiltIn;

    private static bool IsLocalLocality(string? locality) => locality == AiProviderCatalog.Local;

    private static string ModelName(AiApiType apiType, string? typed)
    {
        var name = AiProviderCatalog.NormalizeModelName(apiType, typed);
        if (!AiProviderCatalog.IsValidModelId(apiType, name))
        {
            throw new RequestValidationException("modelName", AiProviderCatalog.ModelIdMessage(apiType));
        }

        return name;
    }

    private static AiProviderRuntime Runtime(AiProviderConfig row, string modelName, string? baseUrl, string? apiKey) =>
        new(row.Id, row.ApiType, row.Name, IsLocalLocality(row.Locality), modelName, baseUrl, apiKey);

    private AiProviderConfig CreateProvider(List<AiProviderConfig> rows, AiProviderRuntime runtime)
    {
        var row = new AiProviderConfig
        {
            Name = runtime.Name,
            ApiType = runtime.ApiType,
            Locality = runtime.IsLocal ? AiProviderCatalog.Local : AiProviderCatalog.Cloud,
            IsBuiltIn = false,
            ModelName = runtime.ModelName,
            BaseUrl = runtime.BaseUrl,
            UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _configs.Add(row);
        rows.Add(row);
        return row;
    }

    /// <summary>Stores what a tested runtime used: Ollama's or a custom provider's address, and the key when it was typed.</summary>
    private void StoreAccess(AiProviderConfig row, AiProviderRuntime runtime, bool saveKey)
    {
        if (UsesAddress(row))
        {
            row.BaseUrl = runtime.BaseUrl ?? row.BaseUrl ?? AiProviderCatalog.DefaultOllamaBaseUrl;
        }

        if (saveKey && TakesKey(row) && runtime.ApiKey is { } key)
        {
            SetKey(row, key);
        }
    }

    private void SetKey(AiProviderConfig row, string key)
    {
        row.EncryptedApiKey = _keyProtector.Protect(key);
        row.KeyLast4 = key.Length <= 4 ? key : key[^4..];
    }

    /// <summary>After a model left a provider: a switched-off provider that still has it saved goes back to its default
    /// model, and a custom provider with no models left is removed with its key. Built-in providers always stay.</summary>
    private async System.Threading.Tasks.Task ReleaseModelAsync(AiProviderConfig row, string name, CancellationToken cancellationToken)
    {
        var remaining = (await _models.GetAllAsync(cancellationToken)).Count(model => model.ProviderId == row.Id && model.Name != name);
        if (!row.IsBuiltIn && remaining == 0)
        {
            _configs.Remove(row);
            return;
        }

        if (row.ModelName == name)
        {
            row.ModelName = row.IsBuiltIn
                ? AiProviderCatalog.DefaultModel(row.ApiType)
                : (await _models.GetAllAsync(cancellationToken)).First(model => model.ProviderId == row.Id && model.Name != name).Name;
        }
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

    /// <summary>A model name typed in Settings that is not in the list yet is added to it, so it is offered from then on.</summary>
    private async System.Threading.Tasks.Task RememberModelAsync(AiProviderConfig row, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (row.Id == 0 || !await _models.IsKnownAsync(row.Id, name, cancellationToken))
        {
            _models.Add(new AiModel { Provider = row, ProviderId = row.Id, Name = name, IsBuiltIn = false });
        }
    }

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

    /// <summary>Built-in providers first in catalog order (Gemini, OpenAI, Ollama), then custom ones by name.</summary>
    private IReadOnlyList<AiProviderConfigDto> ToDtos(IReadOnlyList<AiProviderConfig> rows, IReadOnlyList<AiModel> models) =>
        rows
            .OrderByDescending(row => row.IsBuiltIn)
            .ThenBy(row => row.IsBuiltIn ? AiProviderCatalog.All.ToList().IndexOf(row.ApiType) : 0)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Select(row => ToDto(row, models.Where(model => model.ProviderId == row.Id).ToList()))
            .ToList();

    private AiProviderConfigDto ToDto(AiProviderConfig row, IReadOnlyList<AiModel> models)
    {
        var offered = models.Select(model => model.Name).ToList();
        var chosen = string.IsNullOrWhiteSpace(row.ModelName) ? AiProviderCatalog.DefaultModel(row.ApiType) : row.ModelName;
        if (!offered.Contains(chosen))
        {
            offered.Add(chosen);
        }

        var baseUrl = UsesAddress(row) ? row.BaseUrl ?? AiProviderCatalog.DefaultOllamaBaseUrl : null;
        return new AiProviderConfigDto(
            row.Id,
            row.Name,
            row.ApiType,
            row.IsBuiltIn,
            row.Locality,
            RequiresKey(row),
            row.IsEnabled,
            chosen,
            baseUrl,
            !string.IsNullOrEmpty(row.EncryptedApiKey),
            row.KeyLast4,
            IsConfigured(row),
            // A key that is stored but cannot be decrypted (for example the key ring was lost or restored under another
            // account) has to be entered again; the administrator is told so.
            string.IsNullOrEmpty(row.EncryptedApiKey) || _keyProtector.Unprotect(row.EncryptedApiKey) is not null,
            offered,
            models.Where(model => !model.IsBuiltIn).Select(model => model.Name).ToList());
    }

    /// <summary>Enabled, has a model, and has what a call needs: an address for Ollama and custom providers, a key that
    /// can still be decrypted for built-in Gemini and OpenAI.</summary>
    private bool IsConfigured(AiProviderConfig row)
    {
        if (!row.IsEnabled || string.IsNullOrWhiteSpace(row.ModelName))
        {
            return false;
        }

        if (UsesAddress(row))
        {
            return !string.IsNullOrWhiteSpace(row.BaseUrl) || row.ApiType == AiApiType.Ollama;
        }

        return ReadableKey(row) is not null;
    }

    private static string? NormalizeBaseUrl(string? baseUrl) =>
        string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim().TrimEnd('/');
}
