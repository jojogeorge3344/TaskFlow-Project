namespace TaskFlow.Domain.Enums;

/// <summary>Where a password recovery request is in its lifecycle. A request is Completed only after
/// the password is stored and the user notification exists; a notification failure leaves it Failed
/// so the administrator can retry without resetting the password again.</summary>
public enum PasswordResetStatus
{
    Pending,
    Completed,
    Failed
}
