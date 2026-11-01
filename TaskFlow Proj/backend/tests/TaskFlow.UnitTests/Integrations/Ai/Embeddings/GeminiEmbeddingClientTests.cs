using System.Net;
using System.Text.Json.Nodes;
using TaskFlow.Api.Integrations.Ai.Embeddings;

namespace TaskFlow.UnitTests.Integrations.Ai.Embeddings;

public class GeminiEmbeddingClientTests
{
    private const string Key = "AIzaSyTestKey0123456789";

    private static readonly EmbeddingEndpoint Endpoint = new("gemini-embedding-2", 768, Key);

    private static string Embeddings(int count, string vector = "[3, 4]") =>
        $$"""{"embeddings":[{{string.Join(",", Enumerable.Repeat($$"""{"values":{{vector}}}""", count))}}]}""";

    private static JsonArray Requests(string body) => JsonNode.Parse(body)!["requests"]!.AsArray();

    [Fact]
    public async Task ADocumentBatchIsSentWithTheKeyInTheHeaderAndTheDocumentPrefix()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, Embeddings(2));
        var client = new GeminiEmbeddingClient(new HttpClient(handler));

        await client.EmbedAsync(Endpoint, ["first section", "second section"], EmbeddingPurpose.Document);

        var (request, body) = Assert.Single(handler.Sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:batchEmbedContents",
            request.RequestUri!.ToString());
        Assert.DoesNotContain(Key, request.RequestUri!.ToString());
        Assert.Equal(Key, Assert.Single(request.Headers.GetValues("x-goog-api-key")));

        var requests = Requests(body);
        Assert.All(requests, item =>
        {
            Assert.Equal("models/gemini-embedding-2", item!["model"]!.GetValue<string>());
            Assert.Equal(768, item["outputDimensionality"]!.GetValue<int>());
            Assert.Null(item["taskType"]);
        });
        Assert.Equal(
            ["title: none | text: first section", "title: none | text: second section"],
            requests.Select(item => item!["content"]!["parts"]![0]!["text"]!.GetValue<string>()));
    }

    [Fact]
    public async Task AQueryIsSentWithTheQueryPrefixOnly()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, Embeddings(1));
        var client = new GeminiEmbeddingClient(new HttpClient(handler));

        await client.EmbedAsync(Endpoint, ["who gets the overdue reminder?"], EmbeddingPurpose.Query);

        var item = Assert.Single(Requests(handler.Sent[0].Body))!;
        Assert.Equal("task: search result | query: who gets the overdue reminder?", item["content"]!["parts"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task OneHundredFiftyInputsAreSentInTwoBatchesAndComeBackInOrder()
    {
        var handler = new FakeHttpHandler()
            .Reply(HttpStatusCode.OK, Embeddings(100, "[1, 0]"))
            .Reply(HttpStatusCode.OK, Embeddings(50, "[0, 1]"));
        var client = new GeminiEmbeddingClient(new HttpClient(handler));
        var inputs = Enumerable.Range(0, 150).Select(index => $"chunk {index}").ToList();

        var vectors = await client.EmbedAsync(Endpoint, inputs, EmbeddingPurpose.Document);

        Assert.Equal(150, vectors.Length);
        Assert.Equal([100, 50], handler.Sent.Select(sent => Requests(sent.Body).Count));
        Assert.Equal("title: none | text: chunk 100", Requests(handler.Sent[1].Body)[0]!["content"]!["parts"]![0]!["text"]!.GetValue<string>());
        Assert.Equal([1f, 0f], vectors[99]);
        Assert.Equal([0f, 1f], vectors[100]);
    }

    [Fact]
    public async Task VectorsAreNormalised()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, Embeddings(1, "[3, 4]"));
        var client = new GeminiEmbeddingClient(new HttpClient(handler));

        var vector = Assert.Single(await client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal([0.6f, 0.8f], vector);
    }

    [Fact]
    public async Task NoKeyIsMissingKeyAndNothingIsSent()
    {
        var handler = new FakeHttpHandler();
        var client = new GeminiEmbeddingClient(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<EmbeddingUnavailableException>(
            () => client.EmbedAsync(Endpoint with { ApiKey = "" }, ["x"], EmbeddingPurpose.Query));

        Assert.Equal(EmbeddingFailureKind.MissingKey, failure.Kind);
        Assert.Empty(handler.Sent);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"code":401,"status":"UNAUTHENTICATED","message":"Request had invalid authentication credentials."}}""", EmbeddingFailureKind.InvalidKey)]
    [InlineData(HttpStatusCode.Forbidden, """{"error":{"code":403,"status":"PERMISSION_DENIED","message":"Permission denied."}}""", EmbeddingFailureKind.InvalidKey)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":400,"status":"INVALID_ARGUMENT","message":"API key not valid. Please pass a valid API key."}}""", EmbeddingFailureKind.InvalidKey)]
    [InlineData(HttpStatusCode.TooManyRequests, """{"error":{"code":429,"status":"RESOURCE_EXHAUSTED","message":"Quota exceeded."}}""", EmbeddingFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"code":404,"status":"NOT_FOUND","message":"models/gemini-embedding-9 is not found."}}""", EmbeddingFailureKind.ModelNotFound)]
    [InlineData(HttpStatusCode.InternalServerError, "oops", EmbeddingFailureKind.Unexpected)]
    public async Task ARefusalIsClassified(HttpStatusCode status, string body, EmbeddingFailureKind expected)
    {
        var handler = new FakeHttpHandler().Reply(status, body);
        var client = new GeminiEmbeddingClient(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal(expected, failure.Kind);
        Assert.DoesNotContain(Key, failure.Message);
    }

    [Fact]
    public async Task ARefusedConnectionIsUnreachable()
    {
        var handler = new FakeHttpHandler().Throw(new HttpRequestException("No such host is known"));
        var client = new GeminiEmbeddingClient(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal(EmbeddingFailureKind.Unreachable, failure.Kind);
    }

    [Fact]
    public async Task ATimeoutIsUnavailable()
    {
        var handler = new FakeHttpHandler().Throw(new TaskCanceledException("timed out"));
        var client = new GeminiEmbeddingClient(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal(EmbeddingFailureKind.Timeout, failure.Kind);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"embeddings":[]}""")]
    [InlineData("""{"embeddings":[{"values":[0, 0]}]}""")]
    [InlineData("""{"embeddings":[[3, 4]]}""")]
    public async Task AMalformedAnswerIsUnexpected(string body)
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, body);
        var client = new GeminiEmbeddingClient(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal(EmbeddingFailureKind.Unexpected, failure.Kind);
    }

    [Fact]
    public void TheEndpointNeverPrintsTheKey() => Assert.DoesNotContain(Key, Endpoint.ToString());
}
