# High-Level Design – Assistant

## TaskFlow – Intelligent Task Allocation & Tracking System

> **Document:** High-Level Design (Sub-module: TFA Assistant, code `TFA`)  ·  **Version:** 1.1  ·  **Status:** Draft
> **Project type:** Personal Project (Skill Development)  ·  **Date:** 31-Oct-2026
> **Author:** Jojo George (jojogeorge3344@gmail.com)
> **Based on:** BRD-Assistant v1.1, FRD-Assistant v1.1
> © 2026 Jojo George. All rights reserved.

---

## Document Control

| Version | Date | Author | Summary of changes |
|---|---|---|---|
| 1.0 | 31-Oct-2026 | Jojo George | Initial version |
| 1.1 | 31-Oct-2026 | Jojo George | TFA answers TaskFlow questions from the documents folder (OpenSpec change `tfa-document-rag`). Built-in help (`SearchHelpTool`, `HelpKnowledgeBase`, 19 embedded topics) replaced by document knowledge: background indexing of the Markdown documents, local embeddings on Ollama (`nomic-embed-text`), document index in SQLite, in-memory vector search before every model call, answer sources. Updated §1, §2, §3, §4, §5, §6, §7, §8 (new §8.5), §9, §10, §11, §12, §13 (D-TFA-5 superseded, D-TFA-11 – D-TFA-14 added), §14 and §15. |

**Related documents:**

| Document | Location |
|---|---|
| Overall HLD (stack, deployment, cross-cutting design, decisions `D-nn`) | `../../00-Overall/HLD-TaskFlow-Overall.md` |
| BRD – Assistant | `../../../BRD/08-TFA-Assistant/08.1-Assistant/BRD-Assistant.md` |
| FRD – Assistant (`FR-TFA-nn`, `UC-TFA-nn`) | `../../../FRD/08-TFA-Assistant/08.1-Assistant/FRD-Assistant.md` |
| LLD – Assistant (classes, endpoints, tables) | `../../../LLD/08-TFA-Assistant/08.1-Assistant/LLD-Assistant.md` |
| HLD – TFA Settings (provider, model and API key configuration) | `../../06-Settings/06.3-TFA-Settings/HLD-TFA-Settings.md` |
| HLD – Mail, My Profile, Employee Management (targets of confirmed actions) | `../../04-Communication/04.1-Mail/HLD-Mail.md`, `../../07-My-Profile/07.1-My-Profile/HLD-My-Profile.md`, `../../03-TaskFlow-Management/03.1-Employee-Management/HLD-Employee-Management.md` |
| TDD v2.2 – §16 TFA Assistant Design (document knowledge in §16.8) | `TF - Documents/TF - TDD/TDD-TaskFlow.md` |
| OpenSpec change `tfa-document-rag` (proposal, design D1 – D13, spec) | `openspec/changes/tfa-document-rag/` |

---

## 1. Introduction

### 1.1 Purpose
This HLD describes how TFA (TaskFlow Assistant) is built: a tool-calling chat assistant hosted inside the TaskFlow API,
with a panel on every signed-in screen. It covers the conversation engine, the model gateway to Ollama, Gemini and
OpenAI, the permission-gated tools, the confirm-before-act mechanism for actions, the document knowledge (answers taken only
from the TaskFlow documents, found by a local embedding search before each model call), voice input in the browser, and the safety controls (secret screen, rate limit, audit). Classes, endpoint details and columns
are in the LLD.

### 1.2 Scope
**In scope:** TFA Panel (conversation, model selector, cloud notice, new chat, provider failures), task answers and the
ranked work order, answers about TaskFlow from the Markdown documents of the documents folder with sources (indexing,
search and prompt rules), Action Cards (mail draft, own-profile update, role change), user lookup
and fixed guidance, voice controls, message limit and action audit.

**Out of scope:** configuring providers, models and API keys, and the document index status card and re-index control
(TFA Settings HLD); PDF and Excel documents, document upload, per-role document filtering, hybrid ranking, re-ranking,
streaming answers; the mailbox, profile and employee rules
themselves (their HLDs); creating, editing or reassigning tasks through TFA; password changes through TFA; an audit
screen. Shared stack and deployment are in the overall HLD.

**Change from original BRD:** the original BRD has no assistant; TFA is an addition (TDD D11). It uses the BRD's
existing rules through the target modules (Mail, My Profile, Employee Management, Task Management).

### 1.3 Audience
Developer, tester and reviewer of TaskFlow; the product owner (Jojo George).

### 1.4 Definitions & Abbreviations

| Term | Meaning |
|---|---|
| Provider | AI service that writes answers: Ollama (local), Gemini or OpenAI (cloud) – `AiProviderKind` |
| Tool | Server function the model may ask to run (read tool or action tool) |
| Tool round | One model call that may request tools; at most 4 per answer |
| Pending action | Prepared action stored with its exact payload, waiting for the user's confirm (10-minute lifetime) |
| Action Card | Panel card that shows a pending action with Confirm / Edit / Cancel |
| Secret screen | Check that stops passwords and API keys from reaching a provider or the database |
| SPA | Single Page Application (Angular frontend) |
| Documents folder | `DocumentKnowledge:SourcePath`, by default the workspace's `TF - Documents` folder; its Markdown files are what TFA answers TaskFlow questions from |
| Chunk | One heading-bounded section (or part of a section) of a document, stored with its embedding |
| Embedding | A 768-number vector made by the Ollama model `nomic-embed-text`; similar texts have similar vectors |
| Retrieval | Finding the chunks most similar to a message, in code, before the model is called |
| Source | Document title, heading path and file path of a chunk given to the model, shown under the answer |

---

## 2. System Context

### 2.1 Context Diagram

```mermaid
flowchart LR
  U["Users: Administrator, Manager, Employee"] -->|"HTTPS JSON"| TFA["TFA Assistant in TaskFlow.Api"]
  U -->|"voice, in browser only"| SPEECH["Browser speech service"]
  TFA -->|"HTTP localhost: chat and /api/embed"| OLL["Ollama (local)"]
  TFA -->|"reads .md files"| DOCS["Documents folder (TF - Documents)"]
  TFA -->|"HTTPS"| GEM["Google Gemini (cloud)"]
  TFA -->|"HTTPS"| OAI["OpenAI (cloud)"]
  TFAS["TFA Settings: providers, models, keys"] -.-> TFA
  TFA -->|"read tasks"| TASK["Task Management"]
  TFA -->|"confirmed actions"| ACT["Mail, My Profile, Employee Management"]
  TFA --> DB[("SQLite taskflow.db")]
```

### 2.2 Actors & External Systems

| Actor / system | Interaction | Protocol |
|---|---|---|
| Users with `assistant.chat` | Chat, read answers, ranked tasks, help | HTTPS / REST (JSON) |
| Users with `assistant.actions.confirm` (and `roles.manage` for role changes) | Edit, confirm or cancel Action Cards | HTTPS / REST |
| Ollama | Local chat model (default `llama3.2:3b`); embedding model `nomic-embed-text` for document search, whatever chat provider is chosen | HTTP to `{BaseUrl or http://localhost:11434}/api/chat` and `/api/embed` |
| Documents folder | Markdown files (BRD, FRD, HLD, LLD, TDD, screen specs) indexed in the background | File system reads, recursive, `.md` only |
| Google Gemini | Cloud chat model (default `gemini-2.0-flash`) | HTTPS, key in `x-goog-api-key` header |
| OpenAI | Cloud chat model (default `gpt-4o-mini`) | HTTPS, Bearer key |
| Browser speech service | Wake phrase, dictation, read-aloud (Chrome, Edge) | Web Speech API in the browser; nothing sent to TaskFlow but recognised text |
| TFA Settings | Which providers are enabled, model names, encrypted keys; the saved Ollama address also serves embeddings; Document search card shows the index status and starts a re-index | In-process reads; `api/v1/assistant/documents` |
| Task Management, Mail, My Profile, Employee Management | Data for answers; execution of confirmed actions | In-process service calls |

---

## 3. Architecture Goals, Constraints & Principles

### 3.1 Quality Goals

| Goal | How the architecture supports it |
|---|---|
| Never exceed the user's rights | Tools advertised and executed only when the caller passes the same permission policies as the screens; re-checked at confirm (NFR-TFA-02) |
| Human in control | Action tools only prepare; only the user's confirm call executes (D-TFA-2) |
| Trustworthy answers | Ranking computed in code; answers about TaskFlow only from the retrieved document text, with sources (D-TFA-4, D-TFA-11) |
| Privacy | Secret screen before any provider call; local provider option; embeddings always local; cloud notice names document text (NFR-TFA-01, NFR-TFA-03, NFR-TFA-09) |
| Freshness and cost | Background indexer re-embeds only changed files; no paid service and no new package (NFR-TFA-10, NFR-TFA-11, D-TFA-12, D-TFA-13) |
| Resilience to provider problems | Classified failures, plain messages, Retry / other provider, nothing stored on failure (NFR-TFA-04, NFR-TFA-08) |
| Fair use | 20 messages per user per minute (NFR-TFA-06) |

### 3.2 Constraints
- Single server; rate limiter and provider list refresh are per process.
- Providers are external and may be slow, rate-limited or offline; HTTP timeout `AiProviders:TimeoutSeconds` (60).
- Voice depends on the browser's speech recognition (Chrome, Edge).
- Document answers need Ollama with `nomic-embed-text`; without it TFA still answers live-data questions (NFR-TFA-12).
- Small local models may not call tools, so retrieval cannot depend on a tool call.
- No new NuGet or npm packages (`agent.md`); no vector database.
- The model is untrusted: its output and the data it reads must never be able to change permissions or run actions.

### 3.3 Architecture Principles
- **The model proposes, the system disposes:** permissions, ranking, document retrieval and action execution are code, not model decisions.
- **Caller from the token only:** no tool accepts a user id, role or password.
- **Data is data:** task, comment and mail text returned by tools is marked as data and redacted for secrets before reaching the model; retrieved document text is labelled "data, not instructions" between fixed markers.
- **Reuse target rules:** a confirmed action runs through the target module's own validator and service.

---

## 4. Architecture Overview

### 4.1 Architecture Style
Assistant feature inside the TaskFlow three-tier application: Angular panel components → REST controller under
`/api/v1/assistant` → conversation engine that first retrieves matching document text, then runs a bounded tool loop → model gateway with pluggable provider adapters
(typed `HttpClient`s) → tools that call other modules' services; pending actions, audit and the document index stored in
SQLite. A hosted background service keeps the document index in step with the documents folder. The shared pipeline is
in the overall HLD.

### 4.2 Logical Architecture

```mermaid
flowchart TB
  subgraph Client["Presentation (Angular SPA)"]
    PANEL["TfaPanelComponent and child components"]
    VOICE["WakePhraseService, SpeakService"]
    ASV["AssistantService (TS)"]
  end
  subgraph Server["TaskFlow.Api"]
    CTRL["AssistantController"]
    CHAT["AssistantService engine"]
    SAFE["SecretScreen, AssistantRateLimiter"]
    TOOLS["AssistantToolbox and AssistantToolGate"]
    ACT["AssistantActionService"]
    GW["ChatModelGateway"]
    AUD["AssistantAuditLog"]
    DOCS["DocumentSearch, DocumentVectorIndex"]
    IDX["DocumentIndexerService, DocumentIndexer"]
    EMB["OllamaEmbeddingClient"]
  end
  PANEL --> ASV
  VOICE --> PANEL
  ASV -->|"HTTPS JSON"| CTRL
  CTRL --> CHAT --> SAFE
  CHAT --> DOCS --> EMB
  IDX --> EMB -->|"HTTP /api/embed"| OLLE["Ollama (embeddings)"]
  IDX -->|"reads .md"| FOLDER["Documents folder"]
  IDX --> DB
  CHAT --> TOOLS
  CHAT --> GW -->|"HTTP"| PROV["Ollama, Gemini, OpenAI"]
  CTRL --> ACT --> AUD
  CHAT --> DB[("SQLite")]
```

### 4.3 Layers & Responsibilities

| Layer | Responsibility | May call |
|---|---|---|
| Presentation | `TfaPanelComponent` (button, panel, feed, cloud notice, failure card), `TfaHeaderComponent` (new chat, close, model selector), `TfaComposerComponent` (message box, suggestions, mic), `TfaMessageComponent` (text, ranked list, links, sources, cards), `TfaActionCardComponent` (confirm / edit / cancel); `AssistantService` (TS) signals; `WakePhraseService`, `SpeakService`; browser storage `tfa.provider`, `tfa.cloudAck`, `tfa.handsfree` | API, browser speech |
| API / Controllers | `AssistantController`: caller from the token (`AssistantCaller`), `perm:assistant.chat` / `perm:assistant.actions.confirm` per action | Services |
| Conversation engine | `AssistantService`, `AssistantPrompt`, `AssistantTexts`, `ProviderFailureMessages`: length check, secret screen, provider choice, rate limit, history window, document search, prompt with document block, tool loop, sources, persistence | Gateway, document search, tools, repositories |
| Document knowledge | `DocumentSearch`, `DocumentVectorIndex`, `DocumentIndexer`, `DocumentIndexerService`, `DocumentIndexStatus`, `DocumentEmbeddingEndpoint`, `MarkdownChunker` | Embedding client, document index repository, unit of work |
| Tools | `AssistantToolbox` + `AssistantToolGate` (via `IAssistantAuthorizer`); read tools (tasks, ranking, user lookup, guidance) and action tools (draft mail, profile update, role change) | Other modules' services, `TaskPrioritiser` |
| Actions | `AssistantActionService`, `AssistantActionRules`: ownership, expiry, permission re-check, single claim, execution, audit | Mail, Profile, Employee services |
| Model gateway / integrations | `ChatModelGateway` → `ChatModelProviderRegistry` → `OllamaProvider`, `GeminiProvider`, `OpenAiProvider`; `ProviderHttp`, `ProviderFailure`, `AiProviderRuntime`; `OllamaEmbeddingClient` (`IEmbeddingClient`) | External providers, local Ollama |
| Repositories | Conversation, message, pending action, audit, directory, document index repositories | Database |

---

## 5. Component / Module Design

| Module | Responsibility | Key components (high level) | Depends on |
|---|---|---|---|
| TFA Panel (chat) | Panel on every signed-in screen; conversation load, send, new chat, model selector, cloud notice, provider failure card | `TfaPanelComponent`, `TfaHeaderComponent`, `TfaComposerComponent`, `TfaMessageComponent`, `AssistantService` (TS); `AssistantController`; `AssistantService` engine | TFA Settings (configured providers) |
| Conversation engine | Validate, screen secrets, limit rate, build prompt and history, run up to 4 tool rounds, store question and answer | `AssistantService`, `AssistantPrompt`, `SecretScreen`, `AssistantRateLimiter`, conversation and message repositories | Application Settings (name, timezone) |
| Model gateway | One interface over three providers; failure classification (`ProviderFailureKind`) | `ChatModelGateway`, `ChatModelProviderRegistry`, provider adapters | TFA Settings (`AiProviderConfig`, `AiModel`, encrypted keys via `AiKeyProtector`) |
| Task answers and work order | Own / accessible open tasks; deterministic ranking | `GetMyTasksTool`, `PrioritiseTasksTool`, `TaskPrioritiser` | Task Management (`ITaskService`, access rules) |
| Document knowledge (v1.1; replaces how-to help) | Index the Markdown documents (heading-aware chunks, local embeddings, only changed files); retrieve the best chunks for each message before the model call; prompt block (Found / NoMatch / Unavailable); sources under answers | `DocumentIndexerService`, `DocumentIndexer`, `MarkdownChunker`, `OllamaEmbeddingClient`, `DocumentVectorIndex`, `DocumentSearch`, `AssistantPrompt`, document index repository; `TfaMessageComponent` source chips | Documents folder; Ollama (`nomic-embed-text`); TFA Settings (saved Ollama address; Document search card) |
| Lookup and guidance | Find users for mail; fixed password and "needs an administrator" guidance | `FindUserTool`, `ExplainPasswordChangeTool`, `ContactAdminGuidanceTool` | Mail (`IMailService` contacts) |
| Action Cards | Prepare, edit (mail only), confirm, cancel; expiry; run once; audit | `DraftMailTool`, `ProposeProfileUpdateTool`, `ProposeRoleChangeTool`, `AssistantActionService`, `TfaActionCardComponent`, pending-action repository | Mail, My Profile, Employee Management services and validators |
| Voice controls | Wake phrase, dictation, read-aloud, hands-free toggle | `WakePhraseService`, `SpeakService` (frontend only) | Browser Web Speech API |
| Background safeguards | Rate limit, outcome audit | `AssistantRateLimiter`, `AssistantAuditLog`, audit repository | – |

### 5.1 Module Interaction

```mermaid
flowchart LR
  TFAS["TFA Settings"] -.->|"providers and keys"| TFA["TFA Assistant"]
  APP["Application Settings"] -.->|"name, timezone"| TFA
  TFA -->|"GetMyTasks via ITaskService"| TASK["Task Management"]
  TFA -->|"SendAsync, GetContactsAsync"| MAIL["Mail"]
  TFA -->|"UpdateAsync"| PROF["My Profile"]
  TFA -->|"UpdateAsync with RoleId"| EMP["Employee Management"]
  EMP -->|"sessions ended"| AUTH["Authentication"]
  ROLE["Role Configuration"] -.->|"assistant.* and roles.manage"| TFA
  TFA -->|"links to screens"| UI["Task, Mail, Profile screens"]
```

| From → To | What | Coupling |
|---|---|---|
| TFA → Task Management | Open tasks in the user's scope (`mine` or `accessible`, max 20) | `ITaskService`, so Task Management scope applies (BR-TFA-05) |
| TFA → Mail | Contacts lookup; send a confirmed draft from the user's mailbox | `IMailService` + `SendMailRequestValidator` (BR-TFA-09) |
| TFA → My Profile | Apply a confirmed own name / email change, mobile unchanged | `IProfileService.UpdateAsync` + `UpdateOwnProfileRequestValidator` |
| TFA → Employee Management | Apply a confirmed role change; target user's sessions end | `IEmployeeService.UpdateAsync` |
| TFA Settings → TFA | Enabled providers, model names, keys | Read per request; panel refreshes the list every 60 s |
| TFA Settings ↔ Document knowledge | Saved Ollama address used for embeddings; the Document search card reads `DocumentIndexStatus` and wakes `DocumentIndexerService` | `DocumentEmbeddingEndpoint`; `DocumentIndexService` (TFA Settings HLD) |

---

## 6. Data Architecture (high level)

### 6.1 Data Stores

| Store | Technology | Holds | Notes |
|---|---|---|---|
| Main database | SQLite `taskflow.db` (EF Core) | Conversations, messages (with sources in their metadata), pending actions, audit records, document index (`DocumentSources`, `DocumentChunks`); reads provider configuration | Migrations `20261018183210_AddAssistant`, `20261019163241_AddAiModels`, `20261031134802_AddDocumentIndex`; conversations kept indefinitely; the document index is rebuildable from the folder |
| Documents folder | File system (`DocumentKnowledge:SourcePath`) | The Markdown documents TFA answers from | Read only; checked every `RescanMinutes` (5) |
| In-memory | Process | Rate-limit windows; `DocumentVectorIndex` (every chunk vector of the configured model); `DocumentIndexStatus` | Reset on restart; the vector index is reloaded from the database at startup |
| Browser storage | `localStorage` | Chosen provider, cloud acknowledgements, hands-free off | Not on the server |

### 6.2 Main Data Domains

```mermaid
erDiagram
  User ||--o{ AssistantConversation : owns
  AssistantConversation ||--o{ AssistantMessage : contains
  User ||--o{ AssistantPendingAction : prepares
  AssistantConversation ||--o{ AssistantPendingAction : "raised in"
  User ||--o{ AssistantAudit : "audited for"
  User |o--o{ AiProviderConfig : "last updated by"
  DocumentSource ||--o{ DocumentChunk : "split into"
```

| Domain | Owned / used | Purpose |
|---|---|---|
| `AssistantConversation` | Owned | A user's chat thread; the latest is active |
| `AssistantMessage` | Owned | Question, answer or tool message (`AssistantMessageRole`: User, Assistant, Tool) with provider used and display metadata |
| `AssistantPendingAction` | Owned | Prepared action (`AssistantActionKind`: SendMail, UpdateOwnProfile, ChangeUserRole) with exact payload, `AssistantActionStatus` (Pending, Confirmed, Cancelled, Expired, Refused, Failed) and expiry |
| `AssistantAudit` | Owned | Outcome record: user, kind, outcome, time – no bodies |
| `AiProviderConfig`, `AiModel` | Used (TFA Settings) | Enabled providers (`AiProviderKind`: Ollama, Gemini, OpenAI), model names, encrypted keys |
| `User`, `Task`, `MailMessage`, `Role` | Used through other modules' services | Answers and action targets |
| `DocumentSource` | Owned (v1.1) | One indexed Markdown file: relative path, title, content hash, embedding model, chunk count, indexed time |
| `DocumentChunk` | Owned (v1.1) | One section of a file: heading path, text, embedding vector (float32 BLOB), dimensions; deleted with its source |

### 6.3 Data Flow
- **Question:** panel → length and secret checks → rate limit → document search (the message; a message under 40 characters that matches nothing is
  searched again with the previous user message; embedded locally; best chunks by cosine similarity) → last 12 messages
  (about 6000 characters) + prompt with the document block → provider, with tool rounds whose results are redacted →
  question and answer (with sources) stored together only after a successful answer; a provider failure stores nothing.
- **Indexing:** background service → enumerate `.md` files → skip files whose SHA-256 and model are unchanged → chunk,
  embed (batches of 16) and swap the file's chunks in one save → remove files no longer on disk → reload the in-memory
  index.
- **Action:** action tool stores a pending action with the payload shown on the card → user confirms → ownership, expiry
  and permission re-checked → single atomic claim → target service executes → status and audit outcome stored.
- **Voice:** browser recognises speech locally (browser service) → text goes through the normal send path; no audio
  reaches TaskFlow.

---

## 7. Integration Design

| Integration | Direction | Purpose | Failure behaviour |
|---|---|---|---|
| Ollama (`OllamaProvider`) | Outbound HTTP | Local answers (sends `options.num_ctx` = `AiProviders:OllamaContextTokens`, default 8192, so reference text is not cut off) | Unreachable / model missing → plain message, Retry, Use another provider; nothing stored |
| Ollama embeddings (`OllamaEmbeddingClient`) | Outbound HTTP `POST /api/embed` | Embed document chunks and questions with `nomic-embed-text` (prefixes `search_document: ` / `search_query: `) | `EmbeddingUnavailableException` (unreachable, timeout, model not found, unexpected): a message gets the "documents cannot be searched right now" rule and live-data answers continue; an indexing run stops and records the error |
| Documents folder | File system read | Source of document answers | Missing folder → status "The documents folder was not found: …", stored sources removed, API keeps running; one unreadable file → error recorded with the file, other files continue |
| Gemini (`GeminiProvider`) | Outbound HTTPS | Cloud answers | Rate limited, invalid key, timeout, unreachable, model unavailable, unexpected → `ProviderFailureKind` message; no automatic switch |
| OpenAI (`OpenAiProvider`) | Outbound HTTPS | Cloud answers | As Gemini |
| All providers and the embedding client | – | Typed `HttpClient`s, timeout 60 s, redirects not followed, no header or body logging | Raw provider bodies never shown or logged (provider, kind, HTTP status only) |
| Browser speech (`WakePhraseService`, `SpeakService`) | Browser local | Wake phrase, dictation, read-aloud | Matching message (unsupported browser, microphone blocked or missing, no internet); typing still works |
| Mail / My Profile / Employee Management | Outbound in-process | Execute confirmed actions | Status `Failed`, "I could not do that: … Nothing was sent." / "Nothing was changed." |
| Task Management | Outbound in-process | Task answers | Inaccessible task → "That task was not found, or you do not have access to it." |

---

## 8. Key Flows (high level)

### 8.1 Ask a question (UC-TFA-01, main business flow)

```mermaid
sequenceDiagram
  actor U as User
  participant SPA as TfaPanelComponent
  participant C as AssistantController
  participant E as AssistantService engine
  participant T as AssistantToolbox
  participant G as ChatModelGateway
  participant DB as SQLite
  U->>SPA: Message, cloud notice acknowledged if needed
  SPA->>C: Send message with assistant.chat
  C->>E: Caller from token and text
  E->>E: Length, secret screen, rate limit
  E->>E: Document search (local embedding, in-memory index)
  E->>G: Prompt with document block, history, permitted tools
  G-->>E: Tool request
  E->>T: Execute with gate re-check
  T-->>E: Redacted data
  E->>G: Next round, at most 4
  G-->>E: Final answer
  E->>DB: Store question and answer with sources
  C-->>SPA: Answer with links, ranked tasks or sources
```

### 8.2 Confirm a prepared action (UC-TFA-05 – UC-TFA-07)

```mermaid
sequenceDiagram
  actor U as User
  participant CARD as TfaActionCardComponent
  participant A as AssistantActionService
  participant Z as AssistantAuthorizer
  participant DB as SQLite
  participant S as Target service
  U->>CARD: Confirm
  CARD->>A: Confirm with assistant.actions.confirm
  A->>DB: Own pending action only, else 404
  A->>A: Expired or not pending gives 409
  A->>Z: Permission with current token
  A->>DB: Claim Pending to Confirmed once
  A->>S: Validate and execute (mail, profile, role)
  A->>DB: Status and audit outcome
  A-->>CARD: Done, Not allowed or Not done
```

### 8.3 Provider failure (integration)

```mermaid
sequenceDiagram
  actor U as User
  participant E as AssistantService engine
  participant G as ChatModelGateway
  participant P as Cloud provider
  U->>E: Message to Gemini
  E->>G: Chat request
  G->>P: HTTPS call, 60 s timeout
  P-->>G: 429 or timeout or error
  G-->>E: ProviderFailure with kind
  E-->>U: Plain message, Retry, Use other provider
  Note over E: Nothing stored for this turn
```

### 8.4 Ask by voice (UC-TFA-08)

```mermaid
sequenceDiagram
  actor U as User
  participant W as WakePhraseService
  participant P as TfaPanelComponent
  participant K as SpeakService
  participant API as AssistantController
  U->>W: Hey TF assistant
  W->>P: Open panel and greet
  U->>W: Spoken question
  W->>P: Text after 2 s silence
  P->>API: Same send path as typed text
  API-->>P: Answer
  P->>K: Read aloud up to 500 characters
```

### 8.5 Keep the document index current (background, FR-TFA-66)

```mermaid
sequenceDiagram
  participant S as DocumentIndexerService
  participant I as DocumentIndexer
  participant F as Documents folder
  participant E as OllamaEmbeddingClient
  participant DB as SQLite
  participant V as DocumentVectorIndex
  S->>I: Load stored index at startup
  I->>V: Replace snapshot
  Note over S: 10 s after start, then every RescanMinutes or on Re-index now
  S->>I: RunAsync (one run at a time)
  I->>F: List .md files, hash each
  I->>E: Embed changed files only (batches of 16)
  I->>DB: Swap a file's chunks in one save; remove deleted files
  I->>V: Reload every 10 embedded files and at the end
```

---

## 9. Security Architecture

| Concern | Approach |
|---|---|
| Authentication | Shared JWT bearer + refresh cookie, session validation, MFA setup gate (overall HLD) |
| Authorization | `perm:assistant.chat` on conversation, messages, new chat and providers; `perm:assistant.actions.confirm` on edit, confirm and cancel; `roles.manage` required by the role-change tool and its confirm. `AssistantAuthorizer` evaluates the API's own `perm:{key}` policies through `IAuthorizationService`, so TFA can never do more than the screens. The panel is mounted by `ShellComponent` only with `assistant.chat` |
| Data scope | Own conversation and actions only; foreign ids (Guids) return 404, Administrators included; task answers follow Task Management access |
| Model containment | The model sees only tools the caller may use; every tool call re-checks the gate; action tools only prepare; refusals and confirmations use fixed texts; tool output and retrieved document text treated as data, document text between fixed markers it cannot close (prompt-injection defence, R-TFA-02) |
| Document access | Every user with `assistant.chat` may get answers from every indexed document (owner decision, BR-TFA-15, R-TFA-07); the index holds no user data. The status and re-index endpoints need `settings.ai.view` / `settings.ai.edit` (TFA Settings HLD) |
| Embedding server address | `DocumentKnowledge:OllamaBaseUrl` validated at startup (http/https, no link-local or metadata host); otherwise the saved Ollama address, validated when saved |
| Secrets | `SecretScreen` blocks messages that look like passwords, API keys, Bearer tokens or JWTs before any provider call or storage; tool output passed through redaction; provider keys stored encrypted by TFA Settings (`AiKeyProtector`) and never sent to the SPA |
| Cloud data | One-time cloud notice per provider, naming the matching document text; embeddings always made locally; local Ollama option where nothing leaves the machine (R-TFA-03) |
| Abuse | `AssistantRateLimiter`: 20 messages per user per sliding minute → 429 with `Retry-After` |
| Auditing | `AssistantAuditLog` records prepared, confirmed, cancelled, expired, refused and failed outcomes without bodies; 403s also logged by `DeniedActionAuditHandler` |

---

## 10. Technology Stack

TFA uses the shared TaskFlow stack: ASP.NET Core on .NET 10 (`net10.0`) with typed `HttpClient`s for the three providers
and for Ollama embeddings (no vendor AI SDK is installed), a hosted `BackgroundService` for indexing, the Ollama model
`nomic-embed-text` for embeddings, vectors stored as SQLite BLOBs and searched in memory (no vector database or
`sqlite-vec`), EF Core 10.0.12 with SQLite, FluentValidation 12.2.0 (reused target validators),
ASP.NET Core Data Protection for provider keys (TFA Settings), Angular 19.2 with signals for the panel, and the browser
Web Speech API for voice (no package). Versions and rationale: overall HLD (`../../00-Overall/HLD-TaskFlow-Overall.md`).

---

## 11. Deployment Architecture

TFA runs inside the single `TaskFlow.Api` process. Deployment-specific items: an optional Ollama service on the same
machine or network (`BaseUrl`, default `http://localhost:11434`), outbound internet access only when Gemini or OpenAI are
enabled, `AiProviders:TimeoutSeconds` (60) and the Data Protection key ring (`AiProviders:KeyRingPath`) that holds the keys
protecting provider API keys. On a new installation `AiProviderSeeder` enables Ollama with `llama3.2:3b`; Gemini and OpenAI
start off without a key. Document knowledge adds: the documents folder readable by the API (`DocumentKnowledge:SourcePath`,
default `../../../../TF - Documents` relative to the API content root; in Docker the folder is mounted and the path set),
`ollama pull nomic-embed-text` (the AI setup scripts do it), and the `DocumentKnowledge` settings (TopK, MinScore,
chunk and context limits, RescanMinutes; `Enabled` = false switches it off). Environments and the deployment diagram:
overall HLD (`../../00-Overall/HLD-TaskFlow-Overall.md`).

---

## 12. Non-Functional Design

| Area | Design decision |
|---|---|
| Performance | History window of 12 messages / about 6000 characters; at most 4 tool rounds; conversation load limited to the last 100 messages; task tool max 20, ranking max 10; one embedding call per message and a brute-force dot product over the in-memory vectors; at most 5 chunks and 6000 characters of document text per message; unchanged files skipped by hash (first full index of 89 files about 28 minutes on a laptop CPU, 3,327 chunks; later runs seconds) |
| Scalability | Single instance; provider throughput is the limit; rate limiter protects quotas (NFR-TFA-06) |
| Reliability | Atomic claim prevents double execution (NFR-TFA-05); nothing stored on provider failure, Retry resends the same text (NFR-TFA-04); document search never fails a message: unavailable search becomes a prompt rule (NFR-TFA-12); a file's old chunks stay searchable until its new ones are saved; one indexing run at a time |
| Availability | Local Ollama works offline; cloud providers optional; plain message and next step for every failure (NFR-TFA-08) |
| Security | Secret screen on every message (NFR-TFA-01); permission re-check at confirm (NFR-TFA-02) |
| Maintainability | Provider adapters behind one gateway; document answers come from the project documents themselves, so editing a document updates TFA without a code change; chunking is a pure, unit-tested rule; tools registered in one toolbox |
| Observability | Audit outcomes; provider failures logged with provider, kind and HTTP status only |
| Compatibility | Voice in Chrome and Edge; typing in every supported browser (NFR-TFA-07) |
| Backup & Recovery | Conversations, actions and audit in `taskflow.db`; key ring folder backed up with it; the document index needs no backup (rebuilt from the folder) |

---

## 13. Design Decisions

| ID | Decision | Alternatives considered | Rationale |
|---|---|---|---|
| D-TFA-1 | Tool-calling assistant hosted in the API; the model sees only tools the caller may use and every call is re-checked | Model with direct database access; free-form answers only | Answers grounded in TaskFlow data; the model can never exceed the user's rights (BR-TFA-01) |
| D-TFA-2 | Confirm-before-act: action tools store a pending action with the exact payload; only the user's confirm executes it; 10-minute expiry; single atomic claim | Let the model execute actions; client-side confirm only | Human control, no replay, no double execution (BR-TFA-02, BR-TFA-08) |
| D-TFA-3 | Permission checks evaluate the API's own `perm:{key}` policies with the caller's current token, at prepare and at confirm | Separate assistant permission logic | One source of truth; a permission removed after preparing makes the confirm fail |
| D-TFA-4 | Work order calculated by `TaskPrioritiser`; the model only explains it | Ask the model to rank | Same order for every provider (BR-TFA-06) |
| D-TFA-5 | *Superseded in v1.1 by D-TFA-11.* (Was: how-to answers only from 19 embedded, role-aware markdown topics.) | – | – |
| D-TFA-6 | Secret screen before provider and storage; redaction of tool output | Rely on user care | Passwords and keys never leave the browser request (BR-TFA-03) |
| D-TFA-7 | Pluggable providers (Ollama, Gemini, OpenAI) behind `ChatModelGateway` with classified failures and no automatic fallback | Single vendor; silent fallback to another provider | Free local option; the user decides where data goes (BR-TFA-11) |
| D-TFA-8 | Per-user in-memory rate limit of 20 messages per minute | Distributed limiter; no limit | Protects provider quotas on a single server (BR-TFA-12) |
| D-TFA-9 | Voice entirely in the browser (Web Speech API); recognised text uses the normal send path | Server-side speech-to-text | No audio stored or uploaded by TaskFlow; same permission checks (BR-TFA-14) |
| D-TFA-10 | Audit stores outcomes only (user, kind, outcome, time) | Store full prompts and answers | No message bodies, passwords or keys in the audit (BR-TFA-13) |
| D-TFA-11 | Answers about TaskFlow only from the TaskFlow documents, retrieved in code before the model call and added to the system prompt with a strict rule; every chunk given is a source | A `search_documents` tool; keyword help topics; the model's own knowledge | Works with models that cannot call tools; the documents state the rules exactly; no invented features (BR-TFA-07) |
| D-TFA-12 | Local embeddings with Ollama `nomic-embed-text` through `/api/embed`, whatever chat provider is selected | Gemini or OpenAI embeddings | Free, runs on CPU, documents never leave the machine for indexing |
| D-TFA-13 | Vectors in the existing SQLite database, searched by brute force in memory | Vector database; `sqlite-vec` extension | A few thousand vectors search in milliseconds; no new process, package or native extension |
| D-TFA-14 | Heading-aware Markdown chunks with incremental, hash-based indexing in a background service | Fixed-size chunks; re-index everything on each run | Chunks keep their section context; only changed files are re-embedded; edits apply without a restart |

---

## 14. Risks, Assumptions & Open Issues

| Type | Description | Mitigation / owner |
|---|---|---|
| Risk | Wrong or invented answer (R-TFA-01) | Answers only from retrieved document text with sources; ranking in code; footer warning |
| Risk | Small local model paraphrases loosely or misses a detail (R-TFA-08) | Small labelled chunks, 4000-character cap, strict rule; a larger model (or Gemini) answers better |
| Risk | Technical documents answered to every role (R-TFA-07) | Owner decision; per-role filtering is future work |
| Risk | `MinScore` too high or too low (missed answers or noise) | Configurable (`DocumentKnowledge:MinScore`, default 0.6, tuned against a fixed question set) |
| Risk | Prompt injection through task, comment or mail text (R-TFA-02) | Data marking, redaction, confirm and permission re-check |
| Risk | Company data sent to a cloud provider (R-TFA-03) | Cloud notice; local Ollama; secret screen |
| Risk | Conversations kept indefinitely (R-TFA-04) | ⚠️ To be confirmed: retention period or "delete my history" – Jojo George |
| Open issue | ⚠️ To be confirmed: role change checks only `roles.manage`, while Employee Management also needs `employees.role.assign` (and `roles.administrator.assign` for Administrator) | Jojo George |
| Open issue | ⚠️ To be confirmed: Confirm / Edit / Cancel are not hidden for users without `assistant.actions.confirm` | Jojo George |
| Open issue | ⚠️ To be confirmed: "my manager" matches by role name, so custom roles are treated as others (the role-restricted help topics were removed in v1.1) | Jojo George |
| Open issue | ⚠️ To be confirmed: password guidance text still mentions the current password (R-TFA-06) | Jojo George |
| Assumption | An administrator configures at least one provider; users understand AI answers can be wrong | Jojo George |
| Assumption | Ollama runs with `nomic-embed-text` pulled; the documents folder is kept current | Jojo George |

---

## 15. Traceability (requirements → components)

| BRD/FRD item | Module / component | Section |
|---|---|---|
| FR-TFA-01 – FR-TFA-02 | TFA Panel › `TfaPanelComponent` mounted by `ShellComponent` with `assistant.chat`; `AssistantController` policies | §5, §9 |
| FR-TFA-03 – FR-TFA-05 | TFA Panel › conversation load (last 100), suggestions, `TfaComposerComponent` | §5 |
| FR-TFA-06 | Own-scope repositories; 404 for foreign ids | §9 |
| FR-TFA-07 – FR-TFA-08 | Conversation engine › length check, `SecretScreen` | §8.1, D-TFA-6 |
| FR-TFA-09 | Task answers › `GetMyTasksTool` via `ITaskService` | §5.1 |
| FR-TFA-10 – FR-TFA-11 | Work order › `PrioritiseTasksTool`, `TaskPrioritiser`; ranked list in `TfaMessageComponent` | D-TFA-4 |
| FR-TFA-12 – FR-TFA-14 | Model selector (`TfaHeaderComponent`), providers action, `ChatModelGateway` | §7, D-TFA-7 |
| FR-TFA-15 | `TfaPanelComponent` cloud notice (`tfa.cloudAck`) | §9 |
| FR-TFA-16 | `ProviderFailure` classification, `ProviderFailureMessages`, failure card | §8.3 |
| FR-TFA-17 – FR-TFA-19 | Conversation engine › new chat, history window, tool-round limit | §6.3, §12 |
| FR-TFA-20 | Tool output marked as data and redacted | §9, D-TFA-1 |
| FR-TFA-21 | `AssistantService` (TS) resets on user change | §5 |
| FR-TFA-22 | `AssistantPrompt` with application name and timezone | §5 |
| FR-TFA-23 – FR-TFA-24 | Fixed refusal text (`AssistantTexts`); panel footer | §9 |
| FR-TFA-25 – FR-TFA-28 | Superseded in v1.1 (how-to help removed) | – |
| FR-TFA-29 – FR-TFA-33 | Action Cards › action tools, `AssistantActionService` (prepare, re-check, expiry, single claim, cancel), `TfaActionCardComponent` | §8.2, D-TFA-2, D-TFA-3 |
| FR-TFA-34 – FR-TFA-38 | Mail draft › `DraftMailTool`, edit action, `SendMailRequestValidator`, `IMailService.SendAsync` | §5.1 |
| FR-TFA-39 | Profile card › `ProposeProfileUpdateTool`, `IProfileService.UpdateAsync` | §5.1 |
| FR-TFA-40 – FR-TFA-41 | Role card › `ProposeRoleChangeTool` (`roles.manage`), `IEmployeeService.UpdateAsync`, sessions ended | §5.1, §14 |
| FR-TFA-42 – FR-TFA-44 | Lookup and guidance › `FindUserTool`, `ExplainPasswordChangeTool`, `ContactAdminGuidanceTool` | §5 |
| FR-TFA-45 – FR-TFA-46 | `AssistantActionService` failure status; card error messages | §7 |
| FR-TFA-47 – FR-TFA-52 | Voice controls › `WakePhraseService`, `SpeakService`, `TfaComposerComponent` mic | §8.4, D-TFA-9 |
| FR-TFA-53 | `AssistantRateLimiter` | §9, D-TFA-8 |
| FR-TFA-54 – FR-TFA-56 | `AssistantAuditLog` and audit repository | §9, D-TFA-10 |
| FR-TFA-57 – FR-TFA-66 | Document knowledge › `DocumentIndexerService`, `DocumentIndexer`, `MarkdownChunker`, `OllamaEmbeddingClient`, `DocumentSearch`, `DocumentVectorIndex`, `AssistantPrompt`, sources in `TfaMessageComponent` | §5, §6, §7, §8.1, §8.5, D-TFA-11 – D-TFA-14 |
| NFR-TFA-01 – NFR-TFA-12 | Non-functional design | §12 |
| BR-TFA-01 – BR-TFA-16 | Covered through the FR rows above (see FRD §11) | §15 |

---
<sub>© 2026 Jojo George. All rights reserved. Unauthorized copying or distribution of this document is prohibited.</sub>
