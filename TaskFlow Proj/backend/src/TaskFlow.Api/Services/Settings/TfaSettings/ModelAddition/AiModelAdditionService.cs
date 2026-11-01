using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Enums;
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
        AiProviderKind kind,
        AddAiModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var runtime = await _providers.ResolveCandidateRuntimeAsync(kind, request, cancellationToken);
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
        AiProviderKind kind,
        string currentName,
        AddAiModelRequest request,
        CancellationToken cancellationToken = default)
    {
        await _providers.EnsureEditableAsync(kind, currentName, "edit", cancellationToken);
        var newName = AiProviderCatalog.NormalizeModelName(kind, request.ModelName);

        if (request.ClearKey == true)
        {
            if (newName != currentName || !string.IsNullOrWhiteSpace(request.BaseUrl))
            {
                throw new RequestValidationException("clearKey", "Remove the key on its own, without other changes.");
            }

            var cleared = await _providers.ClearKeyAsync(userId, kind, cancellationToken);
            return new AddAiModelResultDto(true, "The key was removed.", null, cleared);
        }

        if (newName != currentName && await _providers.IsKnownModelAsync(kind, newName, cancellationToken))
        {
            throw new RequestValidationException("modelName", AiProviderConfigService.DuplicateMessage);
        }

        var runtime = await _providers.ResolveCandidateRuntimeAsync(kind, request, cancellationToken);
        var test = await _tester.TestCandidateAsync(runtime, cancellationToken);
        if (!test.Ok)
        {
            return new AddAiModelResultDto(false, test.Message, test.FailureKind, null);
        }

        var saveKey = !string.IsNullOrWhiteSpace(request.ApiKey);
        var providers = await _providers.UpdateModelAsync(userId, kind, currentName, runtime, saveKey, cancellationToken);
        return new AddAiModelResultDto(true, test.Message, null, providers);
    }

    public async Task<AiProviderTestResultDto> TestSavedModelAsync(
        AiProviderKind kind,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        var runtime = await _providers.GetSavedRuntimeForModelAsync(kind, modelName, cancellationToken);
        return runtime is null
            ? new AiProviderTestResultDto(false, AiProviderConfigService.NoKeyMessage(kind), null)
            : await _tester.TestCandidateAsync(runtime, cancellationToken);
    }
}
