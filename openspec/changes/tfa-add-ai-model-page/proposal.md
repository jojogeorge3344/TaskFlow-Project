# Proposal

## Why

Adding an AI model today is hidden inside the TFA Settings Model dropdown ("Add new model…" plus an inline text box), and its connection test only checks settings that are already saved. A mistyped model name or a wrong API key is therefore saved first and only found to be broken afterwards. Administrators need a clear, separate place to add a model that proves the model works before anything is stored.

## What Changes

- Add an **Add AI model** button to the TFA Settings screen (`/settings/tfa`).
- Add a separate **Add AI model** page (`/settings/tfa/models/new`) with a **Back** button to TFA Settings. The page asks, in order:
  1. **AI provider** (required): Google Gemini, OpenAI GPT or Ollama.
  2. **Model name** (required).
  3. **API key**, for Gemini and OpenAI only. It is required when the provider has no readable key yet. When a key is already saved, it shows "key set (ends …1234)" and may be left empty to keep that key.
  4. **Server address**, for Ollama only. It is optional and defaults to `http://localhost:11434`.
  5. **Test connection**.
- Test connection checks the values typed on the page **without saving them**. When the test fails, the reason is shown, the admin stays on the page, and nothing is stored. When it passes, the model is added to the list, the key (or Ollama address) is saved for the provider, that provider and model become the one TFA uses, and the admin is returned to TFA Settings with the new model selected.
- **BREAKING (UI only)**: remove the "Add new model…" entry and its inline "New model name" box from the TFA Settings Model dropdown. Picking an existing model, saving, the existing "Test connection" and "Remove this model from the list" stay as they are.
- Add a backend endpoint that tests a candidate model and, only when the test passes, saves it and makes it active in one step.
- API keys stay **one per provider**. A key entered on the new page replaces that provider's saved key for all of its models, and the page says so.

## Capabilities

### New Capabilities
- `ai-provider-management`: how administrators configure TFA's AI providers and models. This change adds the requirements for the Add AI model page, the test-before-save rule, and activating the new model on success.

### Modified Capabilities
<!-- No archived main specs exist under openspec/specs/ yet; the earlier tfa-assistant change's ai-provider-management delta was never synced. -->

## Impact

- **Frontend** (`TaskFlow Proj/frontend/src/app/modules/settings/`):
  - `settings.routes.ts`: add the new route.
  - New `tfa-settings/pages/add-ai-model.component.*`.
  - `ai-provider-settings.component.{ts,html,css,spec.ts}`: remove the add-model path and add the button.
  - `ai-provider-admin.service.ts`: add a new `addModel` call.
  - `assistant.model.ts`: add new request and result types.
- **Backend** (`TaskFlow Proj/backend/src/TaskFlow.Api/`):
  - New `POST /api/v1/assistant/providers/{kind}/models` in `AiProvidersController`. It needs `settings.ai.edit` and `settings.ai.test`.
  - New request DTO and validator.
  - New methods on `AiProviderConnectionTester` (test candidate values) and `AiProviderConfigService` (add and activate).
- **Database**: no schema change. It reuses `AiModels` and `AiProviderConfigs`.
- **Tests**: unit tests for the service and tester, integration tests for the endpoint, and Angular specs for the new page and the changed settings component.
- **Docs**:
  - `TF - Documents/TFWorkflow/App WF in MD/08-Settings/08.4-TFA-Settings.md`
  - `TF - Documents/TFTechnicalWorkflow/Tech WF in MD/08-Settings/08.4-TFA-Settings.md`
  - The TFA Settings FRD, HLD and LLD.
- **Out of scope**: per-model API keys, an Anthropic/Claude provider, and cloud embeddings for document search.
