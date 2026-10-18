using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using TaskFlow.Application.Assistant;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Assistant;

namespace TaskFlow.UnitTests;

/// <summary>A scripted <see cref="HttpMessageHandler"/> that records what was sent.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<(HttpRequestMessage Request, string Body)> Sent { get; } = [];

    public FakeHttpHandler Reply(HttpStatusCode status, string json, Action<HttpResponseMessage>? configure = null)
    {
        _responses.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            configure?.Invoke(response);
            return response;
        });
        return this;
    }

    public FakeHttpHandler Throw(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Sent.Add((request, body));
        return _responses.Dequeue()(request);
    }
}

public class ChatProviderTests
{
    private static readonly ChatToolDefinition PrioritiseTool = new(
        "prioritise_tasks", "Ranks the user's open tasks.", """{"type":"object","properties":{}}""");

    private static ChatModelRequest Request(params ChatMessage[] messages) =>
        new("You are TFA.", messages, [PrioritiseTool], "test-model");

    private static ChatMessage User(string text) => new(AssistantMessageRole.User, text);

    // ------------------------------------------------------------------ Ollama

    private static AiProviderRuntime OllamaRuntime => new(AiProviderKind.Ollama, "llama3.1:8b", "http://localhost:11434", null);

    [Fact]
    public async Task OllamaTextReplyRoundTrips()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"message":{"role":"assistant","content":"Hello Asha"}}""");
        var provider = new OllamaProvider(new HttpClient(handler));

        var result = await provider.CompleteAsync(OllamaRuntime, Request(User("hi")));

        Assert.Equal("Hello Asha", result.Text);
        Assert.Empty(result.ToolCalls);
        var (request, body) = Assert.Single(handler.Sent);
        Assert.Equal("http://localhost:11434/api/chat", request.RequestUri!.ToString());
        var json = JsonNode.Parse(body)!;
        Assert.Equal("test-model", json["model"]!.GetValue<string>());
        Assert.Equal("system", json["messages"]![0]!["role"]!.GetValue<string>());
        Assert.False(json["stream"]!.GetValue<bool>());
        Assert.Equal("prioritise_tasks", json["tools"]![0]!["function"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task OllamaToolCallAndToolResultRoundTrip()
    {
        var handler = new FakeHttpHandler()
            .Reply(HttpStatusCode.OK, """{"message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"prioritise_tasks","arguments":{"limit":3}}}]}}""")
            .Reply(HttpStatusCode.OK, """{"message":{"role":"assistant","content":"Start with TF-102."}}""");
        var provider = new OllamaProvider(new HttpClient(handler));

        var first = await provider.CompleteAsync(OllamaRuntime, Request(User("what first?")));
        var call = Assert.Single(first.ToolCalls);
        Assert.Equal("prioritise_tasks", call.Name);
        Assert.Equal(3, JsonNode.Parse(call.ArgumentsJson)!["limit"]!.GetValue<int>());

        var second = await provider.CompleteAsync(OllamaRuntime, Request(
            User("what first?"),
            new ChatMessage(AssistantMessageRole.Assistant, "", [call]),
            new ChatMessage(AssistantMessageRole.Tool, "TF-102 overdue", ToolCallId: call.Id, ToolName: call.Name)));

        Assert.Equal("Start with TF-102.", second.Text);
        var sent = JsonNode.Parse(handler.Sent[1].Body)!["messages"]!.AsArray();
        Assert.Equal("tool", sent[^1]!["role"]!.GetValue<string>());
        Assert.Equal("prioritise_tasks", sent[^1]!["tool_name"]!.GetValue<string>());
    }

    [Fact]
    public async Task OllamaModelWithoutToolSupportIsRetriedWithoutTools()
    {
        var handler = new FakeHttpHandler()
            .Reply(HttpStatusCode.BadRequest, """{"error":"registry.ollama.ai/library/tiny does not support tools"}""")
            .Reply(HttpStatusCode.OK, """{"message":{"role":"assistant","content":"Plain answer"}}""");
        var provider = new OllamaProvider(new HttpClient(handler));

        var result = await provider.CompleteAsync(OllamaRuntime, Request(User("hi")));

        Assert.Equal("Plain answer", result.Text);
        Assert.Equal(2, handler.Sent.Count);
        Assert.Null(JsonNode.Parse(handler.Sent[1].Body)!["tools"]);
    }

    [Fact]
    public async Task OllamaConnectionRefusedIsUnreachable()
    {
        var handler = new FakeHttpHandler().Throw(new HttpRequestException("Connection refused"));
        var provider = new OllamaProvider(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(OllamaRuntime, Request(User("hi"))));

        Assert.Equal(ProviderFailureKind.Unreachable, failure.Kind);
        Assert.Equal(AiProviderKind.Ollama, failure.Provider);
    }

    [Fact]
    public async Task OllamaModelNotFoundIsModelUnavailable()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.NotFound, """{"error":"model 'llama3.1:8b' not found"}""");
        var provider = new OllamaProvider(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(OllamaRuntime, Request(User("hi"))));

        Assert.Equal(ProviderFailureKind.ModelUnavailable, failure.Kind);
    }

    // ------------------------------------------------------------------ Gemini

    private static AiProviderRuntime GeminiRuntime => new(AiProviderKind.Gemini, "gemini-2.0-flash", null, "AIzaSyTestKey1234");

    [Fact]
    public async Task GeminiTextReplyAndKeyHeader()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"Hi "},{"text":"there"}]}}]}""");
        var provider = new GeminiProvider(new HttpClient(handler));

        var result = await provider.CompleteAsync(GeminiRuntime, Request(User("hi")));

        Assert.Equal("Hi there", result.Text);
        var (request, body) = Assert.Single(handler.Sent);
        Assert.Contains("models/test-model:generateContent", request.RequestUri!.ToString());
        Assert.DoesNotContain("AIzaSyTestKey1234", request.RequestUri.ToString());
        Assert.Equal("AIzaSyTestKey1234", request.Headers.GetValues("x-goog-api-key").Single());
        var json = JsonNode.Parse(body)!;
        Assert.Equal("You are TFA.", json["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("prioritise_tasks", json["tools"]![0]!["functionDeclarations"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task GeminiDeclaresAToolWithNoArgumentsWithoutParameters()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var provider = new GeminiProvider(new HttpClient(handler));
        var noArguments = new ChatToolDefinition("explain_password_change", "No arguments.", """{"type":"object","properties":{}}""");

        await provider.CompleteAsync(GeminiRuntime, new ChatModelRequest("sys", [User("hi")], [PrioritiseTool, noArguments], "m"));

        var declarations = JsonNode.Parse(handler.Sent[0].Body)!["tools"]![0]!["functionDeclarations"]!.AsArray();
        Assert.Null(declarations.Single(d => d!["name"]!.GetValue<string>() == "explain_password_change")!["parameters"]);
        Assert.Null(declarations.Single(d => d!["name"]!.GetValue<string>() == "prioritise_tasks")!["parameters"]);
    }

    [Fact]
    public async Task GeminiKeepsParametersForAToolThatHasProperties()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var provider = new GeminiProvider(new HttpClient(handler));
        var withArgs = new ChatToolDefinition("get_my_tasks", "Lists tasks.", """{"type":"object","properties":{"limit":{"type":"integer"}}}""");

        await provider.CompleteAsync(GeminiRuntime, new ChatModelRequest("sys", [User("hi")], [withArgs], "m"));

        var declaration = JsonNode.Parse(handler.Sent[0].Body)!["tools"]![0]!["functionDeclarations"]![0]!;
        Assert.Equal("integer", declaration["parameters"]!["properties"]!["limit"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task AFailureCarriesTheHttpStatusForTheLog()
    {
        var provider = new GeminiProvider(new HttpClient(new FakeHttpHandler().Reply(HttpStatusCode.BadRequest, """{"error":{"status":"INVALID_ARGUMENT","message":"bad schema"}}""")));

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(GeminiRuntime, Request(User("hi"))));

        Assert.Equal(ProviderFailureKind.Unexpected, failure.Kind);
        Assert.Equal(400, failure.HttpStatus);
    }

    [Fact]
    public async Task GeminiFunctionCallAndResultRoundTrip()
    {
        var handler = new FakeHttpHandler()
            .Reply(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"functionCall":{"name":"prioritise_tasks","args":{"limit":2}}}]}}]}""")
            .Reply(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"Done"}]}}]}""");
        var provider = new GeminiProvider(new HttpClient(handler));

        var first = await provider.CompleteAsync(GeminiRuntime, Request(User("go")));
        var call = Assert.Single(first.ToolCalls);
        Assert.Equal(2, JsonNode.Parse(call.ArgumentsJson)!["limit"]!.GetValue<int>());

        await provider.CompleteAsync(GeminiRuntime, Request(
            User("go"),
            new ChatMessage(AssistantMessageRole.Assistant, "", [call]),
            new ChatMessage(AssistantMessageRole.Tool, "ranked list", ToolCallId: call.Id, ToolName: call.Name)));

        var contents = JsonNode.Parse(handler.Sent[1].Body)!["contents"]!.AsArray();
        Assert.Equal("model", contents[1]!["role"]!.GetValue<string>());
        Assert.NotNull(contents[1]!["parts"]![0]!["functionCall"]);
        Assert.Equal("prioritise_tasks", contents[2]!["parts"]![0]!["functionResponse"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task GeminiResourceExhaustedIsRateLimitedWithRetryAfter()
    {
        var handler = new FakeHttpHandler().Reply(
            HttpStatusCode.TooManyRequests,
            """{"error":{"code":429,"status":"RESOURCE_EXHAUSTED","message":"quota"}}""",
            response => response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42)));
        var provider = new GeminiProvider(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(GeminiRuntime, Request(User("hi"))));

        Assert.Equal(ProviderFailureKind.RateLimited, failure.Kind);
        Assert.Equal(TimeSpan.FromSeconds(42), failure.RetryAfter);
    }

    [Fact]
    public async Task GeminiStatusAloneMeansRateLimited()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.BadRequest, """{"error":{"status":"RESOURCE_EXHAUSTED"}}""");
        var provider = new GeminiProvider(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(GeminiRuntime, Request(User("hi"))));

        Assert.Equal(ProviderFailureKind.RateLimited, failure.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{"error":{"status":"PERMISSION_DENIED","message":"denied"}}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"status":"INVALID_ARGUMENT","message":"API key not valid. Please pass a valid API key."}}""")]
    public async Task GeminiKeyProblemsAreInvalidKey(HttpStatusCode status, string body)
    {
        var provider = new GeminiProvider(new HttpClient(new FakeHttpHandler().Reply(status, body)));

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(GeminiRuntime, Request(User("hi"))));

        Assert.Equal(ProviderFailureKind.InvalidKey, failure.Kind);
    }

    // ------------------------------------------------------------------ OpenAI

    private static AiProviderRuntime OpenAiRuntime => new(AiProviderKind.OpenAI, "gpt-4o-mini", null, "sk-test-9999");

    [Fact]
    public async Task OpenAiTextReplyAndBearerHeader()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, """{"choices":[{"message":{"role":"assistant","content":"Hello"}}]}""");
        var provider = new OpenAiProvider(new HttpClient(handler));

        var result = await provider.CompleteAsync(OpenAiRuntime, Request(User("hi")));

        Assert.Equal("Hello", result.Text);
        var (request, body) = Assert.Single(handler.Sent);
        Assert.Equal("https://api.openai.com/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test-9999", request.Headers.Authorization.Parameter);
        Assert.DoesNotContain("sk-test-9999", body);
        Assert.Equal("prioritise_tasks", JsonNode.Parse(body)!["tools"]![0]!["function"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task OpenAiToolCallAndToolResultRoundTrip()
    {
        var handler = new FakeHttpHandler()
            .Reply(HttpStatusCode.OK, """{"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[{"id":"call_abc","type":"function","function":{"name":"prioritise_tasks","arguments":"{\"limit\":5}"}}]}}]}""")
            .Reply(HttpStatusCode.OK, """{"choices":[{"message":{"role":"assistant","content":"Ok"}}]}""");
        var provider = new OpenAiProvider(new HttpClient(handler));

        var first = await provider.CompleteAsync(OpenAiRuntime, Request(User("go")));
        var call = Assert.Single(first.ToolCalls);
        Assert.Equal("call_abc", call.Id);
        Assert.Equal(5, JsonNode.Parse(call.ArgumentsJson)!["limit"]!.GetValue<int>());

        await provider.CompleteAsync(OpenAiRuntime, Request(
            User("go"),
            new ChatMessage(AssistantMessageRole.Assistant, "", [call]),
            new ChatMessage(AssistantMessageRole.Tool, "ranked list", ToolCallId: call.Id, ToolName: call.Name)));

        var messages = JsonNode.Parse(handler.Sent[1].Body)!["messages"]!.AsArray();
        Assert.Equal("call_abc", messages[2]!["tool_calls"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("call_abc", messages[3]!["tool_call_id"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, """{"error":{"code":"rate_limit_exceeded"}}""", ProviderFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.TooManyRequests, """{"error":{"code":"insufficient_quota"}}""", ProviderFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"code":"invalid_api_key"}}""", ProviderFailureKind.InvalidKey)]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"code":"model_not_found"}}""", ProviderFailureKind.ModelUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError, "not json", ProviderFailureKind.Unexpected)]
    public async Task OpenAiFailuresAreClassified(HttpStatusCode status, string body, ProviderFailureKind expected)
    {
        var provider = new OpenAiProvider(new HttpClient(new FakeHttpHandler().Reply(status, body)));

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(OpenAiRuntime, Request(User("hi"))));

        Assert.Equal(expected, failure.Kind);
    }

    [Fact]
    public async Task OpenAiTimeoutIsClassifiedAsTimeout()
    {
        var handler = new FakeHttpHandler().Throw(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));
        var provider = new OpenAiProvider(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(OpenAiRuntime, Request(User("hi"))));

        Assert.Equal(ProviderFailureKind.Timeout, failure.Kind);
    }

    [Fact]
    public async Task CallerCancellationIsNotReportedAsAProviderTimeout()
    {
        var handler = new FakeHttpHandler().Throw(new TaskCanceledException());
        var provider = new OpenAiProvider(new HttpClient(handler));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.CompleteAsync(OpenAiRuntime, Request(User("hi")), cts.Token));
    }

    [Fact]
    public async Task FailureMessagesNeverContainTheKeyOrTheRequestText()
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.Unauthorized, """{"error":{"message":"Incorrect API key provided: sk-test-9999"}}""");
        var provider = new OpenAiProvider(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<ProviderFailureException>(() => provider.CompleteAsync(OpenAiRuntime, Request(User("my secret question"))));

        Assert.DoesNotContain("sk-test-9999", failure.Message);
        Assert.DoesNotContain("my secret question", failure.Message);
        Assert.Null(failure.InnerException);
    }

    [Fact]
    public void RegistryResolvesEachKindAndRejectsUnknown()
    {
        var client = new HttpClient(new FakeHttpHandler());
        var registry = new ChatModelProviderRegistry([new OllamaProvider(client), new GeminiProvider(client)]);

        Assert.IsType<OllamaProvider>(registry.Get(AiProviderKind.Ollama));
        Assert.IsType<GeminiProvider>(registry.Get(AiProviderKind.Gemini));
        Assert.Throws<InvalidOperationException>(() => registry.Get(AiProviderKind.OpenAI));
    }
}
