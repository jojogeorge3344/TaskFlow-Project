# Proposal

## Why

The document indexer sends `TF - Documents` (169 Markdown files) to Gemini's `batchEmbedContents` as
fast as it can. A file is one request, but Google counts every chunk in it against the per-minute
quota. Each run therefore hits the rate limit within seconds, stops, and starts again 5 minutes
later. In one 80-minute session the log showed "rate limit was reached while embedding documents"
10 times. The indexer shares the chat's Google key, so it competes with chat for the same quota, and
every chat message's query embedding is sent even while Google is refusing requests.

When chat fails, the chat path logs only the failure class and HTTP status (`ChatModelGateway`).
Google's own reason is already extracted (`ProviderReason`) but thrown away, so "returned an
unexpected error" cannot be diagnosed from the log. A 200 response with no text (blocked, malformed
function call, out of tokens) records nothing at all. Google's "overloaded" errors (HTTP 500/503,
`UNAVAILABLE`) are also reported as "unexpected" when they really mean "busy, retry". During testing
on 2026-11-01, `gemini-3.8-flash` took 37–120 s per call and nothing in the log showed why.

## What Changes

- **Indexer pacing**: the indexer spreads embedding requests out to stay under a configurable
  per-minute input budget, sending smaller batches with a wait between them, instead of sending
  everything at once.
- **Rate-limit cooldown**: when Google answers 429/`RESOURCE_EXHAUSTED`, the indexer waits for the
  time Google asks for (`Retry-After` header or `RetryInfo.retryDelay`) before continuing. If Google
  gives no time, it waits a default cooldown. A daily quota (quota id containing `PerDay`) pauses
  indexing for a longer, configurable period. Already-indexed files are never re-sent.
- **Chat queries respect the cooldown**: while a cooldown is active, document search skips the query
  embedding and goes straight to the existing keyword-only search. Chat neither spends quota nor
  waits on a request Google will refuse.
- **Indexer log says why and when**: a rate-limit warning names the quota kind (per minute or per
  day), when indexing resumes, and how many files are indexed out of the total.
- **Chat failures log Google's reason**: the chat path's warning includes the provider's HTTP status
  and reason, with keys replaced by `[key]` and cut to 200 characters (the existing `Reason()`
  rules). For a 200 response with no content, the reason is Gemini's `finishReason` or
  `promptFeedback.blockReason`.
- **New failure class `Busy`**: Gemini HTTP 500/503 or `UNAVAILABLE`/`INTERNAL` is classified as
  `Busy`. The user sees "Google Gemini is busy right now. Retry in a moment, or choose another
  model." instead of the unexpected-error message. OpenAI's and Ollama's 5xx map the same way.
- Chat replies to users still never include the provider's raw reason.

Out of scope: a separate API key for document indexing, changes to the 120 s timeout, any UI for
index status (existing requirement: no such screen), and the Microsoft.Extensions.AI migration
(`tfa-meai-providers`), which must keep these behaviours when it lands.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `tfa-document-knowledge`: "Index stays in step with the folder" gains paced embedding and a
  rate-limit cooldown. "TFA keeps working when documents cannot be searched" gains keyword-only
  search during a cooldown. "Indexing problems are logged, not shown in the UI" gains the quota kind,
  resume time and progress in the warning.
- `ai-provider-management`: "Provider errors are classified" gains the `Busy` class. "Test connection
  shows the provider's reason for unexpected errors" extends the logged status and reason to chat
  failures, and to empty Gemini responses.

## Impact

- Backend: `DocumentIndexer`, `DocumentIndexerService`, `DocumentIndexStatus`,
  `GeminiEmbeddingClient`, `EmbeddingTypes` (`EmbeddingUnavailableException` carries retry time and
  quota kind), `DocumentSearch`, `DocumentKnowledgeOptions` (new settings), `appsettings.json`,
  `ChatModelGateway`, `GeminiProvider` (`Classify`, `Parse`), `OpenAiProvider` and `OllamaProvider`
  (5xx → `Busy`), `ProviderFailureKind`, `ProviderFailureMessages`, `AiProviderConnectionTester`.
- Frontend: `assistant.model.ts` gains `'Busy'` in the failure-kind union. Any switch over kinds
  treats it like a retryable failure.
- Tests: `DocumentIndexerTests`, `DocumentIndexerServiceTests`, `GeminiEmbeddingClientTests`,
  `ChatModelGatewayTests`, provider classification tests, `ProviderFailureMessages` tests.
- No API route changes, no database migration, no new packages.
