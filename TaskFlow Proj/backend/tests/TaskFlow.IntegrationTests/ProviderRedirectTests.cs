using TaskFlow.Api.Integrations.Interfaces.Ai;
using TaskFlow.Api.Integrations.Ai;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.IntegrationTests;

/// <summary>The provider clients are built by the real application, so these use its own registrations.</summary>
public class ProviderRedirectTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public ProviderRedirectTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task AProviderResponseThatRedirectsIsNotFollowed()
    {
        var redirectPort = FreePort();
        var targetPort = FreePort();
        var targetHits = 0;

        using var target = new HttpListener();
        target.Prefixes.Add($"http://localhost:{targetPort}/");
        target.Start();
        var targetTask = Task.Run(async () =>
        {
            try
            {
                var context = await target.GetContextAsync();
                Interlocked.Increment(ref targetHits);
                context.Response.StatusCode = 200;
                context.Response.Close();
            }
            catch (HttpListenerException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        });

        using var redirector = new HttpListener();
        redirector.Prefixes.Add($"http://localhost:{redirectPort}/");
        redirector.Start();
        var redirectTask = Task.Run(async () =>
        {
            var context = await redirector.GetContextAsync();
            context.Response.StatusCode = 302;
            context.Response.RedirectLocation = $"http://localhost:{targetPort}/api/chat";
            context.Response.Close();
        });

        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<OllamaProvider>();
        var runtime = new AiProviderRuntime(AiProviderKind.Ollama, "llama3.2:3b", $"http://localhost:{redirectPort}", null);
        var request = new ChatModelRequest("sys", [new ChatMessage(AssistantMessageRole.User, "hi")], [], "llama3.2:3b");

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(runtime, request));
        await redirectTask;
        await Task.Delay(300);

        Assert.Equal(0, Volatile.Read(ref targetHits));
        Assert.Equal(302, failure.HttpStatus);
        Assert.Equal(ProviderFailureKind.Unexpected, failure.Kind);

        target.Stop();
        await targetTask;
    }
}
