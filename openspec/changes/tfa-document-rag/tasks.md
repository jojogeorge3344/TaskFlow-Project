# Tasks

## 1. Configuration, data model and migration

- [x] 1.1 Add `Config/DocumentKnowledgeOptions` (section `DocumentKnowledge`, keys and defaults from
  design D12) and `AiProviderOptions.OllamaContextTokens` (default 8192). Add the section to
  `appsettings.json`, bind and validate it in the Config registration (`TopK` 1-20, `MinScore` 0-1,
  `MaxChunkCharacters` 300-6000, `RescanMinutes` ≥ 1), and add a resolver that turns a relative
  `SourcePath` into an absolute path against the content root. Verify with unit tests that the
  default resolves to `<workspace>/TF - Documents` from `backend/src/TaskFlow.Api`, and that an
  absolute path is kept.
- [x] 1.2 Add entities `DocumentSource` and `DocumentChunk` (design D3) under `Models/Entities`, map
  them in `AppDbContext` (unique index on `RelativePath`, index on `DocumentSourceId`, cascade delete,
  length limits), and add migration `AddDocumentIndex`. Verify that it applies to a fresh database
  and to a copy of an existing `taskflow.db` without changing existing tables.
- [x] 1.3 Add `Repositories/Assistant/Documents/IDocumentIndexRepository` + implementation: list
  sources, find a source by path, replace a source's chunks, remove sources by path, and stream all
  chunks with their vectors for loading the index. No `SaveChanges` in the repository. Verify with
  repository tests on SQLite in-memory, and that `ArchitectureTests` still pass.

## 2. Embedding client

- [x] 2.1 Add `Integrations/Ai/Embeddings/IEmbeddingClient`, `EmbeddingPurpose` (Document, Query),
  `EmbeddingUnavailableException` and `OllamaEmbeddingClient` (design D2): `/api/embed`,
  `search_document:` / `search_query:` prefixes, batches of 16, L2 normalisation, base-URL resolution
  order, and failure mapping through `ProviderHttp`. Register it with a named `HttpClient` using
  `AiProviders:TimeoutSeconds`. Verify with fake `HttpMessageHandler` tests: request body and prefixes,
  batching of 40 inputs into 3 calls, normalised output, and connection refused / 404 "model not
  found" / timeout → `EmbeddingUnavailableException`.
- [x] 2.2 Send `options.num_ctx = AiProviders:OllamaContextTokens` in `OllamaProvider` requests
  (design D11). Verify by extending the existing `OllamaProvider` body tests.

## 3. Chunking, indexing and search

- [x] 3.1 Add `Services/Assistant/Documents/Rules/MarkdownChunker` (pure, design D5). Verify with
  unit tests:
  - heading paths across `#`-`####`
  - headings inside fenced code are ignored
  - short sections merge into the next sibling
  - a long table splits between rows with the header repeated in each piece
  - an over-long paragraph splits at sentence ends
  - excluded headings are skipped
  - the title falls back to the file name
  - no chunk exceeds `MaxChunkCharacters` plus the repeated table header
  - include one test on a trimmed copy of `FRD-Task-Management.md`
- [x] 3.2 Add `DocumentVectorIndex` (singleton, design D4): atomic load/swap and
  `Search(float[] query, topK, minScore, maxCharacters)`. Verify with unit tests: ordering by
  score, the `MinScore` cut-off, the character budget, an empty index, and a swap during a search
  returning a consistent result.
- [x] 3.3 Add `DocumentIndexer` (design D6): hash-based skip, model-change re-embed, replace-on-commit,
  delete-missing, per-file error capture, early stop when embeddings are unavailable, and index
  reload when something changed. Add `DocumentIndexStatus` (singleton). Verify with tests over a
  temp folder and a fake `IEmbeddingClient`:
  - first run indexes every `.md` and ignores `.pdf` / `.xlsx`
  - a second run embeds nothing
  - an edited file is re-embedded alone
  - a deleted file's chunks are removed
  - a model change re-embeds everything
  - a failing file does not block the others
  - a missing folder sets the status message
- [x] 3.4 Add `DocumentIndexerService : BackgroundService` (design D7): start delay, rescan interval,
  `RequestReindex()` signal, a single-run guard, a scope per run, and disabled when `Enabled=false`.
  Set `DocumentKnowledge:Enabled=false` in the integration-test factory. Verify with a unit test
  that two concurrent `RequestReindex()` calls cause one run, and that the API starts with the
  service enabled and the folder missing.
- [x] 3.5 Add `IDocumentSearch` / `DocumentSearch`: embed the query (with the short-follow-up rule
  from design D9), search the index, and return `DocumentContext` (Found with chunks, NoMatch, or
  Unavailable when the index is empty or the embedding fails). Verify with unit tests for each
  outcome and for the follow-up query building.

## 4. TFA integration

- [x] 4.1 Change `AssistantService.SendAsync` to call `IDocumentSearch` before `RunTurnAsync` and pass
  the result to `AssistantPrompt.Build`. Add the three prompt blocks and the rules from design D8.
  Verify with unit tests (fake gateway capturing the request) that the system prompt contains the
  numbered chunks for Found, the no-match line for NoMatch, the unavailable line for Unavailable,
  and that retrieval runs once per message even when the provider has no tool support.
- [x] 4.2 Verify the spec requirement "Live-data questions keep using the existing tools" with a unit
  test (fake gateway and a fake `IDocumentSearch` returning Found chunks): for "which of my tasks are
  overdue?" the request still carries the task tool definitions, the fake model's `get_my_tasks` call
  is executed, and the reply contains the caller's own overdue tasks. Add the same question to the
  6.2 question set so it is also checked against the real index and `llama3.2:3b`.
- [x] 4.3 Remove the keyword help (design D1):
  - Delete the `Services/Assistant/HelpKnowledge/` folder (`HelpKnowledgeBase.cs`,
    `IHelpKnowledgeBase.cs`, `HelpTopicDto.cs`) and the 19 `Services/Assistant/Knowledge/*.md` topics.
  - Remove the `<EmbeddedResource Include="Services\Assistant\Knowledge\*.md" />` item from
    `TaskFlow.Api.csproj`.
  - Delete the `SearchHelpTool` class (`search_help`) from `Services/Assistant/Tools/AssistantReadTools.cs`.
  - In `Config/ServiceRegistration.cs`, remove the `IHelpKnowledgeBase` singleton and the
    `SearchHelpTool` `IAssistantTool` registration.
  - In `Services/Assistant/Chat/AssistantPrompt.cs`, remove the "Use the search_help tool" how-to rule
    (it is replaced by the D8 rules in 4.1).
  - Tests: delete `tests/TaskFlow.UnitTests/Assistant/HelpKnowledge/HelpKnowledgeBaseTests.cs`; in
    `Assistant/Tools/AssistantReadToolsTests.cs`, delete the three `SearchHelp*` tests and remove
    `SearchHelpTool` from the tool list in `NoToolTakesAUserIdARoleOrAPassword` (keep that test).
  Verify that the solution builds and that a search for `search_help`, `SearchHelpTool` and
  `HelpKnowledge` over `backend/` finds nothing.
- [x] 4.4 Add sources (design D10): `AssistantSourceDto(Document, Section, Path)`, `Sources` on
  `AssistantMessageMeta` and `AssistantMessageDto` (empty list when absent, so old messages still
  load), filled from the Found context, de-duplicated, and empty for fixed tool replies. Verify with
  unit tests, and with an integration test that a reloaded conversation returns the sources.
- [x] 4.5 Add `DocumentIndexController` (design D13) with `DocumentIndexStatusDto`. Verify with
  integration tests: a user with `SettingsAiView` gets the status, `SettingsAiEdit` is needed for
  `POST reindex` (202), an Employee gets 403 on both, and a second `reindex` while running returns
  202 without starting another run.

## 5. Frontend

- [x] 5.1 Add `sources` to the message model in `modules/assistant/models/assistant.model.ts`, and
  show them in `tfa-message` as chips under the answer (document title + last heading, full heading
  path as the tooltip, styled with existing tokens). Verify with a component spec: chips render
  for a message with sources, and nothing renders without them.
- [x] 5.2 Extend the Cloud notice in `tfa-panel.component.html` to say that matching document text is
  sent too. Update its spec.
- [x] 5.3 Add a Document search card to `modules/settings/tfa-settings` (status fields from the spec,
  a Re-index now button shown only with `SettingsAiEdit`, polling every 3 s while `isRunning`, and the
  last error with its file) and the two calls in `ai-provider-admin.service.ts` (or a sibling
  service). Verify with component specs for the idle, running and error states and for the hidden
  button.

## 6. Setup, documentation and verification

- [x] 6.1 Make `scripts/setup-ai.ps1` and `scripts/setup-ai.sh` also pull `nomic-embed-text` and check
  `/api/embed` answers. Run each script twice to confirm it stays idempotent.
- [x] 6.2 Tuning check: write 15 questions with known answers (5 from screen specs, 5 from FRD/BRD
  rules, 3 from TDD/LLD, 2 not covered), plus the questions the removed help topics answered and
  the overdue-tasks question from 4.2. Run them against `llama3.2:3b` with the real index, and record retrieval hits and answer correctness
  in the change folder (`verification.md`). Adjust `MinScore`, `TopK` and `MaxChunkCharacters`
  until every covered question retrieves its section in the top 5 and both uncovered questions get
  the "documents do not cover it" answer.
- [x] 6.3 Update the README (TFA section: document answers, sources, setup with `nomic-embed-text`,
  the `DocumentKnowledge` settings, the Docker mount), `project.md` (stack table: embeddings, local
  document index), the TDD (assistant section and data model), and the TFA BRD/FRD/HLD/LLD sub-module
  documents (new version entry for the approved BRD), per `agent.md` §6.
- [x] 6.4 Run `dotnet build`, `dotnet test`, `ng test` and `npm run build`, then start the app,
  re-index from TFA Settings, and ask three documentation questions and one task question in TFA as
  an Employee. Confirm the answers, sources and status card. (The status card and Re-index were
  removed afterwards; task 7.5 repeats this check without them.)

## 7. Remove the Document search card and index endpoints (design D13)

- [x] 7.1 Frontend: delete `components/document-index-card.component.{ts,html,css,spec.ts}`, remove
  the card from `pages/tfa-settings.component.{ts,html}` and the card assertion from
  `tfa-settings.component.spec.ts`, and remove `getDocumentStatus()` / `reindexDocuments()` from
  `ai-provider-admin.service.ts` and `DocumentIndexStatus` from `ai-provider-admin.model.ts`.
  Verify that a search for `document-index`, `DocumentIndexStatus`, `getDocumentStatus` and
  `reindexDocuments` over `frontend/src` finds nothing.
- [x] 7.2 Backend: delete `Controllers/Settings/DocumentIndexController.cs`,
  `Services/Settings/TfaSettings/DocumentIndex/` (service and interface) and
  `Dtos/Settings/TfaSettings/DocumentIndexDtos.cs`; remove the `IDocumentIndexService` registration
  from `Config/ServiceRegistration.cs`; remove `DocumentIndexerService.RequestReindex()` and its
  signal, keeping the start delay, the rescan loop and the single-run guard used by the loop. Keep
  `DocumentIndexStatus`, `DocumentIndexer`, `DocumentSearch` and their log warnings.
- [x] 7.3 Tests: delete `IntegrationTests/Settings/TfaSettings/DocumentIndexEndpointsTests.cs`, remove
  the concurrent `RequestReindex()` test from `DocumentIndexerServiceTests.cs`, and add an
  integration test that both old routes return 404. Verify that `ArchitectureTests` still pass.
- [x] 7.4 Docs: remove the Document search card, Re-index now and the two endpoints from the README,
  the TDD, the TFA Settings and Assistant BRD/FRD/HLD/LLD documents, both `08.4-TFA-Settings` workflow
  docs and `verification.md` (keep its tuning results; add a note that the card was removed). Add a
  version entry to each approved document, per `agent.md` §6.
- [ ] 7.5 Run `dotnet build`, `dotnet test`, `ng test` and `npm run build`, start the app, and confirm
  that TFA Settings has no Document search card, that TFA still answers a documentation question with
  sources, and that renaming the documents folder produces the warning in the server log.

## 8. Model names and provider errors (design D14)

- [x] 8.1 Reject a model name containing whitespace in `AddAiModelRequestValidator` and
  `UpdateAiProviderRequestValidator` with the D14 message, and mirror the rule in the Add AI model
  form. Verify with validator unit tests (`Gemini API Key` rejected, `gemini-3.8-flash` and
  `llama3.2:3b` accepted) and a component spec that no test request is sent.
- [x] 8.2 Add the per-provider model-name placeholder and the Gemini key hint (where to get a key in
  Google AI Studio; no check on the key prefix) to the Add AI model page. Verify with a component spec for each
  provider.
- [x] 8.3 In `GeminiProvider.Classify`, map 400 / `INVALID_ARGUMENT` with a model-related message to
  `ModelUnavailable`, after the API-key rule. Verify with unit tests for "unexpected model name
  format" (model not available) and "API key not valid" (key problem).
- [x] 8.4 Add `ProviderReason` to `ProviderFailureException`, filled by the Gemini, OpenAI and Ollama
  providers from the error body (status text and message, at most 200 characters, keys replaced by
  `[key]`). In `AiProviderConnectionTester`, append "<provider> said: HTTP <status> - <reason>" to an
  `Unexpected` or `ModelUnavailable` failure and add the reason to the warning log; leave chat replies unchanged. Verify
  with unit tests: an unclassified 400 shows the reason on Test connection, the sent key is
  replaced by `[key]`, a long reason is cut, and `AssistantService` replies do not contain it.
- [x] 8.5 Update the TFA Settings and Add AI model sections of the FRD/LLD and the TDD with the
  model-name rule and the Test connection reason.
- [ ] 8.6 Manual check: on the Add AI model page, `Gemini API Key` as the model name is refused before
  any test, `gemini-3.8-flash` with a valid AI Studio key passes, and a retired model such as
  `gemini-2.0-flash` shows Google's own reason naming the replacement.
- [x] 8.7 Update the Gemini models in `AiProviderCatalog` (design D14) to `gemini-3.8-flash` (default),
  `gemini-3.5-flash` and `gemini-3.5-flash-lite` (replacing `gemini-3.1-pro-preview`, which has no
  free-tier quota), after checking them with `GET v1beta/models` and a test call. Update the catalog tests, the README and the TFA Settings docs that name the old models.
- [x] 8.8 Retire old built-in models in existing databases (design D14, "Existing databases"): in
  `AiProviderSeeder`, mark built-in `AiModels` rows that are no longer in `BuiltInModels` as
  `IsBuiltIn = false`, and move a provider row that is switched off, has no key and names a retired
  built-in model to `DefaultModel`. Verify with seeder tests: a retired built-in becomes removable
  and is removed through `AiProviderConfigService`; a never-set-up Gemini row moves to
  `gemini-3.8-flash`; a set-up provider keeps its retired model; current built-ins stay built-in;
  running the seeder twice changes nothing more. Add a line to the TFA Settings LLD and the TDD.
