# Spec Delta

## ADDED Requirements

### Requirement: Employee Management page layout

The Employee Management screen SHALL show, in order: a page header with the title "Employee Management", a subtitle with the number of employees that match the current filters (the length of the filtered list the API returns; no separate total is requested), and an "Add employee" primary button aligned to the right; a filter toolbar; and the employee list.

#### Scenario: Header shows matching count

- **WHEN** an Administrator opens `/employees` and 24 employees match the current filters
- **THEN** the header shows "Employee Management", a subtitle that includes "24 employees", and an "Add employee" button

### Requirement: Employee filter toolbar

The screen SHALL provide a search field (name or email), a department filter, a role filter, a status filter, and a Reset action. Search SHALL apply automatically after the user stops typing for 300ms. Each select filter SHALL apply as soon as it changes.

#### Scenario: Search while typing

- **WHEN** the Administrator types "anu" in the search field and pauses
- **THEN** the list refreshes to show only employees whose name or email matches "anu"

#### Scenario: Reset filters

- **WHEN** filters are applied and the Administrator clicks Reset
- **THEN** every filter returns to its default and the full list is shown

### Requirement: Pre-filter by department from the URL

The screen SHALL read an optional `departmentId` query parameter and apply it as the initial department filter.

#### Scenario: Opened from a department

- **WHEN** the Administrator opens `/employees?departmentId=3`
- **THEN** the department filter shows department 3 and only that department's employees are listed

### Requirement: Employee list presentation

On wide screens the list SHALL be a table with these columns: Employee (initials avatar, full name, username), Email, Department, Role, Status, and Actions. Role and status SHALL be shown as colored badges. An employee with no department SHALL show "Unassigned".

#### Scenario: Status badge

- **WHEN** an employee is inactive
- **THEN** their status shows as a grey "Inactive" badge, and active employees show a green "Active" badge

### Requirement: Responsive employee list

On viewports narrower than 768px, each employee SHALL be shown as a stacked card with the same information and actions. The page SHALL NOT scroll horizontally.

#### Scenario: Mobile view

- **WHEN** the screen is viewed 390px wide
- **THEN** employees are shown as cards, the filters stack vertically, and there is no horizontal page scroll

### Requirement: Loading, empty, and error states

The list SHALL show a loading placeholder while data loads. It SHALL show an empty state when no employees match, with a "Clear filters" action if filters are active. It SHALL show an error message with a Retry action if loading fails.

#### Scenario: No matches

- **WHEN** the active filters match no employees
- **THEN** the list shows "No employees match these filters" and a "Clear filters" action

#### Scenario: Load failure

- **WHEN** the employee request fails
- **THEN** an error message is shown with a Retry button that reloads the list

### Requirement: Add and edit employee in a side panel

Add and Edit SHALL open a form in a side panel that slides in over the list. The panel title SHALL say "Add employee" or "Edit employee". The form SHALL include employee code (optional), full name, username, email, role, and department. Initial status SHALL appear only when adding. Adding SHALL require a password (at least 6 characters); editing SHALL offer an optional new password that is kept unchanged when left blank.

#### Scenario: Open add form

- **WHEN** the Administrator clicks "Add employee"
- **THEN** a side panel opens with empty fields, including an optional employee code field and a password field, and focus moves to the first field

#### Scenario: Open edit form

- **WHEN** the Administrator clicks Edit on an employee
- **THEN** the side panel opens with that employee's values filled in, including the employee code, and an empty optional "New password" field

### Requirement: Inline form validation and server errors

The form SHALL show an inline message under each invalid field (required fields, email format) after the field is touched or on submit. While saving, the Save button SHALL be disabled. A server error (for example, duplicate email or username) SHALL be shown inside the panel without closing it.

#### Scenario: Duplicate email

- **WHEN** the Administrator saves an employee whose email already exists
- **THEN** the panel stays open and shows the server's error message

#### Scenario: Successful save

- **WHEN** the Administrator saves valid data
- **THEN** the panel closes, the list refreshes, and a success toast is shown

### Requirement: Employee details view

Selecting an employee's name SHALL open a read-only details view. It SHALL show full name, employee code, username, email, role, department, status, created date, and last login. From the details view, the Administrator SHALL be able to go to Edit.

#### Scenario: View details

- **WHEN** the Administrator clicks an employee's name
- **THEN** a details panel opens with that employee's information and an Edit action

### Requirement: Confirm before deactivating

Deactivating an active employee SHALL require confirmation in a dialog that names the employee and explains that they will not be able to sign in. Activating SHALL NOT require confirmation. A success toast SHALL be shown after either change.

#### Scenario: Deactivate with confirmation

- **WHEN** the Administrator chooses Deactivate on an active employee and confirms
- **THEN** the employee's status changes to Inactive and a success toast is shown

#### Scenario: Cancel deactivation

- **WHEN** the Administrator chooses Deactivate and then cancels the dialog
- **THEN** the employee stays Active

### Requirement: Changing sign-in credentials ends the employee's sessions

When an Administrator changes an employee's username or role, sets a new password, or deactivates the employee, the system SHALL end every existing session of that employee, and reactivating SHALL NOT restore them. The employee's next request SHALL be rejected and the app SHALL return them to the login screen with a message. Other edits (name, email, department, employee code) SHALL NOT end sessions.

#### Scenario: Password changed by an Administrator

- **WHEN** an Administrator sets a new password for an employee who is signed in elsewhere
- **THEN** within about a minute, or as soon as that employee makes a request or returns to the tab, the employee is taken to the login screen with a notice that their session ended
- **AND** only the new password signs them in

#### Scenario: Administrator changes their own username

- **WHEN** the signed-in Administrator changes their own username or password
- **THEN** they are signed out at once and the login screen asks them to sign in with the new details

#### Scenario: Non-credential edit

- **WHEN** an Administrator changes only an employee's full name
- **THEN** the employee stays signed in

### Requirement: Signed-out sessions are noticed promptly

While a user is signed in, the app SHALL re-check their session at least once a minute and whenever the browser tab becomes visible, so an ended session returns them to the login screen without waiting for their next action.

#### Scenario: Idle tab after a password change

- **WHEN** an employee's password is changed while their tab is open but idle
- **THEN** within about a minute the app shows the login screen with a notice that their session ended

### Requirement: New passwords cannot be blank

A new password set while editing an employee SHALL be at least 6 characters and SHALL NOT consist only of whitespace. Leaving the field empty SHALL keep the current password.

#### Scenario: Spaces only

- **WHEN** an Administrator enters only spaces as the new password and saves
- **THEN** the form shows "Password cannot be only spaces." and nothing is saved
