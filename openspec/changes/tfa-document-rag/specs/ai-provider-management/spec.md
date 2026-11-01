# Spec Delta

## Purpose

Makes a wrong model name or an unrecognised provider error on TFA Settings easy to diagnose, found
while setting up Google Gemini.

## ADDED Requirements

### Requirement: Model names are model IDs

A model name SHALL contain no whitespace, on the Add AI model page and when TFA Settings are saved.
The Add AI model page SHALL show an example model ID for the chosen provider (`gemini-3.8-flash` for
Google Gemini, `gpt-4o-mini` for OpenAI, `llama3.2:3b` for Ollama). For Google Gemini it SHALL also
say that the key comes from Google AI Studio, without judging the key by its prefix.

#### Scenario: Label typed as the model name

- **WHEN** an administrator enters `Gemini API Key` as the model name and clicks Test connection
- **THEN** the page says a model name is the provider's model ID without spaces, for example `gemini-3.8-flash`
- **AND** no test is sent and nothing is saved

#### Scenario: Example shown

- **WHEN** an administrator chooses Google Gemini on the Add AI model page
- **THEN** the model name field shows `gemini-3.8-flash` as its example and the key field says where to get a Gemini key

### Requirement: Gemini model-name errors are reported as model not available

When Google Gemini refuses a request because of the model name (HTTP 400 `INVALID_ARGUMENT` whose
message is about the model), the system SHALL report that the model is not available, not an
unexpected error. A refusal that is about the API key SHALL still be reported as a key problem.

#### Scenario: Malformed Gemini model name

- **WHEN** Gemini answers HTTP 400 `INVALID_ARGUMENT` with "unexpected model name format"
- **THEN** the message says the configured model is not available on Google Gemini

#### Scenario: Invalid key still reported as a key problem

- **WHEN** Gemini answers HTTP 400 `INVALID_ARGUMENT` with "API key not valid"
- **THEN** the message says Google Gemini did not accept the API key

### Requirement: Test connection shows the provider's reason for unexpected errors

When Test connection fails with an error TaskFlow does not classify, the message SHALL add the
provider's HTTP status and its error reason, at most 200 characters, with any API key value replaced
by `[key]`. Chat replies to users SHALL keep the plain message without the provider's reason. The
warning written to the server log SHALL include the same status and reason.

#### Scenario: Unclassified Gemini error on Test connection

- **WHEN** an administrator tests a Gemini model and Gemini answers HTTP 400 with a reason TaskFlow does not classify
- **THEN** the message says Google Gemini returned an unexpected error and adds "Google Gemini said: HTTP 400 - <reason>"
- **AND** nothing is saved

#### Scenario: Key never echoed

- **WHEN** the provider's reason contains the API key that was sent
- **THEN** the message and the log show `[key]` in its place

#### Scenario: Chat keeps the plain message

- **WHEN** a user's TFA message fails with an unclassified provider error
- **THEN** the reply says the provider returned an unexpected error and does not include the provider's reason
