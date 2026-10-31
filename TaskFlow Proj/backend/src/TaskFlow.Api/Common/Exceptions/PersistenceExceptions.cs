namespace TaskFlow.Api.Common.Exceptions;

/// <summary>Another request changed the same row first, for example an MFA check finishing while an administrator edits
/// the user. The unit of work raises it in place of the database's own exception, so services can react without knowing
/// how data is stored.</summary>
public class ConcurrencyConflictException : ConflictException
{
    public ConcurrencyConflictException(Exception? innerException = null)
        : base("Someone else changed this record while you were saving. Reload it and try again.", innerException)
    {
    }

    public override string? Title => "The record was changed at the same time.";
}

/// <summary>A save broke a unique index, for example two administrators saving the same role name at once.</summary>
public class UniqueConstraintException : ConflictException
{
    public UniqueConstraintException(Exception? innerException = null)
        : base("That value is already in use.", innerException)
    {
    }
}
