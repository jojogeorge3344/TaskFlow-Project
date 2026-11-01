# Design

## Context

See proposal.md for the motivation. Relevant current state:

- `AiProviderSeeder` runs at startup before `DatabaseSeeder` and treats "no users yet" as a fresh install.
  It adds missing built-in rows; Ollama is enabled only on a fresh install, and the cloud rows are
  seeded off with no key. Only one provider is enabled at a time (`AiProviderConfigService`).
- API keys are encrypted with `IAiKeyProtector` (ASP.NET Data Protection) into
  `AiProviderConfig.EncryptedApiKey`; nothing reads keys from configuration today.
- RAG goes through one interface: `DocumentIndexer` / `DocumentSearch` → `DocumentEmbeddingEndpoint`
  (resolves an `EmbeddingEndpoint(BaseUrl, Model)`) → `IEmbeddingClient`, implemented only by
  `OllamaEmbeddingClient`. Failures surface as `EmbeddingUnavailableException`, which both callers
  already catch, log and turn into "documents cannot be searched right now".
- Chunks store `DocumentSource.EmbeddingModel` and `DocumentChunk.Dimensions`. A model mismatch makes
  the indexer re-embed and makes search ignore the old index, so a model switch needs no migration.
- The TFA panel shows a notice only for `Cloud` providers (`needsCloudNotice`), remembered per provider
  in local storage (`tfa.cloudAck`).

```
 Before                                   After
 ------                                   -----
 Chat:  Ollama (fresh install, on)        Chat:  Gemini (fresh install + configured key, on)
        Gemini / OpenAI (off, no key)            Ollama / OpenAI (off), custom providers
 RAG:   OllamaEmbeddingClient                RAG:   GeminiEmbeddingClient
        nomic-embed-text @ localhost             gemini-embedding-2 @ generativelanguage.googleapis.com
        DocumentKnowledge:OllamaBaseUrl          key = saved Gemini provider key (decrypted per call)
```

## Goals / Non-Goals

**Goals:**
- Gemini works as the default for chat and RAG on a first run, using only a free AI Studio key that a
  developer puts in user-secrets or an environment variable.
- RAG uses one embedding engine (Gemini), so there is one setup path and one failure mode to explain.

**Non-Goals:**
- Removing Ollama as a chat provider, or any change to `OllamaProvider`.
- Choosing a different embedding engine per installation (no embedding-provider setting in the UI).
- Automatic fallback between providers (the existing "No silent provider fallback" rule stands).
- A paid-tier or Vertex AI integration.

## Decisions

### D1. Key comes from configuration `AiProviders:GeminiApiKey`, seeded once

`AiProviderOptions` gets `GeminiApiKey` (nullable, not in `appsettings.json`). It is filled from .NET
user-secrets (`dotnet user-secrets set "AiProviders:GeminiApiKey" "<key>"`) or the environment variable
`AiProviders__GeminiApiKey`. The seeder, after adding the built-in rows:

1. If the Gemini row has no `EncryptedApiKey` and the option is non-empty, store
   `IAiKeyProtector.Protect(key)` on it.
2. On a fresh install, enable Gemini (and leave every other row off) only when step 1 applied.
   Otherwise nothing is enabled.
3. On an existing installation, never change `IsEnabled` on any row.

A row that already has a key is never touched, so a key saved or cleared in TFA Settings, and Gemini
being switched off, survive restarts. A cleared key is null again, so the configured key would be
re-applied on the next start. That is acceptable: removing the secret is how you stop it, and it is
documented.

*Alternatives:* reading the key from configuration on every call (two sources of truth, and Settings
could not override it); committing a key to `appsettings.json` (leaks the key, and Google revokes
leaked keys); requiring the admin to paste the key (rejected by the user: TFA would be off after the
first run).

### D2. `GeminiEmbeddingClient` replaces `OllamaEmbeddingClient` behind `IEmbeddingClient`

- Calls `POST v1beta/models/{model}:batchEmbedContents` on `GeminiProvider.BaseAddress`, sending the key in
  the `x-goog-api-key` header, as `GeminiProvider` does.
- Model: `gemini-embedding-2` (confirmed in task 1.1, 2026-11-01). Google lists it as the current model;
  `gemini-embedding-001` is deprecated and shuts down on 2028-05-14.
- Each request item has `model` (`models/{model}`), `content.parts[0].text` and `outputDimensionality`
  from configuration (default 768). `gemini-embedding-2` refuses the `taskType` field, so the task goes
  in the text as Google documents it: `task: search result | query: {text}` for a query
  (`EmbeddingPurpose.Query`) and `title: none | text: {text}` for a document chunk. The chunk's
  embedding text already carries its document title and headings.
- At most 100 items per request, replacing `BatchSize = 16`. Google doesn't document a limit for
  `gemini-embedding-2`; 100 is the long-standing `batchEmbedContents` limit. With about 4,000 chunks a full
  index is about 40 requests.
- Keeps the existing L2 `Normalise` step as a guard. Google says `gemini-embedding-2` normalises reduced
  dimensions itself, so normalising again changes nothing, and it protects search if the model is changed.
- The Ollama `search_document:` / `search_query:` prefixes are replaced by the Gemini prefixes above.
- Uses a typed `HttpClient` with the cloud timeout (`ConfigureAiClient`) and `ProviderHttp.SendAsync`, so
  failures are classified the way `GeminiProvider.Classify` already does.

`OllamaEmbeddingClient` and its tests are deleted. An `IEmbeddingClient` with a single implementation
keeps the indexer and search code and their test harness unchanged.

*Alternative:* keeping both clients behind a setting. Rejected, because the user asked for Gemini RAG
and two paths double the setup and test surface.

### D3. `EmbeddingEndpoint` carries the key, not a server address

`EmbeddingEndpoint` becomes `(string Model, int Dimensions, string ApiKey)`; its `ToString` leaves the key out, so it is never logged. `DocumentEmbeddingEndpoint.ResolveAsync`
reads the built-in Gemini row through `IAiProviderConfigRepository.GetBuiltInAsync(AiApiType.Gemini)`
and decrypts its key. When there is no key, or the key cannot be decrypted, it throws
`EmbeddingUnavailableException(MissingKey)`. The callers already turn that into the log warning and
"documents cannot be searched". The key is resolved on every index run and every query, so a key saved
in Settings is used at the next rescan (within `RescanMinutes`), with no restart.

The key is read whether or not Gemini is the enabled chat provider. RAG depends on the key being saved,
not on Gemini being selected.

### D4. Failure kinds and messages are rewritten for Gemini

`EmbeddingFailureKind` becomes `MissingKey`, `InvalidKey`, `RateLimited`, `Unreachable`, `Timeout`,
`ModelNotFound` and `Unexpected`. The messages name Google Gemini, never include the key or document
text, and for `MissingKey` say "Save a Google Gemini API key in TFA Settings to enable document search".
During indexing, `RateLimited` stops the run like other unavailable errors. The unchanged files are
kept, and the next rescan continues where it stopped, because only changed files are re-embedded.

### D5. Configuration changes in `DocumentKnowledge`

- `EmbeddingModel` default becomes `gemini-embedding-2`. Changing it still re-embeds everything.
- New `EmbeddingDimensions` setting (default 768; allowed 128 to 3072), validated at startup.
- `OllamaBaseUrl` is removed, along with its validation. A leftover value in a user's appsettings is
  ignored by the binder.
- `MinScore` gets a new default, picked by running about ten real TF questions against the rebuilt
  index (task 4.3). It starts at 0.6 until that measurement is done.

The stored model name includes the dimensions (e.g. `gemini-embedding-2@768`), so changing
`EmbeddingDimensions` also triggers a re-embed through the existing model-mismatch path.

### D6. Ollama keeps its row, not its default

`AiProviderCatalog.All` is reordered to `[Gemini, OpenAI, Ollama]`, so Gemini is listed first and is
the default in the Add AI model page. Ollama keeps its `RequiresKey`, `Locality` and default base URL.
The seeder never enables it. Existing installations keep whatever is enabled.

### D7. Notice for local providers

`needsCloudNotice` becomes "needs notice": shown for `Cloud` providers as today, and also for `Local`
providers, with the text "Your message is sent to Google Gemini to search the TaskFlow documents. The
reply is generated on this computer." The acknowledgement reuses the same per-provider `tfa.cloudAck`
storage. The notice is shown even when no Gemini key is saved, so the frontend needs no new API field.

## Risks / Trade-offs

- [Free-tier rate limits during the first full index] → Up to 100 chunks per request keeps the request
  count small. A rate-limit stop is logged, and the next rescan resumes with the files still missing.
  Measured on 2026-11-01 with a free key: the first run embedded 21 of 169 files (228 chunks) before
  `RESOURCE_EXHAUSTED`, so a full first index takes several rescans (about an hour at `RescanMinutes` 5).
  Waiting for Google's `retryDelay` inside a run would be faster; it is left as a possible follow-up.
- [Free-tier data use: Google may use prompts and documents] → The notices and TFA Settings help text say
  content is sent to Google. The README notes that a paid key, or Ollama for chat, limits what is shared,
  and that document search always uses Google.
- [Gemini model names change over time] → The model is configurable, and `ModelNotFound` names the
  configured model. Task 1.1 checks the current embedding model ID against Google AI Studio before
  coding.
- [Wrong `MinScore` for the new vectors] → Measured in task 4.3 before the default is changed. It can be
  changed in configuration without a code change.
- [Document search breaks offline] → Accepted trade-off of moving off local embeddings. Live-data
  answers keep working (spec: TFA keeps working when documents cannot be searched).
- [A key in an environment variable can leak through process listings or CI logs] → The README
  recommends user-secrets for development. The key never enters the repository, and the seeder never
  logs it.
- [Spec ordering] → This change modifies requirements added by the unarchived `tfa-assistant` and
  `tfa-document-rag` changes, so it must be archived after them.

## Migration Plan

1. Deploy. On first start, existing installations keep their enabled provider. If
   `AiProviders:GeminiApiKey` is set and Gemini has no saved key, the key is stored.
2. The indexer sees that the stored model (`nomic-embed-text`) differs from `gemini-embedding-2@768`
   and re-embeds every document with Gemini. Until that finishes, search uses no chunks and TFA says the
   documents are still being prepared.
3. If there is no Gemini key, indexing stops with the `MissingKey` warning. The administrator saves a key
   in TFA Settings, and the next rescan builds the index.

Rollback: revert the build. The old build re-embeds with `nomic-embed-text`, again through the
model-mismatch path, and needs Ollama running. Stored Gemini keys are harmless to the old build.

## Open Questions

- ~~The exact new `MinScore` default~~ Resolved in task 4.3 (2026-11-01): **0.65**. Against the
  `gemini-embedding-2@768` index (21 files indexed), ten covered questions scored 0.727–0.843 against the
  right section (right document in the top 4 every time, top 1 in 8 of 10); three unrelated questions scored
  at most 0.562; the live-data question "Which of my tasks are overdue this week?" scored 0.665 against the
  task-list spec, which is harmless because live-data questions keep using the task tools. 0.65 leaves
  0.088 above the noise and 0.077 below the weakest relevant match.
