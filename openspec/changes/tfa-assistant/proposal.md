# Proposal

## Why

Users keep asking "how do I change a task's status?", "what should I work on first?" and "can you mail my manager about this task?", and today the only answer is to hunt through screens. TaskFlow already has the data (tasks, deadlines, priorities) and an internal mail system, so an in-app assistant can answer these questions and draft the mail. It must run on free or already-available models (local Ollama, Gemini's free tier), with OpenAI available only if an administrator supplies a paid key, and it must never become a way around the role-based access the application already enforces.

## What Changes

- Add **TFA**, a chat assistant in a small right-side panel available on every authenticated screen. It opens when the user says "Hey TFA", clicks a floating TFA button, or types in the panel.
- Voice input uses the browser's speech recognition (Chrome and Edge). Typing is always available. Replies are written only; TFA does not speak.
- Add a **provider abstraction** with three providers: Ollama (local), Google Gemini and OpenAI GPT. A model selector in the chat header switches provider per message without closing the chat. The selector lists only providers that are configured, each marked Local or Cloud.
- Add **administrator-managed provider settings** (enabled, model name, Ollama URL, API keys) in Application Settings. API keys are org-level, stored encrypted on the server, and never returned to the browser or written to logs.
- Add **server-side conversation context** in a provider-neutral format, so history survives a provider switch.
- TFA answers four kinds of request:
  - how-to questions about the application, from a written knowledge base
  - upcoming-task and overdue-task questions, scoped to what the user may see
  - prioritisation, where the backend ranks tasks and the model only explains the ranking
  - email drafting, shown as a preview and sent only after the user confirms
- Add **guarded actions**: every state-changing request creates a pending action, and only a separate confirm request executes it. Every action is authorized by the backend as the signed-in user, using the same policies the REST API uses. A refusal is a fixed, non-model message that names the restriction and who can help.
- Password changes and other secrets never go through chat. TFA explains the steps and links to the Profile screen.
- Provider failures (rate limit or quota, bad key, timeout, Ollama not running, model not installed) show a clear in-chat message. There is **no automatic fallback** to another provider, so task data is never sent to a cloud provider the user did not pick.
- Resolve "my manager" as the task's creator, falling back to the department's manager, because `User` has no manager field.

## Capabilities

### New Capabilities
- `ai-provider-management`: provider registry, administrator configuration, encrypted org-level API keys, provider availability, error classification and free-tier limit handling.
- `tfa-chat-assistant`: the TFA panel, wake phrase and voice input, model selector, conversation context, application help, task queries and prioritisation.
- `tfa-guarded-actions`: pending actions with explicit confirmation, backend RBAC for every action, email drafting and sending, password-change and role-change handling.

### Modified Capabilities

None. `openspec/specs/` is empty (earlier changes are still in `openspec/changes/`), so this change introduces its capabilities as new deltas. Its integrations with Application Settings, Profile and Internal Mail reuse their existing behavior unchanged.

## Impact

- **Backend:** new `Assistant` area in Application, Infrastructure and Api (`AssistantController`, provider implementations, conversation and pending-action tables, a migration). `SettingKeys` and `ApplicationSettingsController` gain provider settings. `Program.cs` registers Data Protection, a named `HttpClient` per provider and the assistant services.
- **Data:** new tables for conversations, messages and pending actions; encrypted secret values for provider keys. Existing tables are unchanged.
- **Frontend:** a TFA panel and floating button in the shell, an assistant service, speech-recognition wrapper, and an "AI assistant" section in Application Settings for administrators.
- **Dependencies:** no new paid services. Ollama is an optional local install. Gemini and OpenAI are reached over HTTPS only when an administrator enables them.
- **Security and privacy:** with a cloud provider, the user's question and the task data TFA needs for the answer leave the organisation. The UI marks Cloud providers, and the administrator opts each one in.
- **Out of scope:** spoken replies, per-user API keys, streaming-token UI polish beyond what the design states, role changes initiated by TFA for non-administrators (always refused), and AI-initiated actions without confirmation.
