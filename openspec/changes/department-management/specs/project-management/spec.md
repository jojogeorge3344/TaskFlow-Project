# Spec Delta

## Purpose

Makes projects a first-class domain concept: Project CRUD with employee counts for Administrators, project assignment on users and tasks, and the Project Management screen — the behaviour previously mislabelled as "departments".

## ADDED Requirements

### Requirement: View projects with employee counts
The system SHALL expose `GET /api/v1/projects` to users holding `projects.view`, returning every non-deleted project with its id, name, description, creation time, and employee count (non-deleted users assigned to the project).

#### Scenario: Manager lists projects
- **WHEN** a user holding `projects.view` sends `GET /api/v1/projects`
- **THEN** the response is `200 OK` with each project's id, name, description, createdAt, and employeeCount

#### Scenario: Unauthenticated or unauthorized access is refused
- **WHEN** a caller without a valid session sends the request → `401 Unauthorized`
- **WHEN** an authenticated caller lacking `projects.view` (e.g. Employee by default) sends the request → `403 Forbidden`

### Requirement: Create a project
The system SHALL allow users holding `projects.manage` to create a project via `POST /api/v1/projects`. Names are trimmed and must be unique among non-deleted projects, compared case-insensitively; duplicates are rejected with `400 Bad Request` and the error `Project name already exists.`

#### Scenario: Successful creation
- **WHEN** an Administrator posts a valid name and optional description
- **THEN** the response is `201 Created` with a `Location` header pointing at the new project and the body carries the id, trimmed name, description, and createdAt

#### Scenario: Duplicate name is rejected
- **WHEN** an Administrator posts a name matching an existing non-deleted project ignoring case
- **THEN** the response is `400 Bad Request` with the error `Project name already exists.` and no project is created

### Requirement: Rename a project
The system SHALL allow users holding `projects.manage` to update a project's name and description via `PUT /api/v1/projects/{id}`, applying the same trim and uniqueness rules as creation and leaving createdAt unchanged. Unknown or soft-deleted ids return `404 Not Found`; name collisions return `400 Bad Request` with `Project name already exists.`

#### Scenario: Successful rename
- **WHEN** an Administrator renames a project to an unused name
- **THEN** the response is `200 OK` with the updated values

#### Scenario: Unknown project
- **WHEN** the id does not exist or is soft-deleted
- **THEN** the response is `404 Not Found`

### Requirement: Delete is blocked while the project has employees
The system SHALL allow users holding `projects.manage` to soft-delete a project via `DELETE /api/v1/projects/{id}`, but MUST refuse with `400 Bad Request` (error `Project cannot be deleted because it has associated employees.`) while any non-deleted user is assigned to it. A successful delete returns `204 No Content`, removes the project's manager-oversight links, and hides the project from all reads.

#### Scenario: Delete with assigned employees
- **WHEN** an Administrator deletes a project that still has assigned users
- **THEN** the response is `400 Bad Request` naming the employee constraint and the project remains

#### Scenario: Delete an empty project
- **WHEN** an Administrator deletes a project with no assigned users
- **THEN** the response is `204 No Content`, the project disappears from the list, and fetching it returns `404`

### Requirement: List a project's employees
The system SHALL expose `GET /api/v1/projects/{id}/employees` to users holding `projects.view`, returning the non-deleted users currently assigned to the project.

#### Scenario: Employees of a project
- **WHEN** a user holding `projects.view` requests the employees of a project with three assigned users
- **THEN** the response is `200 OK` listing those users

### Requirement: Project permissions gate the API
The system SHALL protect project endpoints with `projects.view` for reads and `projects.manage` for writes. Administrators hold both permissions unconditionally; Managers hold `projects.view` by default; Employees hold neither by default.

#### Scenario: Manager has read-only access
- **WHEN** a Manager calls a project read endpoint → `200 OK`
- **WHEN** a Manager calls create, update, or delete → `403 Forbidden` and data is unchanged

### Requirement: Users and tasks carry a project assignment
A user SHALL hold at most one project at a time, set through the user create and update APIs (`projectId`, null when unassigned), validated to reference an existing non-deleted project (`400 Bad Request` with `Project does not exist.` otherwise), and exposed on the user DTO as projectId and projectName. Tasks SHALL reference a project through their `projectId` field, and workload, report, dashboard, and profile surfaces SHALL label this field "Project" rather than "Department".

#### Scenario: Assign a user to a project
- **WHEN** an Administrator creates or updates a user with a projectId
- **THEN** the saved user DTO carries that projectId and projectName

#### Scenario: Unknown project id is rejected
- **WHEN** the payload references a project id that does not exist or is soft-deleted
- **THEN** the API responds `400 Bad Request` with the error `Project does not exist.` and the user is not changed

#### Scenario: Reports and dashboards use project wording
- **WHEN** an Administrator opens workload, reports (including the CSV export), or dashboards
- **THEN** the grouping/assignment field is labelled Project and carries the user's or task's project name

### Requirement: Managers oversee multiple projects
The system SHALL let a manager be assigned to any number of projects through the user assignments API, storing one manager↔project link per pair, and SHALL reflect those links on the manager's user DTO as an ordered list of projects. Removing a project from the set removes the link; deleting a project removes its links.

#### Scenario: Replace a manager's projects
- **WHEN** an Administrator updates a manager's assignments with a different set of project ids
- **THEN** the manager's project links equal exactly the submitted set, ordered by project name

### Requirement: Project Management screen
The frontend SHALL provide a Project Management screen at `/projects`, guarded by `projects.view`, showing project cards with name, description (or "No description"), and employee count. Add, Edit, and Delete controls SHALL render only for users holding `projects.manage`, and a "View employees" link SHALL render only for users holding `employees.manage`, opening the employee list filtered to that project.

#### Scenario: Manager sees a read-only screen
- **WHEN** a Manager holding `projects.view` but not `projects.manage` opens `/projects`
- **THEN** project cards render with counts, and no Add, Edit, or Delete controls are shown

#### Scenario: Administrator manages projects
- **WHEN** an Administrator opens `/projects` and uses the Add or Edit dialogs (name required, optional description)
- **THEN** the corresponding project API calls are made with the same validation, confirmation, and toast behaviour as departments

#### Scenario: View employees link filters the employee list
- **WHEN** the Administrator activates "View employees" on a project
- **THEN** the app navigates to `/employees` with that project pre-selected as the filter

### Requirement: Project screen search, states, and delete guard
The Project Management screen SHALL offer client-side search over name and description, loading, empty, no-match, and error states with retry, and a delete action that is disabled (with a hint to reassign employees) while the project's employee count is non-zero, confirming deletion only for empty projects.

#### Scenario: Delete is blocked while the project has employees
- **WHEN** the Administrator activates Delete on a project whose employee count is non-zero
- **THEN** no confirmation dialog opens and no delete API call is made
