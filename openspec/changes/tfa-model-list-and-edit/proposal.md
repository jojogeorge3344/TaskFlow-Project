# Proposal

## Why

The TFA Settings screen mixes choosing a model with typing provider details: a Model dropdown, a linked Server dropdown, and an editable API key or address card, followed by a Save button. Administrators find it unclear which fields belong to which model, and they cannot edit a model they added. Delete is hidden without explaining why when the model is in use. TFA Settings should become a read-only overview of the models, and all typing should happen on the Add AI model screen and a matching Edit screen.

## What Changes

- **BREAKING (UI):** TFA Settings no longer shows the Model dropdown, the Server dropdown, the API key / server address card, **Remove the key**, or **Save TFA settings**.
- TFA Settings lists every model as a read-only row grouped by provider. Each row shows the model ID, the provider, whether it runs locally or in the cloud, the provider's key status (the last four characters) or Ollama address, whether it is built in or was added by an administrator, and whether it is in use.
- Each row has actions:
  - **Use this model** switches TFA to that model in one step. It is disabled, with a hint, when the provider has no key or address saved.
  - **Test connection** tests that model with the saved key or address.
  - **Edit** and **Delete** appear only on models an administrator added.
- **Edit** opens the Add AI model screen in edit mode at its own address. The screen has the provider fixed, the model name, an optional new key (with a warning that the key is shared by every model of that provider) or Ollama address, and **Remove the key**. Changes are saved only after Test connection passes, and editing does not change which model TFA uses.
- Editing or deleting the model TFA uses is refused with a message, for example: "gemini-2.5-pro is in use by TFA. Switch TFA to another model first, then delete it." Deleting any other added model asks for confirmation first.
- Built-in models are read-only and have no Edit or Delete. The app adds them back on every start.
- New endpoints: use a model, edit a model, and test a chosen model. The existing delete endpoint is kept, with an updated "in use" rule and message. `PUT config` stays for compatibility but the screen no longer calls it.
- Out of scope: a separate key for each model (the key stays per provider), custom providers, and hiding built-in models.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `ai-provider-management`: TFA Settings becomes a read-only model list with Use, Test, Edit and Delete actions; Add AI model gains an edit mode; edit and delete are refused for the model in use. The capability is defined by in-flight changes (`tfa-assistant`, `tfa-add-ai-model-page`, `tfa-gemini-model-id-validation`) and is not yet in `openspec/specs/`. This delta therefore uses ADDED requirements only. The earlier requirements it touches ("Add AI model entry point", "Activate the new model on success") stay true: there is no dropdown to offer an add entry, and a newly added model still becomes the one in use.

## Impact

- Backend: `AiProvidersController` (new `POST {kind}/models/use`, `PUT {kind}/models`, a `modelName` body on `POST {kind}/test`), `AiProviderConfigService` (use, edit and rename rules, the "in use" rule for delete), `AiModelAdditionService` (test-then-update for edit), `AiProviderConnectionTester` (test a chosen model), DTOs, plus unit and integration tests.
- Frontend: `ai-provider-settings.component` rewritten as a model list; `add-ai-model.component` gains an edit mode; new route `settings/tfa/models/:kind/:name/edit`; `AiProviderAdminService` (`useModel`, `editModel`, `test` with a model); `tf-confirm-dialog` gains a single-button notice mode; specs.
- Permissions: Use needs `settings.ai.edit`. Edit needs `settings.ai.edit` and `settings.ai.test`. Delete needs `settings.ai.model.remove`. Test needs `settings.ai.test`. No new permission keys.
- Depends on: `tfa-add-ai-model-page` and `tfa-gemini-model-id-validation` being finished first. This change reuses their Add AI model screen and model ID rule.
- Docs: TFA Settings BRD, FRD, HLD and LLD, and the app and technical workflow pages for TFA Settings.
