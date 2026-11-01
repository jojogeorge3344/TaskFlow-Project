using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai.Providers;

/// <summary>A classified failure of a provider call. The message is for logs and never contains a key or the user's
/// text; users see the fixed message for the <see cref="Kind"/>. A client knows only its <see cref="ApiType"/>; the
/// caller that knows which provider it called (the gateway, the connection tester) attaches it with
/// <see cref="For"/>, so the message names "Groq" rather than the API type.</summary>
public class ProviderFailureException : Exception
{
    public ProviderFailureException(
        AiApiType apiType,
        ProviderFailureKind kind,
        TimeSpan? retryAfter = null,
        Exception? inner = null,
        int? httpStatus = null,
        string? providerReason = null)
        : base($"{apiType} call failed: {kind}.", inner)
    {
        ApiType = apiType;
        Kind = kind;
        RetryAfter = retryAfter;
        HttpStatus = httpStatus;
        ProviderReason = providerReason;
        ProviderName = AiProviderCatalog.DisplayName(apiType);
        IsLocal = apiType == AiApiType.Ollama;
    }

    public AiApiType ApiType { get; }
    public ProviderFailureKind Kind { get; }
    public TimeSpan? RetryAfter { get; }

    /// <summary>The provider that failed, once the caller attached it; null for a call made outside a provider (for
    /// example document embeddings).</summary>
    public int? ProviderId { get; private set; }
    public string ProviderName { get; private set; }
    public bool IsLocal { get; private set; }

    /// <summary>The HTTP status the provider answered with, when it answered.</summary>
    public int? HttpStatus { get; }

    /// <summary>The provider's own error status and message, at most 200 characters, with any key replaced by
    /// <c>[key]</c> (built by <c>ProviderHttp.Reason</c>). Shown only to administrators on Test connection, and logged;
    /// never put in a chat reply.</summary>
    public string? ProviderReason { get; }

    /// <summary>Attaches the provider that was called. Returns the same exception, so a caller can attach and rethrow.</summary>
    public ProviderFailureException For(AiProviderRuntime runtime)
    {
        ProviderId = runtime.ProviderId;
        ProviderName = runtime.Name;
        IsLocal = runtime.IsLocal;
        return this;
    }
}

/// <summary>The requested provider is disabled or missing its key or address, so nothing was sent.</summary>
public class ProviderNotConfiguredException : Exception
{
    public ProviderNotConfiguredException(int providerId, string providerName)
        : base($"{providerName} is not configured.")
    {
        ProviderId = providerId;
        ProviderName = providerName;
    }

    public int ProviderId { get; }
    public string ProviderName { get; }
}
