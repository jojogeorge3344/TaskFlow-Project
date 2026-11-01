# Proposal

## Why

TFA finds document text only by vector (embedding) similarity. That misses exact terms such as
requirement IDs (`NFR-14`), setting names (`TimeoutSeconds`), model IDs (`gemini-3.8-flash`) and
screen names, which embeddings blur. When the Gemini embedding call fails (no key, rate limit,
timeout), TFA cannot search the documents at all, although every chunk's text is already stored
locally. SQLite's built-in FTS5 full-text index fixes both, with no new package and no new server.

## What Changes

- Add an FTS5 full-text index over the stored document chunks (heading path and text), created by an
  EF Core migration and kept in step with `DocumentChunks` automatically.
- Retrieval becomes **hybrid**: each message is searched both by vector similarity and by keywords
  (bm25), and the two ranked lists are merged with Reciprocal Rank Fusion before the existing
  top-K / character-budget limits apply.
- Exact identifiers in a message (terms with a digit, hyphen, underscore, dot or inner capital, e.g.
  `NFR-14`, `TimeoutSeconds`) let a keyword match through even when its vector score is below the
  minimum similarity.
- When the query cannot be embedded, TFA falls back to keyword-only search instead of saying the
  documents cannot be searched; it says so only when the keyword search also finds nothing.
- New `DocumentKnowledge` settings to switch hybrid search off and tune it; defaults keep current
  behaviour for ordinary questions with no matching keywords.
- No change to the chat model call, the prompt rules, sources shown to the user, or the UI.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `tfa-document-knowledge`: "Relevant document text is retrieved for each message" changes from
  vector-only to hybrid (vector + keyword) retrieval; "TFA keeps working when documents cannot be
  searched" gains the keyword-only fallback when the query cannot be embedded.

## Impact

- **Backend:** `Services/Assistant/Documents/DocumentSearch`, a new pure keyword-query builder and
  rank-fusion rule under `Services/Assistant/Documents/Rules`, a keyword search method on
  `IDocumentIndexRepository`, `Config/DocumentKnowledgeOptions` (+ validation), one new migration
  (FTS5 virtual table, sync triggers, initial rebuild).
- **Database:** new virtual table `DocumentChunks_fts` and its shadow tables in `taskflow.db`; filled
  from existing rows by the migration, so no re-embedding is needed.
- **Dependencies:** none. FTS5 is compiled into the SQLite engine shipped with
  `Microsoft.EntityFrameworkCore.Sqlite`.
- **Tests:** unit tests for query building, identifier detection and fusion; repository and
  `DocumentSearch` tests against a real SQLite file; migration test.
- **Docs:** TFA Assistant HLD/LLD/TDD retrieval sections and `appsettings.json` defaults.
- **Related:** independent of the planned `tfa-meai-providers` change (Microsoft.Extensions.AI
  provider migration); this change does not touch the provider or embedding clients.
