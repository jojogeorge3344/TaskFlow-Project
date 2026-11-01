# Tasks

## 0. Prerequisites

- [ ] 0.1 Confirm that `tfa-add-ai-model-page` and `tfa-gemini-model-id-validation` have all their tasks done (`openspec list`), and that their code is in the working tree. Verify that the Add AI model screen and the Gemini model ID rule work before starting.

## 1. Backend: use, test and delete rules

- [x] 1.1 Add `AiProviderConfigService.UseModelAsync(userId, kind, modelName)`, sharing the switching code with `ActivateModelAsync`. It refuses with 404 when the model is unknown for the provider, and with 400 "No key saved for `{DisplayName}` yet. Add it on the Add AI model screen." when the provider is not configured. Verify with `AiProviderConfigServiceTests`: TFA switches to the chosen model; the other providers are switched off and keep their keys and models; an OpenAI model without a key is refused and nothing changes.
- [x] 1.2 Change `RemoveModelAsync` to design decisions 2 and 3:
  - Refuse a built-in model with "A built-in model cannot be changed or removed."
  - Refuse only the model in use, with "`{name}` is in use by TFA. Switch TFA to another model first, then delete it."
  - When a switched-off provider has the deleted name saved, reset its model to `DefaultModel(kind)`.

  Verify with service tests for each case, and update the existing removal tests to match.
- [x] 1.3 Add `AiModelAdditionService.TestSavedModelAsync(kind, modelName)` (it uses `AiProviderConfigService.GetSavedRuntimeForModelAsync` and `TestCandidateAsync`, so it works for a provider that is switched off; the gateway path only serves the provider in use) and an optional `{ modelName }` body on `POST {kind}/test`. A model not in the provider's list returns 404. Verify with tester tests (the request carries the chosen model; the in-use model is unchanged) and an integration test of the endpoint.
- [x] 1.4 Add `POST {kind}/models/use` (`settings.ai.edit`) to `AiProvidersController`. Verify with integration tests in `AssistantProviderEndpointsTests`: 200 with providers; 400 when the provider has no key; 404 for an unknown model; 403 without edit.

## 2. Backend: edit an added model

- [x] 2.1 Add optional `ClearKey` to `AddAiModelRequest` and its validator ("Provide a new key or clear the key, not both."). Verify with `AddAiModelRequestValidatorTests`.
- [x] 2.2 Add `AiProviderConfigService.UpdateModelAsync` and `AiModelAdditionService.EditAsync`, following the order of checks in design decision 4. Clear-key requests skip the test and remove the key. Verify with `AiModelAdditionServiceTests`:
  - A rename with a passing test renames the model, keeps the model in use unchanged, and updates a switched-off provider's saved name.
  - A failed test saves nothing.
  - A built-in model, the model in use, and a duplicate name are each refused without a provider call.
  - A new key replaces the provider key.
  - Clear-key removes the key without a provider call.
- [x] 2.3 Add `PUT {kind}/models?name=` to `AiProvidersController` (`settings.ai.edit`, plus the in-action `settings.ai.test` check). Verify with integration tests: 200 ok and 200 not-ok; 400 for built-in, in-use and duplicate; 403 without test permission; the response never contains the key.

## 3. Frontend: shared pieces

- [x] 3.1 Add `notice?: boolean` to `ConfirmOptions` and single-button rendering to `tf-confirm-dialog`. Verify with `confirm-dialog.component.spec.ts`: notice mode shows one button and resolves true; the existing modes are unchanged.
- [x] 3.2 Add `useModel(kind, modelName)`, `editModel(kind, name, body)` and `test(kind, modelName?)` to `AiProviderAdminService`, plus `clearKey` on `AddAiModelRequest`. Verify with service spec assertions on URL, method and body.

## 4. Frontend: TFA Settings model list

- [x] 4.1 Rewrite `ai-provider-settings.component` (ts, html, css) as the read-only list from design decision 6. Remove the Model and Server selects, the provider card inputs, Remove the key, and Save. Keep the header text and the Add AI model button. Verify with `ai-provider-settings.component.spec.ts`: rows grouped by provider with their details; In use on exactly one row; no select, input or Save in the section; stacked layout under 640px (CSS check in the spec or a manual check in 6.1).
- [x] 4.2 Add the row actions: Use this model (with the disabled hint when there is no key), Test connection (with a result on the row), and Edit / Delete only on added models. The in-use model shows the notice dialog instead of acting; Delete asks for confirmation (danger variant). Hide each action without its permission. Verify with spec cases for each scenario in the spec's "Use", "Test", "Built-in", "in use" and "Delete" requirements.

## 5. Frontend: edit mode on the Add AI model screen

- [x] 5.1 Add the route `settings/tfa/models/:kind/:name/edit` with the same guard as `models/new`. Put `AddAiModelComponent` in edit mode: provider fixed, name prefilled, the shared-key hint, button text "Test and save", `editModel` on submit, and a toast "`{name}` was updated" before returning to TFA Settings. On failure the screen stays with the key emptied. Opening the screen for an unknown, built-in or in-use model returns to TFA Settings with a toast. Verify with `add-ai-model.component.spec.ts` edit-mode cases.
- [x] 5.2 Add Remove the key in edit mode, with a confirmation that warns when TFA uses that provider. It sends `clearKey: true`. Verify with a spec case: the confirmation text, the request body, and the return to TFA Settings.

## 6. Docs and integration check

- [x] 6.1 Update the TFA Settings BRD, FRD, HLD and LLD, and the workflow pages (`TF - Documents/TFWorkflow/.../08.4-TFA-Settings.md`, `TF - Documents/TFTechnicalWorkflow/.../08.4-TFA-Settings.md`), for the list, the actions, the edit screen, the endpoints and the in-use rule. Verify by searching them for "Use this model" and `models/use`.
- [ ] 6.2 Run `dotnet test` and `npm test`. Then, in the running app, walk through these steps:
  1. Open TFA Settings and check there is no dropdown.
  2. Use a Gemini model.
  3. Edit an added model and rename it.
  4. Try to delete and to edit the model in use, and see the message.
  5. Delete another added model after confirming.

  Verify that every step behaves as in the spec.
