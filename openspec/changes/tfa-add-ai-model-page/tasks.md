# Tasks

## 1. Backend: request, validation and candidate test

- [x] 1.1 Add `AddAiModelRequest { ModelName, ApiKey?, BaseUrl? }` and `AddAiModelResultDto(Ok, Message, FailureKind?, Providers?)` to `Dtos/Settings/TfaSettings/AiProviderDtos.cs`. Verify that the backend builds (`dotnet build`).
- [x] 1.2 Add `AddAiModelRequestValidator`. It enforces: model name required and at most 100 characters; key at most 500 characters with no spaces; `BaseUrl` must be http/https and not a metadata host (reuse `UpdateAiProviderRequestValidator.BeHttpUrl`/`IsMetadataHost`). Verify with new validator unit tests covering each rule.
- [x] 1.3 Add `TestCandidateAsync(AiProviderRuntime, CancellationToken)` to `IAiProviderConnectionTester`/`AiProviderConnectionTester`. It calls the registry provider directly, shares the prompt and failure mapping with `TestAsync`, and logs without the key. Verify with unit tests: success returns ok; a 401, a model-not-found and a timeout each map to the `ProviderFailureMessages` text; the logs contain no key.

## 2. Backend: add-and-activate service and endpoint

- [x] 2.1 Implement add-and-activate (`AiProviderConfigService.ResolveCandidateRuntimeAsync` + `ActivateModelAsync`, run by the new `AiModelAdditionService` to avoid a DI cycle; see design 2). It falls back to the stored key, refuses a cloud provider with no readable key (400 `apiKey`) and refuses a key sent for Ollama, picks the Ollama address (typed, then saved, then default), and runs the test. On success it adds the model if unknown, saves the key or address, enables this provider and disables the others in one save. On failure it writes nothing. Verify with `AiProviderConfigServiceTests` cases: a new model is activated; an existing model is not duplicated; a key left empty keeps the stored key; a failed test changes nothing; other providers keep their keys and models.
- [x] 2.2 Add `POST /api/v1/assistant/providers/{kind}/models` to `AiProvidersController`. It needs `[RequirePermission(SettingsAiEdit)]` plus an in-action `SettingsAiTest` check through `IAuthorizationService` that returns `Forbid()`. Verify with integration tests in `AssistantProviderEndpointsTests`: 200 ok with providers; 200 not ok with nothing saved; 400 for a missing model name or key; 403 without edit or test permission; the response never contains the key.

## 3. Frontend: Add AI model page

- [x] 3.1 Add `AddAiModelRequest`/`AddAiModelResult` types to `assistant.model.ts` and `addModel(kind, body)` to `AiProviderAdminService` (POST `/api/v1/assistant/providers/{kind}/models`). Verify with a service spec asserting the URL and body.
- [x] 3.2 Create `tfa-settings/pages/add-ai-model.component.{ts,html,css}` with:
  - a Back button;
  - the provider select first;
  - the model name;
  - the API key for cloud providers (required only when no readable key is saved, with the saved-key hint and the warning that a new key replaces it for every model of that provider);
  - the optional Ollama server address (placeholder `http://localhost:11434`);
  - Test connection, showing "Testingâ€¦" while it runs.

  Verify with `ng build`.
- [x] 3.3 Wire the results:
  - **Success:** show a toast and navigate to `/settings/tfa`.
  - **Failure:** show the reason inline, empty the key and keep the other values.
  - **HTTP error:** show `apiErrorMessage`.
  - **Missing `SettingsAiTest` on init:** redirect to `/settings/tfa`.

  Verify with `add-ai-model.component.spec.ts`. It must cover: the fields change with the provider; required-field errors block the test; success navigates back; failure keeps the user on the page with the key cleared; Back saves nothing.
- [x] 3.4 Register the `settings/tfa/models/new` route in `settings.routes.ts` with `permissionGuard` and `[SettingsAiEdit]`. Verify that a spec or a manual run shows the page opening from its address and being refused without the permission.

## 4. Frontend: TFA Settings changes

- [x] 4.1 Remove the inline add-model path from `ai-provider-settings.component.{ts,html}`. That means `ADD_MODEL`, `addModelValue`, the `newModel` control, `newModelRequired`, `isAddingModel`, the "Add new modelâ€¦" option, the "New model name" field, and the add-model branches in `save()`, `chosenModelName()` and `canRemoveModel()`. Verify that `ng build` passes and the dropdown shows only listed models.
- [x] 4.2 Add a `+ Add AI model` button (`data-action="add-model"`, `routerLink="/settings/tfa/models/new"`) to the section header, shown only when `canEdit() && canTest()`, with a hint that unsaved edits are dropped. Update the `TfaSettingsComponent` page description. Verify with `ai-provider-settings.component.spec.ts`: remove or replace the old add-model specs, and add specs for button visibility per permission and the absence of the "Add new modelâ€¦" option.
- [x] 4.3 Run the full frontend test suite (`npm test -- --watch=false`) and confirm it passes.

## 5. Documentation

- [x] 5.1 Update the "Add a model" flow, screens and permissions in `TF - Documents/TFWorkflow/App WF in MD/08-Settings/08.4-TFA-Settings.md`. Verify that the doc describes the new page and no longer mentions "Add new modelâ€¦" in the dropdown.
- [x] 5.2 Update `TF - Documents/TFTechnicalWorkflow/Tech WF in MD/08-Settings/08.4-TFA-Settings.md` and the TFA Settings LLD/FRD with the new endpoint, request/response and route. Verify with a search showing no stale `__add__`/"Add new modelâ€¦" references in `TF - Documents`.

## 6. Integration check

- [x] 6.1 Run `dotnet test` for the unit and integration test projects and confirm everything passes.
- [ ] 6.2 Run the app and follow the full flow:
  1. Open TFA Settings and click **Add AI model**.
  2. Choose Gemini and enter a bad key: the error shows and nothing is saved.
  3. Enter a valid key: you return to TFA Settings with the model in use.
  4. Click Back on the add page: nothing changes.

## Workflow follow-up

- Archive the change after review, and sync the `ai-provider-management` delta into `openspec/specs/`.
