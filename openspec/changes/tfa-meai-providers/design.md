# Design

## Context

See proposal.md (Why). Current structure:

```
AssistantService --> ChatModelGateway --> IChatModelProviderRegistry.Get(ApiType)
                                              |
                     +------------------------+------------------------+
                     v                        v                        v
               GeminiProvider           OpenAiProvider           OllamaProvider
           (generateContent JSON)   (chat/completions JSON)   (/api/chat JSON, num_ctx,
                     |                        |                tools retry, text tool calls)
                     +------------- ProviderHttp.SendAsync -----------+
                                    (timeout -> Timeout, HttpRequestException -> Unreachable,
                                     Reason() key redaction, RetryAfter)
DocumentSearch / DocumentIndexer --> IEmbeddingClient --> GeminiEmbeddingClient (batchEmbedContents)
```

- `AiProviderRuntime` carries per-row settings (`ApiType`, `ModelName`, `BaseUrl`, decrypted
  `ApiKey`); several provider rows can share one `ApiType` (custom OpenAI-compatible providers).
- Typed `HttpClient`s are registered in `IntegrationRegistration` with `AllowAutoRedirect = false`,
  `RemoveAllLoggers()`, and the configured timeouts.
- `ChatProviderTests` / `GeminiEmbeddingClientTests` drive providers through a fake
  `HttpMessageHandler` and assert URL, headers, body and error classification.

## Goals / Non-Goals

**Goals:**
- Replace wire code with `IChatClient` / `IEmbeddingGenerator` while every existing provider test
  still passes against the same fake HTTP handler.
- Keep the `IChatModelProvider`, `IEmbeddingClient`, `ProviderFailureException` contracts.

**Non-Goals:**
- Streaming responses, middleware pipelines (caching, telemetry), MEAI function invocation.
- Changing `AssistantService`'s tool loop, prompts, or the TFA Settings UI.
- New providers.

## Decisions

**M1 - Keep our seam; adapters underneath.**
Each `IChatModelProvider` builds an `IChatClient` per call from the runtime (key, base URL, model
differ per row) on top of its typed `HttpClient`, so timeout, redirect and logging policies stay in
one place. Construction is cheap (no I/O).
*Alternative:* register `IChatClient` in DI and drop `IChatModelProvider` - loses per-row
configuration and the error contract the gateway, connection tester and messages rely on.

**M2 - One shared message/tool mapper** (`Integrations/Ai/Providers/ChatClientMapping`).
- `ChatModelRequest` -> `IList<Microsoft.Extensions.AI.ChatMessage>`: system prompt as a System
  message; User -> `TextContent`; Assistant -> `TextContent` + `FunctionCallContent(callId, name,
  args)`; Tool -> `FunctionResultContent(callId, result)`.
- Tools -> `ChatOptions.Tools` of `AIFunctionDeclaration`s created from our JSON schema (the
  existing "no properties -> no parameters" rule for Gemini stays). `ToolMode = Auto`.
- `ChatResponse` -> `ChatModelResult`: concatenated text, `FunctionCallContent` -> `ChatToolCall`
  with arguments serialised to a JSON object string (existing `ArgumentsToJson` rule).
- No `FunctionInvokingChatClient`: TFA executes tools itself with permission checks and
  confirmations.

**M3 - Gemini via `Google.GenAI` (`AsIChatClient`), with a gate.**
Thinking stays low through the SDK's generation config (via `ChatOptions.RawRepresentationFactory`
or the SDK's reasoning option), using the existing `GeminiProvider.ThinkingConfig` rule. Spike task
1.2 must confirm before the migration: the key is sent in `x-goog-api-key` and never in the URL; a
caller-supplied `HttpClient` (or handler) is used; thinking config is accepted; HTTP status and
Google error status are available on the SDK exception.
*Fallback if the spike fails:* call Gemini through its OpenAI-compatible endpoint
(`https://generativelanguage.googleapis.com/v1beta/openai/`) with `Microsoft.Extensions.AI.OpenAI`
(key as Bearer header, `reasoning_effort: "low"`), and keep the hand-written embedding client.
*Alternatives:* community packages `Google_GenerativeAI.Microsoft`, `GeminiDotnet.Extensions.AI` -
not first-party.

**M4 - OpenAI and custom providers via `Microsoft.Extensions.AI.OpenAI`.**
`new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = baseUrl,
Transport = new HttpClientPipelineTransport(httpClient), RetryPolicy = no retries })
.GetChatClient(model).AsIChatClient()`. SDK retries are switched off so one user action is one
request, as today, and rate limits surface immediately.

**M5 - Ollama via `OllamaSharp`.**
`new OllamaApiClient(httpClient /* BaseAddress = runtime.BaseUrl */, model)` as `IChatClient`;
`num_ctx` passed through `ChatOptions.AdditionalProperties` / OllamaSharp request options. The two
Ollama workarounds stay in `OllamaProvider` around the client call: retry once without tools when
the model reports it does not support tools, and the tool-call-written-as-text handling.

**M6 - Embeddings on `IEmbeddingGenerator<string, Embedding<float>>`** from the Gemini SDK (or the
M3 fallback). `GeminiEmbeddingClient` keeps: the `IEmbeddingClient` interface, task-prefix text,
batches of 100, L2 normalisation, `EmbeddingUnavailableException` kinds.

**M7 - Error mapping per SDK** (`ProviderFailureMapping`), replacing `ProviderHttp.SendAsync`:
- `OperationCanceledException` when the caller did not cancel -> `Timeout`; `HttpRequestException`
  without status -> `Unreachable` (unchanged rule).
- Status/Google status from the SDK exception -> the existing classification table (429 /
  `RESOURCE_EXHAUSTED` -> RateLimited; 401/403 / `UNAUTHENTICATED` / `PERMISSION_DENIED` / "API key"
  -> InvalidKey; 404 / `NOT_FOUND` -> ModelUnavailable; 400 + "model name" -> InvalidModelName; else
  Unexpected).
- Provider reason still goes through `ProviderHttp.Reason` (key redaction, 200-char cap) and
  `Retry-After` is read when the SDK exposes the response.
- `ProviderHttp` keeps only `Reason`, `RetryAfter` and argument helpers; send/JSON helpers go.

## Risks / Trade-offs

- [SDK sends the key in a URL query or logs it] -> spike 1.2 asserts headers and URL with the fake
  handler; existing `Assert.DoesNotContain(key, url)` tests stay; `RemoveAllLoggers()` stays.
- [SDK retries or follows redirects behind our back] -> retries disabled (M4), our `HttpClient`
  with `AllowAutoRedirect = false` supplied to every SDK, verified by a redirect test.
- [Error details hidden inside SDK exceptions] -> M7 table tested per provider with the existing
  fake-HTTP error cases.
- [Four new packages and their transitive dependencies] -> pin versions; record in TDD tech stack.
- [Wire format differs slightly, breaking strict JSON assertions] -> assert meaning (fields
  present, values) not byte-exact bodies; changed assertions listed in the PR.
- [Merge conflicts with in-progress provider changes] -> task 1.1 prerequisite.

## Migration Plan

Code-only change, no data or configuration migration. Deploy normally; rollback is reverting the
commit. Saved providers, keys and model names are untouched.

## Open Questions

- Exact package versions are chosen at implementation time (latest stable supporting .NET 10).
