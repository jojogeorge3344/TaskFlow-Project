# Spec Delta

## Purpose

Lets an administrator configure which AI providers (Ollama, Google Gemini, OpenAI) TFA may use, keeps
their API keys secret, and turns provider failures and free-tier limits into clear, safe outcomes.

## ADDED Requirements

### Requirement: Supported providers

The system SHALL support three providers: Ollama (runs locally, no key), Google Gemini and OpenAI
GPT (both cloud, key required). Each provider SHALL have an enabled flag and a model name. Ollama
SHALL also have a server address. A provider that is disabled or missing required configuration
SHALL be reported as not configured.

#### Scenario: Fresh installation

- **WHEN** the provider settings are read on a new installation
- **THEN** all three providers are reported as not configured and TFA reports that no AI provider is available

#### Scenario: Cloud provider without a key

- **WHEN** an administrator enables Gemini but has not saved a key
- **THEN** Gemini is reported as not configured

### Requirement: Administrator-only configuration

Only an Administrator SHALL be able to read the provider configuration in full or change it. Any
other signed-in user SHALL be refused. Managers and Employees SHALL only be able to see which
providers are available to them.

#### Scenario: Employee tries to change providers

- **WHEN** an Employee submits a change to the provider settings
- **THEN** the request is refused with an authorization error and nothing is changed

#### Scenario: Administrator saves settings

- **WHEN** an Administrator enables Ollama with a server address and model name and saves
- **THEN** the settings are stored and Ollama is reported as configured

### Requirement: API keys are secret

An API key SHALL be stored encrypted on the server. After it is saved, the system SHALL never
return it to any client; it SHALL only report whether a key is set and, at most, its last four
characters. Keys SHALL NOT appear in logs, error messages, conversation history or responses. An
administrator SHALL be able to replace or clear a key; leaving the key field empty on save SHALL keep
the existing key.

#### Scenario: Key is not readable after save

- **WHEN** an Administrator saves an OpenAI key and then reads the provider settings
- **THEN** the response says a key is set and shows at most its last four characters, never the full key

#### Scenario: Saving without retyping the key

- **WHEN** an Administrator changes only the model name and leaves the key field empty
- **THEN** the existing key is kept

#### Scenario: Key not in logs

- **WHEN** a provider call fails with an authentication error
- **THEN** the log entry and the message shown to the user contain no part of the key

### Requirement: Provider availability for users

Any signed-in user SHALL be able to list the providers that are configured, each with a display name,
the model name and whether it is Local or Cloud. A provider that is not configured SHALL NOT be
offered or accepted.

#### Scenario: Only configured providers listed

- **WHEN** Ollama and Gemini are configured and OpenAI is not
- **THEN** a user's provider list contains Ollama (Local) and Gemini (Cloud) and not OpenAI

#### Scenario: Unconfigured provider requested

- **WHEN** a chat request names a provider that is not configured
- **THEN** the request is rejected with a clear message and nothing is sent to any provider

### Requirement: Provider errors are classified

When a provider call fails, the system SHALL classify the failure as one of: rate limit or quota
reached, invalid or missing key, request timed out, provider unreachable (including Ollama not
running), model not available (including a model that is not installed in Ollama), or an unexpected
error. The user SHALL receive a plain-language message for that class, naming the provider, and the
conversation SHALL remain usable.

#### Scenario: Free-tier limit reached

- **WHEN** Gemini answers that its free-tier rate limit or quota is exhausted
- **THEN** the user sees that Gemini's limit is reached, with a suggestion to retry later or choose another provider, and no raw provider error is shown

#### Scenario: Ollama not running

- **WHEN** the user sends a message with Ollama selected and the Ollama server cannot be reached
- **THEN** the user sees that Ollama is not running or unreachable and is told to ask an administrator to start it or to choose another provider

#### Scenario: Model not installed

- **WHEN** Ollama reports that the configured model is not installed
- **THEN** the user sees that the model is not available on Ollama and is told to ask an administrator to install it

#### Scenario: Request times out

- **WHEN** a provider does not answer within the time limit
- **THEN** the user sees a timeout message and can retry or choose another provider

### Requirement: No silent provider fallback

When the selected provider fails, the system SHALL NOT send the request to a different provider on
its own. It MAY suggest other available providers, and the user SHALL choose one explicitly.

#### Scenario: Local provider fails

- **WHEN** Ollama is selected and fails, while Gemini is also configured
- **THEN** no request is sent to Gemini unless the user selects Gemini and sends the message again

### Requirement: Provider usage is bounded

The system SHALL limit each user's chat requests per minute and cap the size of one message and the
amount of history and task data sent to a provider, so that free-tier quotas are not exhausted by one
user or one long conversation. A request over the limit SHALL be refused with a message saying when
to retry.

#### Scenario: Too many requests

- **WHEN** a user sends messages faster than the per-minute limit
- **THEN** the excess messages are refused with a retry-later message and are not sent to any provider

#### Scenario: Oversized message

- **WHEN** a user submits a message longer than the maximum length
- **THEN** it is rejected with a validation message and is not sent to any provider
