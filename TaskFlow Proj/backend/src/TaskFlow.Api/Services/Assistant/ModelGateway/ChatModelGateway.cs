using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Common.Exceptions;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Integrations.Ai.Registry;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.Api.Services.Assistant.ModelGateway;

public class ChatModelGateway : IChatModelGateway
{
    private readonly IAiProviderConfigService _config;
    private readonly IChatModelProviderRegistry _registry;
    private readonly ILogger<ChatModelGateway> _logger;

    public ChatModelGateway(IAiProviderConfigService config, IChatModelProviderRegistry registry, ILogger<ChatModelGateway> logger)
    {
        _config = config;
        _registry = registry;
        _logger = logger;
    }

    public async Task<ChatModelResult> CompleteAsync(AiProviderKind kind, ChatModelRequest request, CancellationToken cancellationToken = default)
    {
        var runtime = await _config.GetRuntimeAsync(kind, cancellationToken) ?? throw new ProviderNotConfiguredException(kind);

        try
        {
            return await _registry.Get(kind).CompleteAsync(runtime, request with { ModelName = runtime.ModelName }, cancellationToken);
        }
        catch (ProviderFailureException failure)
        {
            // Only the provider, the class of failure and the HTTP status are logged: never the key, the prompt or the provider's body.
            _logger.LogWarning("AI provider {Provider} call failed: {FailureKind} (HTTP status {HttpStatus})", failure.Provider, failure.Kind, failure.HttpStatus);
            throw;
        }
    }
}
