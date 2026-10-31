using TaskFlow.Api.Common.Constants;
using System.Net;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using System.Text.Json.Nodes;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai.Providers;

/// <summary>Talks to a local Ollama server (<c>/api/chat</c>). No key is needed. A model that does not support tools
/// is retried once without them, on the same provider, so small local models still answer in plain text.</summary>
public partial class OllamaProvider : IChatModelProvider
{
    private readonly HttpClient _client;
    private readonly int _contextTokens;

    public OllamaProvider(HttpClient client, IOptions<AiProviderOptions>? options = null)
    {
        _client = client;
        _contextTokens = (options?.Value ?? new AiProviderOptions()).OllamaContextTokens;
    }

    public AiProviderKind Kind => AiProviderKind.Ollama;

    public async Task<ChatModelResult> CompleteAsync(AiProviderRuntime runtime, ChatModelRequest request, CancellationToken cancellationToken = default)
    {
        var url = $"{(runtime.BaseUrl ?? AiProviderCatalog.DefaultOllamaBaseUrl).TrimEnd('/')}/api/chat";

        var (response, body) = await ProviderHttp.SendAsync(
            _client, Kind, ProviderHttp.Json(HttpMethod.Post, url, BuildBody(request, includeTools: true)), cancellationToken);

        if (!response.IsSuccessStatusCode && request.Tools.Count > 0 && DoesNotSupportTools(body))
        {
            (response, body) = await ProviderHttp.SendAsync(
                _client, Kind, ProviderHttp.Json(HttpMethod.Post, url, BuildBody(request, includeTools: false)), cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw Classify(response, body);
        }

        var result = Parse(body);
        if (result.ToolCalls.Count > 0 || request.Tools.Count == 0 || ToolCallWrittenAsText(result.Text) is not { } written)
        {
            return result;
        }

        // Small models such as llama3.2 sometimes write a tool call as plain JSON text instead of calling the tool.
        // A call to a real tool is run as one; anything else (a made-up "tool", often named after a reference heading) is
        // asked again once without tools, so the model answers in words.
        if (request.Tools.Any(tool => tool.Name == written.Name))
        {
            return new ChatModelResult(null, [written]);
        }

        (response, body) = await ProviderHttp.SendAsync(
            _client, Kind, ProviderHttp.Json(HttpMethod.Post, url, BuildBody(request, includeTools: false)), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw Classify(response, body);
        }

        return Parse(body);
    }

    /// <summary>The tool call when the whole answer is a JSON object such as <c>{"name": "...", "parameters": {...}}</c>.</summary>
    private static ChatToolCall? ToolCallWrittenAsText(string? text)
    {
        var trimmed = text?.Trim();
        if (trimmed is null || !trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
        {
            return null;
        }

        try
        {
            if (JsonNode.Parse(trimmed) is JsonObject obj
                && obj["name"] is JsonValue nameValue && nameValue.TryGetValue<string>(out var name) && !string.IsNullOrWhiteSpace(name)
                && (obj.ContainsKey("parameters") || obj.ContainsKey("arguments")))
            {
                return new ChatToolCall(
                    $"call_text_{Guid.NewGuid():N}"[..20],
                    name,
                    ProviderHttp.ArgumentsToJson(obj["parameters"] ?? obj["arguments"]));
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Broken JSON that still starts like a tool call cannot be run; it is asked again without tools.
            if (BrokenToolCall().IsMatch(trimmed))
            {
                return new ChatToolCall($"call_text_{Guid.NewGuid():N}"[..20], string.Empty, "{}");
            }
        }

        return null;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\{\s*""name""\s*:\s*""")]
    private static partial System.Text.RegularExpressions.Regex BrokenToolCall();

    private JsonObject BuildBody(ChatModelRequest request, bool includeTools)
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
                    var assistant = new JsonObject { ["role"] = "assistant", ["content"] = message.Content };
                    if (includeTools && message.ToolCalls is { Count: > 0 } calls)
                    {
                        assistant["tool_calls"] = new JsonArray(calls.Select(call => (JsonNode)new JsonObject
                        {
                            ["function"] = new JsonObject
                            {
                                ["name"] = call.Name,
                                ["arguments"] = ProviderHttp.ArgumentsToNode(call.ArgumentsJson)
                            }
                        }).ToArray());
                    }

                    messages.Add(assistant);
                    break;
                case AssistantMessageRole.Tool:
                    messages.Add(includeTools
                        ? new JsonObject { ["role"] = "tool", ["content"] = message.Content, ["tool_name"] = message.ToolName }
                        : new JsonObject { ["role"] = "user", ["content"] = $"Result of {message.ToolName}: {message.Content}" });
                    break;
            }
        }

        var body = new JsonObject
        {
            ["model"] = request.ModelName,
            ["messages"] = messages,
            ["stream"] = false,
            ["options"] = new JsonObject { ["num_ctx"] = _contextTokens }
        };
        if (includeTools && request.Tools.Count > 0)
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
        var message = root["message"];
        var text = message?["content"]?.GetValue<string>();

        var calls = new List<ChatToolCall>();
        if (message?["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var call in toolCalls)
            {
                var name = call?["function"]?["name"]?.GetValue<string>();
                if (name is null)
                {
                    continue;
                }

                calls.Add(new ChatToolCall(
                    $"call_{calls.Count + 1}_{Guid.NewGuid():N}"[..20],
                    name,
                    ProviderHttp.ArgumentsToJson(call!["function"]!["arguments"])));
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
        var error = ErrorText(body);
        var kind = response.StatusCode switch
        {
            HttpStatusCode.NotFound => ProviderFailureKind.ModelUnavailable,
            HttpStatusCode.TooManyRequests => ProviderFailureKind.RateLimited,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ProviderFailureKind.InvalidKey,
            _ when error.Contains("not found", StringComparison.OrdinalIgnoreCase) && error.Contains("model", StringComparison.OrdinalIgnoreCase)
                => ProviderFailureKind.ModelUnavailable,
            _ => ProviderFailureKind.Unexpected
        };

        return new ProviderFailureException(Kind, kind, ProviderHttp.RetryAfter(response), httpStatus: (int)response.StatusCode);
    }

    private static bool DoesNotSupportTools(string body) =>
        ErrorText(body).Contains("does not support tools", StringComparison.OrdinalIgnoreCase);

    private static string ErrorText(string body)
    {
        try
        {
            return JsonNode.Parse(body)?["error"]?.ToString() ?? string.Empty;
        }
        catch (System.Text.Json.JsonException)
        {
            return string.Empty;
        }
    }
}
