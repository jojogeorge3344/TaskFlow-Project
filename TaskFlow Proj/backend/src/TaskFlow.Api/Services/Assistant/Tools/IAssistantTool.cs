using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Assistant;
using System.Text.Json;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Services.Assistant.Access;
using TaskFlow.Api.Services.Assistant.Toolbox;

namespace TaskFlow.Api.Services.Assistant.Tools;

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
