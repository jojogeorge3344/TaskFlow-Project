# Spec Delta

## Purpose

Lets managers transfer a task to another employee with a reason and confirmation, preserving
history and updating notification recipients.

## ADDED Requirements

### Requirement: Manager reassigns a task

The system SHALL allow a Manager or Administrator to reassign a task to any active employee,
require an explicit confirmation showing current and new assignee, and require a reassignment
reason.

#### Scenario: Confirmed reassignment succeeds

- **WHEN** a Manager confirms a reassignment with a reason and a valid active new assignee
- **THEN** the task's assignee is changed to the new employee

#### Scenario: Employee cannot reassign

- **WHEN** an Employee attempts to reassign a task
- **THEN** the system rejects the request and the assignee is unchanged

#### Scenario: Missing reason rejected

- **WHEN** a reassignment is submitted without a reason
- **THEN** the system rejects the request and the assignee is unchanged

#### Scenario: Reassignment cancelled

- **WHEN** a Manager cancels the reassignment confirmation
- **THEN** the task retains its current assignee

### Requirement: Reassignment history preserved

The system SHALL record the previous assignee, new assignee, acting user and timestamp as an
activity entry, and SHALL preserve the task's existing comments and activity history.

#### Scenario: Activity entry recorded

- **WHEN** a task is reassigned
- **THEN** an activity entry records the previous assignee, the new assignee, the acting user and the timestamp

#### Scenario: Existing history retained

- **WHEN** a task is reassigned
- **THEN** its existing comments and previous activity records remain unchanged

### Requirement: Recipients and notification on reassignment

The system SHALL update a task's notification recipients on reassignment so the assignee is
always included according to the recipient rules, and SHALL create a notification for the new
assignee.

#### Scenario: New assignee notified

- **WHEN** a task is reassigned
- **THEN** the new assignee receives a notification about the task assignment

#### Scenario: Recipients reflect new assignee

- **WHEN** a task is reassigned
- **THEN** the notification recipient set is updated and the new assignee is included