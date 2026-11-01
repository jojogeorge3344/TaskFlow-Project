# Tasks

## 0. Prerequisites

- [x] 0.1 (Satisfied at the user's request by checkpoint commit ba37f4c9 instead of archiving; the earlier changes' manual checks are still open.) Check that `tfa-add-ai-model-page`, `tfa-gemini-model-id-validation` and `tfa-model-list-and-edit` have all their tasks done and are archived (`openspec list` shows none of them as active), and that the backend and frontend test suites pass on that base.

## 1. Data model and migration

- [x] 1.1 Rename `AiProviderKind` to `AiApiType`, keeping the values. Add `Name`, `ApiType`, `Locality` and `IsBuiltIn` to `AiProviderConfig`, `ProviderId` to `AiModel` and `AssistantMessage`, and the EF configuration from design decision 2 (unique `Name` with NOCASE, unique (`ProviderId`, `Name`), FK cascade on models and set-null on messages). Verify that `dotnet build` passes.
- [x] 1.2 Add the migration `AddCustomAiProviders`, with the data-preserving steps in design decision 3. Verify with a new migration test `CustomProvidersMigrationTests` (in `TaskFlow.UnitTests/Persistence`, next to the other migration tests, on a real SQLite file): a database at the previous migration, holding three provider rows with an encrypted key, added models and assistant messages, upgrades with keys, saved models, enabled flags, models and message providers intact; and down then up again works for a database with no custom providers.
- [x] 1.3 Update `AiProviderSeeder` to key built-in models on the built-in provider rows, and to create missing built-in rows on a fresh install. Verify that `AiProviderSeederTests` and `AiDefaultsOnFirstRunTests` pass after updating.

## 2. Backend: provider id through the chat path

- [x] 2.1 Split `AiProviderCatalog` into per-API-type and built-in facts (design decision 4). Give `AiProviderRuntime` its provider fields. Move the repositories and `AiProviderConfigService` from kind to provider id: get by id, built-in lookup, name lookup. Verify that the existing service tests pass after the mechanical update.
- [x] 2.2 Key `ChatModelGateway`, `IChatModelProviderRegistry` (by API type), `AssistantService`, `ProviderFailureException`/`ProviderFailureMessages` (by provider name), `AssistantFailureDto` and `SendAssistantMessageRequest` on provider id. Verify that `AssistantServiceTests` and `ProviderFailureHandlingTests` pass, and add a case: a failure names a custom provider by its name.
- [x] 2.3 Give `OpenAiProvider` the configurable address and optional key (design decision 5), and the plain-text retry when the provider answers 400 about tools. Verify with `ChatProviderTests` cases: a custom address is joined with `/chat/completions`; there is no Authorization header without a key; built-in OpenAI still calls `https://api.openai.com/v1/chat/completions`; the retry without tools happens.
- [x] 2.4 Keep document embeddings on the built-in Ollama row (design decision 9). Verify that the document-index tests pass.

## 3. Backend: custom providers, add, edit and move

- [x] 3.1 Add the shared address rule (http/https, not metadata or link-local) to every provider address, and the new-provider validator (name 2–40, not taken, API type OpenAI or Ollama, address required). Verify with validator unit tests, including the Gemini-type refusal and a metadata address.
- [x] 3.2 Replace `POST {kind}/models` with `POST /providers/models`, and implement the create-on-pass flow in `AiModelAdditionService` (design decision 6). Verify with `AiModelAdditionServiceTests`: Groq is created and in use on a pass; nothing is created on a fail; LM Studio has no key and no header; a new provider with an existing name (any case) is refused.
- [x] 3.3 Extend `PUT /providers/{providerId}/models` with the move flow, and the removal of an emptied custom provider in move and delete (design decisions 6–7). Verify with tests:
  - moving to an existing provider;
  - moving to a new provider;
  - a duplicate name on the target is refused;
  - a failed test leaves the model in place;
  - the source custom provider is removed when emptied;
  - a built-in provider is never removed;
  - the model in use is refused;
  - `clearKey` together with a move is refused.
- [x] 3.4 Move every provider route to `{providerId:int}` (config, use, test, delete, edit), with 404 for an unknown id. Update `AssistantProviderEndpointsTests`, `AddAiModelEndpointsTests` and `AiModelListEndpointsTests`, and add an integration test that creates a custom provider, uses it, chats through it with the fake provider, and checks the stored message's provider id.

## 4. Frontend

- [x] 4.1 Update the assistant model types (`providerId`, `name`, `apiType`, `locality`, `isBuiltIn`), `assistant.service`, `tfa-header` and `tfa-panel`: the picker sends `providerId`, and the cloud notice comes from locality. Verify with their specs updated, plus a case for a Cloud custom provider showing the cloud notice.
- [x] 4.2 Update `AiProviderAdminService` for the new routes and the add body. Group `ai-provider-settings.component` by provider id: built-ins first, then custom providers by name; custom models count as added. Verify with the service spec and the component spec updated, plus a case for a custom provider group and its disappearance after its last model is deleted.
- [x] 4.3 Make the AI provider field on `add-ai-model.component` a text input with a datalist. It matches existing providers case-insensitively, shows a "New provider" tag and the API type / Server address / Runs / optional key fields for a new name, and gives the "Enter the AI provider." and "Enter the server address." errors. Verify with spec cases from the spec's "AI provider is a textbox", "Create a custom provider" and "Existing provider typed by name" requirements.
- [x] 4.4 Make the provider editable in edit mode, with the move hint, and send the target in the edit body. Verify with spec cases: moving to an existing provider, moving to a new one, and a failed test leaving the screen with the key emptied.

## 5. Docs and integration check

- [ ] 5.1 Update the six TFA Settings documents, plus the assistant FRD and LLD (`TF - Documents/TF - FRD/08-TFA-Assistant/...`, `TF - Documents/TF - LLD/08-TFA-Assistant/...`), for custom providers, the provider textbox, moves, provider-id routes and the migration. Verify by searching them for "custom provider" and `providerId`.
- [ ] 5.2 Run `dotnet test` and `npm test`. Then, on a copy of your real database, start the app and check:
  1. Gemini is still in use with its key.
  2. Create a custom provider (for example a local LM Studio or a Groq key), test it and use it.
  3. Chat with it in the TFA window.
  4. Move a model between providers.
  5. Delete the custom provider's last model and see the group disappear.

  Verify that every step behaves as in the specs.

## Workflow follow-up

- Archive the change after review, and sync `ai-provider-management` and `ai-custom-providers` into `openspec/specs/`.
