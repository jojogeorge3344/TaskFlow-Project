using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai.Providers;

/// <summary>A classified failure of a provider call. The message is for logs and never contains a key or the user's
/// text; users see the fixed message for the <see cref="Kind"/>.</summary>
public class ProviderFailureException : Exception
{
    public ProviderFailureException(
        AiProviderKind provider,
        ProviderFailureKind kind,
        TimeSpan? retryAfter = null,
        Exception? inner = null,
        int? httpStatus = null,
        string? providerReason = null)
        : base($"{provider} call failed: {kind}.", inner)
    {
        Provider = provider;
        Kind = kind;
        RetryAfter = retryAfter;
        HttpStatus = httpStatus;
        ProviderReason = providerReason;
    }

    public AiProviderKind Provider { get; }
    public ProviderFailureKind Kind { get; }
    public TimeSpan? RetryAfter { get; }

    /// <summary>The HTTP status the provider answered with, when it answered.</summary>
    public int? HttpStatus { get; }

    /// <summary>The provider's own error status and message, at most 200 characters, with any key replaced by
    /// <c>[key]</c> (built by <c>ProviderHttp.Reason</c>). Shown only to administrators on Test connection, and logged;
    /// never put in a chat reply.</summary>
    public string? ProviderReason { get; }
}

/// <summary>The requested provider is disabled or missing its key or address, so nothing was sent.</summary>
public class ProviderNotConfiguredException : Exception
{
    public ProviderNotConfiguredException(AiProviderKind provider)
        : base($"{provider} is not configured.")
    {
        Provider = provider;
    }

    public AiProviderKind Provider { get; }
}
