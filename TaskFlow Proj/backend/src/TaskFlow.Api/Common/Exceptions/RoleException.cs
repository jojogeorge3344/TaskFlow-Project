namespace TaskFlow.Api.Common.Exceptions;

public enum RoleFailure
{
    NotFound,

    /// <summary>The name or the permission list is not acceptable.</summary>
    Invalid,

    /// <summary>The role or permission is part of the fixed, read-only set.</summary>
    ReadOnly,

    /// <summary>Another role already has that name.</summary>
    Duplicate,

    /// <summary>Users still hold the role.</summary>
    InUse
}

/// <summary>A role or permission rule was broken. The message is safe to show to the administrator.</summary>
public class RoleException : TaskFlow.Api.Common.Exceptions.AppException
{
    public RoleException(RoleFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public RoleFailure Failure { get; }

    public override int StatusCode => Failure switch
    {
        RoleFailure.NotFound => StatusCodes.Status404NotFound,
        RoleFailure.Invalid => StatusCodes.Status400BadRequest,
        RoleFailure.ReadOnly => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status409Conflict
    };
}

