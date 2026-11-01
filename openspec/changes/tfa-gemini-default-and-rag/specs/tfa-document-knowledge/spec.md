# Spec Delta

## MODIFIED Requirements

### Requirement: Documents folder is indexed

The system SHALL index every Markdown (`.md`) file under the configured documents folder, including
subfolders, and SHALL ignore all other file types. Each file SHALL be split into chunks that follow
its headings, and each chunk SHALL record the file's relative path, the document title and the
heading path of its section. Chunks SHALL be embedded with the configured Google Gemini embedding
model, using the Gemini API key saved for the Gemini provider, whichever chat provider is selected,
and stored in the application database.

#### Scenario: Markdown files are indexed

- **WHEN** the documents folder contains `TF - FRD/02-TaskFlow-Monitoring/02.1-Task-Management/FRD-Task-Management.md`
- **THEN** its sections are stored as chunks whose source is that relative path and whose headings name the section, such as "4.1.2 Screen: Create Task (and Copy Task)"

#### Scenario: Other file types are ignored

- **WHEN** the documents folder contains `FRD-Task-Management.pdf` and `01-All-Permissions.xlsx`
- **THEN** neither file is indexed

#### Scenario: Folder missing

- **WHEN** the configured documents folder does not exist
- **THEN** the index is empty, the status reports that the folder was not found, and the API keeps running

#### Scenario: Embedded with Gemini while Ollama is the chat provider

- **WHEN** Ollama is the selected chat provider and a Gemini key is saved
- **THEN** documents are still embedded with the Gemini embedding model, and Ollama is not called for embeddings

#### Scenario: Old local index replaced

- **WHEN** the application starts after this change with chunks embedded by `nomic-embed-text`
- **THEN** every document is re-embedded with the Gemini embedding model and the old chunks are no longer used for search

### Requirement: TFA keeps working when documents cannot be searched

When the embedding model cannot be reached, no Gemini key is saved, Gemini refuses the key or limits
the request rate, or the index is empty, TFA SHALL still answer live-data questions, and SHALL tell the
user that the documents cannot be searched right now instead of answering a documentation question
from general knowledge.

#### Scenario: No Gemini key saved

- **WHEN** no Gemini API key is saved and a user asks a documentation question
- **THEN** TFA says the documents cannot be searched right now, and the server log has a warning that a Gemini key is needed for document search

#### Scenario: Gemini rate limit reached

- **WHEN** Gemini answers a query embedding with a rate-limit error and a user asks a documentation question
- **THEN** TFA says the documents cannot be searched right now and still answers live-data questions in the same conversation

#### Scenario: Index still building

- **WHEN** the first index has not finished and a user asks a documentation question
- **THEN** TFA answers from the chunks already stored, or says the documents are still being prepared when there are none

### Requirement: Cloud notice mentions documents

When the selected chat provider is a cloud provider, the notice a user acknowledges SHALL say that
matching document text is sent to that provider along with the message. When the selected chat
provider is local, the notice SHALL say that the message is sent to Google to search the TaskFlow
documents, while the reply is generated locally.

#### Scenario: Gemini selected

- **WHEN** a user selects Gemini for the first time
- **THEN** the notice says that the message, related task details and matching document text are sent to Google

#### Scenario: Ollama selected

- **WHEN** a user selects Ollama for the first time and sends a message
- **THEN** the panel first says that the message is sent to Google to search the TaskFlow documents and proceeds only after the user acknowledges it

## ADDED Requirements

### Requirement: Document embedding sends text to Google

Document text SHALL be sent to Google only to be embedded, and the text of each message SHALL be sent
to Google only to search the documents; for a short follow-up the previous user message MAY be added.
Embedding requests SHALL carry the API key in a request header, never in the URL, and SHALL NOT include
task details, user details or TFA's replies.

#### Scenario: Message embedded for search

- **WHEN** a user sends "what validations apply when creating a task?"
- **THEN** only that message text is sent to the Gemini embedding model, with no task, user or reply data

#### Scenario: Follow-up adds only the previous question

- **WHEN** a user asks "who receives the overdue reminder?" and then "and for managers?"
- **THEN** the follow-up search may send both user messages, and never TFA's reply

#### Scenario: Key not in the URL

- **WHEN** documents are embedded
- **THEN** the request URL contains no API key and no log entry contains the key
