using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Application.Assistant;

/// <summary>One AI provider behind a common seam. An implementation translates the neutral request to its own wire
/// format and throws <see cref="ProviderFailureException"/> for every failure.</summary>
public interface IChatModelProvider
{
    AiProviderKind Kind { get; }

    Task<ChatModelResult> CompleteAsync(AiProviderRuntime runtime, ChatModelRequest request, CancellationToken cancellationToken = default);
}

public interface IChatModelProviderRegistry
{
    IChatModelProvider Get(AiProviderKind kind);
}
