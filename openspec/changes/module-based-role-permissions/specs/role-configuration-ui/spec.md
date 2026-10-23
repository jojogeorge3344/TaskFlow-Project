# Spec Delta

## Purpose

Defines the Role Configuration screen used by Administrators to review and assign module-based permissions, and how the rest of the application hides what a role may not use.

## ADDED Requirements

### Requirement: Role selection shows the same permission tree
The screen SHALL list all roles, mark each as System or Custom with its user count, and show the same complete module-based tree for whichever role is selected.

#### Scenario: Same tree for every role
- **WHEN** an Administrator selects Employee, then Manager, then a custom role
- **THEN** each shows the identical modules, screens and features, with checkboxes reflecting that role's grants

#### Scenario: Enabled permissions are clear
- **WHEN** a role is open
- **THEN** each module, screen and feature shows how many of its permissions are enabled, and a checked, partial or unchecked state

### Requirement: Granular selection controls
The screen SHALL provide Select all, Deselect all, module-level, screen-level and feature-level selection, individual permission checkboxes, search, and an Enabled or Disabled filter.

#### Scenario: Module toggle
- **WHEN** an Administrator checks a module's checkbox on a custom role
- **THEN** every permission in that module becomes enabled, and unchecking clears them all

#### Scenario: Partial state
- **WHEN** only some permissions under a screen are enabled
- **THEN** the screen and its module show a partial state, not checked

#### Scenario: Search
- **WHEN** an Administrator types "delete"
- **THEN** only matching permissions and their ancestors are shown, and counts still describe the whole role

#### Scenario: Scope selection
- **WHEN** a permission has a data scope
- **THEN** the screen offers the scope levels as a selector next to it, and a scope can be chosen only while the permission is enabled

### Requirement: System roles are read-only in the screen
The screen SHALL display system roles with their accurate permissions, disabled controls and a clear protected indication, and SHALL offer to duplicate a system role as a custom role.

#### Scenario: System role is locked
- **WHEN** Administrator, Manager or Employee is selected
- **THEN** every checkbox is disabled, no Save or Delete is offered, and a Duplicate as custom action is available

### Requirement: Custom role lifecycle
The screen SHALL let an Administrator create, rename, edit, copy from another role and delete custom roles, and SHALL warn about unsaved changes.

#### Scenario: Unsaved changes
- **WHEN** an Administrator has edited a role and tries to switch roles or leave
- **THEN** they are asked to confirm discarding the changes

#### Scenario: Role in use
- **WHEN** an Administrator deletes a role that users hold
- **THEN** deletion is refused with the number of users affected

### Requirement: Responsive layout
The screen SHALL work from phone to desktop widths, showing a role rail beside the tree on wide screens and a role list that drills into the tree on narrow screens, with a save bar that stays reachable.

#### Scenario: Narrow screen
- **WHEN** the viewport is phone width
- **THEN** no content requires horizontal scrolling and the Save and Discard actions remain visible while editing

### Requirement: Unauthorized features are hidden and blocked in the application
The application SHALL hide menu items, tabs, buttons and dashboard widgets the signed-in user's role does not permit, and SHALL redirect direct navigation to a screen the role may not open.

#### Scenario: Hidden controls
- **WHEN** a role lacks Delete tasks
- **THEN** no delete control appears on the task list or task details

#### Scenario: Direct URL
- **WHEN** a user without access to Reports opens the reports URL
- **THEN** they are redirected and no report data is requested

#### Scenario: Menu follows the role
- **WHEN** a custom role holds only View tasks and View board
- **THEN** its menu shows only the entries those permissions open, plus always-available items
