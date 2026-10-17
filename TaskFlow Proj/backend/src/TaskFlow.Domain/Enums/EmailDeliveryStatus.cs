namespace TaskFlow.Domain.Enums;

/// <summary>State of one email in the delivery log. Pending emails wait for their send time or a retry;
/// Skipped emails were never sent because they became stale or email was switched off.</summary>
public enum EmailDeliveryStatus
{
    Pending,
    Sent,
    Failed,
    Skipped
}
