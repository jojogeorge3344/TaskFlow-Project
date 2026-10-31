using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai;

/// <summary>A classified failure of a provider call. The message is for logs and never contains a key or the user's
/// text; users see the fixed message for the <see cref="Kind"/>.</summary>
public class ProviderFailureException : Exception
{
    public ProviderFailureException(AiProviderKind provider, ProviderFailureKind kind, TimeSpan? retryAfter = null, Exception? inner = null, int? httpStatus = null)
        : base($"{provider} call failed: {kind}.", inner)
    {
        Provider = provider;
        Kind = kind;
        RetryAfter = retryAfter;
        HttpStatus = httpStatus;
    }

    public AiProviderKind Provider { get; }
    public ProviderFailureKind Kind { get; }
    public TimeSpan? RetryAfter { get; }

    /// <summary>The HTTP status the provider answered with, when it answered. For the log only.</summary>
    public int? HttpStatus { get; }
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
