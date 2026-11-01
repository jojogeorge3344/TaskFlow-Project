# Spec Delta

## Purpose

Lets an administrator connect AI providers other than the built-in Google Gemini, OpenAI GPT and Ollama, by naming them and giving an OpenAI-compatible or Ollama server address, and move models between providers.

## ADDED Requirements

### Requirement: Supported provider types

The system SHALL support three API types: OpenAI-compatible (chat completions at a server address), Google Gemini, and Ollama. Google Gemini, OpenAI GPT and Ollama SHALL exist as built-in providers. Any number of custom providers of type OpenAI-compatible or Ollama SHALL be allowed; a custom provider of type Google Gemini SHALL NOT be allowed.

#### Scenario: Built-in providers after upgrade

- **WHEN** the application starts on a database created before this change
- **THEN** Google Gemini, OpenAI GPT and Ollama exist as built-in providers with their saved keys, models, saved model and enabled flag unchanged
- **AND** earlier chat messages still show the provider that answered them

#### Scenario: Gemini type refused for a custom provider

- **WHEN** a request creates a custom provider of type Google Gemini
- **THEN** it is refused with "A custom provider can use the OpenAI-compatible or Ollama API." and nothing is saved

### Requirement: Fields for a new provider

When the AI provider typed on Add AI model matches no existing provider (ignoring case and surrounding spaces), the screen SHALL mark it "New provider" and show API type (OpenAI-compatible or Ollama), Server address (required, http or https), Runs (Local or Cloud, default Cloud) and API key (optional, at most 500 characters, no spaces). The provider name SHALL be 2 to 40 characters.

#### Scenario: Missing address

- **WHEN** a new provider name is typed and Test connection is clicked with the server address empty
- **THEN** the screen shows "Enter the server address." and nothing is sent

### Requirement: Custom provider created only after a passing test

Test connection SHALL test a new provider's typed values without saving. Only when it passes SHALL the system create the provider, add the model, save the key encrypted, and make that model the one TFA uses.

#### Scenario: Groq through the OpenAI-compatible type

- **WHEN** an administrator types provider "Groq", chooses OpenAI-compatible, enters `https://api.groq.com/openai/v1`, model `llama-3.3-70b-versatile` and a key, and the test passes
- **THEN** TFA Settings lists a Groq group (Cloud, key ending shown) with that model marked In use

#### Scenario: Local server without a key

- **WHEN** an administrator creates "LM Studio" (OpenAI-compatible, `http://localhost:1234/v1`, Runs Local, no key) and the test passes
- **THEN** the provider is created as Local with no key, and requests to it carry no Authorization header

#### Scenario: Failed test creates nothing

- **WHEN** the test of a new provider fails
- **THEN** no provider and no model is created, the reason and "Nothing was saved." are shown, and the key field is emptied

### Requirement: Existing provider typed by name

When the typed AI provider matches an existing provider (ignoring case), the screen SHALL behave as for that provider: it SHALL show its own key or address field and SHALL NOT show API type, Runs or a second address field. The textbox SHALL suggest existing provider names while typing.

#### Scenario: Typing a built-in name

- **WHEN** an administrator types "google gemini"
- **THEN** the screen treats it as the built-in Google Gemini provider and shows the API key field with the saved-key hint

#### Scenario: Suggestions

- **WHEN** an administrator types "gr" and a custom provider "Groq" exists
- **THEN** "Groq" is suggested, with Google Gemini, OpenAI GPT and Ollama available too

### Requirement: Custom provider server address is safe

Every provider server address SHALL be an absolute http or https URL and SHALL NOT be a link-local or cloud-metadata address (for example 169.254.169.254 or metadata.google.internal). A private or localhost address SHALL be allowed.

#### Scenario: Metadata address refused

- **WHEN** a custom provider is created with address `http://169.254.169.254/v1`
- **THEN** it is refused with "The server address must be a valid http or https URL, and not a link-local or cloud metadata address." and nothing is sent

### Requirement: Custom provider in the model list

TFA Settings SHALL list each custom provider as its own group after the built-in ones, showing its name, Local or Cloud, its server address and key status. Its models SHALL count as added models: they offer Use this model, Test connection, Edit and Delete under the same rules as other added models. When a custom provider's last model is deleted or moved away, the custom provider SHALL be removed with its key. Built-in providers SHALL never be removed.

#### Scenario: Last model deleted

- **WHEN** "Groq" has one model, it is not in use, and the administrator deletes it
- **THEN** the Groq group no longer appears and its key is no longer stored

#### Scenario: Custom provider's model in use

- **WHEN** TFA uses a Groq model
- **THEN** the line above the list says "TFA uses `<model>` on Groq." and the TFA window offers Groq as its provider

### Requirement: Cloud notice covers custom providers

A custom provider marked Cloud SHALL be treated like Gemini and OpenAI for the privacy notice: TFA Settings and the TFA window SHALL say that the question, related task details and matching document text are sent to it. A custom provider marked Local SHALL be treated like Ollama.

#### Scenario: Cloud custom provider in the TFA window

- **WHEN** TFA uses a model on a Cloud custom provider
- **THEN** the TFA window shows the same "sent to a cloud service" notice it shows for Gemini, naming that provider
