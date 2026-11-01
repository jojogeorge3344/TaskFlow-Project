# Design

## Context

- `AiProviderKind` (Ollama, Gemini, OpenAI) is the identity of a provider everywhere. It is used by 34 backend files and 6 frontend files, and is stored as a string in `AiProviderConfigs.Kind` (one row each), in `AiModels.Kind` (unique index on `Kind`, `Name`) and in `AssistantMessages.ProviderKind`.
- `AiProviderCatalog` holds fixed facts per kind: display name, locality, whether a key is needed, the default model, and the built-in models. `ChatModelProviderRegistry.Get(kind)` returns one client per kind. `OpenAiProvider.BaseAddress` is the constant `https://api.openai.com/`.
- Document search embeds through Ollama (`OllamaEmbeddingClient`, `DocumentEmbeddingEndpoint`), using the built-in Ollama row's address.
- The database is SQLite through EF Core migrations. `tfa-model-list-and-edit` added use, edit and test by `{kind}`.

## Goals / Non-Goals

**Goals:**
- One provider model covers built-in and custom providers, with no special cases in the chat path.
- Existing installations upgrade with no manual step and lose no key, model or chat history.

**Non-Goals:** see the proposal's out-of-scope list. Also out of scope: listing a provider's models live from its API.

## Decisions

1. **`AiProviderConfig` becomes the provider record.** There is no new table: the one-row-per-provider table already holds what a provider needs.
   - Added columns: `Name` (≤ 40 characters, unique and compared case-insensitively through `COLLATE NOCASE`), `ApiType` (the renamed `Kind`, still a string), `Locality` (`Local` or `Cloud`) and `IsBuiltIn`.
   - `BaseUrl` is required for custom providers. Built-in Gemini and OpenAI keep a null `BaseUrl` and their fixed addresses in code.

   The enum `AiProviderKind` is renamed `AiApiType`, with the same values; `OpenAI` now means "OpenAI-compatible". *Alternative:* a separate `AiProviders` table next to the config table. That adds a 1:1 join for no gain.

2. **Identity is `ProviderId` (int).**
   - `AiModel.Kind` becomes `ProviderId` (FK, cascade delete), with a unique index on (`ProviderId`, `Name`).
   - `AssistantMessage.ProviderKind` becomes `ProviderId` (nullable FK, set null on delete). A deleted custom provider then leaves its old messages without a provider name, and the TFA window shows "a provider that was removed".
   - Routes become `/providers/{providerId:int}/...`. The DTOs carry `ProviderId` plus `Name`, `ApiType`, `Locality` and `IsBuiltIn`. `SendAssistantMessageRequest.ProviderId` replaces `Provider`, and `AssistantFailureDto.ProviderId` and `ProviderName` replace `Provider`.

3. **Migration `AddCustomAiProviders` (data-preserving):**
   1. Add the new columns.
   2. Fill the three existing rows: Name from the catalog display name, `IsBuiltIn = 1`, Locality from the catalog.
   3. Insert any missing built-in rows, so `AiModels` can always map to one.
   4. Add `AiModels.ProviderId`, fill it by joining on `Kind`, then drop `Kind` and rebuild the index (SQLite table rebuild through EF).
   5. Add `AssistantMessages.ProviderId` the same way.

   The seeder then keys built-in models on the built-in provider row for each API type. Down migration: reverse the column moves, with custom providers and their models dropped. This is documented as lossy for custom providers only.

4. **Catalog splits into API-type facts and provider facts.**
   - `AiProviderCatalog` keeps per-API-type facts: the model ID rule (Gemini strict, the other types no spaces), default models and built-in models for built-ins, and the fixed address for built-in Gemini and OpenAI.
   - Display name, locality and whether a key is needed come from the provider row: Gemini and built-in OpenAI need a key; OpenAI-compatible custom providers and Ollama don't.
   - `AiProviderRuntime` carries `ProviderId`, `ApiType`, `Name`, `ModelName`, `BaseUrl` and `ApiKey?`.

5. **Clients.**
   - `ChatModelProviderRegistry.Get(apiType)`, with the same three clients.
   - `OpenAiProvider` uses `runtime.BaseUrl ?? "https://api.openai.com/v1"` and joins `/chat/completions`, so a custom address includes the version path as users copy it (`https://api.groq.com/openai/v1`).
   - The Authorization header is sent only when a key is present.
   - Failure classification is unchanged. `ProviderFailureException` and `ProviderFailureMessages` name the provider by `runtime.Name` instead of the catalog display name.

6. **Add and Edit API.**
   - `POST /providers/models` with body `{ providerId?, newProvider?: { name, apiType, baseUrl, locality }, modelName, apiKey?, baseUrl? }`. Exactly one of `providerId` and `newProvider` is required. The route no longer carries the provider, because a new provider has no id yet.
   - `AiModelAdditionService.AddAndActivateAsync` validates the new provider (name 2–40 characters and not taken case-insensitively, API type OpenAI or Ollama, safe address, `IsMetadataHost` reused). It then tests a candidate runtime and, on a pass, creates the provider and model and activates the model in one save.
   - `PUT /providers/{providerId}/models?name=` with the same body. `providerId` / `newProvider` is the **target**, and when the target differs from the current provider the edit becomes a move:
     1. Check the model is not built in and not in use.
     2. Refuse when the target already has the name.
     3. Test the candidate on the target.
     4. On a pass, in one save: create the target if new, delete the old `AiModel`, add the new one, update a switched-off source provider's saved name to its default, and delete the source when it is a custom provider with no models left.
   - `clearKey` keeps its rules and is refused together with a move.

7. **Custom provider removal.** `RemoveModelAsync` and a move delete an emptied custom provider (FK cascade removes nothing else, because it has no models). A provider that is enabled always has its saved model listed, so an enabled provider cannot become empty: its model is the one in use, and that model can't be deleted.

8. **Frontend.**
   - The AI provider field becomes `<input list="ai-provider-options">` with a `<datalist>` of the provider names.
   - A computed `matchedProvider` (case-insensitive name match) decides which fields show. When there is no match, it shows API type (`<select>` with OpenAI-compatible and Ollama), Server address, Runs and API key (optional).
   - Edit mode pre-fills the provider name and leaves it editable. A changed provider shows "The model moves to `{target}` when the test works."
   - TFA Settings groups by provider id: built-ins first in catalog order, then custom providers by name.
   - The TFA window's picker and `assistant.service` send `providerId`.
   - The cloud notice reads `locality` from the provider, not the type.

9. **Embeddings stay on the built-in Ollama provider.** `DocumentEmbeddingEndpoint` looks up the built-in Ollama row (`IsBuiltIn && ApiType == Ollama`). A custom Ollama provider is chat-only.

## Risks / Trade-offs

- [The migration touches three tables on users' SQLite files] → Tested on a copy of a database from before the change, holding keys, custom models and messages (integration test `CustomProvidersMigrationTests`). The application backs up nothing itself, so the deploy notes say to copy the `.db` file first.
- [Large rename across the codebase (enum to provider id)] → Done in one task group with the compiler as the guide. Behaviour stays covered by the existing test suites, which must all pass before the UI work starts.
- [The server calls administrator-typed addresses (SSRF)] → The existing metadata and link-local block applies to every address. Only `settings.ai.edit` + `settings.ai.test` holders can create a provider. The address is never fetched except for the one test request and chat.
- [OpenAI-compatible services differ in tool-call support] → The existing behaviour for models without tool support (retrying in plain text, as Ollama does) is applied to the OpenAI-compatible client when the provider answers 400 about tools. Failures are still classified as before.
- [A name clash with a built-in ("gemini")] → The match is case-insensitive, so "gemini" is not a match for "Google Gemini". It would create a custom provider named "gemini" with the OpenAI-compatible type. The screen shows "New provider" next to the field, so it is clear a new one will be created.

## Migration Plan

1. Stop the API and copy the SQLite file.
2. Deploy the backend and the frontend together. The route change breaks an old frontend.
3. The migration runs on start, and the seeder fills any missing built-in models.

Rollback: restore the copied `.db` file and the previous build.

## Open Questions

- Whether to offer ready-made address suggestions (Groq, Mistral, OpenRouter) next to the address field. This can be added later without changing these specs.
