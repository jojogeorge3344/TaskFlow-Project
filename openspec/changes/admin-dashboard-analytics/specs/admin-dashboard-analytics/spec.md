# Spec Delta

## Purpose

Gives Administrators an organisation-wide, filterable and drillable analytics dashboard covering departments, tasks, employees, managers and projects, so workload and delivery problems are visible in one place.

## ADDED Requirements

### Requirement: Admin analytics access control
The system SHALL serve admin analytics only to users holding the `dashboard.admin` permission, and SHALL enforce this in the API, not only in the UI.

#### Scenario: Administrator loads analytics
- **WHEN** a user with `dashboard.admin` requests the admin analytics endpoint
- **THEN** the system returns organisation-wide data

#### Scenario: Non-admin is refused
- **WHEN** a Manager or Employee without `dashboard.admin` requests the admin analytics endpoint directly
- **THEN** the system responds 403 and returns no data

#### Scenario: Unauthenticated request
- **WHEN** a request has no valid session
- **THEN** the system responds 401

### Requirement: Admin summary cards
The system SHALL show cards for total employees, managers, departments and projects, and for total, completed, pending, in-progress and overdue tasks and overall completion percentage.

#### Scenario: Cards reflect the data
- **WHEN** the Administrator opens the dashboard with no filters
- **THEN** each card equals the count obtained from the underlying records using the shared metric definitions

#### Scenario: Cards follow filters
- **WHEN** the Administrator changes a filter
- **THEN** the task-based cards recalculate for the filtered tasks while the employee, manager, department and project totals follow the filter where it narrows them

### Requirement: Consistent task metric definitions
The system SHALL classify each non-deleted, non-cancelled task into exactly one bucket: Completed; Overdue (open and past its deadline); In Progress (In Progress or In QA, not overdue); or Pending (To Do or Blocked, not overdue). Completion percentage SHALL be Completed divided by the total task count, as a whole-number percentage, and 0 when there are no tasks.

#### Scenario: Buckets add up
- **WHEN** any chart or card shows the four buckets for the same filters
- **THEN** the four values sum to the total task count shown elsewhere on the dashboard

#### Scenario: Cancelled and deleted tasks excluded
- **WHEN** a task is cancelled or deleted
- **THEN** it is counted in no bucket and no total

#### Scenario: Empty set percentage
- **WHEN** the filtered set has no tasks
- **THEN** completion percentage is 0 and no division error occurs

### Requirement: Department-wise analytics
The system SHALL show, per department, tasks in each bucket, total workload and completion percentage, attributing each task to its assignee's department, as a stacked bar chart and a table.

#### Scenario: Compare departments
- **WHEN** the Administrator views the department chart
- **THEN** each department shows Pending, In Progress, Completed and Overdue tasks and its completion percentage

#### Scenario: Unassigned department
- **WHEN** an assignee belongs to no department
- **THEN** their tasks appear under a "No department" group so totals still reconcile

### Requirement: Task-wise analytics
The system SHALL show organisation task distribution by status and by priority (donut charts), by deadline bucket (overdue, due today, next 7 days, later), and a completion trend line over the selected period.

#### Scenario: Status and priority distribution
- **WHEN** the Administrator views task analytics
- **THEN** donut charts show the share of tasks per bucket and per priority with values in tooltips and legends

#### Scenario: Completion trend
- **WHEN** a period is selected
- **THEN** the line chart shows tasks created and tasks completed per day (or per week for periods over 31 days), with zero-filled days

### Requirement: Employee-wise analytics
The system SHALL show per employee the number of tasks in each bucket and completion percentage, counting a task for every employee who is its assignee or a recipient, ordered by open workload.

#### Scenario: Compare employees
- **WHEN** the Administrator views the employee chart
- **THEN** each employee shows open workload, bucket counts and completion percentage

#### Scenario: Shared tasks
- **WHEN** a task has an assignee and recipients
- **THEN** it counts once for each of them, and the chart states that employee totals can exceed the task total

#### Scenario: Large employee list
- **WHEN** more than 10 employees match
- **THEN** the chart shows the 10 with the largest open workload and a "view all" link to the full table

### Requirement: Manager-wise analytics
The system SHALL show per manager the departments, employees, projects and tasks they manage, team workload, completion percentage and overdue count, where a manager's team is their direct reports plus the members of departments they manage.

#### Scenario: Compare managers
- **WHEN** the Administrator views the manager chart
- **THEN** each manager shows counts of departments, employees, projects and team tasks with bucket breakdown

#### Scenario: Manager with no team
- **WHEN** a manager has no reports or departments
- **THEN** the manager is listed with zero values

### Requirement: Project-wise analytics
The system SHALL show per project the task count per bucket, completion percentage as a progress bar, outstanding tasks and overdue tasks, with a project's latest task deadline shown as its projected end because projects have no deadline of their own.

#### Scenario: Compare projects
- **WHEN** the Administrator views the project chart
- **THEN** each project shows a progress bar with completed over total tasks and its overdue count

#### Scenario: Project with no tasks
- **WHEN** a project has no tasks
- **THEN** it shows 0 percent, no projected end and no error

#### Scenario: Tasks without a project
- **WHEN** tasks have no project
- **THEN** they appear under "No project" so totals still reconcile

### Requirement: Date filters
The system SHALL provide Today, Last 7 days, Last 30 days, This month, Previous month and Custom range filters, evaluated in the application timezone, and a date basis selector of Created, Deadline or Completed that decides which task date the range applies to.

#### Scenario: Preset range
- **WHEN** the Administrator selects "Last 7 days" with basis "Created"
- **THEN** all cards and charts include only tasks created in the last 7 days including today

#### Scenario: Custom range
- **WHEN** the Administrator picks a start and end date
- **THEN** the dashboard applies the inclusive range and the end date cannot precede the start date

#### Scenario: Trend ignores basis
- **WHEN** the date basis is changed
- **THEN** the trend chart still plots created and completed per day for the range, and a label states this

### Requirement: Entity filters
The system SHALL allow filtering by department, project, employee and manager, individually or combined with the date range, and SHALL update every component for the matching records only.

#### Scenario: Combined filters
- **WHEN** the Administrator selects a department and a project
- **THEN** only tasks matching both are included in every card and chart

#### Scenario: Manager filter
- **WHEN** the Administrator selects a manager
- **THEN** only tasks assigned to that manager's team are included

#### Scenario: Reset filters
- **WHEN** the Administrator clicks Reset
- **THEN** all filters return to the default (Last 30 days, Created, no entity filters)

#### Scenario: Unknown filter id
- **WHEN** a request names a department, project or user that does not exist
- **THEN** the system responds 400 or 404 and does not return organisation data

### Requirement: Chart presentation
The system SHALL present bar, stacked bar, donut, line and progress bar charts with labels, legends, tooltips and values, using colour together with text so status is not conveyed by colour alone.

#### Scenario: Tooltip values
- **WHEN** the user hovers a bar segment or donut slice
- **THEN** a tooltip shows the series name, value and percentage of the group

### Requirement: Drill-down to records
The system SHALL let the Administrator click a chart segment, bar, donut slice or trend point to open the task list filtered to the same records, with the dashboard filters carried over.

#### Scenario: Click a stacked bar segment
- **WHEN** the Administrator clicks the "Overdue" segment of the Engineering bar
- **THEN** the task list opens showing only overdue tasks of Engineering's members with the same date range

#### Scenario: Drill-down count matches
- **WHEN** a drill-down opens
- **THEN** the number of listed tasks equals the value on the clicked segment

### Requirement: Loading, empty and error states
The system SHALL show per-section loading placeholders, a clear empty state when filters match no tasks, and a retryable error state that does not break other sections.

#### Scenario: No matching data
- **WHEN** filters match no tasks
- **THEN** cards show 0, charts show an empty-state message and nothing throws

#### Scenario: Request fails
- **WHEN** the analytics request fails
- **THEN** an error message with a Retry button appears and the filter bar stays usable

#### Scenario: Filter change refresh
- **WHEN** a filter changes
- **THEN** sections show loading, then update, and an older slower response never overwrites a newer one
