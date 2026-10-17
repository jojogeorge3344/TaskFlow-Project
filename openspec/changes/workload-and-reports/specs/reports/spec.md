# Spec Delta

## Purpose

Lets Administrators and Managers produce a filtered summary of task allocation, status and completion within their authorized scope, and export the underlying task rows as CSV.

## ADDED Requirements

### Requirement: Filter the report scope
An Administrator or Manager SHALL be able to filter a report by a date range, employee, department, task status and task priority. The system MUST apply every supplied filter and leave unspecified filters unrestricted.

#### Scenario: Combined filters applied
- **WHEN** a Manager requests a report filtered by one employee and one status
- **THEN** the system returns only tasks matching every supplied filter

#### Scenario: No filters returns the full scope
- **WHEN** a Manager requests a report with no filters
- **THEN** the system returns tasks across the Manager's authorized scope

### Requirement: Report totals and completion summary
The system SHALL return, for the filtered scope, the total task count and the completed, pending and overdue counts, plus a completion summary usable to render a progress visualisation.

#### Scenario: Totals computed for the filter
- **WHEN** a report is requested with a filter
- **THEN** the system returns the total, completed, pending and overdue counts matching that filter

#### Scenario: Empty scope yields zeros
- **WHEN** a report filter matches no tasks
- **THEN** the system returns zero totals

### Requirement: Export report as CSV
An Administrator or Manager SHALL be able to download the filtered report as a CSV file containing the task rows of the authorized scope.

#### Scenario: CSV download
- **WHEN** a Manager requests the CSV export for a filter
- **THEN** the system returns a CSV file of the matching task rows

#### Scenario: Export honours the same filters
- **WHEN** a Manager exports with a filter applied
- **THEN** the exported rows are the same set the report totals were computed from

### Requirement: Report respects authorized scope and shared rules
The system MUST restrict a report to the records the requester is permitted to see, and MUST apply the same status and overdue rules the dashboards use, so reported figures stay consistent with the rest of the application.

#### Scenario: Unauthorized records excluded
- **WHEN** a Manager requests a report
- **THEN** only tasks within that Manager's authorized scope are included

#### Scenario: Overdue computed consistently
- **WHEN** an open task is past its deadline in the application timezone
- **THEN** it is counted as overdue in the report totals
