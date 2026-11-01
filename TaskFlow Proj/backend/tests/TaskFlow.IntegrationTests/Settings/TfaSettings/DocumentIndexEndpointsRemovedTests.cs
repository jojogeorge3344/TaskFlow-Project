using System.Net;
using TaskFlow.Api.Common.Security;
using TaskFlow.IntegrationTests.Assistant;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests.Settings.TfaSettings;

/// <summary>The Document search card and its endpoints were removed (design D13): indexing problems go to the log.</summary>
public class DocumentIndexEndpointsRemovedTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Base = "/api/v1/assistant/documents";

    private readonly TaskFlowApiFactory _factory;

    public DocumentIndexEndpointsRemovedTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("GET", "status")]
    [InlineData("POST", "reindex")]
    public async Task TheOldIndexEndpointsAreGone(string method, string path)
    {
        var (client, token) = await DocumentAnswerEndpointsTests.SignInAsync(_factory, SystemRoles.Administrator);

        var response = await DocumentAnswerEndpointsTests.SendAsync(client, new HttpMethod(method), $"{Base}/{path}", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
