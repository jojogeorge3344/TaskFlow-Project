# Spec Delta

## Purpose

Lets an administrator configure which AI providers and models TFA uses, add a new model only after it has been proven to work, and keep each provider's API key secret.

## ADDED Requirements

### Requirement: Add AI model entry point

The TFA Settings screen SHALL offer an "Add AI model" button that opens a separate Add AI model page. The button SHALL be shown only to users who hold both `settings.ai.edit` and `settings.ai.test`. The Model dropdown on TFA Settings SHALL NOT offer an "Add new model…" entry or an inline box for typing a new model name.

#### Scenario: Administrator opens the page

- **WHEN** a user holding `settings.ai.edit` and `settings.ai.test` clicks "Add AI model" on TFA Settings
- **THEN** the Add AI model page opens at its own address

#### Scenario: User without both permissions

- **WHEN** a user holding `settings.ai.view` but not `settings.ai.test` opens TFA Settings
- **THEN** no "Add AI model" button is shown
- **AND** opening the Add AI model page address directly is refused

#### Scenario: Dropdown has no add option

- **WHEN** an administrator opens the Model dropdown on TFA Settings
- **THEN** it lists only existing models, grouped by provider, with no "Add new model…" entry

### Requirement: Back to TFA Settings

The Add AI model page SHALL show a Back control that returns to the TFA Settings screen without saving anything typed on the page.

#### Scenario: Leaving without saving

- **WHEN** an administrator types a model name and key and then clicks Back
- **THEN** the TFA Settings screen is shown
- **AND** no model, key or provider setting has changed

### Requirement: Provider chosen first

The Add AI model page SHALL ask for the AI provider first (Google Gemini, OpenAI GPT or Ollama), and the provider SHALL be required. The fields shown after it SHALL depend on the chosen provider: Gemini and OpenAI show an API key field, and Ollama shows a server address field instead.

#### Scenario: Cloud provider fields

- **WHEN** the administrator chooses Google Gemini
- **THEN** the page shows Model name and API key, and no server address field

#### Scenario: Ollama fields

- **WHEN** the administrator chooses Ollama
- **THEN** the page shows Model name and an optional server address, and no API key field

### Requirement: Required fields on the Add AI model page

The model name SHALL be required, trimmed, and at most 100 characters. For Gemini and OpenAI, the API key SHALL be required when the provider has no readable saved key. When a readable key is saved, the field SHALL be optional, and leaving it empty keeps the saved key. A key SHALL contain no spaces and be at most 500 characters. The server address SHALL never be required.

#### Scenario: Missing model name

- **WHEN** the administrator clicks Test connection with an empty model name
- **THEN** the page shows that the model name is required and no test is sent

#### Scenario: Missing key for a provider without one

- **WHEN** OpenAI has no saved key and the administrator clicks Test connection with the API key empty
- **THEN** the page shows that the API key is required and no test is sent

#### Scenario: Provider already has a key

- **WHEN** Gemini already has a readable saved key ending in 1234
- **THEN** the API key field says a key is saved ending …1234, that leaving it empty keeps it, and that a new key replaces it for every Gemini model

#### Scenario: Ollama without an address

- **WHEN** the administrator chooses Ollama, leaves the server address empty, and tests
- **THEN** the test uses `http://localhost:11434`

#### Scenario: Unsafe Ollama address

- **WHEN** the server address is not an http or https URL, or is a link-local or cloud metadata address
- **THEN** the request is refused with a validation message and nothing is tested or saved

### Requirement: Test before save

Test connection on the Add AI model page SHALL send one small request to the chosen provider using the values typed on the page. Those values SHALL NOT be saved before the test passes. When the test fails, nothing SHALL be saved, the failure reason SHALL be shown, and the administrator SHALL stay on the page with the values kept except the API key.

#### Scenario: Wrong key

- **WHEN** the administrator enters a Gemini model and an invalid key and clicks Test connection
- **THEN** the page shows that the key was rejected
- **AND** the saved Gemini key, the model list and the provider TFA uses are unchanged

#### Scenario: Unknown model

- **WHEN** the provider reports that the model name does not exist
- **THEN** the page shows that the model was not found and nothing is saved

#### Scenario: Provider unreachable or rate limited

- **WHEN** the provider times out, cannot be reached, or reports a rate limit
- **THEN** the page shows the same reason TFA Settings shows for that failure, and nothing is saved

### Requirement: Activate the new model on success

When the test passes, the system SHALL in one step add the model to the provider's list if it is not there, save the typed key (or Ollama address) for that provider, and make that provider and model the one TFA uses, switching the other providers off without deleting their keys or models. The page SHALL then return to TFA Settings with the new model shown as in use.

#### Scenario: New Gemini model activated

- **WHEN** the administrator tests `gemini-2.5-flash-lite` on Google Gemini with a valid key and the test passes
- **THEN** TFA Settings opens showing `gemini-2.5-flash-lite` on Google Gemini as in use
- **AND** the model appears in the Model dropdown from then on
- **AND** OpenAI and Ollama are switched off with their saved keys and models kept

#### Scenario: Model already in the list

- **WHEN** the administrator tests a model name that is already listed for that provider and the test passes
- **THEN** no duplicate entry is added and that model becomes the one TFA uses

#### Scenario: Key kept when left empty

- **WHEN** Gemini has a saved key, the administrator leaves the key empty, and the test passes
- **THEN** the saved key is used for the test and stays saved unchanged

### Requirement: API key stays secret on the add flow

An API key typed on the Add AI model page SHALL be sent only to the TaskFlow server and to the chosen provider. It SHALL be stored only in encrypted form, and SHALL never be returned to a client, written to logs, or kept on the page after a test.

#### Scenario: Response after adding

- **WHEN** a model is added with a new key
- **THEN** the response shows only that a key is set and its last four characters

#### Scenario: Failed test clears the key field

- **WHEN** a test fails
- **THEN** the API key field on the page is emptied
