using System.Net;
using System.Text.Json.Nodes;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai.Embeddings;

/// <summary>Calls Google Gemini's <c>batchEmbedContents</c>. Inputs are sent in batches with the task prefix
/// <c>gemini-embedding-2</c> expects (it refuses the <c>taskType</c> field), the key travels in the
/// <c>x-goog-api-key</c> header and never in the URL, and every vector is L2-normalised so cosine similarity is a dot
/// product.</summary>
public class GeminiEmbeddingClient : IEmbeddingClient
{
    public const int BatchSize = 100;

    private readonly HttpClient _client;

    public GeminiEmbeddingClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<float[][]> EmbedAsync(EmbeddingEndpoint endpoint, IReadOnlyList<string> inputs, EmbeddingPurpose purpose, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint.ApiKey))
        {
            throw new EmbeddingUnavailableException(EmbeddingFailureKind.MissingKey, endpoint.Model);
        }

        var url = $"{GeminiProvider.BaseAddress}v1beta/models/{Uri.EscapeDataString(endpoint.Model)}:batchEmbedContents";
        var vectors = new List<float[]>(inputs.Count);

        foreach (var batch in inputs.Chunk(BatchSize))
        {
            var request = ProviderHttp.Json(HttpMethod.Post, url, BuildBody(endpoint, batch, purpose));
            request.Headers.Add("x-goog-api-key", endpoint.ApiKey);

            HttpResponseMessage response;
            string text;
            try
            {
                (response, text) = await ProviderHttp.SendAsync(_client, AiApiType.Gemini, request, cancellationToken);
            }
            catch (ProviderFailureException failure)
            {
                throw new EmbeddingUnavailableException(
                    failure.Kind == ProviderFailureKind.Timeout ? EmbeddingFailureKind.Timeout : EmbeddingFailureKind.Unreachable,
                    endpoint.Model,
                    failure);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new EmbeddingUnavailableException(Classify(response, text), endpoint.Model);
            }

            vectors.AddRange(Parse(text, batch.Length, endpoint.Model));
        }

        return vectors.ToArray();
    }

    /// <summary>The text as Google documents it for retrieval with <c>gemini-embedding-2</c>. A chunk's text already
    /// starts with its document title and headings, so the title slot is left as <c>none</c>.</summary>
    public static string Prefixed(string input, EmbeddingPurpose purpose) => purpose == EmbeddingPurpose.Query
        ? $"task: search result | query: {input}"
        : $"title: none | text: {input}";

    private static JsonObject BuildBody(EmbeddingEndpoint endpoint, IEnumerable<string> batch, EmbeddingPurpose purpose)
    {
        var model = $"models/{endpoint.Model}";
        return new JsonObject
        {
            ["requests"] = new JsonArray(batch.Select(input => (JsonNode)new JsonObject
            {
                ["model"] = model,
                ["content"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = Prefixed(input, purpose) }) },
                ["outputDimensionality"] = endpoint.Dimensions
            }).ToArray())
        };
    }

    private static IEnumerable<float[]> Parse(string body, int expected, string model)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new EmbeddingUnavailableException(EmbeddingFailureKind.Unexpected, model, exception);
        }

        if (root?["embeddings"] is not JsonArray embeddings || embeddings.Count != expected)
        {
            throw new EmbeddingUnavailableException(EmbeddingFailureKind.Unexpected, model);
        }

        return embeddings.Select(embedding => Normalise(
            ((embedding as JsonObject)?["values"] as JsonArray ?? throw new EmbeddingUnavailableException(EmbeddingFailureKind.Unexpected, model))
                .Select(value => value!.GetValue<float>())
                .ToArray(),
            model));
    }

    /// <summary><c>gemini-embedding-2</c> already returns unit vectors; normalising again is a guard for other models.</summary>
    private static float[] Normalise(float[] vector, string model)
    {
        var length = Math.Sqrt(vector.Sum(value => (double)value * value));
        if (vector.Length == 0 || length == 0 || double.IsNaN(length))
        {
            throw new EmbeddingUnavailableException(EmbeddingFailureKind.Unexpected, model);
        }

        return vector.Select(value => (float)(value / length)).ToArray();
    }

    /// <summary>The same reading of Google's error as <see cref="GeminiProvider"/>, reduced to what document search needs.</summary>
    private static EmbeddingFailureKind Classify(HttpResponseMessage response, string body)
    {
        var status = string.Empty;
        var message = string.Empty;
        try
        {
            var error = JsonNode.Parse(body)?["error"];
            status = error?["status"]?.GetValue<string>() ?? string.Empty;
            message = error?["message"]?.GetValue<string>() ?? string.Empty;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidOperationException)
        {
            // Not Google's error shape: classify by status code alone.
        }

        return true switch
        {
            _ when response.StatusCode == HttpStatusCode.TooManyRequests || status == "RESOURCE_EXHAUSTED" => EmbeddingFailureKind.RateLimited,
            _ when response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || status is "UNAUTHENTICATED" or "PERMISSION_DENIED"
                => EmbeddingFailureKind.InvalidKey,
            _ when message.Contains("API key", StringComparison.OrdinalIgnoreCase) => EmbeddingFailureKind.InvalidKey,
            _ when response.StatusCode == HttpStatusCode.NotFound || status == "NOT_FOUND" => EmbeddingFailureKind.ModelNotFound,
            _ => EmbeddingFailureKind.Unexpected
        };
    }
}
