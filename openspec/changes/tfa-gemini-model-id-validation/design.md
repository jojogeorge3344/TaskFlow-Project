# Design

## Context

Model names are checked in three places today, and all three use the same rule (no spaces after trimming):

- Frontend: `modelId()` in `add-ai-model.component.ts`.
- Backend, FluentValidation: `UpdateAiProviderRequestValidator.BeAModelId`, reused by `AddAiModelRequestValidator`.
- Backend service: `AiProviderConfigService.UpdateAsync` checks only the length.

`AddAiModelRequest` has no provider. The provider comes from the route (`POST {kind}/models`), so a FluentValidation rule on that DTO cannot depend on the provider. `AiProviderConfigService.ResolveCandidateRuntimeAsync` already does the checks that depend on the provider (key required, Ollama takes no key) and throws `RequestValidationException`.

`GeminiProvider.Classify` maps a 400 `INVALID_ARGUMENT` whose message contains "model name" to `ModelUnavailable`. `AiProviderConnectionTester.Failed` adds the provider's reason for `Unexpected` and `ModelUnavailable` only.

## Goals / Non-Goals

**Goals:**
- One place in the backend defines the model ID rule, its normalisation and its example for each provider.
- The frontend and the server agree on the rule for each provider.

**Non-Goals:**
- Checking that the model exists before the test. Test connection already does this.
- Changing the stored data or migrating saved model names.

## Decisions

1. **Rule lives in `AiProviderCatalog`.** Add `NormalizeModelName(kind, name)`, which trims and, for Gemini, strips one leading `models/`. Add `IsValidModelId(kind, name)`: for Gemini, `^[a-z0-9][a-z0-9.\-]*$`; for the others, no whitespace. Add `ModelIdMessage(kind)`, which uses the existing example IDs, with the extra AI Studio sentence for Gemini. The catalog already owns provider facts (display name, default model, key needed), so this is where the rule belongs. *Alternative:* a separate validator class for each provider. That is more structure than three rules need.

2. **The server enforces the rule in the service, not in FluentValidation.** `ResolveCandidateRuntimeAsync` and `UpdateAsync` normalise the name, then throw `RequestValidationException("modelName", …)` (in `UpdateAsync`, keyed `providers` as the current length check is). The provider-independent FluentValidation rules (blank, length, no whitespace) stay as they are, so malformed requests are still rejected early. *Alternative:* add the kind to `AddAiModelRequest`. That repeats the route value and allows a mismatch.

3. **The frontend validator depends on the provider.** `modelId` becomes a group-level check (or is re-run when `kind` changes) that uses a `MODEL_ID_RULES` map built to match the catalog. Its error key is `modelId`, and the message comes from a matching `MODEL_ID_MESSAGES` map. The existing template branch for `modelId` shows the message for the chosen provider. A `models/` prefix is accepted on the page, and the server strips it.

4. **New failure kind `InvalidModelName`.** It is added at the end of `ProviderFailureKind` (the enum is serialised as a string, so the order is not part of the contract). `GeminiProvider.Classify` maps the 400/model-name case to it. `ProviderFailureMessages.For` gets a case: "`{name}` did not accept the configured model name as a model ID. Ask an administrator to set a valid model ID, for example `{DefaultModel}`, or choose another model." `AiProviderConnectionTester.Failed` also adds the provider's reason for this kind. The frontend `ProviderFailureKind` union gets the new value. *Alternative:* keep `ModelUnavailable` and only reword the message. That would mix two causes that need different fixes.

5. **OpenAI and Ollama classification is unchanged.** Neither provider sends a distinct response for a malformed name that we rely on today, and the format check is lenient for both.

## Risks / Trade-offs

- [Google may add a model ID with uppercase letters or other characters] → The rule lives in a single constant in the catalog, and the error message points to the example, so a rule that is too strict shows up at once and is a one-line fix.
- [A Gemini model name already saved in the custom model list (from before this change) fails the new rule] → Adding a model requires a passing test, so such entries are unlikely. `UpdateAsync` would refuse saving TFA Settings with one selected, and the administrator would pick another model. No migration is needed.
- [Frontend and backend rules drift apart] → Unit tests on both sides use the same example table (`Gemini_TF_AI`, `Gemini-3.8-Flash`, `models/gemini-3.8-flash`, `llama3.2:3b`, the fine-tuned OpenAI ID).

## Migration Plan

No data migration. Deploy the backend and frontend together. Rolling back reverts both, and an older client simply shows an `InvalidModelName` result as a failed test with its message.
