namespace TaskFlow.Api.Models.Enums;

/// <summary>What a TFA pending action will do once the user confirms it.</summary>
public enum AssistantActionKind
{
    SendMail,
    UpdateOwnProfile,
    ChangeUserRole
}
