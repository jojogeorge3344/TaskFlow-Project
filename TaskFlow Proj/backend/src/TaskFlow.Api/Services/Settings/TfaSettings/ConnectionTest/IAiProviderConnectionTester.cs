using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.Providers;

namespace TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest;

/// <summary>Sends one tiny request to a provider so an administrator can see whether it works. A provider that is not set
/// up or that fails is a test result, not an error.</summary>
public interface IAiProviderConnectionTester
{
    /// <summary>Tests the provider with its saved settings.</summary>
    Task<AiProviderTestResultDto> TestAsync(AiProviderKind kind, CancellationToken cancellationToken = default);

    /// <summary>Tests values that are not saved (the Add AI model page). Nothing is read from or written to the database.</summary>
    Task<AiProviderTestResultDto> TestCandidateAsync(AiProviderRuntime runtime, CancellationToken cancellationToken = default);
}
