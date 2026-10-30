using TaskFlow.Api.Services.Interfaces.Settings.TfaSettings;
using TaskFlow.Api.Services.Interfaces.Assistant;
using TaskFlow.Api.Integrations.Interfaces.Ai;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Assistant;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Services.Assistant;

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
