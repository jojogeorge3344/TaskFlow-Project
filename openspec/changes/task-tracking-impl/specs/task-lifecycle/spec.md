# Spec Delta

## Purpose

Manages the ongoing lifecycle of an existing task: full details, derived stage, editing by
managers, recipient visibility, and no reminders for terminal tasks.

## ADDED Requirements

### Requirement: Full task details displayed

The system SHALL display the task ID, title, description, assigned employee, creator, priority,
start date, deadline, estimated hours, current status, notification recipients and derived
stage to users with access to the task.

#### Scenario: Authorized user sees full details

- **WHEN** a user with task access opens task details
- **THEN** all listed detail fields, including notification recipients and the derived stage, are displayed

### Requirement: Derived stage indicator

The system SHALL derive a task's stage at read time from its current status and deadline —
Upcoming, Due today, Overdue, or a terminal state for Completed/Cancelled — and SHALL NOT
store the derived stage.

#### Scenario: Overdue stage

- **WHEN** a task's deadline has passed and its status is neither Completed nor Cancelled
- **THEN** the derived stage is Overdue

#### Scenario: Due today stage

- **WHEN** a task's deadline falls on the current day and is not completed
- **THEN** the derived stage is Due today

#### Scenario: Upcoming stage

- **WHEN** a task's deadline is in the future and it is not completed
- **THEN** the derived stage is Upcoming

#### Scenario: Completed stage

- **WHEN** a task is Completed or Cancelled
- **THEN** the derived stage reflects that terminal state

### Requirement: Edit task

The system SHALL allow a Manager, an Administrator, or the task's creator to edit the title,
description, priority, start date, deadline and estimated hours, applying the same validation
as creation: a deadline after the start date and a positive estimated hour count.

#### Scenario: Manager edits a non-terminal task

- **WHEN** a Manager edits a non-terminal task with valid values
- **THEN** the system saves the changes

#### Scenario: Employee cannot edit

- **WHEN** an Employee attempts to edit a task
- **THEN** the system rejects the request and the task is unchanged

#### Scenario: Invalid update rejected

- **WHEN** an edit violates validation, such as a deadline not after the start date or zero estimated hours
- **THEN** the system rejects the update and the task is unchanged

#### Scenario: Deadline or priority change recorded

- **WHEN** an edit changes a task's deadline or priority
- **THEN** the previous and new values are recorded in the task activity history

### Requirement: No reminders for terminal tasks

The system SHALL NOT generate future deadline reminders for tasks in Completed or Cancelled
status.

#### Scenario: Terminal tasks produce no reminders

- **WHEN** a task is Completed or Cancelled
- **THEN** the system does not generate future deadline reminders for that task

### Requirement: Notification recipients visible

The system SHALL display a task's notification recipients to authorized users, and SHALL allow
Managers and Administrators to update additional recipients so the assignee always remains
included.

#### Scenario: Recipients displayed

- **WHEN** a user with task access views a task
- **THEN** the task's notification recipients are displayed

#### Scenario: Manager updates additional recipients

- **WHEN** a Manager or Administrator changes the additional recipients of a task
- **THEN** the new recipient set is saved and the assignee remains included