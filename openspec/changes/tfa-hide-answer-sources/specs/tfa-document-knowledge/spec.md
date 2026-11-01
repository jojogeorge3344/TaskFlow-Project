# tfa-document-knowledge (delta)

## MODIFIED Requirements

### Requirement: Answers show their sources

When document text was passed to the model for a reply, the reply SHALL include the list of sources
used, each with the document name and section heading, without duplicates. Sources SHALL be kept
with the stored message. The TFA panel SHALL NOT show the sources to any user: no Sources label,
list or document chips appear under an answer, whether it is new or loaded from an earlier
conversation.

#### Scenario: Answer from documents shows no sources

- **WHEN** TFA answers "what validations apply when creating a task?" from two sections of the Task Management BRD
- **THEN** the panel shows the answer text only, with no Sources row under it

#### Scenario: Sources still recorded

- **WHEN** TFA answers from document text
- **THEN** the reply returned by `POST api/v1/assistant/conversation/messages` still carries the `sources` list, and the stored message keeps it

#### Scenario: Reloaded conversation

- **WHEN** the user closes the browser and opens TFA again
- **THEN** earlier answers are shown without sources, including answers stored before this change

#### Scenario: Administrator

- **WHEN** an Administrator asks a documentation question
- **THEN** the panel shows no sources, the same as for every other role
