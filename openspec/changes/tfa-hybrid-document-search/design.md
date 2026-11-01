# Design

## Context

See proposal.md (Why). Retrieval today (`DocumentSearch.SearchAsync`):

```
message --> embed (Gemini, query prefix) --> DocumentVectorIndex.Search
            (in-memory snapshot of all chunks, brute-force dot product,
             keep score >= MinScore 0.65, TopK 4, MaxContextCharacters 4000)
        --> DocumentContext.Found / NoMatch / Unavailable(CannotSearch | Preparing)
```

- Chunks live in `DocumentChunks` (`Id`, `DocumentSourceId`, `Ordinal`, `HeadingPath`, `Text`,
  `Embedding`, `Dimensions`) and are written only through `IDocumentIndexRepository`
  (`ReplaceChunksAsync`, `RemoveByPathsAsync`). The in-memory snapshot (`IndexedChunk`) is reloaded
  from them after every index run.
- The schema is created by EF Core migrations (`Database.MigrateAsync` in `Program.cs` and in every
  integration-test fixture), so raw SQL in a migration runs everywhere the app or tests run.
- A short follow-up message is searched again together with the previous user message (design D9 of
  `tfa-document-rag`); that behaviour is kept for both search paths.
- Embedding failures surface as `EmbeddingUnavailableException` and today end in
  `Unavailable(CannotSearch)`.

## Goals / Non-Goals

**Goals:**
- Hybrid ranking with no new package and no new process.
- Keyword search reads only from SQLite; the vector path stays in memory.
- Behaviour for ordinary questions changes as little as possible; the "switched off" setting gives
  exactly the old behaviour.

**Non-Goals:**
- Re-ranking models, query rewriting by the model, synonyms.
- Keyword search over document titles or file names separately from chunk text (the outermost
  heading is already in `HeadingPath`).
- Changing chunking, embedding, the prompt or the sources format.

## Decisions

**H1 - FTS5 external-content table kept in sync by triggers.**
`CREATE VIRTUAL TABLE DocumentChunks_fts USING fts5(HeadingPath, Text, content='DocumentChunks',
content_rowid='Id', tokenize='porter unicode61')`, plus `AFTER INSERT / DELETE / UPDATE` triggers on
`DocumentChunks` that write the matching `'delete'` / insert rows, and a final
`INSERT INTO DocumentChunks_fts(DocumentChunks_fts) VALUES('rebuild')` to index existing rows.
- External content: chunk text is not stored twice.
- Triggers: every write path (repository, cascade delete of a `DocumentSource`, tests that seed
  rows) stays in step without code having to remember. SQLite fires child-table delete triggers for
  `ON DELETE CASCADE`; a repository test covers it.
- `porter unicode61`: stemming matches "validation" / "validations"; `unicode61` folds case and
  diacritics. A quoted term such as `"NFR-14"` is tokenised to the phrase `nfr 14`, which matches the
  identifier exactly as written in the documents.
- Down migration drops the triggers and the table.
- *Alternatives:* an in-memory inverted index (no SQL, but re-implements bm25 and stemming, and the
  user chose FTS5); a contentless table filled by the indexer (needs code on every write path);
  `trigram` tokenizer (substring matching, larger index, no stemming).

**H2 - Keyword query built by a pure rule** (`Services/Assistant/Documents/Rules/KeywordQuery`).
- Split the message on anything that is not a letter, digit, `-`, `_` or `.`; trim `-_.` from term
  ends; drop terms shorter than 2 characters and a small built-in English stop-word list
  ("what", "the", "how", "do", "i", ...).
- Each remaining term is wrapped in double quotes (inner quotes doubled) and the terms are joined
  with spaces, which FTS5 reads as AND. Quoting makes `AND`, `OR`, `NOT`, `NEAR`, `*`, `^`, `:` and
  column filters plain words, so no user text can produce an FTS syntax error.
- No terms left -> no keyword search.
- The rule also reports whether the message has an **exact identifier**: a term with a digit, `-`,
  `_`, `.`, or an upper-case letter after the first character.
- *Alternative:* OR of terms (recall up, but common words like "task" match half the corpus and
  would drag live-data questions into document answers).

**H3 - Repository method for keyword search.**
`IDocumentIndexRepository.SearchKeywordAsync(string ftsQuery, int limit)` returns chunk ids ordered by
`bm25(DocumentChunks_fts, 2.0, 1.0)` (heading matches weigh double). Ids are mapped to the in-memory
`IndexedChunk`s; an id missing from the snapshot (index mid-update) is skipped.
- *Alternative:* `DbSet.FromSql` on an entity: the FTS table has no entity and the result is only
  ids, so a raw `SqliteCommand` through the context's connection is simpler.

**H4 - Fusion with Reciprocal Rank Fusion** (`Rules/RankFusion`, pure).
- Vector list: chunks with similarity >= `KeywordAssistMinScore`, best first, up to
  `CandidateCount`. Keyword list: up to `CandidateCount` ids from H3.
- Eligibility (spec): vector score >= `MinScore`; or in the keyword list and vector score >=
  `KeywordAssistMinScore`; or in the keyword list and the message has an exact identifier.
- Score = sum over the lists the chunk appears in of `1 / (RrfK + rank)`, rank from 1. Order by
  score, then by chunk id. Then the existing TopK and character-budget rule (best match always
  kept) is applied unchanged; it moves out of `DocumentVectorIndex.Search` into a shared helper so
  both paths use it.
- `DocumentVectorIndex` gains a method that returns all similarities for a query vector above a
  floor, so the vector list and the "vector score" lookup for keyword hits come from one pass.
- *Alternative:* weighted sum of normalised cosine and bm25: needs per-corpus tuning of two scales;
  RRF needs none.

**H5 - Keyword-only fallback.**
When `EmbedAsync` throws `EmbeddingUnavailableException`, `DocumentSearch` logs the warning as today
and runs the keyword path alone; every keyword hit is eligible (spec). Found -> `Found(chunks)`;
nothing -> `Unavailable(CannotSearch)`, unchanged for the user. The follow-up rule (D9) applies to
both paths: the message alone first, then message + previous user message if the first finds
nothing.

**H6 - Configuration** (`DocumentKnowledge` section, validated in `DocumentKnowledgeOptions`):

| Key | Default | Rule |
|---|---|---|
| `HybridSearch` | `true` | `false` = similarity only, old behaviour |
| `KeywordAssistMinScore` | `0.55` | 0..1; a value above `MinScore` acts as `MinScore` (search uses the lower of the two). Not rejected, so a configuration that already lowered `MinScore` below 0.55 still starts |
| `CandidateCount` | `20` | 1..100 and >= `TopK` |
| `RrfK` | `60` | 1..1000 |

The 0.55 default is a starting point; task 4.3 checks it against the real documents.

## Risks / Trade-offs

- [A keyword match for common words pulls document text into live-data questions] -> AND of all
  significant terms, stop words removed, and ordinary words still need similarity >= 0.55.
- [FTS table drifts from `DocumentChunks`] -> triggers on every write, `rebuild` in the migration,
  and a repository test that inserts, replaces and cascade-deletes and compares counts.
- [FTS5 not compiled into the shipped SQLite] -> the migration fails loudly at start-up; a
  migration test runs it in CI. (The `e_sqlite3` bundle used by EF Core Sqlite enables FTS5.)
- [Extra SQL query per message] -> one indexed FTS lookup on ~a few thousand rows, well under the
  embedding call's latency; skipped when hybrid search is off or no terms remain.
- [`porter` stemming merges unrelated words] -> acceptable for English docs; identifiers are matched
  as quoted phrases and keep their digits.

## Migration Plan

1. Deploy: the new migration creates the FTS table and triggers and runs `rebuild` from existing
   chunks; no re-embedding, no downtime beyond the normal restart.
2. Rollback: set `DocumentKnowledge:HybridSearch` to `false` and restart (the index is kept), or
   roll back the migration, which drops the triggers and the table.
