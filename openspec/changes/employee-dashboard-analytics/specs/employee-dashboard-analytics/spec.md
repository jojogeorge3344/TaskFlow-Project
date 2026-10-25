# Spec Delta

## Purpose

Gives Employees a personal analytics dashboard of their own tasks, projects, progress and permitted department-level summary, without exposing other employees' private data.

## ADDED Requirements

### Requirement: Employee analytics identity and privacy
The system SHALL derive the subject of employee analytics from the authenticated session only, SHALL accept no employee or user identifier from the request, and SHALL NOT return any other employee's name, task or per-person figure.

#### Scenario: Own data only
- **WHEN** an Employee loads the dashboard
- **THEN** the response contains only that user's tasks and projects plus aggregate department figures

#### Scenario: Identifier in request ignored
- **WHEN** a request carries a user id or employee id parameter
- **THEN** the system ignores it and returns the caller's own data

#### Scenario: Unauthenticated
- **WHEN** the request has no valid session
- **THEN** the system responds 401

### Requirement: Department overview
The system SHALL show the employee's department name and aggregate department statistics (total tasks, completed, in progress, pending, overdue, completion percentage) with no per-colleague breakdown.

#### Scenario: Department aggregates
- **WHEN** the employee belongs to a department
- **THEN** the overview shows the department name and aggregate counts using the shared metric definitions

#### Scenario: Privacy of department view
- **WHEN** the department overview is shown
- **THEN** it lists no colleague names and no task titles

#### Scenario: No department
- **WHEN** the employee has no department
- **THEN** the section shows "No department assigned" and no error

### Requirement: My task analytics
The system SHALL show the employee's tasks (assigned to them or shared with them as recipient) by Completed, In Progress, Pending and Overdue, by priority, and a list of upcoming deadlines.

#### Scenario: Status and priority
- **WHEN** the Employee views task analytics
- **THEN** donut charts show their bucket and priority distribution with values

#### Scenario: Upcoming deadlines
- **WHEN** open tasks have deadlines within the next 7 days
- **THEN** they are listed soonest first with title, priority and days remaining, and each links to the task

#### Scenario: Overdue highlighted
- **WHEN** an open task is past its deadline
- **THEN** it shows an "Overdue" label with the days overdue

### Requirement: Overall task summary
The system SHALL show total assigned tasks, completion percentage and completed versus outstanding counts, using the same bucket definitions as the other dashboards.

#### Scenario: Completion percentage
- **WHEN** 3 of 12 tasks are completed
- **THEN** the dashboard shows 25 percent, 3 completed and 9 outstanding

#### Scenario: No tasks
- **WHEN** the employee has no tasks
- **THEN** the summary shows 0 tasks and 0 percent with a friendly empty message

### Requirement: My project overview
The system SHALL list the projects in which the employee has tasks or to which the employee belongs, each with the employee's task count, a progress bar of the employee's completed tasks in that project, and the latest deadline of the employee's open tasks.

#### Scenario: Project progress
- **WHEN** the employee has tasks in a project
- **THEN** the project shows completed over total of the employee's own tasks in it

#### Scenario: No projects
- **WHEN** the employee has no project tasks and no project membership
- **THEN** the section shows an empty state

### Requirement: Personal performance insights
The system SHALL show the employee's completed-versus-created trend for the selected period, tasks completed in the period, upcoming deadlines and overdue tasks, with a period choice of Last 7 days, Last 30 days and This month.

#### Scenario: Trend for period
- **WHEN** the employee picks Last 30 days
- **THEN** a line chart shows their tasks created and completed per day with zero-filled days, and the completed-in-period count matches the chart total

#### Scenario: Default period
- **WHEN** the dashboard first loads
- **THEN** the period is Last 30 days

### Requirement: No entity filters for employees
The system SHALL NOT offer the Employee dashboard filters for other employees, managers or departments.

#### Scenario: Filter bar
- **WHEN** the Employee views the dashboard
- **THEN** only the period selector is shown

#### Scenario: Direct request for filters
- **WHEN** an Employee calls the analytics endpoint with a department, project or employee filter
- **THEN** the system ignores the filter and returns only their own data

### Requirement: Employee drill-down
The system SHALL let the Employee click a chart element or deadline row to open their own task list for the same records.

#### Scenario: Click overdue segment
- **WHEN** the Employee clicks the Overdue segment
- **THEN** their task list opens with their overdue tasks and the count equals the segment

#### Scenario: Tampered query
- **WHEN** a task-list URL is edited to name another user's tasks
- **THEN** the list returns only tasks the caller may already see

### Requirement: Employee loading, empty and error states
The system SHALL show loading placeholders, empty states and a retryable error state without breaking the page.

#### Scenario: Failure
- **WHEN** the analytics request fails
- **THEN** an error message with Retry appears and other page areas remain usable
