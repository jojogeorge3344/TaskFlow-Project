using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace TaskFlow.IntegrationTests;

/// <summary>A published QA build carries the compiled client in wwwroot and serves it beside the API, so one address and one
/// origin cover both. Without a client the API behaves exactly as before.</summary>
public class ClientHostingTests : IClassFixture<TaskFlowApiFactory>, IDisposable
{
    private const string ClientPage = "<html><body>TaskFlow client</body></html>";

    private readonly TaskFlowApiFactory _factory;
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"taskflow-webroot-tests-{Guid.NewGuid():N}");

    public ClientHostingTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(bool withClient)
    {
        Directory.CreateDirectory(_webRoot);
        if (withClient)
        {
            File.WriteAllText(Path.Combine(_webRoot, "index.html"), ClientPage);
        }

        return _factory.WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot)).CreateClient();
    }

    [Fact]
    public async Task Root_ReturnsTheClientPage()
    {
        var response = await CreateClient(withClient: true).GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("TaskFlow client", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ClientRoute_ReturnsTheClientPage()
    {
        var response = await CreateClient(withClient: true).GetAsync("/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("TaskFlow client", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownApiPath_StaysNotFound()
    {
        var response = await CreateClient(withClient: true).GetAsync("/api/v1/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("TaskFlow client", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutAClient_ClientRouteIsNotFound()
    {
        var response = await CreateClient(withClient: false).GetAsync("/admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
        {
            try
            {
                Directory.Delete(_webRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
