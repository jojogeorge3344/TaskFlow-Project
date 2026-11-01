# Proposal

## Why

TFA talks to Gemini, OpenAI and Ollama through about 1,070 lines of hand-written HTTP and JSON code
(three chat providers, one embedding client and shared plumbing). Every provider API change, new
option (such as Gemini thinking levels) or new provider means more custom wire code. Microsoft.Extensions.AI
is the standard .NET abstraction for chat (`IChatClient`) and embeddings (`IEmbeddingGenerator`),
with maintained implementations for all three providers, so TaskFlow can drop its own wire code and
follow a recognised architecture.

## What Changes

- Add the packages `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`, `Google.GenAI`
  (Google's official SDK, which provides an `IChatClient`) and `OllamaSharp` (the `IChatClient`
  implementation Microsoft recommends for Ollama).
- Re-implement `GeminiProvider`, `OpenAiProvider` and `OllamaProvider` as thin adapters over
  `IChatClient`, behind the existing `IChatModelProvider` seam. `ChatModelGateway`,
  `AiProviderConnectionTester`, the provider registry and `AssistantService` do not change.
- Re-implement `GeminiEmbeddingClient` on `IEmbeddingGenerator`, keeping the `IEmbeddingClient`
  interface, task prefixes, batching and normalisation.
- Map each SDK's errors to the existing `ProviderFailureKind` values so users keep seeing the same
  messages (rate limit, invalid key, timeout, unreachable, model unavailable, invalid model name).
- Keep today's safeguards: API keys only in headers, never in URLs or logs; redirects not followed;
  `AiProviders:TimeoutSeconds` / `OllamaTimeoutSeconds`; Gemini thinking kept low; Ollama context
  size; Ollama's retry without tools and its tool-call-written-as-text handling.
- TFA runs tools itself (permission checks, confirmation): Microsoft.Extensions.AI's automatic
  function invocation is **not** used.
- Remove the wire code the SDKs replace (request/response JSON building and parsing in the providers
  and most of `ProviderHttp`).

No user-visible behaviour changes; this is a refactor (`skip_specs: true`).

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

_None._ Requirements in `ai-provider-management`, `ai-custom-providers` and `tfa-document-knowledge`
(key handling, failure messages, timeouts, custom OpenAI-compatible providers, embeddings) stay as
they are and are protected by the existing tests.

## Impact

- **Backend:** `Integrations/Ai/Providers/*`, `Integrations/Ai/Embeddings/GeminiEmbeddingClient`,
  `Config/IntegrationRegistration` (HTTP client wiring), `TaskFlow.Api.csproj` (4 packages).
- **Tests:** `ChatProviderTests` and `GeminiEmbeddingClientTests` keep their fake-HTTP assertions
  (URL, key header, request shape, error mapping) as the regression gate, adjusted only where an SDK
  sends an equivalent but differently formatted request.
- **Docs:** TFA Settings and TFA Assistant HLD/LLD, `TDD-TaskFlow.md` technology stack and
  integration sections.
- **Prerequisite:** the in-progress provider changes (`tfa-custom-ai-providers`,
  `tfa-model-list-and-edit`, `tfa-gemini-model-id-validation`) should be finished first to avoid
  conflicting edits in the same files.
- **Related:** independent of `tfa-hybrid-document-search`.
