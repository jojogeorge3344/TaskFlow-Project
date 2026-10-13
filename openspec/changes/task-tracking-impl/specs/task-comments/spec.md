# Spec Delta

## Purpose

Lets authorized task participants add and view an immutable, chronological discussion with
comments that must not be empty.

## ADDED Requirements

### Requirement: Add task comment

The system SHALL allow authorized task participants to add comments to a task, and SHALL store
each comment with its author and timestamp; empty or whitespace-only comments SHALL be
rejected.

#### Scenario: Participant adds a comment

- **WHEN** an authorized participant submits a non-empty comment
- **THEN** the system stores the comment with the author and a timestamp

#### Scenario: Empty comment rejected

- **WHEN** a participant submits an empty or whitespace-only comment
- **THEN** the system rejects the comment and does not store it

### Requirement: View task discussion

The system SHALL display a task's comments in chronological order to authorized participants
and SHALL preserve all previously submitted comments.

#### Scenario: Comments listed oldest first

- **WHEN** an authorized user views a task discussion
- **THEN** comments are shown oldest first with their author and timestamp

#### Scenario: Previous comments preserved

- **WHEN** new comments are added to a task
- **THEN** all previously existing comments remain unchanged and visible

### Requirement: Comments are immutable

The system SHALL treat stored comments as immutable so that participants cannot edit or delete
a comment after submission.

#### Scenario: Edit or delete rejected

- **WHEN** a user attempts to edit or delete an existing comment
- **THEN** the system rejects the request and the original comment is retained