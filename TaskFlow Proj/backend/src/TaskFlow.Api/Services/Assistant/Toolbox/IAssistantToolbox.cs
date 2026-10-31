using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Services.Assistant.Access;

namespace TaskFlow.Api.Services.Assistant.Toolbox;

/// <summary>The tools the model may call, filtered to what the caller's role may use.</summary>
public interface IAssistantToolbox
{
    /// <summary>The tool definitions to advertise to the model for this caller.</summary>
    Task<IReadOnlyList<ChatToolDefinition>> DefinitionsForAsync(AssistantCaller caller, CancellationToken cancellationToken = default);

    Task<ToolOutcome> ExecuteAsync(AssistantCaller caller, int conversationId, ChatToolCall call, CancellationToken cancellationToken = default);
}
