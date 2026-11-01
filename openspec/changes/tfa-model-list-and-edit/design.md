# Design

## Context

- **Data:** `AiProviderConfig` holds one row per provider (enabled flag, saved `ModelName`, `BaseUrl`, encrypted key, `KeyLast4`). `AiModel` holds the dropdown names (`Kind`, `Name`, `IsBuiltIn`). The key and address belong to the provider, not to a model. `AiProviderSeeder` adds missing built-in models back on every start (`AiProviderSeeder.cs:69-75`).
- **Screen:** `ai-provider-settings.component` currently renders the Model and Server selects, an editable card for each provider, and a Save button that calls `PUT config`. Removal is offered only for added models that are not saved for their provider (`RemoveModelAsync`, `UsesModelAsync`).
- **Add AI model:** `AddAiModelComponent` together with `POST {kind}/models` → `AiModelAdditionService.AddAndActivateAsync` tests a candidate runtime (`ResolveCandidateRuntimeAsync`, `TestCandidateAsync`), then calls `ActivateModelAsync`.
- **Dialog:** `tf-confirm-dialog` always shows Cancel and a confirm button.

## Goals / Non-Goals

**Goals:**
- One read-only list as the single overview, with all typing on the Add or Edit screen.
- Reuse the existing candidate test path for editing, so the Edit and Add screens behave the same.

**Non-Goals:**
- A key for each model, custom providers, hiding built-ins (see proposal).
- Removing the `PUT config` endpoint. It stays, unused by the screen, so an older client and the existing tests keep working.

## Decisions

1. **Key stays per provider.** This was agreed during exploration. Edit writes the typed key to the provider row, and the screen warns that the key is shared. There is no schema change.

2. **Built-in models are read-only.** This was agreed during exploration. The server refuses edit and delete when `IsBuiltIn`, and the existing remove message changes to "A built-in model cannot be changed or removed." *Alternative:* an `IsHidden` flag so a built-in could be deleted. That needs a migration and seeder changes for little benefit.

3. **"In use" means TFA's active model.** That is the model saved on the enabled provider. Deleting or renaming a model that is saved only on a switched-off provider is allowed. Delete resets that provider's `ModelName` to `AiProviderCatalog.DefaultModel(kind)`, and a rename updates it to the new name. The provider stays switched off. This replaces today's broader `UsesModelAsync` rule, which blocked removal for a switched-off provider too and confused administrators because nothing was visibly using the model. In-use message: "`{name}` is in use by TFA. Switch TFA to another model first, then `{edit|delete}` it."

4. **Endpoints** (all under `api/v1/assistant/providers`):
   - `POST {kind}/models/use` with body `{ modelName }`, needs `settings.ai.edit`. It calls a new `AiProviderConfigService.UseModelAsync`, which refuses an unknown model (404), a provider that is not configured (400 "No key saved for `{DisplayName}` yet. Add it on the Add AI model screen."), and a missing user. Otherwise it reuses the switching part of `ActivateModelAsync` (enable this provider, set the model, switch the others off). It returns the provider list.
   - `PUT {kind}/models?name={current}` with body `AddAiModelRequest`, needs `settings.ai.edit`, and `settings.ai.test` checked in the action as `AddModel` does. It calls a new `AiModelAdditionService.EditAsync`, which applies this order of checks:
     1. Refuse a built-in model.
     2. Refuse the model in use.
     3. Normalise and validate the new name, and refuse a duplicate.
     4. Resolve the candidate runtime and test it.
     5. On pass, call `AiProviderConfigService.UpdateModelAsync` in one save: rename the `AiModel` row, update a switched-off provider's saved name, save the key or address.

     It returns `AddAiModelResultDto`, and a failed test saves nothing.
   - `POST {kind}/test` gets an optional body `{ modelName }`. When it is present, `AiModelAdditionService.TestSavedModelAsync` builds the saved runtime with that model (`AiProviderConfigService.GetSavedRuntimeForModelAsync`, which ignores whether the provider is switched on; the model must be in the provider's list) and tests it with `TestCandidateAsync`. The gateway path is not used because it only serves the provider TFA uses. When it is absent, the behaviour is unchanged.
   - `DELETE {kind}/models?name=` stays as it is, with the rules from decisions 2 and 3.

5. **Edit screen = `AddAiModelComponent` in edit mode.** The new route `settings/tfa/models/:kind/:name/edit` uses the same guard as `models/new` (edit and test permissions). In edit mode:
   - The provider select is disabled and preset.
   - The model name is prefilled.
   - The button calls `editModel`, and success returns to TFA Settings with a toast "`{name}` was updated".
   - **Remove the key** sends `clearKey: true`. `AddAiModelRequest` gains an optional `ClearKey`, which is refused together with a typed key, as in `UpdateAiProviderRequestValidator`. A test still runs against the address or the remaining key: with the key removed, the provider becomes not configured. So for Remove the key, the screen skips the test and saves directly, and the spec allows it because nothing is sent to the provider.

   *Alternative:* a separate Edit component. That would duplicate the provider-dependent fields and validation.

6. **The list is built on the client from `GET config`.** Each `AiProviderConfigDto` already carries `Models`, `CustomModels` (the added ones), the key status, the address and `IsEnabled`/`ModelName`. A row is in use when `provider.isEnabled && model === provider.modelName`. No new read endpoint is needed.

7. **Notice dialog.** `ConfirmOptions` gains `notice?: boolean`. In notice mode only one button is shown (the `confirmLabel`, for example "OK"). The in-use message uses notice mode, and Delete confirmation uses the existing danger variant. The component hosts a single `<tf-confirm-dialog>`.

8. **Permissions** keep their existing keys: view for the list, edit for Use, edit + test for Edit, `settings.ai.model.remove` for Delete, test for Test connection. The buttons are hidden when the user lacks the permission, and the server enforces the same rules.

## Risks / Trade-offs

- [An administrator relies on the old Save button for key changes] → Changing a key moves to Edit, or to Add AI model for a provider that has only built-ins. The FRD and the screen hint both say so.
- [Renaming a model changes nothing visible in chat history] → Chat stores no model name per message today. Nothing to migrate.
- [Two administrators edit at once] → Rename uses the existing unique index on `AiModel (Kind, Name)`. A clash returns "That model is already in the list."
- [Remove the key on the edit screen of a model whose provider TFA uses] → Editing the model in use is refused, so the key of the provider in use can only be removed from another model of the same provider. That is acceptable: removing it would switch TFA off, and the screen warns "TFA uses this provider; removing the key makes TFA unavailable until a key is added." The remove still requires confirmation.

## Migration Plan

No data migration. Deploy the backend before the frontend, or both together. The old screen keeps working against the new backend because `PUT config` is kept. Rollback means reverting both.

## Open Questions

- Whether TFA Settings should also show a short "Last tested" time on each row. This can be added later without changing these specs.
