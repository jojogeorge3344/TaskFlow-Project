using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Services.Assistant.Documents;

namespace TaskFlow.IntegrationTests.Assistant;

/// <summary>The API with document indexing switched on and a documents folder that does not exist.</summary>
public class MissingDocumentsFolderFactory : TaskFlowApiFactory
{
    public string MissingFolder { get; } = Path.Combine(Path.GetTempPath(), $"taskflow-no-docs-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DocumentKnowledge:Enabled"] = "true",
            ["DocumentKnowledge:SourcePath"] = MissingFolder,
            // Nothing listens here, so an embedding call could never succeed.
            ["DocumentKnowledge:OllamaBaseUrl"] = "http://127.0.0.1:9"
        }));
    }
}

public class DocumentIndexStartupTests : IClassFixture<MissingDocumentsFolderFactory>
{
    private readonly MissingDocumentsFolderFactory _factory;

    public DocumentIndexStartupTests(MissingDocumentsFolderFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async System.Threading.Tasks.Task TheApiStartsAndAnswersWithTheIndexerOnAndTheFolderMissing()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assistant/providers")).StatusCode);

        var indexer = _factory.Services.GetRequiredService<DocumentIndexerService>();
        Assert.True(await indexer.RunOnceAsync());

        var status = _factory.Services.GetRequiredService<DocumentIndexStatus>().Current;
        Assert.True(status.Enabled);
        Assert.False(status.FolderFound);
        Assert.Equal(Path.GetFullPath(_factory.MissingFolder), status.SourcePath);
        Assert.Contains("documents folder was not found", status.LastError);
        Assert.Equal(0, status.ChunkCount);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assistant/providers")).StatusCode);
    }
}
