using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.Api.Services.Settings.TfaSettings.ModelAddition;

/// <summary>Kept apart from <see cref="AiProviderConfigService"/> because the connection tester depends on the chat
/// gateway, which depends on the config service: the config service cannot depend on the tester.</summary>
public class AiModelAdditionService : IAiModelAdditionService
{
    private readonly IAiProviderConfigService _providers;
    private readonly IAiProviderConnectionTester _tester;

    public AiModelAdditionService(IAiProviderConfigService providers, IAiProviderConnectionTester tester)
    {
        _providers = providers;
        _tester = tester;
    }

    public async Task<AddAiModelResultDto> AddAndActivateAsync(
        int userId,
        AddAiModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(request, cancellationToken)
            ?? throw new RequestValidationException("providerId", "Enter the AI provider.");

        var runtime = await _providers.ResolveCandidateRuntimeAsync(target.Provider, request, cancellationToken);
        var test = await _tester.TestCandidateAsync(runtime, cancellationToken);
        if (!test.Ok)
        {
            return new AddAiModelResultDto(false, test.Message, test.FailureKind, null);
        }

        var saveKey = !string.IsNullOrWhiteSpace(request.ApiKey);
        var providers = await _providers.ActivateModelAsync(userId, runtime, saveKey, cancellationToken);
        return new AddAiModelResultDto(true, test.Message, null, providers);
    }

    public async Task<AddAiModelResultDto> EditAsync(
        int userId,
        int providerId,
        string currentName,
        AddAiModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await _providers.GetProviderAsync(providerId, cancellationToken);
        await _providers.EnsureEditableAsync(providerId, currentName, "edit", cancellationToken);

        // Neither a provider id nor a new provider: the model stays on its own provider.
        var target = await ResolveTargetAsync(request, cancellationToken) ?? new Target(source);
        var moving = target.Provider?.Id != source.Id;

        if (request.ClearKey == true)
        {
            var newName = request.ModelName?.Trim();
            if (moving || newName != currentName || !string.IsNullOrWhiteSpace(request.BaseUrl))
            {
                throw new RequestValidationException("clearKey", "Remove the key on its own, without other changes.");
            }

            var cleared = await _providers.ClearKeyAsync(userId, providerId, cancellationToken);
            return new AddAiModelResultDto(true, "The key was removed.", null, cleared);
        }

        var runtime = await _providers.ResolveCandidateRuntimeAsync(target.Provider, request, cancellationToken);
        var renamedInPlace = !moving && runtime.ModelName != currentName;
        if ((moving || renamedInPlace) && target.Provider is { } existing
            && await _providers.IsKnownModelAsync(existing.Id, runtime.ModelName, cancellationToken))
        {
            throw new RequestValidationException("modelName", AiProviderConfigService.DuplicateMessage);
        }

        var test = await _tester.TestCandidateAsync(runtime, cancellationToken);
        if (!test.Ok)
        {
            return new AddAiModelResultDto(false, test.Message, test.FailureKind, null);
        }

        var saveKey = !string.IsNullOrWhiteSpace(request.ApiKey);
        var providers = await _providers.UpdateModelAsync(userId, providerId, currentName, runtime, saveKey, cancellationToken);
        return new AddAiModelResultDto(true, test.Message, null, providers);
    }

    public async Task<AiProviderTestResultDto> TestSavedModelAsync(
        int providerId,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetProviderAsync(providerId, cancellationToken);
        var runtime = await _providers.GetSavedRuntimeForModelAsync(providerId, modelName, cancellationToken);
        return runtime is null
            ? new AiProviderTestResultDto(false, AiProviderConfigService.NoKeyMessage(provider.Name), null)
            : await _tester.TestCandidateAsync(runtime, cancellationToken);
    }

    /// <summary>The provider the request names: an existing one by id, or a new one (checked, not created yet; its
    /// <see cref="Target.Provider"/> is null). Null when the request names neither.</summary>
    private async Task<Target?> ResolveTargetAsync(AddAiModelRequest request, CancellationToken cancellationToken)
    {
        if (request.ProviderId is { } id)
        {
            if (request.NewProvider is not null)
            {
                throw new RequestValidationException("newProvider", "Choose an existing provider or a new one, not both.");
            }

            return new Target(await _providers.GetProviderAsync(id, cancellationToken));
        }

        if (request.NewProvider is { } newProvider)
        {
            await _providers.ValidateNewProviderAsync(newProvider, cancellationToken);
            return new Target(null);
        }

        return null;
    }

    private sealed record Target(AiProviderConfig? Provider);
}
