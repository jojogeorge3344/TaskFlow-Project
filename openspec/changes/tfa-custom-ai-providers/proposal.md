# Proposal

## Why

TaskFlow can only talk to three AI providers that are fixed in code: Google Gemini, OpenAI GPT and Ollama. Administrators want to connect other services, such as Groq, Mistral, OpenRouter, Azure OpenAI, or a company server running LM Studio or vLLM, by typing the provider's name on the Add AI model screen. Most of these services accept the OpenAI chat format at their own address, so one generic client can serve them all. Administrators also want to move a model to another provider from the Edit screen.

## What Changes

- **Providers become data.** A provider is a stored record with:
  - a name;
  - an API type: **OpenAI-compatible**, **Google Gemini** or **Ollama**;
  - a server address;
  - Local or Cloud;
  - an optional encrypted key;
  - a built-in flag.

  The three current providers become built-in records, with their keys, models, saved model, enabled flag and chat history kept.
- **AI provider is a textbox** on the Add AI model and Edit AI model screens. It suggests the existing providers while you type. Choosing an existing name works as today. A new name creates a **custom provider** and shows extra fields:
  - **API type** (OpenAI-compatible or Ollama);
  - **Server address** (required);
  - **Runs** (Local or Cloud, default Cloud);
  - **API key** (optional).

  The provider is created only when the connection test passes.
- **Edit can change the provider.** Typing another provider tests the model there, and only on a pass moves the model from the old provider to the new one. A new name on Edit creates a custom provider in the same way. The model in use still cannot be edited.
- **TFA Settings** lists custom providers like the built-in ones, using the same row actions (Use this model, Test, Edit, Delete). A custom provider with no models left is removed automatically. Built-in providers are never removed.
- **The chat assistant and its provider picker** identify providers by id, so a custom provider can be chosen in the TFA window when it is in use.
- **The OpenAI client** accepts any server address and an optional key, so it serves every OpenAI-compatible provider. Gemini and Ollama clients stay as they are.
- **Server-address safety**: the rule that blocks link-local and cloud-metadata addresses, which today applies only to Ollama, applies to every provider address.
- **BREAKING (API):** provider routes change from `/providers/{kind}/...` to `/providers/{providerId}/...`. The chat request and the failure DTO carry `providerId` instead of the provider kind. The frontend changes together with the API; there are no external clients.
- **Out of scope:**
  - Custom providers using the Gemini API type. Gemini has one fixed address, so a second Gemini provider adds nothing.
  - Renaming a provider or changing a provider's API type after it is created.
  - Changing Ollama's role as the embedding server for document search.

## Capabilities

### New Capabilities

- `ai-custom-providers`: defining, testing, listing and removing custom AI providers reached through an OpenAI-compatible or Ollama API, and moving models between providers.

### Modified Capabilities

- `ai-provider-management`: the provider field on Add and Edit becomes a textbox that accepts existing or new providers; Edit can change a model's provider; providers are identified by id. This capability is defined only in in-flight changes (`tfa-assistant`, `tfa-add-ai-model-page`, `tfa-gemini-model-id-validation`, `tfa-model-list-and-edit`) and is not yet in `openspec/specs/`. This delta therefore uses ADDED requirements. Earlier requirements that named "three providers" are superseded by "Supported provider types" here, to be reconciled when those changes are archived.

## Impact

- **Database:** one migration on SQLite.
  - `AiProviderConfigs` gains `Name`, `ApiType`, `Locality` and `IsBuiltIn`. `Kind` becomes `ApiType`, and the unique index moves from `Kind` to `Name`.
  - `AiModels` replaces `Kind` with `ProviderId` (FK), with the unique index on (`ProviderId`, `Name`).
  - `AssistantMessages.ProviderKind` becomes `ProviderId` (nullable FK).
  - The existing data is mapped onto the three built-in rows.
- **Backend:** `AiProviderKind` becomes the API-type enum. Every file that keys on it moves to a provider id: the catalog, the provider config, model and connection-test services, the gateway and registry, `AssistantService`, the DTOs, the controller and the seeder. `OpenAiProvider` gets a configurable address and an optional key. Unit and integration tests are updated and new ones added.
- **Frontend:** the assistant model types, `tfa-header` and `tfa-panel` (the provider picker), `assistant.service`, `ai-provider-admin.service`, `ai-provider-settings.component` and `add-ai-model.component` (provider textbox with suggestions, custom-provider fields, moving a model on Edit), and their specs.
- **Security:** keys stay encrypted and are never returned. The server-address rules apply to every provider. Data sent to a custom Cloud provider is covered by the same cloud notice.
- **Depends on:** `tfa-model-list-and-edit` being finished (manual check 6.2) and archived first, together with `tfa-add-ai-model-page` and `tfa-gemini-model-id-validation`.
- **Docs:** the six TFA Settings documents, and the assistant FRD and LLD for the provider picker and the chat request.
