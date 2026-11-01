# Spec Delta

## Purpose

Lets an administrator configure which AI providers and models TFA uses, add a new model only after it has been proven to work, and keep each provider's API key secret.

## ADDED Requirements

### Requirement: Gemini model ID format

A Google Gemini model name SHALL be a Gemini model ID: lowercase letters, digits, dots and hyphens only, starting with a letter or digit. A single leading `models/` SHALL be removed before the check, and the name SHALL be tested and saved without it. A name that fails the check SHALL be refused with a validation message, and nothing SHALL be sent to Google or saved.

#### Scenario: Key name typed as the model

- **WHEN** an administrator chooses Google Gemini on the Add AI model page, enters `Gemini_TF_AI` as the model name, and clicks Test connection
- **THEN** the page shows that this is not a Gemini model ID, gives `gemini-3.8-flash` as an example, and says that a key's name from Google AI Studio is not a model ID
- **AND** no request is sent to Google and nothing is saved

#### Scenario: Uppercase model ID

- **WHEN** the Gemini model name is `Gemini-3.8-Flash`
- **THEN** it is refused with the same message and nothing is sent

#### Scenario: Name copied with the models/ prefix

- **WHEN** the administrator enters `models/gemini-3.8-flash` and the test passes
- **THEN** the model is saved and shown as `gemini-3.8-flash`

#### Scenario: Valid Gemini model ID

- **WHEN** the administrator enters `gemini-3.5-flash-lite`
- **THEN** the format check passes and the connection test is sent

#### Scenario: Server checks a request that skipped the page

- **WHEN** a request to add a Gemini model, or to save TFA Settings with a Gemini model name that does not match the format, reaches the server
- **THEN** the server refuses it with the same validation message and nothing is sent or saved

### Requirement: Other providers' model ID format

For OpenAI GPT and Ollama, the model name SHALL be refused only when it contains a space after trimming. Uppercase letters, `_`, `:`, `/` and `.` SHALL be allowed, so fine-tuned OpenAI IDs and Ollama tags are accepted. The validation message SHALL give that provider's example model ID.

#### Scenario: Ollama tag

- **WHEN** the administrator chooses Ollama and enters `llama3.2:3b`
- **THEN** the format check passes

#### Scenario: Fine-tuned OpenAI ID

- **WHEN** the administrator chooses OpenAI GPT and enters `ft:gpt-4o-mini:my_org:custom:abc123`
- **THEN** the format check passes

#### Scenario: Name with a space

- **WHEN** the administrator chooses OpenAI GPT and enters `GPT API Key`
- **THEN** the page shows that a model name is the provider's model ID without spaces, for example `gpt-4o-mini`, and nothing is sent

### Requirement: Malformed model name reported separately

When a provider refuses a request because the model name has an invalid format (Google Gemini: HTTP 400 `INVALID_ARGUMENT` about the model name), the failure SHALL be classified as an invalid model name, not as a missing model. Its message SHALL say that the configured name is not a valid model ID for that provider and give an example. On Test connection, the provider's own reason SHALL be added for administrators, and a chat reply SHALL show only the fixed message.

#### Scenario: Google refuses the name format

- **WHEN** a test reaches Google Gemini and it answers 400 `INVALID_ARGUMENT` "unexpected model name format"
- **THEN** the result's failure kind is `InvalidModelName`
- **AND** the message says the name is not a valid Google Gemini model ID, for example `gemini-3.8-flash`, followed by "Google Gemini said: HTTP 400 - …"
- **AND** nothing is saved

#### Scenario: Model does not exist

- **WHEN** Google Gemini answers 404 `NOT_FOUND` for a well-formed model ID
- **THEN** the failure kind stays `ModelUnavailable` and the message still says the model is not available

#### Scenario: Chat with a saved malformed name

- **WHEN** a user chats with TFA and Google refuses the saved model name format
- **THEN** the reply shows the fixed invalid-model-name message, without Google's own reason
