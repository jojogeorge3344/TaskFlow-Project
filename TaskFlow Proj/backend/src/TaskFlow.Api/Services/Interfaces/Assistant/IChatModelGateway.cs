using TaskFlow.Api.Integrations.Interfaces.Ai;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Services.Interfaces.Assistant;

/// <summary>The one way the rest of the application calls a model. It resolves the provider's configuration, decrypts
/// the key in memory for the length of the call, and logs failures without the key or the request text.</summary>
public interface IChatModelGateway
{
    /// <exception cref="ProviderNotConfiguredException">The provider is not enabled and fully configured.</exception>
    /// <exception cref="ProviderFailureException">The call failed; the exception carries the classified kind.</exception>
    Task<ChatModelResult> CompleteAsync(AiProviderKind kind, ChatModelRequest request, CancellationToken cancellationToken = default);
}
