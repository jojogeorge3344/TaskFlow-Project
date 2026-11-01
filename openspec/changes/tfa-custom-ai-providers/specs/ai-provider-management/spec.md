# Spec Delta

## Purpose

Lets an administrator configure which AI providers and models TFA uses, add a new model only after it has been proven to work, and keep each provider's API key secret.

## ADDED Requirements

### Requirement: AI provider is a textbox

On the Add AI model and Edit AI model screens, the AI provider field SHALL be a required text input with suggestions of existing providers, not a dropdown. It SHALL be editable on both screens. A blank value SHALL be refused with "Enter the AI provider." before anything is sent.

#### Scenario: Add screen

- **WHEN** an administrator opens Add AI model
- **THEN** the AI provider field is an empty text input that suggests Google Gemini, OpenAI GPT, Ollama and any custom providers

#### Scenario: Blank provider

- **WHEN** the administrator clicks Test connection with the AI provider empty
- **THEN** the screen shows "Enter the AI provider." and nothing is sent

### Requirement: Provider editable on Edit

On Edit AI model the AI provider SHALL be prefilled with the model's provider and editable. It SHALL accept another existing provider or a new name, which creates a custom provider as on Add. When it differs from the current provider the screen SHALL say "The model moves to `<target>` when the test works." The model in use SHALL still be refused for edit.

#### Scenario: Changing the provider

- **WHEN** an administrator edits `gemini-2.0-flash` and types "OpenRouter" as the provider
- **THEN** the screen says "The model moves to OpenRouter when the test works."

### Requirement: Move saves only after a passing test on the target

When the provider was changed, Test connection SHALL test the model on the target with its key or address (typed, or saved for an existing provider). Only on a pass SHALL the system remove the model from the old provider's list, add it to the target's, and save a typed key or address for the target. Which model TFA uses SHALL NOT change.

#### Scenario: Move a Gemini model to OpenRouter

- **WHEN** an administrator edits `gemini-2.0-flash` (not in use), changes the provider to the existing custom "OpenRouter", changes the name to `google/gemini-2.0-flash-001`, and the test passes
- **THEN** the model is listed under OpenRouter as `google/gemini-2.0-flash-001` and no longer under Google Gemini

#### Scenario: Target already has that model

- **WHEN** the target provider already lists the same model name
- **THEN** the edit is refused with "That model is already in the list." and nothing is tested or saved

#### Scenario: Moving away a custom provider's last model

- **WHEN** a custom provider's only model is moved to another provider and the test passes
- **THEN** the model is listed under the target and the emptied custom provider is removed

#### Scenario: Failed test on the target

- **WHEN** the test on the target provider fails
- **THEN** the model stays where it was, nothing is saved, and the reason and "Nothing was saved." are shown

### Requirement: Model ID format follows the API type

The model ID format check SHALL apply by API type: Google Gemini keeps its lowercase format and the `models/` prefix removal; OpenAI-compatible and Ollama providers SHALL refuse only names with spaces, so IDs such as `meta-llama/Llama-3.3-70B-Instruct` and `llama3.2:3b` are accepted.

#### Scenario: Slash and uppercase on a custom provider

- **WHEN** the model `meta-llama/Llama-3.3-70B-Instruct` is entered for a custom OpenAI-compatible provider
- **THEN** the format check passes and the test is sent

### Requirement: Providers are identified by id

API routes, the TFA window's provider choice and stored chat messages SHALL identify a provider by its id, so built-in and custom providers are handled the same way. A request naming an unknown provider id SHALL be refused with 404.

#### Scenario: Unknown provider

- **WHEN** a request is sent to `/api/v1/assistant/providers/999/models/use`
- **THEN** it is refused with 404 and nothing changes

#### Scenario: Chat with a custom provider

- **WHEN** TFA uses a model on custom provider "Groq" and a user sends a message
- **THEN** the message is answered through the OpenAI-compatible client at Groq's address and stored with Groq's provider id
