# Tasks

## 1. Gemini chat diagnostics

- [ ] 1.1 Add `ProviderFailureKind.Busy` as the last enum member and its message in `ProviderFailureMessages` ("{name} is busy right now. Retry in a moment, or choose another model.", with retry-after text when present) (design P7); verify `ProviderFailureMessages` unit tests cover `Busy` with and without `RetryAfter`
- [ ] 1.2 In `GeminiProvider.Classify`, map status `UNAVAILABLE`/`INTERNAL` or HTTP 500/502/503/504 to `Busy`, after the rate-limit, key and model checks; in `OpenAiProvider` and `OllamaProvider`, map HTTP 5xx to `Busy` (design P7); verify provider tests for Gemini 503 `UNAVAILABLE`, Gemini 500 `INTERNAL`, OpenAI 502 and Ollama 500 each give `Busy`, and the existing 429/401/404/400-model-name cases are unchanged
- [ ] 1.3 In `GeminiProvider.Parse`, when there is no text and no function call, set `ProviderReason` from `candidates[0].finishReason` (+ `finishMessage`) or `promptFeedback.blockReason`, through `ProviderHttp.Reason`, with HTTP status 200 (design P6); verify tests for `MALFORMED_FUNCTION_CALL`, `MAX_TOKENS`, and no candidates with `blockReason: SAFETY`
- [ ] 1.4 Add `{ProviderReason}` to `ChatModelGateway`'s failure warning (design P5); verify a `ChatModelGatewayTests` case with a fake logger asserts the warning holds provider, kind, status and reason, shows `[key]` for a reason containing the sent key, and does not contain the prompt text
- [ ] 1.5 Make `AiProviderConnectionTester` treat `Busy` like `Timeout` (no provider reason appended) (design P7); verify a connection-tester test for a `Busy` failure
- [ ] 1.6 Add `'Busy'` to the failure-kind union in `frontend/src/app/modules/assistant/models/assistant.model.ts`, and check that every switch or map over failure kinds handles it (the failure card shows the message and Retry); verify `ng test` for the assistant module and `npm run build` pass

## 2. Embedding rate-limit details

- [ ] 2.1 Extend `EmbeddingUnavailableException` with `RetryAfter`, `QuotaKind` (`PerMinute`/`Daily`) and a sanitised `ProviderReason` (design P2); verify the existing `EmbeddingUnavailableException` messages are unchanged
- [ ] 2.2 In `GeminiEmbeddingClient.Classify`, read `Retry-After`, `google.rpc.RetryInfo.retryDelay` and `google.rpc.QuotaFailure` quota ids (`PerDay` → `Daily`), and attach the reason via `ProviderHttp.Reason` (design P2); verify `GeminiEmbeddingClientTests` for a 429 with a header, with `RetryInfo` `"40s"`, with a `PerDay` quota id, with neither, and that a key inside the reason becomes `[key]`

## 3. Paced indexing and cooldown

- [ ] 3.1 Add `EmbeddingTextsPerMinute` (80), `EmbeddingBatchSize` (20), `EmbeddingCooldownSeconds` (60) and `EmbeddingDailyCooldownMinutes` (60) to `DocumentKnowledgeOptions` with validation (each ≥ 1, batch size ≤ texts per minute and ≤ 100) and to `appsettings.json` (design P1, P2); verify `DocumentKnowledgeOptionsTests` cover each rule
- [ ] 3.2 Add a `TimeProvider`-based sliding-window `EmbeddingPacer` that waits until a batch fits the per-minute budget (design P1); verify unit tests with a fake time provider: under budget no wait, over budget waits until the window frees, batch exactly at budget
- [ ] 3.3 Add `CooldownUntil` to `DocumentIndexState` (design P2); verify existing `DocumentIndexStatus` usages compile and tests pass
- [ ] 3.4 Make `DocumentIndexer` embed each file in `EmbeddingBatchSize` batches through the pacer. On a rate limit, set `CooldownUntil` (Google's wait, else the per-minute or daily default), log one warning with quota kind, resume time, "N of M files indexed" and the reason, then stop the run (design P1, P2, P4). Verify `DocumentIndexerTests` cover: 169 fake files never exceeding the budget per minute, a 429 with a 40 s retry setting the cooldown and a single warning with the right counts, a daily quota using the daily default, and already-indexed files not being re-sent on the next run
- [ ] 3.5 Make `DocumentIndexerService` wait until the later of `RescanMinutes` and `CooldownUntil`, and resume right after a short cooldown rather than the full interval (design P2); verify `DocumentIndexerServiceTests` with a fake time provider for a cooldown shorter and one longer than the interval
- [ ] 3.6 Make `DocumentSearch` skip the query embedding while `CooldownUntil` is in the future and use keyword-only search, and set the cooldown when a query embedding gets a rate limit (design P3); verify `DocumentSearchTests`: cooldown active → no embedding call and keyword results with sources; cooldown over → embedding called again; query 429 → cooldown set; hybrid off plus cooldown → `CannotSearch`

## 4. Documentation and end-to-end check

- [ ] 4.1 Update the TFA Assistant HLD/LLD and `TF - Documents/TF - TDD/TDD-TaskFlow.md` sections on document indexing and provider errors (pacing, cooldown, the four new settings, the `Busy` class, reason logging), and note in `openspec/changes/tfa-meai-providers/design.md` that its adapters must keep design P2 and P5–P7; verify the docs name the new settings and `Busy`
- [ ] 4.2 Run `dotnet build`, the backend unit and integration test suites, `ng test` and `npm run build`; verify all pass
- [ ] 4.3 Start the API with the real `TF - Documents` folder and a free-tier Gemini key, and leave it until the index completes; verify the log shows paced progress, at most one rate-limit warning per cooldown (each with quota kind, resume time and counts), and a final "document index updated" line with 169 files
- [ ] 4.4 During that run, ask TFA "what validations apply when creating a task?" and "do i have any assigned tasks now?"; verify both answer, the first cites Task Management BRD sources, and that a forced chat failure (for example, an invalid model ID) leaves a log warning with HTTP status and Google's reason
