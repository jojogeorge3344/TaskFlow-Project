using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Assistant;

/// <summary>A classified provider problem as shown to the user: a fixed message plus the other providers they could
/// choose. The raw provider error is never part of it.</summary>
public record AssistantFailureDto(
    ProviderFailureKind Kind,
    AiProviderKind Provider,
    string Message,
    int? RetryAfterSeconds,
    IReadOnlyList<AiProviderOptionDto> Alternatives);

/// <summary>The fixed, plain-language message for each kind of provider failure.</summary>
public static class ProviderFailureMessages
{
    public static string For(AiProviderKind provider, ProviderFailureKind kind, TimeSpan? retryAfter = null)
    {
        var name = AiProviderCatalog.DisplayName(provider);
        var local = provider == AiProviderKind.Ollama;

        return kind switch
        {
            ProviderFailureKind.RateLimited =>
                $"{name}'s free limit or rate limit has been reached. {RetryText(retryAfter)} or choose another model.",
            ProviderFailureKind.InvalidKey =>
                $"{name} did not accept the API key. Ask an administrator to check the key in TFA Settings, or choose another model.",
            ProviderFailureKind.Timeout =>
                $"{name} took too long to answer. You can retry, or choose another model.",
            ProviderFailureKind.Unreachable when local =>
                "Ollama is not running or cannot be reached. Ask an administrator to start it, or choose another model.",
            ProviderFailureKind.Unreachable =>
                $"{name} cannot be reached right now. Check the connection and retry, or choose another model.",
            ProviderFailureKind.ModelUnavailable when local =>
                "The configured model is not available on Ollama. Ask an administrator to install it, or choose another model.",
            ProviderFailureKind.ModelUnavailable =>
                $"The configured model is not available on {name}. Ask an administrator to check the model name, or choose another model.",
            _ =>
                $"{name} returned an unexpected error. Please try again, or choose another model."
        };
    }

    public static string NotConfigured(AiProviderKind provider) =>
        $"{AiProviderCatalog.DisplayName(provider)} is not configured. Choose another model, or ask an administrator to set it up.";

    private static string RetryText(TimeSpan? retryAfter)
    {
        if (retryAfter is not { } wait || wait <= TimeSpan.Zero)
        {
            return "Try again in about a minute,";
        }

        var seconds = (int)Math.Ceiling(wait.TotalSeconds);
        return seconds >= 120
            ? $"Try again in about {(int)Math.Ceiling(wait.TotalMinutes)} minutes,"
            : $"Try again in about {seconds} seconds,";
    }
}
