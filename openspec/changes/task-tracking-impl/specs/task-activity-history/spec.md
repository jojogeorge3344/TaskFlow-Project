# Spec Delta

## Purpose

Maintains an immutable, chronological audit trail of important task changes that authorized
users can filter and view but never alter.

## ADDED Requirements

### Requirement: Activity events are recorded

The system SHALL record an immutable activity entry for task creation, assignment,
reassignment, status changes, deadline changes, priority changes, completion and relevant
administrative actions, each with the acting user, a timestamp, and previous/new values where
applicable.

#### Scenario: Creation recorded

- **WHEN** a task is created
- **THEN** an activity entry records the creation and the creator

#### Scenario: Assignment recorded

- **WHEN** a task is assigned or reassigned
- **THEN** an activity entry records the previous and new assignee and the acting user

#### Scenario: Status change recorded

- **WHEN** a task's status changes
- **THEN** an activity entry records the previous and new status

#### Scenario: Deadline or priority change recorded

- **WHEN** a task's deadline or priority changes
- **THEN** an activity entry records the previous and new value

#### Scenario: Completion recorded

- **WHEN** a task transitions to Completed
- **THEN** an activity entry records the completion

#### Scenario: Administrative action recorded

- **WHEN** an administrator performs an action that affects a task
- **THEN** an activity entry records the action and the acting administrator

### Requirement: History is chronological and immutable

The system SHALL display activity entries in chronological order with actor, description,
previous/new values and timestamp, and SHALL prevent ordinary users from modifying or deleting
activity records.

#### Scenario: Chronological display

- **WHEN** a user opens a task's activity history
- **THEN** entries appear oldest first with the actor, description, values and timestamp

#### Scenario: Modification or deletion rejected

- **WHEN** an employee or manager attempts to modify or delete an activity entry
- **THEN** the system rejects the request and the entry is retained

### Requirement: Activity filtering

The system SHALL allow authorized users to filter activity history by task, event type, user,
date range or search terms.

#### Scenario: Filter applied

- **WHEN** a user applies an activity filter
- **THEN** only the matching activity entries are shown