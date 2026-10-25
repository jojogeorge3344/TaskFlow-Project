# Spec Delta

## Purpose

The system SHALL make discussion comments visible on the Edit Task screen in the expected location with a proper empty state.

## ADDED Requirements

### Requirement: Display comments on Edit Task screen
The system SHALL display comments posted via Discussion on the Edit Task screen above/beside the tab context.

#### Scenario: Comments exist
- **WHEN** a task has discussion comments
- **THEN** the Edit Task screen lists them in the discussion/comments area

#### Scenario: No comments
- **WHEN** a task has no discussion comments
- **THEN** the Edit Task screen shows "No comments yet"
