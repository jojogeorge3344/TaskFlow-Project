# Spec Delta

## Purpose

Gives Administrators and Managers a per-employee view of how task load is distributed across the organization, filtered by department, so they can make informed allocation decisions.

## ADDED Requirements

### Requirement: Read per-employee workload
An Administrator or Manager SHALL be able to read a per-employee distribution of task load showing, for each employee, the number of active, high-priority, overdue and completed tasks together with the employee's department. Employees MUST NOT be able to read the workload view.

#### Scenario: Manager reads workload
- **WHEN** a Manager requests the workload
- **THEN** the system returns, for each employee, the active, high-priority, overdue and completed task counts and that employee's department

#### Scenario: Employee denied
- **WHEN** an Employee requests the workload
- **THEN** the system denies the request

### Requirement: Filter workload by department
An Administrator or Manager SHALL be able to filter the workload to a single department, so only employees in that department are shown.

#### Scenario: Department filter applied
- **WHEN** a Manager requests the workload for one department
- **THEN** the system returns only the employees in that department

#### Scenario: Unassigned department handling
- **WHEN** a workload is requested without a department filter
- **THEN** the system returns employees across all departments

### Requirement: Workload counts are consistent with dashboards
The system SHALL count active, overdue and open tasks for the workload using the same timezone-aware status and overdue rules that the dashboards and reminders use, so the figures agree across screens.

#### Scenario: Overdue counted consistently
- **WHEN** an open task is past its deadline in the application timezone
- **THEN** it is counted as overdue in the workload

### Requirement: Workload respects the authorized scope
The system MUST restrict the workload to the tasks and employees within the requesting user's authorized scope, and MUST NOT expose tasks the requester is not permitted to see.

#### Scenario: Scope enforced
- **WHEN** a Manager requests the workload
- **THEN** only tasks and employees within that Manager's authorized scope contribute to the counts
