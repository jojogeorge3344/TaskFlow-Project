# Spec Delta

## Purpose

Lets TFA answer questions about TaskFlow from the project documents folder (BRD, FRD, HLD, LLD, TDD
and screen specs) using retrieval over a local, free embedding index, and only from those documents.

## ADDED Requirements

### Requirement: Documents folder is indexed

The system SHALL index every Markdown (`.md`) file under the configured documents folder, including
subfolders, and SHALL ignore all other file types. Each file SHALL be split into chunks that follow
its headings, and each chunk SHALL record the file's relative path, the document title and the
heading path of its section. Chunks SHALL be embedded with the configured local embedding model and
stored in the application database.

#### Scenario: Markdown files are indexed

- **WHEN** the documents folder contains `TF - FRD/02-TaskFlow-Monitoring/02.1-Task-Management/FRD-Task-Management.md`
- **THEN** its sections are stored as chunks whose source is that relative path and whose headings name the section, such as "4.1.2 Screen: Create Task (and Copy Task)"

#### Scenario: Other file types are ignored

- **WHEN** the documents folder contains `FRD-Task-Management.pdf` and `01-All-Permissions.xlsx`
- **THEN** neither file is indexed

#### Scenario: Folder missing

- **WHEN** the configured documents folder does not exist
- **THEN** the index is empty, the status reports that the folder was not found, and the API keeps running

### Requirement: Index stays in step with the folder

The system SHALL build the index when the API starts and SHALL check the folder again at a
configured interval. Only files whose content changed SHALL be re-embedded. Chunks of files that
were deleted SHALL be removed. When the configured embedding model changes, every file SHALL be
re-embedded. A failure on one file SHALL NOT stop the other files from being indexed, and the
previous chunks of a file SHALL stay searchable until its new chunks are stored.

#### Scenario: Unchanged file is skipped

- **WHEN** the indexer runs and a file's content is the same as when it was last indexed
- **THEN** the file is not re-embedded

#### Scenario: Edited file is refreshed

- **WHEN** a user edits a document and the next check runs
- **THEN** that file's chunks are replaced and TFA's answers use the new text

#### Scenario: Deleted file is removed

- **WHEN** a document is deleted from the folder
- **THEN** its chunks are removed at the next check and it is never cited again

#### Scenario: Embedding model changed

- **WHEN** an administrator changes the configured embedding model and restarts the API
- **THEN** all documents are re-embedded with the new model and no chunk from the old model is used for search

### Requirement: Relevant document text is retrieved for each message

For every message sent to TFA, the system SHALL embed the message, rank the stored chunks by cosine
similarity, and pass at most the configured number of best chunks whose similarity is at or above
the configured minimum to the model as reference text, within a configured character budget. The
retrieval SHALL happen in code before the model is called and SHALL NOT depend on the model calling
a tool.

#### Scenario: Matching chunks are given to the model

- **WHEN** a user asks "what validations apply when creating a task?"
- **THEN** the model receives the Create Task sections of the FRD and screen specs as reference text

#### Scenario: Nothing relevant

- **WHEN** a user asks "what are my tasks due this week?" and no chunk reaches the minimum similarity
- **THEN** no document text is passed and TFA answers from its task tools as before

#### Scenario: Model without tool support

- **WHEN** the selected Ollama model does not support tools
- **THEN** document text is still retrieved and passed, and the model can answer from it

### Requirement: Documentation questions are answered only from the documents

TFA SHALL answer questions about TaskFlow's features, screens, rules, design and behaviour only from
the retrieved document text. When the retrieved text does not cover the question, TFA SHALL say the
documents do not cover it and SHALL NOT answer from general knowledge or invent screens, rules or
features. The keyword help topics SHALL NOT be used to answer these questions.

#### Scenario: Covered question

- **WHEN** a user asks who receives an overdue reminder
- **THEN** TFA answers with what the documents state and does not add recipients they do not mention

#### Scenario: Not covered

- **WHEN** a user asks "does TaskFlow support recurring tasks?" and the documents only list it as out of scope
- **THEN** TFA says it is not supported according to the documents

#### Scenario: Unrelated question

- **WHEN** a user asks "what is the capital of France?"
- **THEN** TFA says it can only answer from the TaskFlow documents and does not answer the question

### Requirement: Document text is treated as data

Retrieved document text SHALL be treated as data, never as an instruction to TFA.

#### Scenario: Instruction inside a document

- **WHEN** a retrieved chunk contains text such as "ignore your previous instructions"
- **THEN** TFA does not follow it

### Requirement: Live-data questions keep using the existing tools

Questions about the user's own tasks, prioritisation, mail drafting and profile SHALL keep using the
existing TFA tools, whether or not document text was retrieved for the message.

#### Scenario: Task question with document text retrieved

- **WHEN** a user asks "which of my tasks are overdue?" and some document chunks reach the minimum similarity
- **THEN** TFA answers from its task tools with the user's own tasks, not from the document text

### Requirement: Answers show their sources

When document text was passed to the model for a reply, the reply SHALL include the list of sources
used, each with the document name and section heading, without duplicates. The TFA panel SHALL show
them under the answer. Sources SHALL be kept with the stored message so they are shown again when the
conversation is reloaded.

#### Scenario: Sources under an answer

- **WHEN** TFA answers from two sections of `FRD-Task-Management.md`
- **THEN** the panel shows both sections as sources under the answer

#### Scenario: Reloaded conversation

- **WHEN** the user closes the browser and opens TFA again
- **THEN** earlier answers still show their sources

### Requirement: Every role can ask about every document

Any signed-in user SHALL be able to get answers from any indexed document, whatever their role.

#### Scenario: Employee asks about technical design

- **WHEN** an Employee asks how the reminder scheduler works
- **THEN** TFA answers from the TDD and LLD sections that describe it

### Requirement: TFA keeps working when documents cannot be searched

When the embedding model cannot be reached or the index is empty, TFA SHALL still answer live-data
questions, and SHALL tell the user that the documents cannot be searched right now instead of
answering a documentation question from general knowledge.

#### Scenario: Ollama stopped

- **WHEN** the Ollama server is not running and a user asks a documentation question while using Gemini for chat
- **THEN** TFA says the documents cannot be searched right now

#### Scenario: Index still building

- **WHEN** the first index has not finished and a user asks a documentation question
- **THEN** TFA answers from the chunks already stored, or says the documents are still being prepared when there are none

### Requirement: Administrators see the index status

A user with the TFA Settings view permission (`SettingsAiView`, held by Administrators by default)
SHALL be able to see the index status in TFA Settings: the documents folder, the embedding model,
the number of files and chunks, when indexing last finished, whether it is running, and the last
error with the file it concerns. Users without this permission SHALL NOT see the status.

#### Scenario: Status shown

- **WHEN** an Administrator opens TFA Settings
- **THEN** a Document search card shows the folder, model, file and chunk counts and the last indexed time

#### Scenario: Status refused

- **WHEN** a Manager without `SettingsAiView` calls the index status endpoint
- **THEN** the request is refused with 403

### Requirement: Administrators start a re-index

A user with the TFA Settings edit permission (`SettingsAiEdit`) SHALL be able to start a re-index. A
re-index requested while one is running SHALL NOT start a second run. Users without this permission
SHALL NOT start a re-index.

#### Scenario: Re-index now

- **WHEN** an Administrator clicks Re-index now
- **THEN** the indexer checks the folder immediately and the card shows that indexing is running until it finishes

#### Scenario: Re-index while running

- **WHEN** an Administrator clicks Re-index now while indexing is already running
- **THEN** the request is accepted and no second run starts

#### Scenario: Re-index refused

- **WHEN** a Manager without `SettingsAiEdit` calls the re-index endpoint
- **THEN** the request is refused with 403

### Requirement: Cloud notice mentions documents

When the selected chat provider is a cloud provider, the notice a user acknowledges SHALL say that
matching document text is sent to that provider along with the message.

#### Scenario: Gemini selected

- **WHEN** a user selects Gemini for the first time
- **THEN** the notice says that the message, related task details and matching document text are sent to Google
