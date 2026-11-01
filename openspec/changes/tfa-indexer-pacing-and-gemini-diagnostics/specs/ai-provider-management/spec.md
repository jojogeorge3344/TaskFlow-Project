# ai-provider-management (delta)

## MODIFIED Requirements

### Requirement: Provider errors are classified

When a provider call fails, the system SHALL classify the failure as one of: rate limit or quota
reached, invalid or missing key, request timed out, provider unreachable (including Ollama not
running), model not available (including a model that is not installed in Ollama), provider busy
(the provider reports an internal error or that it is overloaded or temporarily unavailable), or an
unexpected error. The user SHALL receive a plain-language message for that class, naming the
provider, and the conversation SHALL remain usable.

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

#### Scenario: Gemini overloaded

- **WHEN** Gemini answers HTTP 503 with status `UNAVAILABLE`, or HTTP 500 with status `INTERNAL`
- **THEN** the user sees "Google Gemini is busy right now. Retry in a moment, or choose another model." and not the unexpected-error message

#### Scenario: Other provider busy

- **WHEN** OpenAI or Ollama answers with an HTTP 5xx status
- **THEN** the user sees that the provider is busy right now, with a suggestion to retry or choose another model

### Requirement: Test connection shows the provider's reason for unexpected errors

When Test connection fails with an error TaskFlow does not classify, the message SHALL add the
provider's HTTP status and its error reason, at most 200 characters, with any API key value replaced
by `[key]`. Chat replies to users SHALL keep the plain message without the provider's reason. The
warning written to the server log SHALL include the same status and reason.

A failed chat call SHALL also write a server-log warning with the provider, the failure class, the
HTTP status and the provider's reason, under the same 200-character and `[key]` rules. The warning
SHALL NOT include the prompt, the conversation or the documents sent. When Gemini answers HTTP 200
with no text and no function call, the reason SHALL be Gemini's finish reason for the first
candidate, or its prompt block reason when there is no candidate.

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

#### Scenario: Chat failure logs the reason

- **WHEN** a user's TFA message fails because Gemini answers HTTP 400 `INVALID_ARGUMENT` with a reason
- **THEN** the server log has a warning naming Google Gemini, the class `Unexpected`, HTTP status 400 and `INVALID_ARGUMENT: <reason>`

#### Scenario: Empty Gemini answer logs why

- **WHEN** Gemini answers HTTP 200 for a chat message, with no text and no function call and a finish reason of `MALFORMED_FUNCTION_CALL`
- **THEN** the user sees the unexpected-error message, and the server log warning includes `MALFORMED_FUNCTION_CALL`

#### Scenario: Blocked prompt logs why

- **WHEN** Gemini answers HTTP 200 with no candidates and a prompt block reason of `SAFETY`
- **THEN** the server log warning includes `SAFETY`
