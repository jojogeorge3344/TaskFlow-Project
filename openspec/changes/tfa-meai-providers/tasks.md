# Tasks

## 1. Prerequisites and Gemini SDK spike

- [ ] 1.1 Finish or archive `tfa-custom-ai-providers`, `tfa-model-list-and-edit` and `tfa-gemini-model-id-validation` so no other change edits `Integrations/Ai/Providers`; verify `openspec list` shows them complete or archived
- [ ] 1.2 Spike in a scratch test: build a `Google.GenAI` `IChatClient` and embedding generator on an `HttpClient` with the fake handler and confirm the key is sent only in `x-goog-api-key`, the caller's handler is used, a thinking config is sent, and HTTP/Google status is readable from the SDK exception (design M3); record the outcome in design.md M3 and choose the SDK path or the OpenAI-compatible fallback before continuing

## 2. Packages and shared mapping

- [ ] 2.1 Add `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`, `OllamaSharp` and (if 1.2 passed) `Google.GenAI` with pinned versions to `TaskFlow.Api.csproj`; verify `dotnet build` succeeds
- [ ] 2.2 Add `ChatClientMapping` (request -> MEAI messages/options, response -> `ChatModelResult`, tool declarations incl. the empty-parameters rule) (design M2); verify new unit tests cover each message role, tool calls with ids, tool results, text + tool call together, and argument JSON normalisation
- [ ] 2.3 Add `ProviderFailureMapping` for SDK exceptions (design M7), reusing `ProviderHttp.Reason` and `RetryAfter`; verify unit tests for timeout, unreachable, 429, 401/403, 404, 400 model name, other 4xx/5xx, and key redaction in the reason

## 3. Chat providers

- [ ] 3.1 Rewrite `OpenAiProvider` on `Microsoft.Extensions.AI.OpenAI` with our `HttpClient` transport, custom base URL and SDK retries off (design M4); verify all OpenAI cases in `ChatProviderTests` pass, including custom base URL and Bearer key header
- [ ] 3.2 Rewrite `GeminiProvider` on the path chosen in 1.2, keeping `ThinkingConfig` (design M3); verify all Gemini cases in `ChatProviderTests` pass, including key header not in URL, thinking level/budget per model family, empty-parameter tools, and error classification
- [ ] 3.3 Rewrite `OllamaProvider` on `OllamaSharp`, keeping `num_ctx`, the retry without tools and the tool-call-as-text handling (design M5); verify all Ollama cases in `ChatProviderTests` pass
- [ ] 3.4 Update `IntegrationRegistration` so every SDK uses the typed `HttpClient` (timeouts, `AllowAutoRedirect = false`, no loggers); verify `ProviderTimeoutTests` pass and a new test shows a 302 from a provider is not followed

## 4. Embeddings and cleanup

- [ ] 4.1 Rewrite `GeminiEmbeddingClient` on `IEmbeddingGenerator` (or keep it on the fallback path), keeping prefixes, batches of 100, normalisation and `EmbeddingUnavailableException` kinds (design M6); verify `GeminiEmbeddingClientTests` and `DocumentIndexerServiceTests` pass
- [ ] 4.2 Remove now-unused wire helpers from `ProviderHttp` and the providers; verify `dotnet build` has no unused-code warnings for them and `grep` finds no remaining `generateContent` / `chat/completions` / `/api/chat` JSON building outside the SDKs
- [ ] 4.3 Update the TFA Settings and TFA Assistant HLD/LLD and the TDD technology stack and integration sections to describe Microsoft.Extensions.AI and the four packages; verify each doc names `IChatClient`, `IEmbeddingGenerator` and the packages

## 5. End-to-end check

- [ ] 5.1 Run the full backend unit and integration test suites; verify both pass
- [ ] 5.2 In the running app, use TFA Settings "Test connection" and ask TFA a task question and a documentation question with Gemini, and (if available) Ollama and OpenAI; verify answers, tool calls, sources and friendly error messages (wrong key, wrong model name) behave as before
