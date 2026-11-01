# Spec Delta

## MODIFIED Requirements

### Requirement: Supported providers

The system SHALL support three providers: Ollama (runs locally, no key), Google Gemini and OpenAI
GPT (both cloud, key required). Each provider SHALL have an enabled flag and a model name. Ollama
SHALL also have a server address. A provider that is disabled or missing required configuration
SHALL be reported as not configured. Google Gemini SHALL be the default provider: it SHALL be listed
first, and on a new installation it is the only provider that can be enabled without an administrator.
Ollama SHALL NOT be enabled on a new installation.

#### Scenario: Fresh installation with a Gemini key configured

- **WHEN** the application starts for the first time with a Gemini API key supplied in its configuration
- **THEN** Google Gemini is enabled with its default model and reported as configured, and Ollama and OpenAI are reported as not configured

#### Scenario: Fresh installation without a Gemini key

- **WHEN** the application starts for the first time without a Gemini API key in its configuration
- **THEN** all three providers are reported as not configured and TFA reports that no AI provider is available

#### Scenario: Cloud provider without a key

- **WHEN** an administrator enables Gemini but has not saved a key
- **THEN** Gemini is reported as not configured

#### Scenario: Ollama remains optional

- **WHEN** an Administrator enables Ollama with a server address and model name in TFA Settings and saves
- **THEN** Ollama is reported as configured and can be selected for chat

## ADDED Requirements

### Requirement: Configured Gemini key is seeded once

The system SHALL read an optional Gemini API key from the application configuration (user secrets or
an environment variable, never a file in the repository). When the Gemini provider has no saved key,
the system SHALL store the configured key encrypted, as TFA Settings does, and enable Gemini only on a
new installation. A key saved in TFA Settings SHALL NOT be overwritten, and the configured key SHALL
NOT appear in logs.

#### Scenario: Key applied on first run

- **WHEN** a new installation starts with `AiProviders:GeminiApiKey` set
- **THEN** the Gemini provider has a key set, shows at most its last four characters, and is enabled

#### Scenario: Saved key wins

- **WHEN** an Administrator has saved a Gemini key in TFA Settings and the application restarts with a different configured key
- **THEN** the saved key is kept and the configured key is ignored

#### Scenario: Existing installation not switched over

- **WHEN** an existing installation that uses Ollama for chat restarts with a configured Gemini key and no saved Gemini key
- **THEN** the Gemini key is stored, but Ollama stays the enabled chat provider until an Administrator changes it

#### Scenario: Administrator disables Gemini

- **WHEN** an Administrator disables Gemini in TFA Settings and the application restarts with the key still configured
- **THEN** Gemini stays disabled

#### Scenario: Key not logged

- **WHEN** the configured key is stored at startup
- **THEN** no log entry contains any part of the key
