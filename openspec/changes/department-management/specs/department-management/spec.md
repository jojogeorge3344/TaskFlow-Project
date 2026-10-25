# Spec Delta

## Purpose

Lets Administrators organise people into real departments: department CRUD with manager and employee counts, full-replacement member management, and placing users in a department when employees are created or edited.

## ADDED Requirements

### Requirement: View departments with member counts
The system SHALL expose `GET /api/v1/departments` to users holding `departments.view`, returning every non-deleted department with its id, name, description, creation time, manager count, and employee count. Manager count is the number of manager links; employee count is the number of non-deleted users assigned to the department.

#### Scenario: Administrator lists departments
- **WHEN** an Administrator holding `departments.view` sends `GET /api/v1/departments`
- **THEN** the response is `200 OK` with each department's id, name, description, createdAt, managerCount, and employeeCount

#### Scenario: Unauthenticated or unauthorized access is refused
- **WHEN** a caller without a valid session sends the request → `401 Unauthorized`
- **WHEN** an authenticated caller lacking `departments.view` (e.g. Employee by default) sends the request → `403 Forbidden`

### Requirement: Create a department
The system SHALL allow users holding `departments.manage` to create a department via `POST /api/v1/departments`. Name is required and at most 200 characters; description is optional and at most 500 characters. Names are trimmed and must be unique among non-deleted departments, compared case-insensitively.

#### Scenario: Successful creation
- **WHEN** an Administrator posts a valid name and optional description
- **THEN** the response is `201 Created` with a `Location` header pointing at the new department and the body carries the id, trimmed name, description, and createdAt

#### Scenario: Duplicate name is rejected
- **WHEN** an Administrator posts a name that matches an existing non-deleted department ignoring case
- **THEN** the response is `400 Bad Request` with the error `Department name already exists.` and no department is created

#### Scenario: Invalid input is rejected
- **WHEN** the name is blank or longer than 200 characters, or the description exceeds 500 characters
- **THEN** the response is `400 Bad Request` with a validation problem body and no department is created

### Requirement: Rename a department
The system SHALL allow users holding `departments.manage` to update a department's name and description via `PUT /api/v1/departments/{id}`, applying the same trim and uniqueness rules as creation, without changing its creation time.

#### Scenario: Successful rename
- **WHEN** an Administrator renames a department to an unused name
- **THEN** the response is `200 OK` with the updated values and the same createdAt

#### Scenario: Unknown department
- **WHEN** the id does not exist or is soft-deleted
- **THEN** the response is `404 Not Found`

#### Scenario: Rename collides with another department
- **WHEN** the new name matches a different non-deleted department ignoring case
- **THEN** the response is `400 Bad Request` with the error `Department name already exists.`

### Requirement: Delete is blocked while the department has members
The system SHALL allow users holding `departments.manage` to soft-delete a department via `DELETE /api/v1/departments/{id}`, but MUST refuse with `400 Bad Request` (error `Department cannot be deleted because it still has members. Remove them first.`) while any manager link or any assigned non-deleted employee remains. A successful delete returns `204 No Content` and hides the department from all reads.

#### Scenario: Delete with an assigned employee
- **WHEN** an Administrator deletes a department that still has employees or managers
- **THEN** the response is `400 Bad Request` naming members and the department remains

#### Scenario: Delete an empty department
- **WHEN** an Administrator deletes a department with no managers and no employees
- **THEN** the response is `204 No Content`, the department disappears from the list, and fetching it returns `404`

### Requirement: Read department members
The system SHALL expose `GET /api/v1/departments/{id}/members` to users holding `departments.view`, returning the department id plus the managers (users linked as managers of the department) and employees (users assigned to the department), each as id, fullName, username, email, and isActive, ordered by full name. Inactive members MUST be included so they can be reviewed; soft-deleted users are excluded.

#### Scenario: Members are returned with active flags
- **WHEN** a user with `departments.view` requests the members of a department containing an active manager, an active employee, and an inactive employee
- **THEN** the response is `200 OK` listing all three with `isActive` reflecting each user's status

#### Scenario: Unknown department
- **WHEN** the id does not exist or is soft-deleted
- **THEN** the response is `404 Not Found`

### Requirement: Replace department members atomically
The system SHALL allow users holding `departments.manage` to replace a department's entire membership via `PUT /api/v1/departments/{id}/members` with the full sets of managerIds and employeeIds. Managers not in the new set lose their link; employees removed from the set have their department cleared. Validation MUST complete before any mutation so that an invalid request leaves membership unchanged.

#### Scenario: Assign and clear members
- **WHEN** an Administrator replaces members with one manager id and two employee ids
- **THEN** the response is `200 OK` with exactly those members, the employees' user records show this department, and the department's counts rise to match

#### Scenario: Clearing all members
- **WHEN** an Administrator replaces members with empty manager and employee sets
- **THEN** the response is `200 OK` with no members, the affected users' department becomes unassigned, and counts drop to zero

#### Scenario: Employee already in another department is moved
- **WHEN** an employee id belonging to a user currently assigned to a different department is included in the new employee set
- **THEN** the user's department becomes this department and the previous department loses that employee

### Requirement: Member replacement validates every submitted user
Every manager id MUST resolve to an active, non-deleted user whose role is Manager, otherwise the request fails with `400 Bad Request` and the error `User {id} is not an active manager.`. Every employee id MUST resolve to an active, non-deleted user of any role, otherwise the request fails with `400 Bad Request` and the error `User {id} is not an active user.`. Duplicate ids in the request are ignored.

#### Scenario: Inactive or wrong-role manager is rejected atomically
- **WHEN** the manager set contains a user who is inactive, soft-deleted, or not in the Manager role
- **THEN** the response is `400 Bad Request` with the error `User {id} is not an active manager.` and no membership changes are applied

#### Scenario: Inactive employee is rejected atomically
- **WHEN** the employee set contains an inactive or soft-deleted user
- **THEN** the response is `400 Bad Request` with the error `User {id} is not an active user.` and no membership changes are applied

### Requirement: Department permissions gate the API
The system SHALL protect department endpoints with `departments.view` for reads and `departments.manage` for writes. Administrators hold both permissions unconditionally; Managers hold `departments.view` by default; Employees hold neither by default.

#### Scenario: Manager has read-only access
- **WHEN** a Manager calls a department read endpoint → `200 OK`
- **WHEN** a Manager calls create, update, replace members, or delete → `403 Forbidden` and data is unchanged

#### Scenario: Employee has no access
- **WHEN** an Employee calls any department endpoint
- **THEN** the response is `403 Forbidden`

### Requirement: Department Management screen
The frontend SHALL provide a Department Management screen at `/departments`, guarded by `departments.manage`, showing department cards in the same layout as Project Management: name, description (or "No description"), and separate manager and employee counts with correct singular/plural labels, plus Add and Edit dialogs (name required, optional description).

#### Scenario: Administrator opens the screen
- **WHEN** a user holding `departments.manage` navigates to `/departments`
- **THEN** the Department Management screen renders one card per department showing its name, description, manager count, and employee count

#### Scenario: Route is guarded
- **WHEN** a user lacking `departments.manage` (e.g. Manager or Employee by default) navigates to `/departments`
- **THEN** the route guard blocks access

#### Scenario: Create and rename through the dialogs
- **WHEN** the Administrator submits the Add or Edit dialog with a trimmed name
- **THEN** the matching create or update API call is made, a success toast names the department, the dialog closes, and the list reloads; on a server error the dialog stays open and shows the error

### Requirement: Department screen search and states
The Department Management screen SHALL offer client-side search filtering by name without additional API calls, a no-match state that offers to clear the search, skeleton cards while the first load is in progress, an empty state inviting the first department, and an error state with a retry action that reloads the list.

#### Scenario: Client-side search
- **WHEN** the Administrator types a name fragment into search
- **THEN** the list filters client-side by name without additional API calls, and a no-match state offers to clear the search

### Requirement: Manage members dialog replaces membership from the UI
The screen SHALL provide a Manage members dialog per department that loads current members together with active Manager-role and active Employee-role user pickers in parallel, pre-checks current members, marks inactive current members with an Inactive badge instead of dropping them, and on save submits the full checked sets to the replace-members API. Save failures keep the dialog open and show the server error; success shows a toast, closes the dialog, and reloads the list.

#### Scenario: Assign members
- **WHEN** the Administrator ticks one manager and two employees and saves
- **THEN** the dialog sends the complete checked id sets, shows a members-updated toast, closes, and the refreshed list shows the new counts

#### Scenario: Server rejects the replacement
- **WHEN** the replace-members API returns an error
- **THEN** the dialog stays open displaying the error message and the department's membership is unchanged

### Requirement: Deleting from the screen is blocked while members exist
The Department Management screen SHALL disable the delete action for any department whose manager or employee count is non-zero, explaining that members must be removed first, and SHALL only allow deletion of empty departments after confirmation.

#### Scenario: Delete attempt on a department with members
- **WHEN** the Administrator activates Delete on a department with managers or employees
- **THEN** no confirmation dialog opens, no delete API call is made, and the control is announced as disabled with a hint naming the member counts

#### Scenario: Delete an empty department
- **WHEN** the Administrator confirms deletion of a department with no members
- **THEN** the delete API is called, a success toast names the department, and the list reloads without it

### Requirement: Employee form assigns a department
The employee create/edit form SHALL include a Department select offering "Unassigned" plus the available departments, and the submitted user create/update payload SHALL carry the chosen departmentId (null when unassigned). The employee details view SHALL show the assigned department or "Unassigned". Unknown department ids are rejected by the API with `400 Bad Request` (`Department does not exist.`).

#### Scenario: Create an employee inside a department
- **WHEN** an Administrator creates a user with a department selected
- **THEN** the create payload includes that departmentId and the saved user's details show the department name

#### Scenario: Clear the department
- **WHEN** the Administrator sets the select back to Unassigned and saves
- **THEN** the payload sends departmentId null and the user's department becomes unassigned

#### Scenario: Stale department id is rejected
- **WHEN** the payload references a department id that does not exist or is soft-deleted
- **THEN** the API responds `400 Bad Request` with the error `Department does not exist.` and the user is not changed
