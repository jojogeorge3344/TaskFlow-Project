# Spec Delta

## Purpose

Lets authorized task participants see every discussion comment for a task on the Discussion screen and lets authors correct or remove their own comments.

## ADDED Requirements

### Requirement: Discussion lists the task's comments
The Discussion screen SHALL list all non-deleted comments saved for the task being viewed, oldest first, each with author and timestamp, and SHALL show a "No comments yet" state only when the task has none.

#### Scenario: Existing comments are listed
- **WHEN** an authorized user opens the Discussion screen of a task that has saved comments
- **THEN** every saved comment for that task is listed oldest first with its author and time

#### Scenario: Posted comment stays listed after reload
- **WHEN** a user posts a comment and then reloads the Discussion screen
- **THEN** the comment is still listed

#### Scenario: Comments are scoped to the task
- **WHEN** a user opens the Discussion screen of task A
- **THEN** only task A's comments are listed, not those of other tasks

#### Scenario: Empty discussion
- **WHEN** a task has no comments
- **THEN** the screen shows "No comments yet"

### Requirement: Author edits own comment
The system SHALL let a comment's author change its text from the Discussion screen; the text MUST NOT be empty, and the comment SHALL show that it was edited.

#### Scenario: Author edits comment
- **WHEN** the author saves a non-empty new text for their comment
- **THEN** the list shows the new text marked as edited

#### Scenario: Non-author cannot edit
- **WHEN** a user who did not write a comment attempts to edit it
- **THEN** the system rejects the edit and the text is unchanged

#### Scenario: Empty edit rejected
- **WHEN** the author saves an empty or whitespace-only text
- **THEN** the system rejects the edit and keeps the previous text

### Requirement: Author deletes own comment
The system SHALL let a comment's author delete it from the Discussion screen, after which it SHALL no longer be listed.

#### Scenario: Author deletes comment
- **WHEN** the author confirms deleting their comment
- **THEN** the comment disappears from the Discussion list

#### Scenario: Non-author cannot delete
- **WHEN** a user who did not write a comment attempts to delete it
- **THEN** the system rejects the request and the comment remains listed
