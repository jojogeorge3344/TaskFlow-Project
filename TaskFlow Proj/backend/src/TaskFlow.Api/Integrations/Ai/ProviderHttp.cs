using TaskFlow.Api.Integrations.Interfaces.Ai;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TaskFlow.Application.Assistant;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai;

/// <summary>Shared plumbing for the provider clients: sends a request, and turns every network problem into a
/// <see cref="ProviderFailureException"/> so callers never see a raw exception or a raw provider message.</summary>
internal static class ProviderHttp
{
    public static async Task<(HttpResponseMessage Response, string Body)> SendAsync(
        HttpClient client,
        AiProviderKind provider,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response, body);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderFailureException(provider, ProviderFailureKind.Timeout, inner: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderFailureException(provider, ProviderFailureKind.Unreachable, inner: exception);
        }
    }

    public static HttpRequestMessage Json(HttpMethod method, string url, JsonNode body)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        return request;
    }

    public static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : null;
        }

        return null;
    }

    public static JsonNode ParseOrFail(AiProviderKind provider, string body)
    {
        try
        {
            return JsonNode.Parse(body) ?? throw new ProviderFailureException(provider, ProviderFailureKind.Unexpected);
        }
        catch (JsonException exception)
        {
            throw new ProviderFailureException(provider, ProviderFailureKind.Unexpected, inner: exception);
        }
    }

    public static JsonNode ParseSchema(string schemaJson) =>
        JsonNode.Parse(schemaJson) ?? new JsonObject();

    /// <summary>The model's tool arguments as a JSON object string; anything else becomes an empty object.</summary>
    public static string ArgumentsToJson(JsonNode? arguments)
    {
        if (arguments is JsonObject obj)
        {
            return obj.ToJsonString();
        }

        if (arguments is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
        {
            try
            {
                return JsonNode.Parse(text) is JsonObject parsed ? parsed.ToJsonString() : "{}";
            }
            catch (JsonException)
            {
                return "{}";
            }
        }

        return "{}";
    }

    public static JsonNode ArgumentsToNode(string argumentsJson)
    {
        try
        {
            return JsonNode.Parse(argumentsJson) is JsonObject parsed ? parsed : new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }
}
