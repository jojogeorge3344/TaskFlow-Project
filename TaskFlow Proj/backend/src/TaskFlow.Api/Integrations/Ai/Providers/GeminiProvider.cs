using System.Net;
using System.Text.Json.Nodes;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai.Providers;

/// <summary>Google Gemini (<c>generateContent</c>). The key travels in the <c>x-goog-api-key</c> header, never in the
/// URL, so it cannot end up in a request log.</summary>
public class GeminiProvider : IChatModelProvider
{
    public const string BaseAddress = "https://generativelanguage.googleapis.com/";

    private readonly HttpClient _client;

    public GeminiProvider(HttpClient client)
    {
        _client = client;
    }

    public AiApiType ApiType => AiApiType.Gemini;

    public async Task<ChatModelResult> CompleteAsync(AiProviderRuntime runtime, ChatModelRequest request, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseAddress}v1beta/models/{Uri.EscapeDataString(request.ModelName)}:generateContent";
        var httpRequest = ProviderHttp.Json(HttpMethod.Post, url, BuildBody(request));
        httpRequest.Headers.Add("x-goog-api-key", runtime.ApiKey ?? string.Empty);

        var (response, body) = await ProviderHttp.SendAsync(_client, ApiType, httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw Classify(response, body, runtime.ApiKey);
        }

        return Parse(body);
    }

    private static JsonObject BuildBody(ChatModelRequest request)
    {
        var contents = new JsonArray();
        foreach (var message in request.Messages)
        {
            switch (message.Role)
            {
                case AssistantMessageRole.User:
                    contents.Add(Content("user", new JsonObject { ["text"] = message.Content }));
                    break;
                case AssistantMessageRole.Assistant:
                    var parts = new List<JsonNode>();
                    if (!string.IsNullOrWhiteSpace(message.Content))
                    {
                        parts.Add(new JsonObject { ["text"] = message.Content });
                    }

                    foreach (var call in message.ToolCalls ?? [])
                    {
                        parts.Add(new JsonObject
                        {
                            ["functionCall"] = new JsonObject { ["name"] = call.Name, ["args"] = ProviderHttp.ArgumentsToNode(call.ArgumentsJson) }
                        });
                    }

                    if (parts.Count > 0)
                    {
                        contents.Add(Content("model", parts.ToArray()));
                    }

                    break;
                case AssistantMessageRole.Tool:
                    contents.Add(Content("user", new JsonObject
                    {
                        ["functionResponse"] = new JsonObject
                        {
                            ["name"] = message.ToolName,
                            ["response"] = new JsonObject { ["result"] = message.Content }
                        }
                    }));
                    break;
            }
        }

        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemPrompt }) },
            ["contents"] = contents
        };

        if (ThinkingConfig(request.ModelName) is { } thinking)
        {
            body["generationConfig"] = new JsonObject { ["thinkingConfig"] = thinking };
        }

        if (request.Tools.Count > 0)
        {
            body["tools"] = new JsonArray(new JsonObject
            {
                ["functionDeclarations"] = new JsonArray(request.Tools.Select(tool => (JsonNode)Declaration(tool)).ToArray())
            });
        }

        return body;
    }

    /// <summary>Budget for Gemini 2.5 models, which take a token budget instead of a level. 512 is within the range every
    /// 2.5 model accepts (Pro cannot turn thinking off, Flash-Lite needs at least 512).</summary>
    public const int Gemini25ThinkingBudget = 512;

    /// <summary>Keeps thinking short so an answer arrives well inside the timeout. Gemini 3 models take a
    /// <c>thinkingLevel</c> and refuse a budget; Gemini 2.5 models take a <c>thinkingBudget</c> and refuse a level; older
    /// and custom models do not think, so nothing is sent for them.</summary>
    public static JsonObject? ThinkingConfig(string modelName)
    {
        var model = modelName.Trim().ToLowerInvariant();
        if (model.StartsWith("gemini-3", StringComparison.Ordinal))
        {
            return new JsonObject { ["thinkingLevel"] = "low" };
        }

        if (model.StartsWith("gemini-2.5", StringComparison.Ordinal))
        {
            return new JsonObject { ["thinkingBudget"] = Gemini25ThinkingBudget };
        }

        return null;
    }

    /// <summary>Gemini rejects a function whose parameter schema is an object with no properties, so a tool that takes
    /// no arguments is declared without parameters.</summary>
    private static JsonObject Declaration(ChatToolDefinition tool)
    {
        var declaration = new JsonObject { ["name"] = tool.Name, ["description"] = tool.Description };
        if (ProviderHttp.ParseSchema(tool.ParametersJsonSchema) is JsonObject schema
            && schema["properties"] is JsonObject { Count: > 0 })
        {
            declaration["parameters"] = schema;
        }

        return declaration;
    }

    private static JsonObject Content(string role, params JsonNode[] parts) =>
        new() { ["role"] = role, ["parts"] = new JsonArray(parts) };

    private ChatModelResult Parse(string body)
    {
        var root = ProviderHttp.ParseOrFail(ApiType, body);
        var parts = root["candidates"]?[0]?["content"]?["parts"] as JsonArray;

        var text = new System.Text.StringBuilder();
        var calls = new List<ChatToolCall>();
        foreach (var part in parts ?? [])
        {
            if (part?["text"]?.GetValue<string>() is { } piece)
            {
                text.Append(piece);
            }
            else if (part?["functionCall"] is JsonObject call && call["name"]?.GetValue<string>() is { } name)
            {
                calls.Add(new ChatToolCall($"call_{calls.Count + 1}_{Guid.NewGuid():N}"[..20], name, ProviderHttp.ArgumentsToJson(call["args"])));
            }
        }

        if (text.Length == 0 && calls.Count == 0)
        {
            throw new ProviderFailureException(ApiType, ProviderFailureKind.Unexpected);
        }

        return new ChatModelResult(text.Length == 0 ? null : text.ToString(), calls);
    }

    private ProviderFailureException Classify(HttpResponseMessage response, string body, string? apiKey)
    {
        var status = string.Empty;
        var message = string.Empty;
        try
        {
            var error = JsonNode.Parse(body)?["error"];
            status = error?["status"]?.GetValue<string>() ?? string.Empty;
            message = error?["message"]?.GetValue<string>() ?? string.Empty;
        }
        catch (System.Text.Json.JsonException)
        {
            // Not JSON: classify by status code alone.
        }

        var kind = true switch
        {
            _ when response.StatusCode == HttpStatusCode.TooManyRequests || status == "RESOURCE_EXHAUSTED" => ProviderFailureKind.RateLimited,
            _ when response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || status is "UNAUTHENTICATED" or "PERMISSION_DENIED"
                => ProviderFailureKind.InvalidKey,
            _ when message.Contains("API key", StringComparison.OrdinalIgnoreCase) => ProviderFailureKind.InvalidKey,
            _ when response.StatusCode == HttpStatusCode.NotFound || status == "NOT_FOUND" => ProviderFailureKind.ModelUnavailable,
            // A malformed model name comes back as 400 INVALID_ARGUMENT ("unexpected model name format"), not 404: the
            // name is not a model ID at all, which needs a different fix from a model that does not exist.
            _ when (response.StatusCode == HttpStatusCode.BadRequest || status == "INVALID_ARGUMENT")
                   && message.Contains("model name", StringComparison.OrdinalIgnoreCase) => ProviderFailureKind.InvalidModelName,
            _ => ProviderFailureKind.Unexpected
        };

        return new ProviderFailureException(
            ApiType, kind, ProviderHttp.RetryAfter(response), httpStatus: (int)response.StatusCode,
            providerReason: ProviderHttp.Reason(status, message, apiKey));
    }
}
