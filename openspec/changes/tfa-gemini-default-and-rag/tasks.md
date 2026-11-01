# Tasks

## 1. Gemini embedding client

- [x] 1.1 Confirm the current Gemini embedding model ID and its `batchEmbedContents` request/response shape and batch limit against Google AI Studio / the Gemini API docs; record the confirmed ID in design.md D2 if it differs from `gemini-embedding-001`
- [x] 1.2 Change `EmbeddingEndpoint` to `(Model, ApiKey)` and rewrite `EmbeddingFailureKind` and the `EmbeddingUnavailableException` messages for Gemini (`MissingKey`, `InvalidKey`, `RateLimited`, `Unreachable`, `Timeout`, `ModelNotFound`, `Unexpected`; design D3/D4); verify no message format includes the key and the project builds
- [x] 1.3 Add `GeminiEmbeddingClient : IEmbeddingClient` (`batchEmbedContents`, `x-goog-api-key` header, document and query task prefixes, `outputDimensionality`, batches of at most 100, L2 normalisation kept as a guard, error classification as in `GeminiProvider`; design D2)
- [x] 1.4 Replace `OllamaEmbeddingClientTests` with `GeminiEmbeddingClientTests` covering: request URL has no key and the header carries it, task type follows `EmbeddingPurpose`, 150 inputs are sent as two requests in order, vectors are unit length, 401/403 → `InvalidKey`, 429 / `RESOURCE_EXHAUSTED` → `RateLimited`, 404 → `ModelNotFound`, wrong vector count → `Unexpected`; verify `dotnet test --filter GeminiEmbeddingClientTests` passes
- [x] 1.5 Delete `OllamaEmbeddingClient`, register `GeminiEmbeddingClient` as the `IEmbeddingClient` with the cloud timeout in `IntegrationRegistration`, and update `ProviderTimeoutTests` to expect the 60-second cloud timeout for the embedding client; verify `dotnet test --filter ProviderTimeoutTests` passes

## 2. Document knowledge on Gemini

- [x] 2.1 Update `DocumentKnowledgeOptions`: `EmbeddingModel` default `gemini-embedding-2`, new `EmbeddingDimensions` (default 768, validated 128 to 3072), remove `OllamaBaseUrl` and its validation; update `appsettings.json`; verify the options validation tests pass with the new rules
- [x] 2.2 Rewrite `DocumentEmbeddingEndpoint.ResolveAsync` to read and decrypt the built-in Gemini key, throwing `EmbeddingUnavailableException(MissingKey)` when it is missing or cannot be decrypted (design D3); add unit tests for key present, no key and an undecryptable key, and verify they pass
- [x] 2.3 Store the embedding model as `<model>@<dimensions>` in the indexer and search, so changing dimensions re-embeds (design D5); verify a `DocumentIndexer` test that switches from `nomic-embed-text` to `gemini-embedding-2@768` re-embeds every document and search ignores the old chunks (spec: Old local index replaced)
- [x] 2.4 Update `DocumentIndexerHarness`, `DocumentIndexerServiceTests` and `DocumentSearchTests` for the new endpoint and failure kinds; add tests for no Gemini key (index empty, warning logged, TFA says documents cannot be searched) and a rate limit during indexing (unchanged files kept, run stops, next run continues); verify `dotnet test --filter Documents` passes
- [x] 2.5 Add a test that a query embedding request contains only the message text (no task, user or conversation data), and verify it passes (spec: Document embedding sends text to Google)

## 3. Gemini as the default provider

- [x] 3.1 Add `GeminiApiKey` to `AiProviderOptions` (not in `appsettings.json`) and reorder `AiProviderCatalog.All` to Gemini, OpenAI, Ollama (design D1/D6); verify the Settings provider list and the Add AI model page list Google Gemini first
- [x] 3.2 Change `AiProviderSeeder`: never enable Ollama; store the configured key encrypted when the Gemini row has none; enable Gemini only on a fresh install when the key was applied; never change `IsEnabled` on an existing installation; never log the key (design D1)
- [x] 3.3 Extend `AiProviderSeederTests` with one test per scenario in the `ai-provider-management` delta (fresh install with/without key, saved key wins, existing Ollama installation not switched, disabled Gemini stays disabled, key not logged) and update the existing fresh-install test that expected Ollama enabled; verify `dotnet test --filter AiProviderSeederTests` passes
- [x] 3.4 Run the full backend suite (`dotnet test` in `TaskFlow Proj/backend`) and verify it passes with no remaining references to `OllamaEmbeddingClient` or `DocumentKnowledge:OllamaBaseUrl` (`grep -r` returns nothing outside migrations)

## 4. Notice, docs and tuning

- [x] 4.1 Change the TFA panel notice to also show for Local providers with the "sent to Google Gemini to search the TaskFlow documents" text, keeping the Cloud text and the `tfa.cloudAck` per-provider acknowledgement (design D7); update `tfa-panel.component.spec.ts` and `assistant.service.spec.ts` for Ollama and Gemini, and verify `npm test` passes in `TaskFlow Proj/frontend`
- [x] 4.2 Update the TFA Settings help text to say a Gemini key is needed for document search and that document text is sent to Google; verify it shows on the TFA Settings screen when you run the app
- [x] 4.3 With a real free Gemini key, rebuild the index on `TF - Documents`, ask about ten documentation questions and a few live-data questions, record the similarity scores of the right and wrong chunks, and set the `MinScore` default in `DocumentKnowledgeOptions` and `appsettings.json` from them; write the measured values and the chosen default in design.md Open Questions, then mark it resolved
- [x] 4.4 Update the README and `scripts/setup-ai.ps1` / `scripts/setup-ai.sh`: get a free key from Google AI Studio, set it with `dotnet user-secrets set "AiProviders:GeminiApiKey" "<key>"` (or `AiProviders__GeminiApiKey`), the free-tier data-use note, Ollama as an optional chat provider only; verify by following the README on a clean database that TFA answers a chat and a documentation question
- [x] 4.5 Update the TFA Settings and Assistant documents in `TF - Documents` (BRD, FRD, HLD, LLD and the overall documents that mention Ollama embeddings or Ollama as default) to describe Gemini as the default provider and document embedding engine; verify `grep -ri "ollama" "TF - Documents"` shows Ollama only as an optional chat provider

## 5. End-to-end check

- [x] 5.1 On a fresh database with the key in user-secrets, start the API and frontend and verify: Gemini is enabled and listed first, Ollama is off, the index is built with `gemini-embedding-2@768`, a documentation question shows sources, and switching to Ollama for chat shows the Google notice and still returns document sources
- [x] 5.2 Remove the user-secret, clear the saved key in TFA Settings, restart, and verify TFA says the documents cannot be searched right now while live-data questions still work, and the server log names the missing Gemini key

## Workflow follow-up

- Archive `tfa-assistant` and `tfa-document-rag` before archiving this change, since it modifies their requirements.
