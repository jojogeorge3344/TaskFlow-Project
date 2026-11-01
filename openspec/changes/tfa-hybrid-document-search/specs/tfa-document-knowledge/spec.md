# tfa-document-knowledge (delta)

## MODIFIED Requirements

### Requirement: Relevant document text is retrieved for each message

For every message sent to TFA, the system SHALL search the stored chunks in two ways: by cosine
similarity of the message embedding, and by a full-text keyword search of the chunk headings and
text. The system SHALL merge the two ranked lists into one ranking and pass at most the configured
number of best chunks to the model as reference text, within a configured character budget.

A chunk SHALL be eligible only when at least one of the following holds:
- its similarity is at or above the configured minimum similarity;
- it matches the keyword search and its similarity is at or above the configured keyword-assist
  minimum (lower than the minimum similarity);
- it matches the keyword search for an exact identifier in the message. An exact identifier is a
  term that contains a digit, a hyphen, an underscore, a dot, or a capital letter after its first
  character (for example `NFR-14`, `TimeoutSeconds`, `gemini-3.8-flash`).

A keyword match SHALL require every significant term of the message (ignoring common stop words) to
appear in the chunk. Words in the message SHALL be matched as plain words and SHALL NOT be read as
search operators. Hybrid search SHALL be switchable off by configuration, in which case retrieval is
by similarity only. The retrieval SHALL happen in code before the model is called and SHALL NOT
depend on the model calling a tool.

#### Scenario: Matching chunks are given to the model

- **WHEN** a user asks "what validations apply when creating a task?"
- **THEN** the model receives the Create Task sections of the FRD and screen specs as reference text

#### Scenario: Exact identifier is found by keyword

- **WHEN** a user asks "what does NFR-14 say?" and the chunk containing NFR-14 is below the minimum similarity
- **THEN** that chunk is passed to the model as reference text and is listed as a source

#### Scenario: Keyword match lifts a borderline chunk

- **WHEN** a chunk contains every significant term of the message and its similarity is between the keyword-assist minimum and the minimum similarity
- **THEN** that chunk is eligible and is ranked by the merged ranking

#### Scenario: Keyword match alone is not enough for ordinary words

- **WHEN** a message has no exact identifier and a chunk matches its words but is below the keyword-assist minimum similarity
- **THEN** that chunk is not passed to the model

#### Scenario: Nothing relevant

- **WHEN** a user asks "what are my tasks due this week?" and no chunk is eligible
- **THEN** no document text is passed and TFA answers from its task tools as before

#### Scenario: Search operators in the message

- **WHEN** a user asks `what is "AND" OR NOT NEAR(task)?`
- **THEN** the search completes without an error and the words are matched as plain words

#### Scenario: Hybrid search switched off

- **WHEN** hybrid search is switched off in configuration
- **THEN** chunks are chosen by similarity only, exactly as before this change

#### Scenario: Model without tool support

- **WHEN** the selected Ollama model does not support tools
- **THEN** document text is still retrieved and passed, and the model can answer from it

### Requirement: TFA keeps working when documents cannot be searched

When the message cannot be embedded (the embedding model cannot be reached, no Gemini key is saved,
or Gemini refuses the key or limits the request rate), the system SHALL search the documents by
keyword only, using the same keyword rules, and SHALL pass any chunks found as reference text. A
keyword match SHALL be enough on its own in this case. When the keyword search also finds nothing,
or the index is empty, TFA SHALL still answer live-data questions, and SHALL tell the user that the
documents cannot be searched right now instead of answering a documentation question from general
knowledge.

#### Scenario: No Gemini key saved, keyword match found

- **WHEN** no Gemini API key is saved and a user asks "what does NFR-14 say?"
- **THEN** TFA answers from the chunk containing NFR-14 and lists it as a source, and the server log has a warning that a Gemini key is needed for document search

#### Scenario: No Gemini key saved, nothing found by keyword

- **WHEN** no Gemini API key is saved and a user asks a documentation question whose terms match no chunk
- **THEN** TFA says the documents cannot be searched right now, and the server log has a warning that a Gemini key is needed for document search

#### Scenario: Gemini rate limit reached

- **WHEN** Gemini answers a query embedding with a rate-limit error and a user asks a documentation question
- **THEN** TFA answers from keyword matches if there are any, otherwise says the documents cannot be searched right now, and still answers live-data questions in the same conversation

#### Scenario: Index still building

- **WHEN** the first index has not finished and a user asks a documentation question
- **THEN** TFA answers from the chunks already stored, or says the documents are still being prepared when there are none
