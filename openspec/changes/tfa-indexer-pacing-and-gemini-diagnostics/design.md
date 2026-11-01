# Design

## Context

- `DocumentIndexer.RunAsync` walks every Markdown file and calls `IEmbeddingClient.EmbedAsync` once
  per changed file. `GeminiEmbeddingClient` splits a file's chunks into batches of 100 and posts
  each batch to `batchEmbedContents` straight away. There is no pause between files or batches.
- Google counts each text in a batch against the per-minute embedding quota, so a run over 169 files
  exhausts it within seconds.
- On 429 the client throws `EmbeddingUnavailableException(RateLimited)`. The indexer logs one fixed
  sentence and stops the run (`break`), and `DocumentIndexerService` waits `RescanMinutes` (5) before
  the next run. Neither `Retry-After` nor Google's `RetryInfo` is read, and the per-day and
  per-minute quotas are not told apart.
- `DocumentSearch.SearchAsync` embeds each message's queries with the same client and key. On
  `EmbeddingUnavailableException` it already falls back to `KeywordOnlyAsync` (hybrid search on).
- `DocumentIndexStatus` is a singleton shared by indexer and search, so it is the natural place for
  a cooldown both can see.
- `ProviderFailureException` already carries `HttpStatus` and `ProviderReason` (sanitised by
  `ProviderHttp.Reason`). `ChatModelGateway` logs only `Provider`, `FailureKind` and `HttpStatus`.
  `AiProviderConnectionTester` logs the reason.
- `GeminiProvider.Parse` throws `Unexpected` with no reason when a 200 has no text and no function
  call. `Classify` maps everything not 429/401/403/404/model-name to `Unexpected`, including 500
  and 503.
- The frontend types failure kinds as a string union in `assistant.model.ts`, and the backend
  serialises the enum by name.

## Goals / Non-Goals

**Goals:**
- A full first index of `TF - Documents` finishes on a free-tier key without rate-limit errors in
  normal conditions, and does so unattended after a rate limit.
- Chat never spends quota or waits on an embedding request Google is known to refuse.
- Every chat failure leaves enough in the log (status, Google's reason, finish reason) to diagnose
  it without reproducing it.
- "Gemini is overloaded" reads as busy, not unexpected.

**Non-Goals:**
- A separate API key or project for embeddings.
- Retrying chat calls automatically (the existing "no silent fallback" rule stays; users press
  Retry).
- Changing `AiProviders:TimeoutSeconds` or the chat model defaults.
- Persisting the cooldown across API restarts. A restart simply tries again, and Google answers 429
  again if the quota is still exhausted.
- Any index-status UI.

## Decisions

### P1. Pace by texts per minute, in the indexer only

Add `DocumentKnowledge:EmbeddingTextsPerMinute` (default **80**) and
`DocumentKnowledge:EmbeddingBatchSize` (default **20**). The indexer sends a file's chunks in batches
of `EmbeddingBatchSize` and, before each batch, waits until the texts sent in the last 60 s plus this
batch fit the budget. A small sliding-window `EmbeddingPacer` class does this, using `TimeProvider`
so tests can drive time.

- Pacing lives in the indexer path, not in `GeminiEmbeddingClient`. Query embeddings (one or two
  texts per message) must never wait behind indexing.
- 80 sits below the free-tier per-minute limit, leaving headroom for chat queries on the same key.
  It is configurable because paid keys allow far more.
- Alternative considered: lower `RescanMinutes` and let each run go until it hits 429. Rejected,
  because it still produces a 429 on every run and gives chat no headroom.

### P2. Cooldown from Google's answer, shared through `DocumentIndexStatus`

`GeminiEmbeddingClient.Classify` reads:
- the `Retry-After` header (`ProviderHttp.RetryAfter`), else
- `error.details[]` with `@type` ending `google.rpc.RetryInfo`, using its `retryDelay` (for example
  `"40s"`), and
- `error.details[]` with `@type` ending `google.rpc.QuotaFailure`. If any `violations[].quotaId`
  contains `PerDay`, the quota kind is `Daily`, otherwise `PerMinute`.

`EmbeddingUnavailableException` gains `RetryAfter` (`TimeSpan?`), `QuotaKind` and `ProviderReason`
(via `ProviderHttp.Reason`, so keys become `[key]`).

On a rate limit the indexer sets `DocumentIndexState.CooldownUntil` to
`now + (RetryAfter ?? default)`. The default is `EmbeddingCooldownSeconds` (default **60**) for
per-minute limits and `EmbeddingDailyCooldownMinutes` (default **60**) for daily ones. It then
stops the run. A daily cooldown is re-checked hourly rather than waiting until Pacific midnight, so
the code needs no time-zone logic.

`DocumentIndexerService` replaces its fixed `Task.Delay(interval)` with "wait until the later of the
next scan and `CooldownUntil`", where the next scan is `max(interval, CooldownUntil - now)`. It
resumes as soon as a short per-minute cooldown ends, instead of 5 minutes later. Skipping files
already indexed is unchanged: hash and model match.

### P3. Search skips the embedding during a cooldown

`DocumentSearch.SearchAsync` checks `_status.Current.CooldownUntil > now` before resolving the
endpoint. If a cooldown is active it takes the existing keyword-only branch directly and logs
nothing extra, because the indexer already logged the cooldown.

If a query embedding itself gets a 429, `DocumentSearch` also sets the cooldown, so the next message
skips the call too. With hybrid search off, the cooldown still means "cannot search", as today.

### P4. One warning per cooldown, with progress

The indexer's rate-limit warning becomes, for example:
`TFA document indexing paused: Google Gemini per-minute embedding limit reached. Resuming in about
40 s. 60 of 169 files indexed. Google said: RESOURCE_EXHAUSTED: <reason>`.
It is written once, when the cooldown is set. Requests are not sent during a cooldown, so there is
nothing else to log.

### P5. Chat path logs the sanitised reason

`ChatModelGateway`'s warning template gains `{ProviderReason}`. The value comes from
`ProviderHttp.Reason`, which already cuts to 200 characters and replaces the sent key and key-shaped
tokens. The prompt, history and documents are still never logged.

### P6. Empty Gemini answers carry their finish reason

In `GeminiProvider.Parse`, when there is no text and no function call, build the reason from
`candidates[0].finishReason` (plus `finishMessage` when present), or from
`promptFeedback.blockReason` when `candidates` is missing. Pass it to the `ProviderFailureException`
through `ProviderHttp.Reason`, keeping HTTP status 200 so the log shows it was a 200. The kind stays
`Unexpected`, because the user message is unchanged by the spec.

### P7. `Busy` failure kind

Add `ProviderFailureKind.Busy` as the last enum member, so existing numeric values are unchanged.
- Gemini `Classify`: status `UNAVAILABLE` or `INTERNAL`, or HTTP 500/502/503/504, maps to `Busy`.
  This is checked after the rate-limit, key and model checks.
- OpenAI and Ollama `Classify`: HTTP 5xx maps to `Busy`.
- `ProviderFailureMessages`: `"{name} is busy right now. Retry in a moment, or choose another
  model."`, with `RetryAfter` text when the provider sent `Retry-After`.
- `AiProviderConnectionTester` treats `Busy` like `Timeout`: not a configuration problem, so no
  provider reason is appended to the admin message.
- Frontend: add `'Busy'` to the union. The chat failure card already shows the message text and a
  Retry button for any kind, so no template change is needed. Verify during implementation.

## Risks / Trade-offs

- **The budget is a guess at Google's limit.** If Google's real free-tier limit is lower, a 429 can
  still happen. P2 then handles it cleanly, and the setting can be lowered.
- **A first full index is slower** (about 169 files × ~6 chunks ÷ 80 per minute ≈ 13 minutes). In
  exchange it completes. Chunks already stored stay searchable throughout, and keyword search covers
  the rest.
- **The cooldown lives in memory.** A restart during a daily cooldown sends one request and gets one
  429, which is acceptable.
- **Users see keyword-only answers during a cooldown.** These may be less precise than hybrid ones,
  but are better than a slow failed embedding call.
- **Reason text in logs.** Google's messages could in theory echo content. They are capped at 200
  characters with key-shaped tokens masked, the same rule already accepted for Test connection.
- **Overlap with `tfa-meai-providers`.** That change rewrites the providers. Its tasks must keep
  P5–P7 (reason logging, finish reason, `Busy`) and the embedding cooldown inputs (P2).
