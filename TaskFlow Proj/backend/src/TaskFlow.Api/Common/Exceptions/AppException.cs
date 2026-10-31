namespace TaskFlow.Api.Common.Exceptions;

/// <summary>A failure the application expects and can explain. The exception-handling middleware turns it into a problem
/// response with <see cref="StatusCode"/>, <see cref="Title"/> and the message as its detail, so the message must be safe to
/// show to the user. Anything that is not an <see cref="AppException"/> is reported as a generic server error.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public abstract int StatusCode { get; }

    /// <summary>A short heading for the problem response; null uses the standard phrase for the status code.</summary>
    public virtual string? Title => null;

    /// <summary>When set, sent as the Retry-After header in seconds.</summary>
    public virtual TimeSpan? RetryAfter => null;
}

/// <summary>The request breaks a business rule (HTTP 400).</summary>
public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }

    public override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>One or more request fields are not acceptable (HTTP 400 with a field-error map).</summary>
public class RequestValidationException : AppException
{
    public RequestValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(string.Join(" ", errors.SelectMany(error => error.Value)))
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>The caller may not see or change this resource (HTTP 403).</summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message)
        : base(message)
    {
    }

    public override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>The resource does not exist (HTTP 404).</summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public override int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>The resource is not in a state that allows this change (HTTP 409).</summary>
public class ConflictException : AppException
{
    public ConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public override int StatusCode => StatusCodes.Status409Conflict;
}

/// <summary>The caller has used up an allowance and must wait (HTTP 429).</summary>
public class RateLimitedException : AppException
{
    private readonly TimeSpan? _retryAfter;

    public RateLimitedException(string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        _retryAfter = retryAfter;
    }

    public override int StatusCode => StatusCodes.Status429TooManyRequests;

    public override string? Title => "Too many requests";

    public override TimeSpan? RetryAfter => _retryAfter;
}
