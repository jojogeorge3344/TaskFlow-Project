using TaskFlow.Api.Models.Enums;
using TaskFlow.Application.Assistant;

namespace TaskFlow.Infrastructure.Assistant;

public class AiProviderConnectionTester : IAiProviderConnectionTester
{
    private readonly IChatModelGateway _gateway;

    public AiProviderConnectionTester(IChatModelGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<AiProviderTestResultDto> TestAsync(AiProviderKind kind, CancellationToken cancellationToken = default)
    {
        var request = new ChatModelRequest(
            "This is a connection test. Reply with the single word OK.",
            [new ChatMessage(AssistantMessageRole.User, "Reply with OK.")],
            [],
            string.Empty);

        try
        {
            await _gateway.CompleteAsync(kind, request, cancellationToken);
            return new AiProviderTestResultDto(true, "The connection works.", null);
        }
        catch (ProviderNotConfiguredException)
        {
            return new AiProviderTestResultDto(false, ProviderFailureMessages.NotConfigured(kind), null);
        }
        catch (ProviderFailureException failure)
        {
            return new AiProviderTestResultDto(false, ProviderFailureMessages.For(kind, failure.Kind, failure.RetryAfter), failure.Kind);
        }
    }
}
