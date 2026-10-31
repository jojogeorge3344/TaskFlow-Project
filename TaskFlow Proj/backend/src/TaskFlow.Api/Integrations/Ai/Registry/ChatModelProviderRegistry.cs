using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.Providers;

namespace TaskFlow.Api.Integrations.Ai.Registry;

public class ChatModelProviderRegistry : IChatModelProviderRegistry
{
    private readonly IReadOnlyDictionary<AiProviderKind, IChatModelProvider> _providers;

    public ChatModelProviderRegistry(IEnumerable<IChatModelProvider> providers)
    {
        _providers = providers.ToDictionary(provider => provider.Kind);
    }

    public IChatModelProvider Get(AiProviderKind kind) =>
        _providers.TryGetValue(kind, out var provider)
            ? provider
            : throw new InvalidOperationException($"No provider is registered for {kind}.");
}
