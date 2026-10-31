using TaskFlow.Api.Integrations.Ai;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.IntegrationTests.TestSupport;

/// <summary>A stand-in AI provider for integration tests: it never leaves the process. A test sets
/// <see cref="Handler"/> to decide each reply and reads <see cref="Calls"/> to see what was sent.</summary>
public sealed class ScriptedChatProvider : IChatModelProvider
{
    public ScriptedChatProvider(AiProviderKind kind)
    {
        Kind = kind;
    }

    public AiProviderKind Kind { get; }

    public Func<AiProviderRuntime, ChatModelRequest, ChatModelResult> Handler { get; set; } =
        (_, _) => ChatModelResult.FromText("OK");

    public List<(AiProviderRuntime Runtime, ChatModelRequest Request)> Calls { get; } = [];

    public Task<ChatModelResult> CompleteAsync(AiProviderRuntime runtime, ChatModelRequest request, CancellationToken cancellationToken = default)
    {
        lock (Calls)
        {
            Calls.Add((runtime, request));
        }

        return Task.FromResult(Handler(runtime, request));
    }

    public void Reset()
    {
        Handler = (_, _) => ChatModelResult.FromText("OK");
        lock (Calls)
        {
            Calls.Clear();
        }
    }
}
