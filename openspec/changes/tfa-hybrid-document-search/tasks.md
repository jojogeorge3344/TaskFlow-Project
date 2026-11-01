# Tasks

## 1. FTS5 index in the database

- [x] 1.1 Add an EF Core migration `AddDocumentChunkFullText` that creates `DocumentChunks_fts` (fts5, external content on `DocumentChunks`, `porter unicode61`), the insert/delete/update sync triggers and the `rebuild` command, with a `Down` that drops triggers and table (design H1); verify `dotnet ef migrations list` shows it and the app starts against a copy of the current `taskflow.db`
- [x] 1.2 Add a migration test (next to `DocumentIndexMigrationTests`) that migrates an empty database and one with seeded chunks and asserts the FTS row count equals the chunk count; verify the test passes
- [x] 1.3 Add `SearchKeywordAsync(ftsQuery, limit)` to `IDocumentIndexRepository` / `DocumentIndexRepository` returning chunk ids by `bm25` with headings weighted 2.0 (design H3); verify new `DocumentIndexRepositoryTests` cover a hit, no hit, heading-over-text ordering, and that `ReplaceChunksAsync`, `RemoveByPathsAsync` and a cascade delete of a source keep the FTS table in step

## 2. Pure search rules

- [x] 2.1 Add `Rules/KeywordQuery` that turns a message into a quoted AND query and reports whether it has an exact identifier (design H2); verify unit tests for stop words, short terms, `NFR-14`, `TimeoutSeconds`, `gemini-3.8-flash`, inner quotes, FTS operator words (`AND`, `OR`, `NOT`, `NEAR(...)`, `*`, `col:`) and an all-stop-word message yielding no query
- [x] 2.2 Add `Rules/RankFusion` implementing eligibility and Reciprocal Rank Fusion, and move the TopK / character-budget selection (best match always kept) into a shared helper used by both paths (design H4); verify unit tests for each eligibility rule, RRF ordering, tie-break by chunk id, and that existing `DocumentVectorIndexTests` still pass
- [x] 2.3 Add a `DocumentVectorIndex` method that returns every chunk's similarity above a floor in one pass; verify unit tests for dimension mismatch skipping and floor filtering

## 3. Hybrid retrieval in DocumentSearch

- [x] 3.1 Add `HybridSearch`, `KeywordAssistMinScore`, `CandidateCount`, `RrfK` to `DocumentKnowledgeOptions` with the validation rules in design H6 and defaults in `appsettings.json`; verify `DocumentKnowledgeOptionsTests` cover each rule
- [x] 3.2 Make `DocumentSearch.SearchAsync` run vector + keyword search and fuse them, keeping the follow-up query rule for both paths, and use similarity only when `HybridSearch` is false; verify `DocumentSearchTests` (real SQLite file, fake embeddings) for: identifier found below `MinScore`, borderline chunk lifted, ordinary-word match below the assist floor rejected, nothing relevant, operator words, and hybrid off matching the old results
- [x] 3.3 Add the keyword-only fallback when embedding throws `EmbeddingUnavailableException` (design H5), keeping the existing warning log; verify `DocumentSearchTests` for missing key with a keyword hit (Found + warning), missing key with no hit (CannotSearch), and rate limit with a keyword hit

## 4. Documentation and end-to-end check

- [x] 4.1 Update the retrieval sections of the TFA Assistant HLD, LLD and `TDD-TaskFlow.md` (hybrid flow diagram, FTS table, new settings, fallback), and the Tech WF `09.1-Chat.md` if it describes retrieval; verify the docs mention `DocumentChunks_fts`, the four new settings and the keyword fallback
- [x] 4.2 Run the full backend unit and integration test suites; verify both pass
- [ ] 4.3 With the real `TF - Documents` index, ask TFA "what does NFR-14 say?", "what validations apply when creating a task?" and "what are my tasks due this week?"; verify the first two cite the right documents and the third uses task tools with no document sources, and adjust `KeywordAssistMinScore` in design H6 and `appsettings.json` if the third pulls in documents
