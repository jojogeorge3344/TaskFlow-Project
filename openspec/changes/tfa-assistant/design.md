# Design

## Context

See `proposal.md` for the motivation and the three specs (`ai-provider-management`,
`tfa-chat-assistant`, `tfa-guarded-actions`) for required behavior. The current state that shapes the
approach:

- **Authorization is split between the controller and the service.**
  - Controllers carry the policies: `AdminOnly`, `ManagerOrAdmin` and `TaskAccess` (defined in
    `Program.cs`, names in `AuthPolicies`).
  - Task services take `(currentUserId, currentUserRole)` and re-check scope.
  - **`IUserService.UpdateAsync` has no role check.** It trusts the controller's `AdminOnly`. It also
    takes a full `UpdateUserRequest`, and a role change ends that user's sessions.
  - Consequence: if TFA called the services directly, it would skip the policies. TFA therefore needs
    its own authorization step that uses the same policies (D3).
- **Mail is internal.** `IMailService.CreateDraftAsync` and `SendAsync` take the caller id. Recipients
  must be active TaskFlow users, addresses are resolved by email, and sent mail lands in Sent and in
  recipients' Inboxes.
- **Own profile and password already exist:** `UpdateOwnProfileAsync` (name and email only) and
  `ChangeOwnPasswordAsync`, exposed as `PUT /users/me` and `POST /users/me/password`.
- **No manager link.** `User` has `Role` and `DepartmentId`. `Department` has no manager field. `Task`
  has `CreatedByUserId`, `AssignedToUserId`, `Priority`, `Status` and `Deadline`.
- **`ApplicationSetting` holds non-secret key/values** and its own comment says secrets never live
  there. `GET /application-settings` is open to any signed-in user; `PUT` is `AdminOnly`.
- **Stack:** .NET 10, EF Core with SQLite, FluentValidation, thin controllers, `{ items, total, page,
  pageSize }` lists. Angular 19 standalone components, root services in `core/services`, the shell in
  `core/layout`, `ToastService`, and a `.tf-*` primitive set.
- **Not present:** any HTTP client to an external service, ASP.NET Data Protection setup, or any chat
  or conversation storage.

## Goals / Non-Goals

**Goals:**
- One internal seam, `IChatModelProvider`, so adding or swapping a model needs no change to the
  assistant logic.
- **TFA is never more privileged than the user**, and enforcement is code the model cannot influence.
- Provider keys are unreadable after they are saved.
- A provider failure is always an understandable message and never a silent switch of provider.
- Prioritisation is deterministic, so the same data gives the same order on any model.

**Non-Goals:**
- Streaming token-by-token output. A reply arrives whole, with a waiting indicator.
- Spoken replies, per-user keys, fine-tuning, embeddings or a vector database.
- Letting the model choose arbitrary endpoints, run code or browse the web.
- Server-side speech recognition. Voice is a browser feature (D8).

## Decisions

**D1 - Provider abstraction.** Application defines:

```
IChatModelProvider { Kind; Task<ChatModelResult> CompleteAsync(ChatModelRequest, ct) }
ChatModelRequest   { SystemPrompt, Messages[], Tools[], ModelName }
ChatModelResult    { Text?, ToolCalls[] }
```

`Messages` are provider-neutral (`role` = user, assistant or tool, plus text or a tool result).
Infrastructure has three implementations, each over a named `HttpClient`:

| Provider | Endpoint | Auth | Tools |
|---|---|---|---|
| Ollama | `{baseUrl}/api/chat` | none | native `tools` field when the model supports it |
| Gemini | `generativelanguage.googleapis.com/.../models/{m}:generateContent` | key in `x-goog-api-key` header | `functionDeclarations` |
| OpenAI | `api.openai.com/v1/chat/completions` | `Authorization: Bearer` | `tools` / `tool_calls` |

Each implementation translates the neutral messages to its wire format and back, and maps its own
HTTP and network failures to `ProviderFailure` (D5). `IChatModelProviderRegistry` resolves a kind to
its implementation and reports which are configured.

Alternatives considered:
- A single OpenAI-compatible client for all three (Ollama and Gemini both expose such endpoints). It
  is less code, but it hides provider-specific error shapes and tool-call differences that D5 needs,
  and the compatibility layers lag the native APIs. Rejected.
- A third-party SDK or Semantic Kernel. It adds a large dependency to a project that otherwise has
  none for this, and the three wire formats are small. Rejected.

**D2 - Tool-calling loop on the server.** `AssistantService.SendAsync(callerId, request)`:
1. Validates the message (length, rate limit, secrets screen, D6), loads the caller's conversation
   and the provider.
2. Calls the provider with the system prompt, the trimmed history and the tool definitions.
3. If the model returns tool calls, runs each through the tool gate (D3), appends the results, and
   calls the model again. The loop is capped at 4 rounds.
4. Stores the assistant reply and returns it, plus any pending action card and links.

Tools are **read tools** (run immediately, caller-scoped) and **propose tools** (create a pending
action, never execute):

| Tool | Kind | Backed by |
|---|---|---|
| `get_my_tasks` (filter: upcoming, due today, overdue) | read | `ITaskService.ListAsync` |
| `prioritise_tasks` | read | `TaskPrioritiser` over `ITaskService.ListAsync` (D4) |
| `search_help` (topic) | read | help knowledge base (D7) |
| `find_user` (name) | read | `IMailService.GetContactsAsync` |
| `draft_mail` (to, subject, body, taskId?) | propose | `IMailService.CreateDraftAsync` shape |
| `propose_profile_update` (fullName?, email?) | propose | `IUserService.UpdateOwnProfileAsync` |
| `propose_role_change` (userId, role) | propose | `IUserService.UpdateAsync` |
| `explain_password_change` | read | fixed text and a link; takes no argument |
| `contact_admin_guidance` | read | fixed text |

The model cannot name a user id or role for any read tool: caller id and role always come from
`ClaimsPrincipal`, never from tool arguments.

**D3 - Authorization gate (the RBAC rule).** Every tool declares the policy it needs, using the
**same policy names as the REST API** (`AdminOnly`, `ManagerOrAdmin`, `TaskAccess`, or none for
"signed-in"). `AssistantToolGate` calls `IAuthorizationService.AuthorizeAsync(user, resource, policy)`
with the caller's real `ClaimsPrincipal`, so a policy change in `Program.cs` changes TFA at the same
time. A denied tool call returns `ToolResult { Allowed = false, Reason, Who }` and the assistant
emits the **fixed refusal template** (the model is told the result but its text is not used for the
refusal). Specifically:

- `propose_role_change` requires `AdminOnly`. For anyone else it returns
  `Allowed = false, Who = "an authorized administrator"`.
- Tools never call `IUserService.UpdateAsync` directly from a model decision. Only
  `ConfirmActionAsync` does, after the gate runs **again** (D6).
- Read tools pass the caller's id and role to `ITaskService`, which applies `TaskAccessScope`.
- Tools that are not allowed for the role are also **not advertised** to the model, which reduces
  refused attempts and keeps the prompt smaller. The gate stays authoritative either way.

Alternatives considered: trusting a system-prompt instruction such as "only admins may change roles".
Rejected: it is advice to a model, not a control, and a crafted task description could override it.

**D4 - Prioritisation is computed, not generated.** `TaskPrioritiser` scores each open task and
returns a sorted list; the model only phrases it.

- Exclude `Completed` and `Cancelled`.
- Rank by: overdue (more days overdue first), then due today, then nearest deadline, then priority
  (`Critical > High > Medium > Low`), then task code.
- `Blocked` tasks are flagged and kept in the list.
- Priority breaks ties inside the same due day. Overdue always outranks a higher priority.

Alternative considered: letting the model rank. Rejected: results differ between providers and
between runs, which would break "the same order on any model".

**D5 - Error classification.** `ProviderFailure { Kind, ProviderKind, RetryAfter? }` where `Kind` is
one of `RateLimited`, `InvalidKey`, `Timeout`, `Unreachable`, `ModelUnavailable`, `Unexpected`.

| Signal | Kind |
|---|---|
| HTTP 429, or Gemini `RESOURCE_EXHAUSTED`, or OpenAI `insufficient_quota` / `rate_limit_exceeded` | `RateLimited` (uses `Retry-After` when present) |
| HTTP 401/403, or a 400 that names the key as invalid | `InvalidKey` |
| `TaskCanceledException` after the 60 s timeout | `Timeout` |
| `HttpRequestException` (connection refused, DNS) | `Unreachable` (Ollama: "not running") |
| Ollama 404 `model not found`, Gemini or OpenAI model-not-found | `ModelUnavailable` |
| anything else | `Unexpected` |

`AssistantService` turns the failure into a fixed, localisable message for that kind. Raw provider
bodies are logged at Warning with the key and request text removed, and are never sent to the client.
The user message is **not** retried against another provider. The response lists the other
configured providers so the UI can offer them as an explicit choice.

A per-user limit of 20 chat requests a minute is enforced in memory (a `PartitionedRateLimiter` keyed
by user id), and history sent to a provider is trimmed to the last 12 messages and about 6,000
characters. These keep free-tier quotas usable and are settings constants, not user-facing options.

**D6 - Pending actions and confirmation.** Table `AssistantPendingAction`:

| Column | Notes |
|---|---|
| `Id` | `Guid` |
| `UserId` | Owner. Every read and confirm filters on it; other users get 404 |
| `ConversationId` | |
| `Kind` | `SendMail`, `UpdateOwnProfile`, `ChangeUserRole` |
| `PayloadJson` | The exact details shown to the user (recipients, subject, body, or field values) |
| `Status` | `Pending`, `Confirmed`, `Cancelled`, `Expired`, `Refused`, `Failed` |
| `CreatedAt`, `ExpiresAt` | 10-minute lifetime |
| `ResolvedAt?` | |

- `POST /assistant/actions/{id}/confirm` is the only path that executes. It runs in a transaction: it
  loads the row for the caller, checks `Status = Pending` and not expired, **re-runs the gate** with
  the caller's current role, then runs the real service call, then sets `Confirmed`. The `Pending` to
  `Confirmed` change uses a conditional update, so two simultaneous confirms perform it once.
- `PUT /assistant/actions/{id}` lets the user edit a mail draft's recipients, subject and body, and
  re-validates them. Edits replace the stored payload, so the executed action is always the one the
  user saw.
- `POST /assistant/actions/{id}/cancel` sets `Cancelled`.
- `ChangeUserRole` reads the current user with `IUserService.GetByIdAsync` and builds the full
  `UpdateUserRequest` with only the role changed, because `UpdateAsync` replaces every field.
- Mail: `SendMail` calls `IMailService.SendAsync` as the caller. Recipients are resolved on the
  server by `find_user`, not typed by the model, so an unknown name fails before a card is shown.
- The model has no access to the confirm endpoint, and nothing the model emits can set `Status`.
- `AssistantAudit` rows (`UserId`, `Kind`, `Outcome`, `At`) are written for prepare, confirm, cancel,
  expire and refuse. They hold no payload.

**D7 - Application help.** A markdown knowledge base ships with the API under
`Assistant/Knowledge/*.md`, one file per topic (change task status, compose mail, profile, change
password, notifications, reminders, and the admin areas), each tagged with the roles it applies to.
`search_help` returns the best topic files for the caller's role by keyword match on title and tags.
This is deliberately small: the screens are few, and no embeddings or vector store are needed.
Content is derived from `Requirements/screens wise feature/*.md` and the implemented screens. A topic
a role cannot use is not returned for that role, which gives the "feature the user cannot use" answer.

Alternative considered: embeddings and retrieval. Rejected as disproportionate for about 15 short
topics, and it needs an embedding model on every provider.

**D8 - Frontend and voice.**
- `core/services/assistant.service.ts` holds the conversation signals, the provider list, the
  selected provider (persisted in `localStorage`, with a safe fallback), and pending-action calls.
- `core/layout/tfa-panel.component` is mounted in the shell next to the router outlet. It has the
  header (title, model selector with Local/Cloud badge, New chat, close), the message list, an input
  with a microphone button, and a pending-action card for mail, profile and role actions with
  Confirm, Edit and Cancel. The panel is a fixed 380 px right dock, full width below 768 px.
- `core/speech/wake-phrase.service.ts` wraps `SpeechRecognition` / `webkitSpeechRecognition`:
  - Off by default. A toggle in the panel turns it on and shows a persistent "listening" indicator.
  - While on, it runs continuous recognition and matches the phrase "hey tfa" (also "hey t f a" and
    "hey tea eff ay") in each final transcript, then opens the panel and switches to capturing the
    question, which goes into the input box and is sent only when the user submits.
  - If the API is missing or permission is denied, it reports a reason string that the panel shows.
- The Cloud acknowledgement (first message to a Cloud provider) is remembered per provider in
  `localStorage`.
- Admin UI: an "AI assistant" section in `application-settings.component` with a card per provider:
  enabled, model, base URL (Ollama), key (write-only password field, shows "key set, ends …1234"),
  and a **Test connection** button that calls `POST /assistant/providers/{kind}/test`.

Alternative considered: a server-side wake-word engine. Rejected: it would require streaming
microphone audio to the server and an extra component, and the browser already offers recognition.
Known limit: in Chrome the recognition audio is processed by Google's speech service, which is stated
in the help text, and voice stays off until the user enables it.

**D9 - Provider configuration and key storage.** Table `AiProviderConfig` (one row per provider
kind): `Kind`, `IsEnabled`, `ModelName`, `BaseUrl?`, `EncryptedApiKey?`, `KeyLast4?`, `UpdatedByUserId`,
`UpdatedAt`. It is separate from `ApplicationSetting` because that table is, by its own contract,
non-secret and is readable by every signed-in user.

- Keys are protected with `IDataProtector` (purpose `TaskFlow.AiProviderKeys`). The Data Protection
  key ring is persisted to a folder outside `wwwroot` (`AiProviders:KeyRingPath`, default
  `../database/dp-keys`, resolved like the database path) so keys survive restarts.
- Endpoints:
  - `GET /assistant/providers` (signed-in): `[{ kind, displayName, model, locality }]`, configured only.
  - `GET /assistant/providers/config` (`AdminOnly`): full status with `keySet` and `keyLast4`, never the key.
  - `PUT /assistant/providers/config` (`AdminOnly`): saves; an empty `apiKey` keeps the stored key, and
    a separate `clearKey` flag removes it.
  - `POST /assistant/providers/{kind}/test` (`AdminOnly`): one tiny completion, returning the
    classified result.
- The key is decrypted only in memory inside the provider call. Provider `HttpClient` logging is
  configured so request headers are not logged. Serilog destructuring skips key fields.
- Base URL validation: Ollama's URL must be http or https. Cloud provider base URLs are fixed in
  code, not editable, so an administrator cannot point a key at another host.

Alternatives considered: environment variables or `appsettings` for keys. Rejected because the
requirement is administrator entry in the application and rotation without a redeploy. The trade-off
is that the keys live in the database, protected by the key ring (see Risks).

**D10 - Data model summary and migration.** Migration `AddAssistant` adds four tables:
`AssistantConversation` (`Id`, `UserId`, `CreatedAt`, `UpdatedAt`), `AssistantMessage`
(`Id`, `ConversationId`, `Role`, `Content`, `ToolCallJson?`, `ProviderKind?`, `CreatedAt`),
`AssistantPendingAction` and `AssistantAudit` (D6), plus `AiProviderConfig` (D9). It is additive
and forward-only. One active conversation per user, and "New chat" starts a new one. Messages that
were rejected as containing a secret are never stored (D11).

**D11 - Secrets screen.** Before the provider call, `SecretScreen` looks for password-like and
key-like content (for example `password is ...`, `my password`, `passwd:`, strings that match
`sk-...`, `AIza...` or long high-entropy tokens). A match short-circuits the request: the text is not
stored and not sent, and the fixed password guidance is returned. It is a safety net, not a
guarantee, which is why the tools also never accept a password argument. Tool results are also
passed through it so that no key or hash can be echoed back.

**D12 - API surface** (`AssistantController`, `[Authorize]`, `api/v1/assistant`):

| Method and path | Purpose |
|---|---|
| `GET /providers` | Configured providers for the selector |
| `GET /conversation` | The caller's current conversation (messages, pending actions) |
| `POST /conversation/messages` | `{ text, provider }` returns the reply, action card, links, or a classified provider error |
| `POST /conversation/new` | Starts a new conversation |
| `PUT /actions/{id}` | Edit a pending mail |
| `POST /actions/{id}/confirm` | Execute a pending action |
| `POST /actions/{id}/cancel` | Cancel a pending action |
| `GET/PUT /providers/config`, `POST /providers/{kind}/test` | Administrator configuration |

Provider failures are returned as a normal response body with a `failure` object and HTTP 200, so the
panel can show them inline, while validation and authorization problems use the standard
`ValidationProblemDetails` and 401/403/404.

## Risks / Trade-offs

- **[Risk] A prompt-injected model proposes a harmful action.** → The model can only propose; the
  user sees the exact payload; the confirm endpoint re-authorizes; mail recipients are resolved
  server-side; role changes need an administrator.
- **[Risk] A user is misled by the model's wording.** → The confirmation card is rendered from the
  stored payload, not from model text, and refusals are fixed templates.
- **[Risk] Task data goes to a cloud provider.** → Per-provider opt-in by an administrator, Local/Cloud
  labels, a one-time acknowledgement, a capped history and capped task data per request, and no silent
  fallback. Ollama keeps everything on the machine.
- **[Risk] Keys in the database are exposed by a copied database file.** → Keys are encrypted with a
  key ring stored separately. The README gets a note to keep `dp-keys` out of backups that also hold
  the database, or to protect it at the file-system level. If the key ring is lost, administrators
  re-enter the keys.
- **[Risk] Free tiers throttle or change.** → Classified `RateLimited` handling, a per-user limit, a
  trimmed context, and the ability to switch provider. Free-tier quotas are not hard-coded.
- **[Risk] Small local models call tools unreliably.** → Few tools, short schemas, a 4-round cap, and
  a plain-text answer path. Prioritisation and refusals do not depend on model quality. The admin
  **Test connection** shows whether the chosen model responds.
- **[Risk] OpenAI is not free.** → It is off until an administrator supplies a paid key, and the
  settings screen says so.
- **[Trade-off] Browser speech recognition is Chrome/Edge only and sends audio to the vendor's
  service.** Typing is the primary path, and voice is opt-in.
- **[Trade-off] The "manager" rule is a heuristic.** It uses the task creator, then a single active
  Manager in the sender's department, and otherwise asks. It is documented in help and in specs.
- **[Trade-off] One request, whole reply (no streaming).** Slow local models show a waiting state for
  up to the 60 s timeout.
- **[Risk] `IUserService.UpdateAsync` has no internal authorization.** → TFA's gate calls
  `AdminOnly` before it, as the controller does. A follow-up (not in this change) could move the check
  into the service.

## Migration Plan

1. Add the code and the `AddAssistant` migration. `MigrateAsync` applies it on startup, creating
   empty tables. Nothing is enabled, so TFA reports "not available yet" until an administrator
   configures a provider.
2. Create the Data Protection key-ring folder on first use.
3. An administrator opens Application Settings, enables a provider, enters a key if needed, and runs
   **Test connection**.
4. Optional: install Ollama and pull the configured model for the free local path.
5. Rollback: disable all providers (TFA goes quiet), or revert the migration to drop the assistant
   tables. No existing table is altered.

## Open Questions

- Default model names for each provider (suggest `llama3.1:8b` for Ollama and the current
  free-tier-eligible Gemini and OpenAI models at implementation time). They are settings, not code.
- How long conversations and audit rows are kept. This can be a retention follow-up without changing
  anything here.
