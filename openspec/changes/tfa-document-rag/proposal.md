# Proposal

## Why

TaskFlow's project documentation (the BRD, FRD, HLD and LLD sets, the TDD and the 20 screen specs in
`TF - Documents/`) is the source of truth for how the application behaves, but nobody can ask it a
question. TFA's current help comes from 19 short, hand-written topics (`Services/Assistant/Knowledge/*.md`)
matched by keywords, so it cannot answer "what validations apply when creating a task?" or "who
receives the overdue reminder?" even though the documents state it exactly. We want TFA to answer
those questions from the documents, and only from them, at no cost: everything runs on the local
Ollama server TFA already uses, and the index lives in the existing SQLite database.

## What Changes

- Add **document retrieval (RAG) to TFA**. The Markdown files under a configured documents folder
  (default: the workspace's `TF - Documents/`) are split into heading-aware chunks, embedded with
  the free local Ollama model `nomic-embed-text`, and stored in SQLite.
- When a user sends a message, the backend embeds the question, finds the most similar chunks and
  gives them to the model as reference text, with an instruction to answer documentation questions
  only from that text and to say so when the documents do not cover the question. Retrieval runs in
  code before the model call, so it works even with small local models that cannot call tools.
- Answers that used the documents carry **sources** (document name and section heading) that the TFA
  panel shows under the answer.
- **Documentation questions use only the documents.** The keyword help topics are no longer used to
  answer them (the `search_help` tool is removed). TFA's live-data features (my tasks,
  prioritisation, mail drafting, profile help) are unchanged.
- A **background indexer** builds the index at startup and checks the folder every few minutes. It
  re-embeds only files whose content changed and removes chunks of deleted files, so editing a
  document updates TFA's answers without a restart.
- Administrators (users with the TFA Settings permissions) get a **Document search** card in TFA Settings showing the index status (files,
  chunks, last indexed, embedding model, any error) with a **Re-index now** button.
- The AI setup scripts also pull `nomic-embed-text`.

## Capabilities

### New Capabilities
- `tfa-document-knowledge`: indexing the documents folder, retrieval for each TFA message, answering
  documentation questions only from the documents with sources, and the administrator index status
  and re-index control.

### Modified Capabilities

None in `openspec/specs/` (it is empty; earlier changes are not archived). This change supersedes
the **Application help** requirement of the unarchived `tfa-assistant` change
(`specs/tfa-chat-assistant`): documentation and how-to questions are answered from the documents
folder instead of the keyword help knowledge base. That change's other requirements are unchanged.

## Impact

- **Backend:** new `Integrations/Ai/Embeddings` (Ollama embedding client), `Repositories/Assistant/Documents`,
  `Services/Assistant/Documents` (chunker, indexer, vector search, background service), two new
  entities and an additive migration `AddDocumentIndex`, a `DocumentKnowledge` configuration section,
  changes to `AssistantService`, `AssistantPrompt`, `AssistantReadTools` and the assistant DTOs, and
  two endpoints guarded by the existing TFA Settings permissions (`SettingsAiView`, `SettingsAiEdit`). **No new NuGet packages.**
- **Frontend:** source chips in `tfa-message`, and a Document search card in TFA Settings.
- **Data:** tables `DocumentSources` and `DocumentChunks` (89 Markdown files, about 3.2 MB, give
  roughly 3,000-4,000 chunks and 10-15 MB of vectors; task 6.2 records the real count). Existing
  tables are unchanged.
- **Runtime:** Ollama must be running with `nomic-embed-text` pulled for document answers. The first
  index takes a few minutes on a laptop CPU; later runs only process changed files. Without Ollama,
  TFA still works for live-data questions and says the documents cannot be searched right now.
- **Privacy:** all roles may get answers from all documents (decided with the project owner). With a
  cloud chat provider (Gemini/OpenAI), the retrieved document text is sent to that provider along
  with the question; the existing Cloud notice covers this and is extended to mention documents.
- **Docs:** README (TFA section and setup), `project.md` (stack table), TDD, and the TFA BRD/FRD/HLD/LLD
  sub-module documents record the new behaviour.
- **Out of scope:** PDF and Excel files (69 PDFs are exports of a Markdown file next to them and the
  other 4 are exports of the permission spreadsheets, which can be added later), per-role document filtering, uploading documents
  through the UI, hybrid keyword + vector ranking, re-ranking models, and streaming answers.
