# Spec Delta

## Purpose

Gives Managers a filterable analytics dashboard limited to the employees and projects within their authorised scope, so they can monitor team workload and delivery without seeing other teams' data.

## ADDED Requirements

### Requirement: Manager authorised scope
The system SHALL define a manager's team as the distinct union of users who report directly to the manager and users who belong to departments the manager manages, and SHALL base every manager dashboard figure on tasks assigned to or shared with that team.

#### Scenario: Direct report is in scope
- **WHEN** a user reports directly to the manager but is in a department the manager does not manage
- **THEN** that user and their tasks are included

#### Scenario: Department member is in scope
- **WHEN** a user belongs to a managed department but does not report directly to the manager
- **THEN** that user and their tasks are included

#### Scenario: Both relations
- **WHEN** a user qualifies through both relations
- **THEN** they are counted once

#### Scenario: Manager with no team
- **WHEN** the manager has no reports and no managed departments
- **THEN** the dashboard shows zeros and an empty-state message

### Requirement: Manager analytics access control
The system SHALL serve manager analytics only to users holding the `dashboard.manager` permission, shall never return data outside the caller's scope, and SHALL enforce this in the API and query.

#### Scenario: Manager loads analytics
- **WHEN** a Manager requests the manager analytics endpoint
- **THEN** only in-scope data is returned

#### Scenario: Out-of-scope filter
- **WHEN** the request names an employee or project outside the manager's scope
- **THEN** the system responds 403 and returns no data

#### Scenario: Another manager's team
- **WHEN** Manager A requests data for an employee who belongs only to Manager B's team
- **THEN** the system responds 403

#### Scenario: Without permission
- **WHEN** a user without `dashboard.manager` calls the endpoint directly
- **THEN** the system responds 403

### Requirement: No department-wise view for managers
The system SHALL NOT show a department-wise breakdown or a department filter on the Manager dashboard; managed departments only determine which team members are in scope.

#### Scenario: No department chart or filter
- **WHEN** a Manager views the dashboard
- **THEN** no department chart, department table or department filter is shown

#### Scenario: Department filter parameter ignored
- **WHEN** a Manager calls the analytics endpoint with a department filter
- **THEN** the system ignores it and returns the full team scope

### Requirement: Manager employee-wise analytics
The system SHALL show each team member's tasks per bucket, completion percentage and open workload, and SHALL flag members with high workload or overdue tasks.

#### Scenario: High workload flag
- **WHEN** a member's open task count is at least 1.5 times the team average and at least 5
- **THEN** the member is flagged "High workload" with text, not colour alone

#### Scenario: Overdue flag
- **WHEN** a member has any overdue task
- **THEN** the member is flagged "Overdue" and shows the overdue count

#### Scenario: Only team members
- **WHEN** the Manager views employee analytics
- **THEN** no user outside the team appears

### Requirement: Manager project-wise analytics
The system SHALL show the projects in the manager's scope (projects the manager is assigned to and projects of the team's tasks), each with task buckets, completion percentage as a progress bar, projected end (latest task deadline) and overdue count, counting only the team's tasks in each project.

#### Scenario: Team tasks only
- **WHEN** a project also has tasks assigned to people outside the team
- **THEN** those tasks are not counted in the Manager's figures

#### Scenario: Assigned project with no team tasks
- **WHEN** the manager is assigned a project where the team has no tasks
- **THEN** the project is listed with 0 tasks and 0 percent

### Requirement: Manager task analytics and team summary
The system SHALL show team totals for tasks, completed, pending, in progress and overdue, status and priority distributions, and a team performance summary listing the most delayed tasks and projects needing attention.

#### Scenario: Bucket consistency
- **WHEN** the dashboard shows the cards and charts for the same filters
- **THEN** the four buckets sum to the total, using the same definitions as the Admin dashboard

#### Scenario: Attention list
- **WHEN** team tasks are overdue
- **THEN** the summary lists up to five most overdue tasks (title, assignee, days overdue) and projects with overdue tasks, each linking to the record

### Requirement: Manager date filters
The system SHALL provide Today, Last 7 days, Last 30 days, This month, Previous month and Custom range with a basis of Created, Deadline or Completed, applied consistently to every component.

#### Scenario: Range applies everywhere
- **WHEN** the Manager selects "This month"
- **THEN** cards, charts, flags and the summary all reflect that month

#### Scenario: Invalid custom range
- **WHEN** the end date is before the start date
- **THEN** the system rejects the range and keeps the previous results

### Requirement: Manager scoped entity filters
The system SHALL offer filters for employees and projects limited to the manager's scope, and SHALL update all components for matching records only.

#### Scenario: Filter options
- **WHEN** the Manager opens a filter
- **THEN** the options list only in-scope employees and projects

#### Scenario: Combined filters
- **WHEN** the Manager selects an employee and a project
- **THEN** only that employee's tasks in that project are included

### Requirement: Manager drill-down
The system SHALL let the Manager click a chart element to open the task list for the same records, restricted to the manager's scope. The same limit applies to the ordinary Tasks screen, board and task reads for a manager (see design D7); workload and reports are unchanged.

#### Scenario: Drill into an employee's overdue tasks
- **WHEN** the Manager clicks the Overdue segment on a team member's bar
- **THEN** the task list shows that member's overdue tasks and the count equals the segment value

#### Scenario: Tampered query
- **WHEN** a drill-down URL is edited to name an out-of-scope user
- **THEN** the task list returns no out-of-scope tasks

### Requirement: Manager loading, empty and error states
The system SHALL show loading placeholders, an empty state when filters match nothing, and a retryable error state without breaking the page.

#### Scenario: Empty filtered result
- **WHEN** filters match no tasks
- **THEN** cards show 0 and charts show an empty message

#### Scenario: Server error
- **WHEN** the analytics request fails
- **THEN** an error with Retry appears and filters remain usable
