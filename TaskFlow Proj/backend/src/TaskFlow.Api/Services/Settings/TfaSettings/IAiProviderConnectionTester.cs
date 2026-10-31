using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Services.Settings.TfaSettings;

/// <summary>Sends one tiny request to a provider so an administrator can see whether it works. A provider that is not set
/// up or that fails is a test result, not an error.</summary>
public interface IAiProviderConnectionTester
{
    Task<AiProviderTestResultDto> TestAsync(AiProviderKind kind, CancellationToken cancellationToken = default);
}
