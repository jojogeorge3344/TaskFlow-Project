# Spec Delta

## Purpose

Lets an administrator configure which AI providers and models TFA uses, add a new model only after it has been proven to work, and keep each provider's API key secret.

## ADDED Requirements

### Requirement: TFA Settings model list

TFA Settings SHALL show every model of every provider as a read-only list, grouped by provider, with no Model dropdown, no Server dropdown, no key or address input and no Save button. Each row SHALL show the model ID, the provider, Local or Cloud, the provider's key status (set with its last four characters, or not set) or Ollama address, "Built-in" or "Added by you", and whether TFA uses it.

#### Scenario: Administrator opens TFA Settings

- **WHEN** a user holding `settings.ai.view` opens TFA Settings
- **THEN** each model is listed in a row with its provider, Local or Cloud, key status or address, origin and status
- **AND** no dropdown, key field, address field or Save TFA settings button is shown

#### Scenario: Key shared by a provider's models

- **WHEN** Google Gemini has a saved key ending in 1234
- **THEN** every Gemini row shows the key as set, ending …1234

#### Scenario: Model in use

- **WHEN** TFA uses `gemini-3.8-flash`
- **THEN** that row is marked "In use" and no other row is

#### Scenario: Narrow screen

- **WHEN** TFA Settings is shown at phone width
- **THEN** each row is shown as a stacked card with the same details and actions, without horizontal scrolling

### Requirement: Use a model from the list

Each row not in use SHALL offer **Use this model**, shown to users holding `settings.ai.edit`. Clicking it SHALL make that provider and model the ones TFA uses in one step, switch the other providers off without deleting their keys or models, and refresh the list. The action SHALL be disabled with a hint when the provider has no readable key (Gemini, OpenAI); the server SHALL refuse it then too.

#### Scenario: Switch models

- **WHEN** an administrator clicks Use this model on `gemini-3.5-flash` while TFA uses `llama3.2:3b`
- **THEN** TFA uses `gemini-3.5-flash` on Google Gemini, the list marks it In use, and Ollama is switched off with its address kept

#### Scenario: Provider without a key

- **WHEN** OpenAI GPT has no saved key
- **THEN** Use this model on its rows is disabled with the hint "No key saved for OpenAI GPT yet. Add it on the Add AI model screen."
- **AND** a request to use an OpenAI model is refused and nothing changes

#### Scenario: User without edit permission

- **WHEN** a user holds `settings.ai.view` but not `settings.ai.edit`
- **THEN** no Use this model action is shown, and a request to use a model is refused

### Requirement: Test a model from the list

Each row SHALL offer **Test connection** to users holding `settings.ai.test`. It SHALL send one small request to that row's provider with that row's model and the saved key or address, save nothing, and show the result on that row with the same failure messages as Add AI model.

#### Scenario: Test a model that is not in use

- **WHEN** an administrator tests `gemini-3.5-flash` while TFA uses another model
- **THEN** the request uses model `gemini-3.5-flash` with the saved Gemini key, and the row shows "The connection works." or the failure reason
- **AND** which model TFA uses is unchanged

### Requirement: Built-in models are read-only

Models that come with TaskFlow SHALL show no Edit or Delete action, and requests to edit or delete one SHALL be refused with "A built-in model cannot be changed or removed."

#### Scenario: Built-in row

- **WHEN** the list shows `gemini-3.8-flash`, a built-in model
- **THEN** its row shows Use this model (unless in use) and Test connection, but no Edit or Delete

### Requirement: Edit an added model

Rows of models an administrator added SHALL offer **Edit** to users holding `settings.ai.edit` and `settings.ai.test`. Edit SHALL open the Add AI model screen in edit mode at its own address, with the provider fixed and shown, the model name filled in, and an optional new API key (Gemini, OpenAI) or server address (Ollama). The key field SHALL say that a new key replaces it for every model of that provider. Edit mode SHALL also offer Remove the key.

#### Scenario: Open Edit

- **WHEN** an administrator clicks Edit on `gemini-2.5-pro`, which they added
- **THEN** the edit screen opens with Google Gemini fixed, `gemini-2.5-pro` in the model name, the key field empty with "A key is saved (ends …1234). Leave the box empty to keep it. A new key replaces it for every Google Gemini model."

### Requirement: Edit saves only after a passing test

On the edit screen, Test connection SHALL test the typed values without saving. Only when the test passes SHALL the system rename the model if the name changed, save a typed key or address for the provider, and return to TFA Settings. Editing SHALL NOT change which model TFA uses. A failed test SHALL save nothing and empty the key field. The model ID format rules of Add AI model SHALL apply.

#### Scenario: Rename an added model

- **WHEN** an administrator changes `gemini-2.5-pro` to `gemini-2.5-flash` and the test passes
- **THEN** the list shows `gemini-2.5-flash` instead of `gemini-2.5-pro`, and the model TFA uses is unchanged

#### Scenario: Rename to a name already listed

- **WHEN** the new name is already listed for that provider
- **THEN** the edit is refused with "That model is already in the list." and nothing is tested or saved

#### Scenario: Failed test

- **WHEN** the test fails
- **THEN** the screen shows the reason and "Nothing was saved.", stays open with the typed name, and empties the key field

#### Scenario: Remove the key

- **WHEN** an administrator clicks Remove the key on the edit screen and confirms
- **THEN** the provider's key is removed without a test being sent, every model of that provider shows the key as not set, and the screen returns to TFA Settings
- **AND** if TFA uses that provider, the confirmation first says that TFA will be unavailable until a key is added

### Requirement: Model in use cannot be edited or deleted

When an administrator tries to edit or delete the model TFA uses, the system SHALL NOT edit or delete it and SHALL show a message such as "gemini-2.5-pro is in use by TFA. Switch TFA to another model first, then delete it." (with "edit" for Edit). The server SHALL refuse such a request with the same message.

#### Scenario: Delete the model in use

- **WHEN** an administrator clicks Delete on the row marked In use
- **THEN** a message says the model is in use by TFA and must be switched first, with only an OK button, and the model stays in the list

#### Scenario: Edit the model in use

- **WHEN** an administrator clicks Edit on the row marked In use
- **THEN** the same kind of message is shown and the edit screen does not open

#### Scenario: Request that skipped the screen

- **WHEN** a request to delete or edit the model TFA uses reaches the server
- **THEN** it is refused with the in-use message and nothing changes

### Requirement: Delete an added model

Rows of models an administrator added and that TFA does not use SHALL offer **Delete** to users holding `settings.ai.model.remove`. Delete SHALL ask for confirmation and then remove the model from the list. If a switched-off provider still has that model saved as its model, that provider's model SHALL be reset to its default model.

#### Scenario: Confirm delete

- **WHEN** an administrator clicks Delete on `gemini-2.5-pro`, which is not in use, and confirms
- **THEN** `gemini-2.5-pro` is no longer listed and no other model changes

#### Scenario: Cancel delete

- **WHEN** the administrator cancels the confirmation
- **THEN** nothing is removed

#### Scenario: Model saved for a switched-off provider

- **WHEN** Gemini is switched off with `gemini-2.5-pro` saved as its model, and that model is deleted
- **THEN** Gemini's saved model becomes `gemini-3.8-flash` and Gemini stays switched off
