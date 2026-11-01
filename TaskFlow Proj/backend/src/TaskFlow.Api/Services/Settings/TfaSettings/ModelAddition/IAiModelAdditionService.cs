using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Services.Settings.TfaSettings.ModelAddition;

/// <summary>The Add AI model page: tests a model with the typed values and, only when the test passes, saves it and makes
/// it the one TFA uses. A failed test is a result, not an error, and saves nothing.</summary>
public interface IAiModelAdditionService
{
    Task<AddAiModelResultDto> AddAndActivateAsync(int userId, AiProviderKind kind, AddAiModelRequest request, CancellationToken cancellationToken = default);

    /// <summary>The edit screen: refuses a built-in model, the one TFA uses, or a new name already listed; tests the typed
    /// values; and only when the test passes renames the model and saves the key or address. Which model TFA uses is
    /// unchanged. <see cref="AddAiModelRequest.ClearKey"/> removes the provider's key without a test.</summary>
    Task<AddAiModelResultDto> EditAsync(int userId, AiProviderKind kind, string currentName, AddAiModelRequest request, CancellationToken cancellationToken = default);

    /// <summary>Tests a listed model with the saved key or address, whether or not TFA uses that provider. Nothing is saved.</summary>
    Task<AiProviderTestResultDto> TestSavedModelAsync(AiProviderKind kind, string modelName, CancellationToken cancellationToken = default);
}
