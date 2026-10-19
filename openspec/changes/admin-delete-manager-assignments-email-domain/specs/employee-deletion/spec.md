# Spec Delta

## Purpose

Lets Administrators remove an employee account from the system without losing the history of the work that employee did, while protecting the organization from losing all administrator access.

## ADDED Requirements

### Requirement: Administrator-only employee deletion

The system SHALL allow only Administrators to delete an employee. The ability SHALL NOT be grantable to any other role through role permissions.

#### Scenario: Administrator deletes an employee

- **WHEN** an Administrator deletes an employee
- **THEN** the employee is removed from employee lists and the system confirms the deletion

#### Scenario: Non-administrator cannot delete

- **WHEN** a Manager or Employee, including one whose role holds the manage-employees permission, attempts to delete an employee
- **THEN** the system rejects the request as forbidden and the employee is unchanged

#### Scenario: Delete control hidden from non-administrators

- **WHEN** a non-Administrator views the employees screen
- **THEN** no delete action is shown

#### Scenario: Deleting an unknown employee

- **WHEN** an Administrator deletes an employee that does not exist or is already deleted
- **THEN** the system responds that the employee was not found

### Requirement: Deletion confirmation

The system SHALL ask the Administrator to confirm before an employee is deleted.

#### Scenario: Administrator cancels

- **WHEN** an Administrator opens the delete confirmation and cancels it
- **THEN** the employee is not deleted

### Requirement: Protect administrator access

The system SHALL NOT allow an Administrator to delete their own account, and SHALL NOT allow deletion of the last active Administrator.

#### Scenario: Admin deletes self

- **WHEN** an Administrator attempts to delete their own account
- **THEN** the system rejects the request with an explanatory message

#### Scenario: Last active administrator

- **WHEN** an Administrator attempts to delete the only remaining active Administrator
- **THEN** the system rejects the request with an explanatory message

### Requirement: Deleted employees keep their username

The system SHALL keep a deleted employee's username reserved, so it cannot be given to another employee.

#### Scenario: Reusing a deleted username

- **WHEN** an Administrator creates or renames an employee to the username of a deleted employee
- **THEN** the system rejects the request with the "already exists" message

### Requirement: Deleted employees lose access

The system SHALL end a deleted employee's ability to sign in and to use existing sessions.

#### Scenario: Deleted employee signs in

- **WHEN** a deleted employee submits their credentials
- **THEN** sign-in fails with the same generic message used for invalid credentials

#### Scenario: Existing session of a deleted employee

- **WHEN** a deleted employee uses a previously issued session or refresh token
- **THEN** the system rejects the request as unauthenticated

### Requirement: Deleted employees are hidden but history is kept

The system SHALL exclude deleted employees from employee lists, assignee and recipient pickers, department member lists and workload, and SHALL retain the tasks, comments, activity history and mail they authored.

#### Scenario: Pickers exclude deleted employees

- **WHEN** a user creates or reassigns a task
- **THEN** deleted employees are not offered as assignee or recipient

#### Scenario: History remains readable

- **WHEN** a user opens a task that a deleted employee created or commented on
- **THEN** the task and comments are shown with the deleted employee's name

#### Scenario: Open tasks assigned to a deleted employee

- **WHEN** an employee who still has unfinished assigned tasks is deleted
- **THEN** the system rejects the deletion and tells the Administrator to reassign those tasks first

### Requirement: Deleted employee's manager links are cleared

The system SHALL remove a deleted employee's manager assignment, and for a deleted manager SHALL clear the manager link of their employees and their department assignments.

#### Scenario: Deleting a manager

- **WHEN** an Administrator deletes a Manager who has employees and departments assigned
- **THEN** those employees have no manager and the manager's department assignments are removed
