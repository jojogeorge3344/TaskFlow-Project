# Design

## Context

- **Current UI.** `AiProviderSettingsComponent` (`modules/settings/tfa-settings/components/`) owns one form per provider. Its Model dropdown carries an `ADD_MODEL` (`__add__`) entry per provider group, with a `newModel` control and the `newModelRequired` validator. Save sends every provider in `PUT /api/v1/assistant/providers/config`. The selected provider is enabled and all others are disabled.
- **Current test.** `POST /api/v1/assistant/providers/{kind}/test` takes no body. `AiProviderConnectionTester` goes through `ChatModelGateway`, which reads the **saved** runtime from `AiProviderConfigService.GetRuntimeAsync`. To "test" typed values, the screen saves them first.
- **Data.**
  - `AiProviderConfigs` holds one row per provider: `IsEnabled`, `ModelName`, `BaseUrl` (Ollama only), `EncryptedApiKey`, `KeyLast4`.
  - `AiModels` holds the list per provider, with a unique `(Kind, Name)` index and `IsBuiltIn`.
  - Keys are protected with `IAiKeyProtector`.
- **Providers.** `IChatModelProviderRegistry.Get(kind).CompleteAsync(AiProviderRuntime, ChatModelRequest)` accepts a runtime directly, so a call can be made without reading the database.
- **Permissions.**
  - Backend: `RequirePermissionAttribute` has `AllowMultiple = false`, so only one can be put on an action.
  - Frontend: `permissionGuard` allows the route when the user has **any** of `data.permissions`.

## Goals / Non-Goals

**Goals:**
- A separate Add AI model page that asks for provider, model name, then the API key (cloud) or the address (Ollama), and has a Back button.
- Test the typed values without saving them. Save and activate only when the test passes, and do both in one request.
- Remove the inline add-model path from TFA Settings.

**Non-Goals:**
- A key per model. Keys stay one per provider.
- New providers (Anthropic), or changes to embeddings or document search.
- Changing how the existing Save, Test connection or Remove model work on TFA Settings.

## Decisions

### 1. One endpoint: test, then save and activate
`POST /api/v1/assistant/providers/{kind}/models`, body `AddAiModelRequest { modelName, apiKey?, baseUrl? }`.

The endpoint runs these steps:
1. **Validate.** The model name is required, trimmed, and at most 100 characters. The key has no spaces and is at most 500 characters. A key is refused for Ollama. `baseUrl` is accepted only for Ollama and goes through the same http/https and `IsMetadataHost` check as `UpdateAiProviderRequestValidator`.
2. **Resolve the runtime in memory:**
   - **Cloud provider:** use the typed key. If none was typed, decrypt the saved key. If there is no readable key, return 400 `apiKey: "Enter the API key."`.
   - **Ollama:** use the normalized `baseUrl`. If none was given, use the saved address, then `http://localhost:11434`.
3. **Test.** Call `registry.Get(kind).CompleteAsync(runtime, testRequest with { ModelName })` with the same tiny prompt the tester uses today.
4. **On failure:** return `200 { ok: false, message, failureKind, providers: null }`. The message comes from `ProviderFailureMessages.For(...)`. Nothing is written.
5. **On success,** in one `SaveChanges`:
   - Add an `AiModel` if `(Kind, Name)` is unknown. Reuse the existing `RememberModelAsync` and the `SaveKeepingAnyDuplicateModelAsync` race handling.
   - Upsert the provider row: `IsEnabled = true`, `ModelName`, plus the key (protect it and set `KeyLast4`) or the normalized `BaseUrl`.
   - Set `IsEnabled = false` on every other row, without touching their keys or models.
   - Stamp `UpdatedByUserId` and `UpdatedAt`.

   Then return `200 { ok: true, message: "The connection works.", failureKind: null, providers: AiProviderConfigDto[] }`.

**Why one endpoint:** two calls (test, then save) leave a gap where the client could save untested values, and they would send the key twice. A failed test is a normal result, not an error, which matches the existing test endpoint returning 200 for a failure.

**Rejected alternative:** reuse `PUT /config` after a client-side test. That would still need a new "test candidate" endpoint, and the client would have to rebuild every provider's update.

### 2. Where the code lives
- `IAiProviderConnectionTester.TestCandidateAsync(AiProviderRuntime, CancellationToken)`: shares the prompt and failure mapping with `TestAsync`. It logs only the provider, the failure kind and the HTTP status, never the key, the same as `ChatModelGateway`.
- `IAiProviderConfigService` gets two methods that never call a provider:
  - `ResolveCandidateRuntimeAsync(kind, request, ct)`: the stored-key fallback, the missing-key and Ollama-key refusals, and the address choice.
  - `ActivateModelAsync(userId, runtime, apiKeyTyped, ct)`: the single save.
- `IAiModelAdditionService.AddAndActivateAsync(userId, kind, request, ct)`: new, in its own folder `Services/Settings/TfaSettings/ModelAddition/` (the architecture tests require one service per folder). It runs resolve, then test, then activate.

  The orchestration cannot live in `AiProviderConfigService`, because the tester depends on the gateway, which depends on the config service. Injecting the tester into the config service would create a dependency-injection cycle.
- `AiProvidersController.AddModel`: a thin action.
- DTOs go in `Dtos/Settings/TfaSettings/AiProviderDtos.cs`: `AddAiModelRequest` and `AddAiModelResultDto(bool Ok, string Message, ProviderFailureKind? FailureKind, IReadOnlyList<AiProviderConfigDto>? Providers)`. The validator is `AddAiModelRequestValidator`, which reuses the URL and metadata-host helpers.

### 3. Permissions: edit **and** test
- **Backend:** the action has `[RequirePermission(SettingsAiEdit)]` and checks `SettingsAiTest` in the action through `IAuthorizationService.AuthorizeAsync(User, PermissionPolicy.NameFor(SettingsAiTest))`. If the check fails it returns `Forbid()`. This avoids changing `AllowMultiple`.
- **Frontend:** the route `settings/tfa/models/new` uses `permissionGuard` with `[SettingsAiEdit]`. The page also checks `SettingsAiTest` on init and navigates back to `/settings/tfa` when it is missing, because the guard matches any permission, not all. The TFA Settings button shows only when `canEdit() && canTest()`.

### 4. Frontend page
- A new standalone `AddAiModelComponent` in `tfa-settings/pages/add-ai-model.component.{ts,html,css,spec.ts}`, lazy-loaded from `settings.routes.ts`. The `settings/tfa/models/new` route is declared before any catch-all.
- On init it loads `getConfig()` to learn each provider's `keySet`, `keyReadable`, `keyLast4` and saved Ollama address. There is no provider selected at first.
- The form controls are `kind` (required), `modelName` (required, max 100), `apiKey` (no spaces, max 500), and `baseUrl` (http/https pattern). The `apiKey` required rule turns on only when `kind` is a cloud provider whose key is not set or not readable.
- **Test connection** calls `AiProviderAdminService.addModel(kind, body)`:
  - **ok:** show the toast "`<model>` on `<provider>` is now in use", then `router.navigate(['/settings/tfa'])`.
  - **not ok:** show the result inline in the same style as `ai-settings__result`, empty `apiKey`, and keep the other values.
  - **HTTP error** (400 or 403): show `apiErrorMessage(...)`.
- While the request runs, the button reads "Testing…" and is disabled.
- **Back** is a `routerLink="/settings/tfa"` button. Nothing is saved.
- Visual style follows the existing `tf-page`, `tf-card`, `tf-field` and `tf-btn` classes.

### 5. TFA Settings changes
- Remove `ADD_MODEL`, `addModelValue`, the `newModel` control, `newModelRequired`, `isAddingModel`, and the add-model branch in `chosenModelName`, `save()` and `canRemoveModel`. Also remove the matching template blocks.
- Add a `+ Add AI model` button (`routerLink="/settings/tfa/models/new"`, `data-action="add-model"`) in the section header.
- TFA Settings already reloads config in `ngOnInit`, so on return it shows the new active model with no extra state. Unsaved edits on TFA Settings are dropped when the user leaves, the same as with Remove model. The button's hint says so.

## Risks / Trade-offs

- **Server-side request to an admin-typed Ollama address (SSRF).** The same validation and redirect handling that already apply to saved Ollama addresses also apply here, so the add page adds no new risk.
- **A key replaces the provider's key for all of its models.** This is by design (one key per provider), and the page states it when a key already exists.
- **Cost of the test call.** One tiny completion per click. Repeated clicks are covered by the disabled button.
- **Breaking UI change.** Specs and e2e flows that use "Add new model…" must move to the new page. They're listed in tasks.
- **Concurrent admins.** Two admins adding the same model at once are handled by the existing duplicate-model handling. The last one to activate wins, the same as today's Save.
