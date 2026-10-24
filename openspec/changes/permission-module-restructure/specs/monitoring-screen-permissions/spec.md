# Spec Delta

## Purpose

Gives the Board and Employee Workload screens their own permissions, separate from Task Management, so an administrator can allow or deny each screen and its main controls independently.

## ADDED Requirements

### Requirement: Board has its own view permission
The system SHALL provide a Board view permission, separate from Task Management, that controls opening the Board screen and loading its data.

#### Scenario: Role without Board view
- **WHEN** a signed-in user whose role lacks Board view requests the board
- **THEN** the Board menu item is hidden, opening `/board` is blocked, and the board API returns 403

#### Scenario: Role with Board view
- **WHEN** a user whose role holds Board view opens the Board
- **THEN** the board shows the tasks the user's task scope allows

### Requirement: Board filters are separately controlled
The system SHALL provide a Board filters permission. Without it the Board screen SHALL show tasks without the project, assignee, status and deadline filters.

#### Scenario: Filters hidden
- **WHEN** a user holds Board view but not Board filters
- **THEN** the board shows its columns and cards and none of the filter controls

### Requirement: Moving a card stays under task status permission
Dragging a card to change its status SHALL continue to require the existing task status permission and SHALL NOT require any Board permission beyond view.

#### Scenario: View without move
- **WHEN** a user holds Board view but not the task status permission
- **THEN** cards cannot be dragged and the API rejects status changes

### Requirement: Existing roles keep the Board
When Board permissions are first added, every role that has access to the board today SHALL receive Board view and Board filters, so no current user loses the Board.

#### Scenario: Manager and Employee after upgrade
- **WHEN** the application starts after the upgrade
- **THEN** the Manager and Employee roles, and every existing custom role, hold Board view and Board filters

#### Scenario: Administrator
- **WHEN** the application starts
- **THEN** the Administrator role holds every Board permission

### Requirement: Employee Workload has screen permissions
The system SHALL keep the existing workload view permission for opening the screen, and SHALL add a Workload filters permission and a permission to open an employee's tasks from the workload table.

#### Scenario: Workload without filters
- **WHEN** a user holds workload view but not Workload filters
- **THEN** the workload table is shown without the project and employee filters

#### Scenario: Workload without opening tasks
- **WHEN** a user holds workload view but not the open-tasks permission
- **THEN** the employee rows have no link to that employee's task list

#### Scenario: Existing workload holders keep everything
- **WHEN** the application starts after the upgrade
- **THEN** every role that held workload view also holds the two new workload permissions

### Requirement: Task activity route is guarded
The route to a task's activity history SHALL require the existing task activity permission, and links to it SHALL be hidden without it.

#### Scenario: Direct URL without permission
- **WHEN** a user without the task activity permission opens a task's activity URL
- **THEN** the user is sent to a not-permitted screen and no error page from a failed request is shown

### Requirement: Monitoring menu lists Board and Workload by permission
The Board and Employee Workload menu entries SHALL appear only when the user holds the matching view permission. The Task Management entry SHALL stay available to every user, because viewing own tasks is always on.

#### Scenario: Only Board permitted
- **WHEN** a user holds Board view and not workload view
- **THEN** the menu shows Task Management and Board, and no Employee Workload entry

#### Scenario: Neither permitted
- **WHEN** a user holds neither Board view nor workload view
- **THEN** the menu shows Task Management only
