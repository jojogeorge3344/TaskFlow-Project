using System.Net;
using System.Text.Json.Nodes;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai.Embeddings;

/// <summary>Calls Ollama's <c>/api/embed</c>. Inputs are sent in batches, with the task prefix <c>nomic-embed-text</c>
/// expects, and every vector is L2-normalised so cosine similarity is a dot product.</summary>
public class OllamaEmbeddingClient : IEmbeddingClient
{
    public const int BatchSize = 16;

    private readonly HttpClient _client;

    public OllamaEmbeddingClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<float[][]> EmbedAsync(EmbeddingEndpoint endpoint, IReadOnlyList<string> inputs, EmbeddingPurpose purpose, CancellationToken cancellationToken = default)
    {
        var url = $"{endpoint.BaseUrl.TrimEnd('/')}/api/embed";
        var prefix = purpose == EmbeddingPurpose.Query ? "search_query: " : "search_document: ";
        var vectors = new List<float[]>(inputs.Count);

        foreach (var batch in inputs.Chunk(BatchSize))
        {
            var body = new JsonObject
            {
                ["model"] = endpoint.Model,
                ["input"] = new JsonArray(batch.Select(input => (JsonNode)JsonValue.Create(prefix + input)!).ToArray())
            };

            HttpResponseMessage response;
            string text;
            try
            {
                (response, text) = await ProviderHttp.SendAsync(_client, AiProviderKind.Ollama, ProviderHttp.Json(HttpMethod.Post, url, body), cancellationToken);
            }
            catch (ProviderFailureException failure)
            {
                throw new EmbeddingUnavailableException(
                    failure.Kind == ProviderFailureKind.Timeout ? EmbeddingFailureKind.Timeout : EmbeddingFailureKind.Unreachable,
                    endpoint,
                    failure);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new EmbeddingUnavailableException(IsModelNotFound(response, text) ? EmbeddingFailureKind.ModelNotFound : EmbeddingFailureKind.Unexpected, endpoint);
            }

            vectors.AddRange(Parse(text, batch.Length, endpoint));
        }

        return vectors.ToArray();
    }

    private static IEnumerable<float[]> Parse(string body, int expected, EmbeddingEndpoint endpoint)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new EmbeddingUnavailableException(EmbeddingFailureKind.Unexpected, endpoint, exception);
        }

        if (root?["embeddings"] is not JsonArray embeddings || embeddings.Count != expected)
        {
            throw new EmbeddingUnavailableException(EmbeddingFailureKind.Unexpected, endpoint);
        }

        return embeddings.Select(embedding => Normalise(
            (embedding as JsonArray ?? throw new EmbeddingUnavailableException(EmbeddingFailureKind.Unexpected, endpoint))
                .Select(value => value!.GetValue<float>())
                .ToArray(),
            endpoint));
    }

    private static float[] Normalise(float[] vector, EmbeddingEndpoint endpoint)
    {
        var length = Math.Sqrt(vector.Sum(value => (double)value * value));
        if (vector.Length == 0 || length == 0 || double.IsNaN(length))
        {
            throw new EmbeddingUnavailableException(EmbeddingFailureKind.Unexpected, endpoint);
        }

        return vector.Select(value => (float)(value / length)).ToArray();
    }

    private static bool IsModelNotFound(HttpResponseMessage response, string body)
    {
        string error;
        try
        {
            error = JsonNode.Parse(body)?["error"]?.ToString() ?? string.Empty;
        }
        catch (System.Text.Json.JsonException)
        {
            error = string.Empty;
        }

        return response.StatusCode == HttpStatusCode.NotFound
               || (error.Contains("not found", StringComparison.OrdinalIgnoreCase) && error.Contains("model", StringComparison.OrdinalIgnoreCase));
    }
}
