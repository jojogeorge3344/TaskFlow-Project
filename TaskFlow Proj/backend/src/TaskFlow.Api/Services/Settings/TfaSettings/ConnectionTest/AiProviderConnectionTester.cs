using Microsoft.Extensions.Logging;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Integrations.Ai.Registry;
using TaskFlow.Api.Services.Assistant.Chat;
using TaskFlow.Api.Services.Assistant.ModelGateway;

namespace TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest;

public class AiProviderConnectionTester : IAiProviderConnectionTester
{
    private static readonly ChatModelRequest TestRequest = new(
        "This is a connection test. Reply with the single word OK.",
        [new ChatMessage(AssistantMessageRole.User, "Reply with OK.")],
        [],
        string.Empty);

    private readonly IChatModelGateway _gateway;
    private readonly IChatModelProviderRegistry _registry;
    private readonly ILogger<AiProviderConnectionTester> _logger;

    public AiProviderConnectionTester(IChatModelGateway gateway, IChatModelProviderRegistry registry, ILogger<AiProviderConnectionTester> logger)
    {
        _gateway = gateway;
        _registry = registry;
        _logger = logger;
    }

    public async Task<AiProviderTestResultDto> TestAsync(int providerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _gateway.CompleteAsync(providerId, TestRequest, cancellationToken);
            return Passed();
        }
        catch (ProviderNotConfiguredException notConfigured)
        {
            return new AiProviderTestResultDto(false, ProviderFailureMessages.NotConfigured(notConfigured.ProviderName), null);
        }
        catch (ProviderFailureException failure)
        {
            _logger.LogWarning(
                "AI provider {Provider} connection test failed: {FailureKind} (HTTP status {HttpStatus}) {ProviderReason}",
                failure.ProviderName, failure.Kind, failure.HttpStatus, failure.ProviderReason);
            return Failed(failure);
        }
    }

    public async Task<AiProviderTestResultDto> TestCandidateAsync(AiProviderRuntime runtime, CancellationToken cancellationToken = default)
    {
        try
        {
            await _registry.Get(runtime.ApiType).CompleteAsync(runtime, TestRequest with { ModelName = runtime.ModelName }, cancellationToken);
            return Passed();
        }
        catch (ProviderFailureException failure)
        {
            failure.For(runtime);

            // Never the key or the provider's raw body: only the cleaned, shortened reason (keys replaced by [key]).
            _logger.LogWarning(
                "AI provider {Provider} test of a new model failed: {FailureKind} (HTTP status {HttpStatus}) {ProviderReason}",
                failure.ProviderName, failure.Kind, failure.HttpStatus, failure.ProviderReason);
            return Failed(failure);
        }
    }

    private static AiProviderTestResultDto Passed() => new(true, "The connection works.", null);

    /// <summary>The fixed message for the failure. For an unexpected error or a missing or malformed model, the provider's own status
    /// and reason are added, so the administrator can see what it refused (Google, for example, names the model to use
    /// instead of a retired one). Test connection needs settings.ai.test, so only administrators see it; chat replies
    /// keep the fixed message.</summary>
    private static AiProviderTestResultDto Failed(ProviderFailureException failure)
    {
        var message = ProviderFailureMessages.For(failure);
        if (failure.Kind is ProviderFailureKind.Unexpected or ProviderFailureKind.ModelUnavailable or ProviderFailureKind.InvalidModelName
            && failure.ProviderReason is { } reason)
        {
            var status = failure.HttpStatus is { } code ? $"HTTP {code} - " : string.Empty;
            message = $"{message} {failure.ProviderName} said: {status}{reason}";
        }

        return new(false, message, failure.Kind);
    }
}
