# Spec Delta

## Purpose

Lets users see task progress at a glance as a Kanban board grouped by status, and move tasks
between statuses from the board.

## ADDED Requirements

### Requirement: Board shows tasks grouped by status

The system SHALL display tasks in four columns (To Do, In Progress, Blocked, Completed) based
on their current status, and SHALL only show tasks the user is authorized to access.

#### Scenario: Board groups tasks by status

- **WHEN** a user opens the task board
- **THEN** tasks appear in the column matching their current status and only accessible tasks are shown

#### Scenario: Board refresh reflects latest statuses

- **WHEN** a user refreshes the board
- **THEN** the board mirrors the latest persisted task statuses and changes made elsewhere are reflected

### Requirement: Task cards show summary

The system SHALL render each task card with task ID, title, assigned employee, priority,
deadline and current status, and SHALL highlight overdue tasks.

#### Scenario: Card displays summary

- **WHEN** a board is rendered
- **THEN** each task card shows the task ID, title, assigned employee, priority, deadline and current status

#### Scenario: Overdue task highlighted

- **WHEN** a task's deadline has passed and its status is neither Completed nor Cancelled
- **THEN** its card is visually flagged as overdue

### Requirement: Board-led status changes

The system SHALL allow authorized users to change a task's status from the board, persist the
change, enforce the allowed transitions, and record the change in the task activity history.

#### Scenario: Allowed transition applied

- **WHEN** an authorized user moves a task to a status reachable under the transition rules
- **THEN** the system persists the new status and records a status-change activity entry

#### Scenario: Unauthorized change rejected

- **WHEN** a user without update access attempts a status change from the board
- **THEN** the system rejects the change and the task status remains unchanged

#### Scenario: Disallowed transition rejected

- **WHEN** a user attempts a transition outside the allowed set, such as moving a To Do task directly to Completed
- **THEN** the system rejects the change and the task status remains unchanged