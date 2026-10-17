namespace TaskFlow.Domain.Enums;

/// <summary>Why an email was sent: a deadline reminder, or a test from the Email Configuration screen.</summary>
public enum EmailKind
{
    Reminder,
    Test
}
