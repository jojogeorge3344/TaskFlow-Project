# Spec Delta

## ADDED Requirements

### Requirement: Department Management page layout

The Department Management screen SHALL show a page header with the title "Department Management", a subtitle with the number of departments, and an "Add department" primary button aligned to the right. Below the header SHALL be a search field and then the department list.

#### Scenario: Header shows count

- **WHEN** an Administrator opens `/departments` and 6 departments exist
- **THEN** the header shows "Department Management", a subtitle that includes "6 departments", and an "Add department" button

### Requirement: Department card grid

Departments SHALL be shown as cards in a responsive grid. The grid SHALL use one column below 576px, two columns from 576px, and three or more columns from 1200px. Each card SHALL show the department name, its description (or "No description"), the employee count, and Edit, Delete, and View employees actions.

#### Scenario: Card content

- **WHEN** the "Engineering" department has 12 employees
- **THEN** its card shows "Engineering", its description, and "12 employees"

#### Scenario: Mobile view

- **WHEN** the screen is viewed 390px wide
- **THEN** cards stack in a single column with no horizontal page scroll

### Requirement: Search departments

The search field SHALL filter the shown departments by name or description as the user types, without a server round-trip.

#### Scenario: Filter by name

- **WHEN** the Administrator types "fin"
- **THEN** only departments whose name or description contains "fin" (case-insensitive) are shown

### Requirement: View employees of a department

The View employees action SHALL navigate to Employee Management already filtered to that department.

#### Scenario: Navigate to filtered employees

- **WHEN** the Administrator clicks View employees on the department with id 3
- **THEN** the app navigates to `/employees?departmentId=3`

### Requirement: Add and edit department in a dialog

Add and Edit SHALL open a modal dialog with Name (required) and Description fields. The dialog title SHALL say "Add department" or "Edit department". Validation and server errors (such as a duplicate name) SHALL be shown inside the dialog without closing it. On success, the dialog SHALL close, the list SHALL refresh, and a success toast SHALL be shown.

#### Scenario: Duplicate name

- **WHEN** the Administrator saves a department name that already exists
- **THEN** the dialog stays open and shows the server's error message

#### Scenario: Required name

- **WHEN** the Administrator submits the dialog with an empty name
- **THEN** an inline "Name is required" message is shown and nothing is saved

### Requirement: Guarded department deletion

The Delete action SHALL be disabled for a department that has one or more employees, and SHALL show a tooltip explaining that the employees must be reassigned first. For an empty department, Delete SHALL open a confirmation dialog that names the department before deleting it.

#### Scenario: Department with employees

- **WHEN** a department has 4 employees
- **THEN** its Delete action is disabled and its tooltip says the employees must be reassigned before deletion

#### Scenario: Confirmed delete

- **WHEN** the Administrator deletes an empty department and confirms
- **THEN** the department is removed from the list and a success toast is shown

### Requirement: Department list feedback states

The screen SHALL show a loading placeholder while loading. It SHALL show an empty state with an "Add department" action when no departments exist, a "no matches" message when the search matches nothing, and an error message with Retry when loading fails.

#### Scenario: No departments yet

- **WHEN** no departments exist
- **THEN** the screen shows an empty state that invites the Administrator to add the first department
