# tfa-document-knowledge (delta)

## MODIFIED Requirements

### Requirement: Index stays in step with the folder

The system SHALL build the index when the API starts and SHALL check the folder again at a
configured interval. Only files whose content changed SHALL be re-embedded. Chunks of files that
were deleted SHALL be removed. When the configured embedding model changes, every file SHALL be
re-embedded. A failure on one file SHALL NOT stop the other files from being indexed, and the
previous chunks of a file SHALL stay searchable until its new chunks are stored.

The indexer SHALL keep the number of texts it sends for embedding within a configured per-minute
budget, waiting between requests when needed rather than sending every file at once. When Gemini
answers an embedding request with a rate-limit or quota error, the indexer SHALL stop sending
requests and SHALL NOT send another embedding request until a cooldown ends. The cooldown SHALL be
the wait Gemini asks for when it gives one. Otherwise it SHALL be a configured default, or a longer
configured period when Gemini reports a per-day quota. Files already indexed SHALL NOT be sent
again, and the next run after the cooldown SHALL continue with the files that are not yet indexed.

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

#### Scenario: Large folder stays within the budget

- **WHEN** the indexer has 169 files to embed and the per-minute budget is smaller than their total number of chunks
- **THEN** it sends no more texts in any minute than the budget allows, and keeps going until every file is indexed without Gemini answering with a rate-limit error

#### Scenario: Gemini asks for a wait

- **WHEN** Gemini answers an embedding request with HTTP 429 and a retry delay of 40 seconds
- **THEN** the indexer sends no embedding request for 40 seconds, and then continues with the files not yet indexed

#### Scenario: Daily quota used up

- **WHEN** Gemini answers that the per-day embedding quota is exhausted
- **THEN** the indexer sends no embedding request until the configured daily cooldown has passed, and the files already indexed stay searchable

#### Scenario: Index finished, nothing changed

- **WHEN** every file is already indexed and no document changed
- **THEN** a check sends no embedding request at all

### Requirement: TFA keeps working when documents cannot be searched

When the message cannot be embedded (the embedding model cannot be reached, no Gemini key is saved,
or Gemini refuses the key or limits the request rate), the system SHALL search the documents by
keyword only, using the same keyword rules, and SHALL pass any chunks found as reference text. A
keyword match SHALL be enough on its own in this case. When the keyword search also finds nothing,
or the index is empty, TFA SHALL still answer live-data questions, and SHALL tell the user that the
documents cannot be searched right now instead of answering a documentation question from general
knowledge.

While an embedding cooldown is active, the system SHALL NOT send the message to Gemini for
embedding, and SHALL search by keyword only as above.

#### Scenario: No Gemini key saved, keyword match found

- **WHEN** no Gemini API key is saved and a user asks "what does NFR-14 say?"
- **THEN** TFA answers from the chunk containing NFR-14 and lists it as a source, and the server log has a warning that a Gemini key is needed for document search

#### Scenario: No Gemini key saved, nothing found by keyword

- **WHEN** no Gemini API key is saved and a user asks a documentation question whose terms match no chunk
- **THEN** TFA says the documents cannot be searched right now, and the server log has a warning that a Gemini key is needed for document search

#### Scenario: Gemini rate limit reached

- **WHEN** Gemini answers a query embedding with a rate-limit error and a user asks a documentation question
- **THEN** TFA answers from keyword matches if there are any, otherwise says the documents cannot be searched right now, and still answers live-data questions in the same conversation

#### Scenario: Message sent during a cooldown

- **WHEN** an embedding cooldown is active and a user asks "what validations apply when creating a task?"
- **THEN** no embedding request is sent to Gemini, and TFA answers from keyword matches with their sources

#### Scenario: Cooldown over

- **WHEN** the cooldown has ended and a user sends a message
- **THEN** the message is embedded and searched by vector and keyword again

#### Scenario: Index still building

- **WHEN** the first index has not finished and a user asks a documentation question
- **THEN** TFA answers from the chunks already stored, or says the documents are still being prepared when there are none

### Requirement: Indexing problems are logged, not shown in the UI

The system SHALL NOT offer a screen or endpoint for the document index status or for starting a
re-index. A missing documents folder, an unavailable embedding model and a file that cannot be
indexed SHALL each be written to the server log as a warning, naming the folder or file.

A rate-limit warning SHALL also say whether the limit is per minute or per day, when indexing will
resume, and how many files are indexed out of the total. It SHALL include Gemini's reason with any
API key value replaced by `[key]`, cut to at most 200 characters. One rate-limit warning SHALL be
written per cooldown, not one per refused request.

#### Scenario: No Document search card

- **WHEN** an Administrator opens TFA Settings
- **THEN** the screen shows no Document search card and no Re-index now button

#### Scenario: Old endpoints are gone

- **WHEN** any user calls `GET api/v1/assistant/documents/status` or `POST api/v1/assistant/documents/reindex`
- **THEN** the response is 404

#### Scenario: Folder missing

- **WHEN** the configured documents folder does not exist
- **THEN** the server log has a warning naming the folder, and TFA says the documents cannot be searched right now

#### Scenario: Rate limit logged with progress

- **WHEN** Gemini limits the indexer after 60 of 169 files are indexed and asks for a 40-second wait
- **THEN** the server log has one warning saying the per-minute limit was reached, that indexing resumes in about 40 seconds, and that 60 of 169 files are indexed

#### Scenario: Key never logged

- **WHEN** Gemini's rate-limit reason contains the API key that was sent
- **THEN** the warning shows `[key]` in its place
