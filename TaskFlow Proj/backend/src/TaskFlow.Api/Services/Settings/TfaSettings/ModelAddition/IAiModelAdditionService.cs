using TaskFlow.Api.Dtos.Settings.TfaSettings;

namespace TaskFlow.Api.Services.Settings.TfaSettings.ModelAddition;

/// <summary>The Add AI model page: tests a model with the typed values and, only when the test passes, saves it (creating
/// a custom provider when a new one was typed) and makes it the one TFA uses. A failed test is a result, not an error, and
/// saves nothing.</summary>
public interface IAiModelAdditionService
{
    /// <summary>The request names the provider by <see cref="AddAiModelRequest.ProviderId"/> or
    /// <see cref="AddAiModelRequest.NewProvider"/>.</summary>
    Task<AddAiModelResultDto> AddAndActivateAsync(int userId, AddAiModelRequest request, CancellationToken cancellationToken = default);

    /// <summary>The edit screen: refuses a built-in model, the one TFA uses, or a name already listed on the target; tests
    /// the typed values on the target provider (the same one, another existing one, or a new one); and only when the test
    /// passes renames or moves the model and saves the key or address. Which model TFA uses is unchanged.
    /// <see cref="AddAiModelRequest.ClearKey"/> removes the provider's key without a test.</summary>
    Task<AddAiModelResultDto> EditAsync(int userId, int providerId, string currentName, AddAiModelRequest request, CancellationToken cancellationToken = default);

    /// <summary>Tests a listed model with the saved key or address, whether or not TFA uses that provider. Nothing is saved.</summary>
    Task<AiProviderTestResultDto> TestSavedModelAsync(int providerId, string modelName, CancellationToken cancellationToken = default);
}
