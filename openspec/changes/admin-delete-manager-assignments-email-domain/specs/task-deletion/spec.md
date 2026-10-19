# Spec Delta

## Purpose

Lets Administrators remove a task that was created in error or is no longer wanted, while keeping the audit trail of what happened to it.

## ADDED Requirements

### Requirement: Administrator-only task deletion

The system SHALL allow only Administrators to delete a task. The ability SHALL NOT be grantable to any other role through role permissions.

#### Scenario: Administrator deletes a task

- **WHEN** an Administrator deletes a task
- **THEN** the task is removed from every task view and the system confirms the deletion

#### Scenario: Non-administrator cannot delete

- **WHEN** a Manager or Employee, including one whose role holds the edit-tasks permission, attempts to delete a task
- **THEN** the system rejects the request as forbidden and the task is unchanged

#### Scenario: Delete control hidden from non-administrators

- **WHEN** a non-Administrator views the task list or task details
- **THEN** no delete action is shown

#### Scenario: Deleting an unknown task

- **WHEN** an Administrator deletes a task that does not exist or is already deleted
- **THEN** the system responds that the task was not found

### Requirement: Deletion confirmation

The system SHALL ask the Administrator to confirm before a task is deleted.

#### Scenario: Administrator cancels

- **WHEN** an Administrator opens the delete confirmation and cancels it
- **THEN** the task is not deleted

### Requirement: Deleted tasks disappear everywhere

The system SHALL exclude deleted tasks from task lists, the board, search, reports and exports, workload figures, dashboards, deadline reminders and assistant results.

#### Scenario: Lists and board

- **WHEN** any user opens the task list or board after a task is deleted
- **THEN** the deleted task is not shown

#### Scenario: Direct link

- **WHEN** any user opens the link of a deleted task
- **THEN** the system answers exactly as it does for a task that does not exist, and shows nothing of the task

#### Scenario: Reminders stop

- **WHEN** a task with an upcoming deadline is deleted
- **THEN** no further deadline reminders are sent for it

#### Scenario: Counts update

- **WHEN** a task is deleted
- **THEN** dashboard counts, workload totals and reports no longer include it

### Requirement: Task history is retained

The system SHALL retain a deleted task's comments and activity history, and SHALL record the deletion as an activity entry naming the Administrator and the time.

#### Scenario: Deletion is recorded

- **WHEN** an Administrator deletes a task
- **THEN** an activity entry records who deleted it and when
