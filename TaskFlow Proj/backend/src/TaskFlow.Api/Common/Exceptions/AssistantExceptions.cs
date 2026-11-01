using Microsoft.AspNetCore.Http;

namespace TaskFlow.Api.Common.Exceptions;

/// <summary>The request cannot be handled and nothing was sent to any provider (HTTP 400).</summary>
public class AssistantRequestException : AppException
{
    public AssistantRequestException(string message)
        : base(message)
    {
    }

    public override int StatusCode => StatusCodes.Status400BadRequest;

    public override string? Title => "TFA could not handle that";
}

/// <summary>The user is over the per-minute limit (HTTP 429).</summary>
public class AssistantRateLimitedException : AssistantRequestException
{
    private readonly TimeSpan _retryAfter;

    public AssistantRateLimitedException(TimeSpan retryAfter)
        : base(Message(retryAfter))
    {
        _retryAfter = retryAfter;
    }

    public override int StatusCode => StatusCodes.Status429TooManyRequests;

    public override string? Title => "Too many requests";

    public override TimeSpan? RetryAfter => _retryAfter;

    private static string Message(TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return $"You are sending messages too quickly. Please wait about {seconds} seconds and try again.";
    }
}

/// <summary>The action is not in a state that allows this (expired, already done, cancelled). HTTP 409.</summary>
public class AssistantActionConflictException : ConflictException
{
    public AssistantActionConflictException(string message)
        : base(message)
    {
    }

    public override string? Title => "This action can no longer be done";
}
