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
  an Employee. Confirm the answers, sources and status card.
