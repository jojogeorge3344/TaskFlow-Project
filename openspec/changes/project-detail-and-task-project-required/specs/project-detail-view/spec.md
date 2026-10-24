## ADDED Requirements

### Requirement: Open a project's detail screen
Clicking a project in Project Management SHALL open its detail screen at `/projects/:id` for users with the projects-view permission.

#### Scenario: Open from the list
- **WHEN** the user clicks a project card
- **THEN** the detail screen for that project opens, and a back link returns to Project Management

#### Scenario: Unknown project
- **WHEN** the id does not match a project
- **THEN** a "Project not found" state is shown

### Requirement: Detail screen shows the assigned team
The screen SHALL list every team member with name, role (position) on this project, allocation percentage and whether they manage the project.

#### Scenario: Team with members
- **WHEN** the project has members
- **THEN** each is shown with role, allocation % and a manager marker

#### Scenario: Empty team
- **WHEN** the project has no members
- **THEN** an empty state says no one is assigned

### Requirement: Detail screen lists the project's tasks
The screen SHALL list the tasks mapped to the project with code, title, assignee, the assignee's role on the project, priority, status, start date and deadline, and link each to its task page.

#### Scenario: Project with tasks
- **WHEN** tasks are mapped to the project
- **THEN** they are listed with the details above

#### Scenario: No tasks
- **WHEN** no tasks are mapped
- **THEN** an empty state is shown

### Requirement: Filter project tasks
The task list SHALL be filterable by status, priority, deadline date range, assigned user and role, combined with AND, with a control to clear all filters. The user and role options SHALL contain only people and roles present on this project.

#### Scenario: Role filter options
- **WHEN** the project team holds the roles Developer and Tester
- **THEN** the role filter offers exactly Developer and Tester

#### Scenario: Filter by role
- **WHEN** the user picks the role Tester
- **THEN** only tasks assigned to members whose role on this project is Tester are shown

#### Scenario: Combined filters yield nothing
- **WHEN** the filters match no task
- **THEN** a "no tasks match" state with a clear-filters action is shown

### Requirement: Project tasks API
`GET /projects/{id}/tasks` SHALL return the project's non-deleted tasks, each with the assignee's role on the project, filtered by optional status, priority, deadline-from, deadline-to, assigned user and position (role) id. It SHALL return 404 for an unknown project.

#### Scenario: Filtered query
- **WHEN** status and priority filters are supplied
- **THEN** only tasks matching both are returned

### Requirement: Modern, responsive presentation
The screen SHALL follow the app's design tokens, with a summary header, a team panel and a task table that adapts to narrow screens.

#### Scenario: Narrow viewport
- **WHEN** the viewport is phone width
- **THEN** the layout stacks without horizontal page scroll
