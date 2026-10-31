using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai;

public interface IChatModelProviderRegistry
{
    IChatModelProvider Get(AiProviderKind kind);
}
