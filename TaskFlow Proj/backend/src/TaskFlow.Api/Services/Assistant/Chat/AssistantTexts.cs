using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Common.Exceptions;
namespace TaskFlow.Api.Services.Assistant.Chat;

/// <summary>Messages TFA shows as fixed text. None of these is produced by an AI model, so they cannot be argued away
/// by a prompt, and they say the same thing every time.</summary>
public static class AssistantTexts
{
    public const string SecretBlocked =
        "For your safety I did not send or keep that message: it looks like it contains a password or an API key. "
        + "Please never type passwords into chat. To change your password, open My Profile and use the Change password form.";

    public const string EmptyAnswer =
        "I could not come up with an answer. Please try again, or ask in a different way.";

    public const string ActionPrepared =
        "I have prepared this for you. Please review it. Nothing happens until you confirm.";

    public const string PasswordGuidance =
        "You can change your own password yourself: open My Profile and use the Change password form (current password, "
        + "new password, confirm). For your safety I never ask for or accept passwords in chat. If you have forgotten your "
        + "current password, please contact your system administrator.";

    public const string AdminGuidance =
        "This needs an administrator. Please contact your system administrator for assistance.";

    /// <summary>The refusal for an action the user's role does not allow. It states the missing permission, what the
    /// action is, who can do it and what to do next.</summary>
    public static string PermissionDenied(string actionDescription, string whoCanDoIt) =>
        $"You do not have the required permission to perform this action. {actionDescription} can only be performed by {whoCanDoIt}. "
        + "Please contact your system administrator for assistance.";
}
