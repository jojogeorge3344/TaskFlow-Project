## Purpose

Enable task creation, listing, viewing, and basic status management with appropriate role-based access control.

## ADDED Requirements

### Requirement: Task creation

The system SHALL allow Managers and Administrators to create tasks with title, description, assignee, priority, start date, deadline, estimated hours, and optional additional recipients.

#### Scenario: Manager creates task

- **WHEN** a Manager creates a task with valid data
- **THEN** the system creates the task and assigns it to the specified employee

#### Scenario: Admin creates task

- **WHEN** an Administrator creates a task
- **THEN** the system creates the task successfully

#### Scenario: Employee cannot create task

- **WHEN** an Employee attempts to create a task
- **THEN** the system responds with forbidden

### Requirement: Task listing and filtering

The system SHALL allow authorized users to list and filter tasks by employee, status, priority, deadline, and search terms.

#### Scenario: Assignee sees own tasks

- **WHEN** an Employee lists tasks
- **THEN** the system returns tasks they are assigned to or related to (per access policy)

#### Scenario: Manager lists tasks in scope

- **WHEN** a Manager lists tasks
- **THEN** the system returns tasks they can access

### Requirement: Task details

The system SHALL allow users with task access to view task details.

#### Scenario: Authorized user views task

- **WHEN** a user with access views a task
- **THEN** the system returns full task details

### Requirement: Status updates

The system SHALL allow assignees and Managers to update task status.

#### Scenario: Assignee updates own task status

- **WHEN** the assignee updates their task's status
- **THEN** the system persists the status change
