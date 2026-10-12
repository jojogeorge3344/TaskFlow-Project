## Purpose

Provide role-scoped dashboard views showing relevant statistics and quick actions for Administrator, Manager, and Employee.

## ADDED Requirements

### Requirement: Admin dashboard

The system SHALL provide admin dashboard with total employees, active employees, total tasks, pending tasks, completed tasks, overdue tasks, department summary, and recent system activity.

#### Scenario: Admin sees overview stats

- **WHEN** an Administrator views the admin dashboard
- **THEN** the system displays the required statistics cards and summaries

#### Scenario: Admin can navigate from dashboard

- **WHEN** an Administrator clicks navigation links on admin dashboard
- **THEN** the system navigates to employee/department management screens

### Requirement: Manager dashboard

The system SHALL provide manager dashboard with tasks by scope, due today/upcoming/overdue/completed, tasks by status, employee workload summary, recent activity, and quick task creation.

#### Scenario: Manager sees their scoped stats

- **WHEN** a Manager views the manager dashboard
- **THEN** the system displays manager-relevant statistics

#### Scenario: Manager can create task from dashboard

- **WHEN** a Manager clicks "Create New Task" on manager dashboard
- **THEN** the system navigates to task creation

### Requirement: Employee dashboard

The system SHALL provide employee dashboard with personal stats (assigned tasks, due today, overdue, in progress, completed) and recent activity.

#### Scenario: Employee sees personal stats

- **WHEN** an Employee views their dashboard
- **THEN** the system displays their personal task statistics and recent activity

### Requirement: Dashboard API endpoints

The system SHALL expose role-scoped dashboard endpoints returning aggregated statistics.

#### Scenario: Admin endpoint returns stats

- **WHEN** an Administrator calls GET /api/v1/dashboard/admin
- **THEN** the system returns admin dashboard statistics

#### Scenario: Manager endpoint returns stats

- **WHEN** a Manager calls GET /api/v1/dashboard/manager
- **THEN** the system returns manager dashboard statistics

#### Scenario: Employee endpoint returns stats

- **WHEN** an Employee calls GET /api/v1/dashboard/employee
- **THEN** the system returns employee dashboard statistics

### Requirement: Shared dashboard layout

The system SHALL render a single shared dashboard layout containing a responsive sidebar and the role-specific dashboard content. Sidebar navigation links SHALL be limited to the destinations permitted for the logged-in user's role.

#### Scenario: Role-scoped navigation menu

- **WHEN** a user views any dashboard
- **THEN** the sidebar shows only the navigation links permitted for that user's role

#### Scenario: Responsive sidebar

- **WHEN** the dashboard is viewed on a narrow (mobile) viewport
- **THEN** the sidebar collapses into a toggleable menu and the dashboard content remains usable

#### Scenario: Sign out from layout

- **WHEN** the user chooses Sign out in the layout
- **THEN** the session is cleared and the user is returned to the login screen

### Requirement: Role-scoped dashboard routing

The system SHALL load the role-specific dashboard component for each role and SHALL prevent a user from viewing another role's dashboard.

#### Scenario: Each role reaches its own dashboard

- **WHEN** an Administrator, Manager, or Employee logs in
- **THEN** the system navigates to `/admin`, `/manager`, or `/me` respectively and renders that role's dashboard component

#### Scenario: Cross-role access is blocked

- **WHEN** a user attempts to open a dashboard path not permitted for their role
- **THEN** the role guard redirects them to their own dashboard

### Requirement: Dashboard data fidelity and states

The system SHALL source dashboard statistics from the dashboard API endpoints rather than hardcoded client values, and SHALL display loading, error, and empty states instead of fabricated numbers.

#### Scenario: Statistics come from the API

- **WHEN** a dashboard loads
- **THEN** its statistics are retrieved from the corresponding `/api/v1/dashboard/*` endpoint using the authenticated session

#### Scenario: Loading state

- **WHEN** a dashboard request is in flight
- **THEN** the system shows a loading indicator

#### Scenario: Error state

- **WHEN** a dashboard request fails
- **THEN** the system shows an error message rather than fabricated statistics

#### Scenario: Empty state

- **WHEN** a dashboard request succeeds but returns no relevant data
- **THEN** the system shows an explicit empty state

#### Scenario: Unavailable metric is not fabricated

- **WHEN** a statistic is not yet provided by the backend
- **THEN** the system surfaces it as unavailable rather than displaying a fabricated value
