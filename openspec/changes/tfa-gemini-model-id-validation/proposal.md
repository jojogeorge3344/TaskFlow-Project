# Proposal

## Why

An administrator typed `Gemini_TF_AI` (the name they gave their key in Google AI Studio) as the Gemini model name on the Add AI model page. The page sent it to Google, and the test failed with "The configured model is not available on Google Gemini… unexpected model name format". The current model-name check only rejects spaces, so the wrong value got through. The error also says the model is "not available", when the real problem is that the value is not a model ID at all. A clear check before anything is sent, and a clearer message when Google still refuses the name, would let administrators fix this mistake on their own.

## What Changes

- Model-name format rules that depend on the provider. Google Gemini accepts only Gemini-style model IDs: lowercase letters, digits, dots and hyphens, starting with a letter or digit (for example `gemini-3.8-flash`). A leading `models/` (as Google AI Studio shows it) is removed before the check and before saving. OpenAI and Ollama keep the current rule (no spaces), because their valid IDs can contain uppercase letters, `_`, `:` and `/` (fine-tuned OpenAI IDs, Ollama tags such as `llama3.2:3b`).
- The rule is applied on the Add AI model page as soon as the administrator picks the provider and types the name, and again on the server for both Add AI model and saving TFA Settings, so a bad name is never sent to the provider.
- The validation message names the expected shape and gives an example. For Gemini it also explains that a key's name from Google AI Studio is not a model ID.
- A new provider failure kind, `InvalidModelName`. Gemini's `400 INVALID_ARGUMENT … model name` response is reported as this kind instead of `ModelUnavailable`, with its own message ("… is not a valid model ID …"). On Test connection, Google's own reason is still added for administrators.
- Out of scope: fetching the provider's live model list. OpenAI and Ollama validation is unchanged apart from sharing the new rule structure.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `ai-provider-management`: adds requirements for checking the model ID format for each provider before any test or save, and for reporting a model name the provider refuses as malformed as "not a valid model ID", separately from a model that is missing. The capability is defined by the in-flight changes `tfa-assistant` and `tfa-add-ai-model-page`, not yet in `openspec/specs/`, so this delta uses ADDED requirements.

## Impact

- Backend: `AiProviderCatalog` (per-provider model ID rule, normalisation and example), `AiProviderConfigService` (`UpdateAsync` and `ResolveCandidateRuntimeAsync` enforce the rule), `UpdateAiProvidersRequestValidator` / `AddAiModelRequestValidator` (keep the provider-independent whitespace rule; message reuse), `ProviderFailureKind` (new `InvalidModelName`), `GeminiProvider.Classify`, `ProviderFailureMessages`, `AiProviderConnectionTester.Failed`.
- Frontend: `add-ai-model.component` (validator depends on the provider, message), `assistant.model.ts` (`ProviderFailureKind` union).
- API: `failureKind` in the test/add-model responses can now be `InvalidModelName`. This is an addition, and existing clients treat an unknown kind as a generic failure.
- Tests: validator/service unit tests, `ChatProviderTests` Gemini classification, frontend component spec.
- Docs: TFA Settings FRD/LLD sections that describe model-name validation and failure kinds.
