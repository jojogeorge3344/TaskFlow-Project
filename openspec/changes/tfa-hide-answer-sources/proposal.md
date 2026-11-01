# Proposal

## Why

Every TFA answer that used document text shows a "Sources" row of file chips under it. Users do not
want to see it. Because every section passed to the model is listed, whether or not the answer used
it, the row also appears on answers that have nothing to do with the documents. For example, "hi"
listed a Dashboard BRD section on 2026-11-01.

## What Changes

- The TFA panel no longer shows the Sources row under answers, for any role.
- The backend keeps attaching sources to the reply and storing them with the message, unchanged.
  Showing them again later is a frontend-only change, and older conversations keep their stored
  sources.
- Assumption (recorded because the question was left open): sources are hidden for everyone,
  administrators included. There is no per-role or per-user toggle.

Out of scope: changing how sources are chosen, removing `sources` from the API or the database,
and asking the model to cite documents in its answer text.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `tfa-document-knowledge`: "Answers show their sources" changes from "the panel shows them under
  the answer" to "sources are recorded with the reply but not shown in the panel".

## Impact

- Frontend: `modules/assistant/components/tfa-message.component.html` (remove the Sources block),
  `tfa-message.component.ts` (remove the `sources` computed and the `SourceChip` type),
  `tfa-message.component.css` (remove `.tfa-sources*` / `.tfa-source*`), and
  `tfa-message.component.spec.ts` (replace the source-chip tests with one asserting nothing is
  shown).
- Backend, API and database: no change.
- Docs: the TFA chat workflow and TDD notes that describe sources under answers.
