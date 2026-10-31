using System.Net;
using System.Text.Json.Nodes;
using TaskFlow.Api.Integrations.Ai.Embeddings;

namespace TaskFlow.UnitTests.Integrations.Ai.Embeddings;

public class OllamaEmbeddingClientTests
{
    private static readonly EmbeddingEndpoint Endpoint = new("http://localhost:11434/", "nomic-embed-text");

    private static string Embeddings(int count, string vector = "[3, 4]") =>
        $$"""{"model":"nomic-embed-text","embeddings":[{{string.Join(",", Enumerable.Repeat(vector, count))}}]}""";

    [Fact]
    public async Task ADocumentBatchIsSentWithTheDocumentPrefix()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, Embeddings(2));
        var client = new OllamaEmbeddingClient(new HttpClient(handler));

        await client.EmbedAsync(Endpoint, ["first section", "second section"], EmbeddingPurpose.Document);

        var (request, body) = Assert.Single(handler.Sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://localhost:11434/api/embed", request.RequestUri!.ToString());
        var json = JsonNode.Parse(body)!;
        Assert.Equal("nomic-embed-text", json["model"]!.GetValue<string>());
        Assert.Equal(
            ["search_document: first section", "search_document: second section"],
            json["input"]!.AsArray().Select(input => input!.GetValue<string>()));
    }

    [Fact]
    public async Task AQueryIsSentWithTheQueryPrefix()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, Embeddings(1));
        var client = new OllamaEmbeddingClient(new HttpClient(handler));

        await client.EmbedAsync(Endpoint, ["who gets the overdue reminder?"], EmbeddingPurpose.Query);

        Assert.Equal("search_query: who gets the overdue reminder?", JsonNode.Parse(handler.Sent[0].Body)!["input"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task FortyInputsAreSentInThreeBatchesAndComeBackInOrder()
    {
        var handler = new FakeHttpHandler()
            .Reply(HttpStatusCode.OK, Embeddings(16))
            .Reply(HttpStatusCode.OK, Embeddings(16))
            .Reply(HttpStatusCode.OK, Embeddings(8));
        var client = new OllamaEmbeddingClient(new HttpClient(handler));
        var inputs = Enumerable.Range(0, 40).Select(index => $"chunk {index}").ToList();

        var vectors = await client.EmbedAsync(Endpoint, inputs, EmbeddingPurpose.Document);

        Assert.Equal(40, vectors.Length);
        Assert.Equal([16, 16, 8], handler.Sent.Select(sent => JsonNode.Parse(sent.Body)!["input"]!.AsArray().Count));
        Assert.Equal("search_document: chunk 16", JsonNode.Parse(handler.Sent[1].Body)!["input"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task VectorsAreNormalised()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, Embeddings(1, "[3, 4]"));
        var client = new OllamaEmbeddingClient(new HttpClient(handler));

        var vector = Assert.Single(await client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal([0.6f, 0.8f], vector);
    }

    [Fact]
    public async Task ARefusedConnectionIsUnavailable()
    {
        var handler = new FakeHttpHandler().Throw(new HttpRequestException("Connection refused"));
        var client = new OllamaEmbeddingClient(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal(EmbeddingFailureKind.Unreachable, failure.Kind);
        Assert.Contains("http://localhost:11434", failure.Message);
    }

    [Fact]
    public async Task AMissingModelIsUnavailableAndSaysHowToPullIt()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.NotFound, """{"error":"model \"nomic-embed-text\" not found, try pulling it first"}""");
        var client = new OllamaEmbeddingClient(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal(EmbeddingFailureKind.ModelNotFound, failure.Kind);
        Assert.Contains("ollama pull nomic-embed-text", failure.Message);
    }

    [Fact]
    public async Task ATimeoutIsUnavailable()
    {
        var handler = new FakeHttpHandler().Throw(new TaskCanceledException("timed out"));
        var client = new OllamaEmbeddingClient(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal(EmbeddingFailureKind.Timeout, failure.Kind);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"embeddings":[]}""")]
    [InlineData("""{"embeddings":[[0, 0]]}""")]
    public async Task AMalformedAnswerIsUnavailable(string body)
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, body);
        var client = new OllamaEmbeddingClient(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => client.EmbedAsync(Endpoint, ["x"], EmbeddingPurpose.Query));

        Assert.Equal(EmbeddingFailureKind.Unexpected, failure.Kind);
    }
}
