using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.Providers;

namespace TaskFlow.Api.Integrations.Ai.Registry;

public interface IChatModelProviderRegistry
{
    IChatModelProvider Get(AiProviderKind kind);
}
