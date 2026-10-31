using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai;

/// <summary>A tool the model asked to run. <see cref="ArgumentsJson"/> is the raw JSON object of arguments.</summary>
public record ChatToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>One turn in provider-neutral form. A Tool message answers the call named by <see cref="ToolCallId"/>.</summary>
public record ChatMessage(
    AssistantMessageRole Role,
    string Content,
    IReadOnlyList<ChatToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? ToolName = null);

/// <summary>A tool the model may call. <see cref="ParametersJsonSchema"/> is a JSON Schema object.</summary>
public record ChatToolDefinition(string Name, string Description, string ParametersJsonSchema);

public record ChatModelRequest(
    string SystemPrompt,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ChatToolDefinition> Tools,
    string ModelName);

/// <summary>What the model answered: text, tool calls, or both.</summary>
public record ChatModelResult(string? Text, IReadOnlyList<ChatToolCall> ToolCalls)
{
    public static ChatModelResult FromText(string text) => new(text, []);
}
