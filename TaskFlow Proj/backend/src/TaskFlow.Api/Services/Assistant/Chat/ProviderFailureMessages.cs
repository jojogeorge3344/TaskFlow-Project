using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.Providers;

namespace TaskFlow.Api.Services.Assistant.Chat;

/// <summary>The fixed, plain-language message for each kind of provider failure.</summary>
public static class ProviderFailureMessages
{
    /// <summary>The message for a failure, naming the provider that was called (a built-in or a custom one).</summary>
    public static string For(ProviderFailureException failure) =>
        For(failure.ProviderName, failure.IsLocal, failure.ApiType, failure.Kind, failure.RetryAfter);

    /// <summary>The message for a failure of a built-in provider (Google Gemini, OpenAI GPT or Ollama).</summary>
    public static string For(AiApiType builtIn, ProviderFailureKind kind, TimeSpan? retryAfter = null) =>
        For(AiProviderCatalog.DisplayName(builtIn), builtIn == AiApiType.Ollama, builtIn, kind, retryAfter);

    public static string For(string name, bool local, AiApiType apiType, ProviderFailureKind kind, TimeSpan? retryAfter = null)
    {

        return kind switch
        {
            ProviderFailureKind.RateLimited =>
                $"{name}'s free limit or rate limit has been reached. {RetryText(retryAfter)} or choose another model.",
            ProviderFailureKind.InvalidKey =>
                $"{name} did not accept the API key. Ask an administrator to check the key in TFA Settings, or choose another model.",
            ProviderFailureKind.Timeout =>
                $"{name} took too long to answer. You can retry, or choose another model.",
            ProviderFailureKind.Unreachable when local =>
                $"{name} is not running or cannot be reached. Ask an administrator to start it, or choose another model.",
            ProviderFailureKind.Unreachable =>
                $"{name} cannot be reached right now. Check the connection and retry, or choose another model.",
            ProviderFailureKind.ModelUnavailable when local =>
                $"The configured model is not available on {name}. Ask an administrator to install it, or choose another model.",
            ProviderFailureKind.ModelUnavailable =>
                $"The configured model is not available on {name}. Ask an administrator to check the model name, or choose another model.",
            ProviderFailureKind.InvalidModelName =>
                $"{name} did not accept the configured model name as a model ID. Ask an administrator to set a valid model ID, for example {AiProviderCatalog.DefaultModel(apiType)}, or choose another model.",
            _ =>
                $"{name} returned an unexpected error. Please try again, or choose another model."
        };
    }

    public static string NotConfigured(string providerName) =>
        $"{providerName} is not configured. Choose another model, or ask an administrator to set it up.";

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
