using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Services.Assistant;

namespace TaskFlow.Api.Services.Assistant.Rules;

public static class AssistantActionRules
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>The policy the action needs when it is confirmed, with the words for the fixed refusal.</summary>
    public static (string? Policy, string Description, string WhoCanDoIt) Requirement(AssistantActionKind kind) => kind switch
    {
        AssistantActionKind.ChangeUserRole => (AssistantPolicies.RolesManage, "Changing a user's role", "an authorized administrator"),
        AssistantActionKind.UpdateOwnProfile => (null, "Updating a profile", "the account owner"),
        _ => (null, "Sending mail", "a signed-in user")
    };
}
