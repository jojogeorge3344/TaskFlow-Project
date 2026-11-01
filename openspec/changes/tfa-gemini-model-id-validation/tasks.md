# Tasks

## 1. Backend: per-provider model ID rule

- [x] 1.1 In `Common/Constants/AiProviderCatalog.cs`, add three methods (design 1):
  - `NormalizeModelName(kind, name)`: trims; for Gemini, strips one leading `models/`.
  - `IsValidModelId(kind, name)`: Gemini `^[a-z0-9][a-z0-9.\-]*$`; OpenAI and Ollama, no whitespace.
  - `ModelIdMessage(kind)`: example ID for each provider; for Gemini, also says a key name from AI Studio is not a model ID.

  Verify with new catalog unit tests over the example table: `Gemini_TF_AI` ✗, `Gemini-3.8-Flash` ✗, `models/gemini-3.8-flash` → `gemini-3.8-flash` ✓, `gemini-3.5-flash-lite` ✓, `llama3.2:3b` ✓ (Ollama), `ft:gpt-4o-mini:my_org:custom:abc123` ✓ (OpenAI), `GPT API Key` ✗ (OpenAI).
- [x] 1.2 In `AiProviderConfigService.ResolveCandidateRuntimeAsync`, normalise the model name and throw `RequestValidationException("modelName", ModelIdMessage(kind))` when it fails the rule, before any key lookup or provider call. Verify with `AiProviderConfigServiceTests`: adding `Gemini_TF_AI` throws, nothing is sent to the provider and nothing is saved; `models/gemini-3.8-flash` is activated as `gemini-3.8-flash`.
- [x] 1.3 In `AiProviderConfigService.UpdateAsync`, apply the same normalisation and rule (field `providers`, message prefixed with the provider's display name, as the length check already does). Verify with a service test: saving TFA Settings with a Gemini model `Gemini_TF_AI` is refused and the rows are unchanged; an Ollama `llama3.2:3b` save still succeeds.
- [x] 1.4 Keep `UpdateAiProviderRequestValidator.BeAModelId` / `ModelIdMessage` as the rule that applies to every provider, and check that `AddAiModelRequestValidatorTests` still pass unchanged (`dotnet test --filter AddAiModelRequestValidator`).

## 2. Backend: `InvalidModelName` failure kind

- [x] 2.1 Add `InvalidModelName` to `Models/Enums/ProviderFailureKind.cs` (appended). Map Gemini's 400/`INVALID_ARGUMENT` "model name" case to it in `GeminiProvider.Classify`. Verify by changing the existing `ChatProviderTests` case (line ~320) to expect `InvalidModelName` with the same `ProviderReason`, and confirm that a 404 `NOT_FOUND` still gives `ModelUnavailable`.
- [x] 2.2 Add the `InvalidModelName` case to `ProviderFailureMessages.For`: "{name} did not accept the configured model name as a model ID. Ask an administrator to set a valid model ID, for example {DefaultModel}, or choose another model." Verify with a unit test of the exact Gemini text.
- [x] 2.3 In `AiProviderConnectionTester.Failed`, add the provider reason for `InvalidModelName` too. Verify with a tester unit test: the message ends "Google Gemini said: HTTP 400 - INVALID_ARGUMENT: …" and `FailureKind` is `InvalidModelName`. Also verify with a chat-path test that the reply keeps only the fixed message.

## 3. Frontend: Add AI model page

- [x] 3.1 Add `'InvalidModelName'` to the `ProviderFailureKind` union in `modules/assistant/models/assistant.model.ts`. Verify that `ng build` passes.
- [x] 3.2 In `add-ai-model.component.ts`, add `MODEL_ID_RULES` / `MODEL_ID_MESSAGES` that match the catalog (design 3). Make the `modelId` check use the selected provider, and run it again when `kind` changes. Update the `modelId` error branch in `add-ai-model.component.html` to show the message for that provider. Verify with `add-ai-model.component.spec.ts` cases:
  - Gemini + `Gemini_TF_AI` shows the Gemini message (with the AI Studio sentence) and sends no request.
  - Gemini + `models/gemini-3.8-flash` sends the request.
  - Ollama + `llama3.2:3b` is valid.
  - Switching the provider from Ollama to Gemini with `Llama3.2:3b` typed shows the error.
- [x] 3.3 Add a spec case to the component: a server result with `failureKind: 'InvalidModelName'` renders its message followed by "Nothing was saved." Verify that the spec passes (`npm test -- --include add-ai-model`).

## 4. Documentation

- [x] 4.1 Update the model-name validation and failure-kind sections in `TF - Documents/TF - FRD/06-Settings/06.3-TFA-Settings/FRD-TFA-Settings.md` and `TF - Documents/TF - LLD/06-Settings/06.3-TFA-Settings/LLD-TFA-Settings.md`: the Gemini format rule, the `models/` normalisation and `InvalidModelName`. Verify by searching both files for `InvalidModelName` and for the Gemini rule.

## 5. Integration check

- [ ] 5.1 Run the full backend (`dotnet test`) and frontend (`npm test`) test suites, then try the original case by hand in the running app: on Add AI model, choose Google Gemini, enter `Gemini_TF_AI`, and click Test connection. Verify that the field error appears and no request reaches Google (no provider log line). Then enter `gemini-3.8-flash` and confirm that the test is sent.
