using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>One admin-assisted password recovery request: who asked, from which screen, what
/// happened and when. <see cref="ReferenceId"/> is the identifier exposed to the UI and the audit
/// trail so internal ids never leave the system.</summary>
public class PasswordResetRequest
{
    public int Id { get; set; }

    public Guid ReferenceId { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public PasswordResetOrigin Origin { get; set; }

    public PasswordResetStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>The administrator who processed the request, once it is processed.</summary>
    public int? ProcessedByUserId { get; set; }

    public User? ProcessedByUser { get; set; }

    /// <summary>Why processing or notification failed; null while nothing has failed.</summary>
    public string? FailureReason { get; set; }

    /// <summary>When the administrator notification mail was written; null when it failed or has not run.</summary>
    public DateTime? AdminNotifiedAt { get; set; }

    /// <summary>When the user's recovery mail and in-app notification were written.</summary>
    public DateTime? UserNotifiedAt { get; set; }

    /// <summary>True when the password set for this request was an automatically generated temporary one.</summary>
    public bool TemporaryPasswordIssued { get; set; }
}
