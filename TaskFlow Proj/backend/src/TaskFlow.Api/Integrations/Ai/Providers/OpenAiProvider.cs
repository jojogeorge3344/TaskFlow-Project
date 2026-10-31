using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai.Providers;

/// <summary>OpenAI chat completions. OpenAI has no free tier: this only works with a paid key an administrator saved.</summary>
public class OpenAiProvider : IChatModelProvider
{
    public const string BaseAddress = "https://api.openai.com/";

    private readonly HttpClient _client;

    public OpenAiProvider(HttpClient client)
    {
        _client = client;
    }

    public AiProviderKind Kind => AiProviderKind.OpenAI;

    public async Task<ChatModelResult> CompleteAsync(AiProviderRuntime runtime, ChatModelRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = ProviderHttp.Json(HttpMethod.Post, $"{BaseAddress}v1/chat/completions", BuildBody(request));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", runtime.ApiKey ?? string.Empty);

        var (response, body) = await ProviderHttp.SendAsync(_client, Kind, httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw Classify(response, body);
        }

        return Parse(body);
    }

    private static JsonObject BuildBody(ChatModelRequest request)
    {
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt } };
        foreach (var message in request.Messages)
        {
            switch (message.Role)
            {
                case AssistantMessageRole.User:
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = message.Content });
                    break;
                case AssistantMessageRole.Assistant:
                    var assistant = new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = string.IsNullOrEmpty(message.Content) ? null : message.Content
                    };
                    if (message.ToolCalls is { Count: > 0 } calls)
                    {
                        assistant["tool_calls"] = new JsonArray(calls.Select(call => (JsonNode)new JsonObject
                        {
                            ["id"] = call.Id,
                            ["type"] = "function",
                            ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.ArgumentsJson }
                        }).ToArray());
                    }

                    messages.Add(assistant);
                    break;
                case AssistantMessageRole.Tool:
                    messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = message.ToolCallId, ["content"] = message.Content });
                    break;
            }
        }

        var body = new JsonObject { ["model"] = request.ModelName, ["messages"] = messages };
        if (request.Tools.Count > 0)
        {
            body["tools"] = new JsonArray(request.Tools.Select(tool => (JsonNode)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = ProviderHttp.ParseSchema(tool.ParametersJsonSchema)
                }
            }).ToArray());
        }

        return body;
    }

    private ChatModelResult Parse(string body)
    {
        var root = ProviderHttp.ParseOrFail(Kind, body);
        var message = root["choices"]?[0]?["message"];
        var text = message?["content"]?.GetValue<string>();

        var calls = new List<ChatToolCall>();
        if (message?["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var call in toolCalls)
            {
                var name = call?["function"]?["name"]?.GetValue<string>();
                var id = call?["id"]?.GetValue<string>();
                if (name is null || id is null)
                {
                    continue;
                }

                calls.Add(new ChatToolCall(id, name, ProviderHttp.ArgumentsToJson(call!["function"]!["arguments"])));
            }
        }

        if (string.IsNullOrWhiteSpace(text) && calls.Count == 0)
        {
            throw new ProviderFailureException(Kind, ProviderFailureKind.Unexpected);
        }

        return new ChatModelResult(string.IsNullOrWhiteSpace(text) ? null : text, calls);
    }

    private ProviderFailureException Classify(HttpResponseMessage response, string body)
    {
        var code = string.Empty;
        try
        {
            code = JsonNode.Parse(body)?["error"]?["code"]?.ToString() ?? string.Empty;
        }
        catch (System.Text.Json.JsonException)
        {
            // Not JSON: classify by status code alone.
        }

        var kind = true switch
        {
            _ when response.StatusCode == HttpStatusCode.TooManyRequests || code is "insufficient_quota" or "rate_limit_exceeded"
                => ProviderFailureKind.RateLimited,
            _ when response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || code == "invalid_api_key"
                => ProviderFailureKind.InvalidKey,
            _ when response.StatusCode == HttpStatusCode.NotFound || code == "model_not_found" => ProviderFailureKind.ModelUnavailable,
            _ => ProviderFailureKind.Unexpected
        };

        return new ProviderFailureException(Kind, kind, ProviderHttp.RetryAfter(response), httpStatus: (int)response.StatusCode);
    }
}
