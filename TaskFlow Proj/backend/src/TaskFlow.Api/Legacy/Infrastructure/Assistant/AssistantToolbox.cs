using System.Text.Json;
using TaskFlow.Application.Assistant;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Infrastructure.Assistant;

/// <summary>One thing TFA can do. The model only ever names a tool and its arguments; the caller's identity, the
/// authorization check and the confirmation step are all outside the model's reach.</summary>
public interface IAssistantTool
{
    string Name { get; }

    string Description { get; }

    /// <summary>A JSON Schema object describing the arguments. No tool takes a user id, a role or a password.</summary>
    string ParametersSchema { get; }

    /// <summary>The API policy that must succeed for the user, or null for any signed-in user.</summary>
    string? Policy { get; }

    /// <summary>For the fixed refusal: what this action is, e.g. "Changing a user's role".</summary>
    string ActionDescription { get; }

    /// <summary>For the fixed refusal: who may do it, e.g. "an authorized administrator".</summary>
    string WhoCanDoIt { get; }

    /// <summary>For tools that prepare an action: which kind, so a refusal can be recorded in the audit log.</summary>
    AssistantActionKind? ActionKind => null;

    Task<ToolOutcome> ExecuteAsync(AssistantCaller caller, int conversationId, JsonElement arguments, CancellationToken cancellationToken);
}

/// <summary>Checks, for every tool and every call, that the signed-in user is allowed to use it: through the same
/// authorization policies the REST API uses, with the caller's own token claims. The result never depends on what the
/// model said.</summary>
public class AssistantToolGate
{
    private readonly IAssistantAuthorizer _authorizer;

    public AssistantToolGate(IAssistantAuthorizer authorizer)
    {
        _authorizer = authorizer;
    }

    public Task<bool> IsAllowedAsync(AssistantCaller caller, IAssistantTool tool, CancellationToken cancellationToken = default) =>
        _authorizer.IsAllowedAsync(caller.Principal, tool.Policy, cancellationToken);

    /// <summary>The fixed refusal text for a tool the caller may not use.</summary>
    public static string Refusal(IAssistantTool tool) => AssistantTexts.PermissionDenied(tool.ActionDescription, tool.WhoCanDoIt);
}

public class AssistantToolbox : IAssistantToolbox
{
    private readonly IReadOnlyDictionary<string, IAssistantTool> _tools;
    private readonly AssistantToolGate _gate;
    private readonly IAssistantAuditLog _audit;

    public AssistantToolbox(IEnumerable<IAssistantTool> tools, AssistantToolGate gate, IAssistantAuditLog audit)
    {
        _tools = tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        _gate = gate;
        _audit = audit;
    }

    /// <summary>Only the tools the user may use are advertised, which keeps the prompt short and avoids pointless
    /// refusals. The gate still runs on every call, so this is a convenience and not the control.</summary>
    public async Task<IReadOnlyList<ChatToolDefinition>> DefinitionsForAsync(AssistantCaller caller, CancellationToken cancellationToken = default)
    {
        var definitions = new List<ChatToolDefinition>();
        foreach (var tool in _tools.Values)
        {
            if (await _gate.IsAllowedAsync(caller, tool, cancellationToken))
            {
                definitions.Add(new ChatToolDefinition(tool.Name, tool.Description, tool.ParametersSchema));
            }
        }

        return definitions;
    }

    public async Task<ToolOutcome> ExecuteAsync(AssistantCaller caller, int conversationId, ChatToolCall call, CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(call.Name, out var tool))
        {
            return new ToolOutcome($"There is no tool named {call.Name}.");
        }

        if (!await _gate.IsAllowedAsync(caller, tool, cancellationToken))
        {
            if (tool.ActionKind is { } kind)
            {
                await _audit.RecordAsync(caller.UserId, kind, "refused", cancellationToken);
            }

            return new ToolOutcome(
                "The user is not permitted to do this. Tell them they do not have the required permission.",
                AssistantToolGate.Refusal(tool),
                AssistantMessageKind.Refusal);
        }

        JsonElement arguments;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
            arguments = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return new ToolOutcome("The tool arguments were not valid JSON. Try again with a valid JSON object.");
        }

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return new ToolOutcome("The tool arguments must be a JSON object.");
        }

        return await tool.ExecuteAsync(caller, conversationId, arguments, cancellationToken);
    }
}
