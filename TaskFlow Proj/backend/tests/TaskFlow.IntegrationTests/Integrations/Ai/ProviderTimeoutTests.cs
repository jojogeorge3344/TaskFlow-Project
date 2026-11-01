using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Integrations.Ai.Embeddings;
using TaskFlow.Api.Integrations.Ai.Providers;

namespace TaskFlow.IntegrationTests.Integrations.Ai;

/// <summary>The API with both provider timeouts set in configuration.</summary>
public class ConfiguredTimeoutsFactory : TaskFlowApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:TimeoutSeconds"] = "45",
            ["AiProviders:OllamaTimeoutSeconds"] = "240"
        }));
    }
}

public class ProviderTimeoutTests : IClassFixture<TaskFlowApiFactory>, IClassFixture<ConfiguredTimeoutsFactory>
{
    private readonly TaskFlowApiFactory _defaults;
    private readonly ConfiguredTimeoutsFactory _configured;

    public ProviderTimeoutTests(TaskFlowApiFactory defaults, ConfiguredTimeoutsFactory configured)
    {
        _defaults = defaults;
        _configured = configured;
    }

    private static TimeSpan TimeoutOf<TClient>(TaskFlowApiFactory factory) =>
        factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(TClient).Name).Timeout;

    [Fact]
    public void OllamaChatWaits180SecondsAndCloudProvidersAndEmbeddings120ByDefault()
    {
        Assert.Equal(TimeSpan.FromSeconds(180), TimeoutOf<OllamaProvider>(_defaults));
        Assert.Equal(TimeSpan.FromSeconds(120), TimeoutOf<GeminiEmbeddingClient>(_defaults));
        Assert.Equal(TimeSpan.FromSeconds(120), TimeoutOf<GeminiProvider>(_defaults));
        Assert.Equal(TimeSpan.FromSeconds(120), TimeoutOf<OpenAiProvider>(_defaults));
    }

    [Fact]
    public void BothTimeoutsFollowConfiguration()
    {
        Assert.Equal(TimeSpan.FromSeconds(240), TimeoutOf<OllamaProvider>(_configured));
        Assert.Equal(TimeSpan.FromSeconds(45), TimeoutOf<GeminiEmbeddingClient>(_configured));
        Assert.Equal(TimeSpan.FromSeconds(45), TimeoutOf<GeminiProvider>(_configured));
    }
}
