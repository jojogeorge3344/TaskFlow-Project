# Spec Delta

## Purpose

Defines the Administrator-only Settings screen where roles are listed and their permissions are viewed or edited using checkboxes.

## ADDED Requirements

### Requirement: Permissions Management screen under Settings
The system SHALL provide a Permissions Management screen under Settings that is reachable and visible only to Administrators.

#### Scenario: Administrator sees the entry
- **WHEN** an Administrator opens the Settings menu
- **THEN** a Permissions Management entry is shown and opens the screen

#### Scenario: Others cannot reach it
- **WHEN** a Manager, an Employee or a custom-role user opens the Permissions Management address directly
- **THEN** they are redirected away and the menu never shows the entry

### Requirement: Roles are listed with their type
The screen SHALL list every role, marking Administrator, Manager and Employee as system roles and the rest as custom roles.

#### Scenario: Role list
- **WHEN** the screen loads
- **THEN** all roles are listed, system roles are labeled as such, and selecting a role shows its permissions

### Requirement: Permissions are shown as checkboxes
The screen SHALL show every catalog permission as a checkbox for the selected role, ticked when the role holds it.

#### Scenario: Checkbox state matches the role
- **WHEN** a role is selected
- **THEN** each permission is ticked exactly when that role holds it

### Requirement: System roles are view-only
The screen SHALL show system roles with all checkboxes disabled and SHALL offer no save, rename or delete action for them.

#### Scenario: System role is read-only
- **WHEN** an Administrator selects Administrator, Manager or Employee
- **THEN** every checkbox is disabled, no save or delete control is offered, and a note says the role is read-only

### Requirement: Custom roles can be created and edited
The screen SHALL let an Administrator create a custom role and edit its name and permission checkboxes, saving the change explicitly.

#### Scenario: Create a role
- **WHEN** an Administrator enters a name, ticks permissions and saves
- **THEN** the role appears in the list with those permissions ticked

#### Scenario: Edit a role
- **WHEN** an Administrator changes checkboxes on a custom role and saves
- **THEN** the saved state is shown and a confirmation appears

#### Scenario: Unsaved edits are not applied
- **WHEN** an Administrator changes checkboxes and leaves without saving
- **THEN** the role's stored permissions are unchanged

#### Scenario: Server rejection is shown
- **WHEN** a save is rejected, for example because the name is already used
- **THEN** the screen shows the reason and keeps the Administrator's edits

### Requirement: Custom roles can be deleted
The screen SHALL let an Administrator delete a custom role after confirmation, and SHALL show the reason when deletion is refused.

#### Scenario: Delete after confirmation
- **WHEN** an Administrator confirms deletion of an unused custom role
- **THEN** the role disappears from the list

#### Scenario: Role in use
- **WHEN** deletion is refused because users hold the role
- **THEN** the screen explains how many users hold it and the role remains

### Requirement: Administrators can assign a role to a user
The system SHALL let an Administrator choose any role, system or custom, for a user from Employee Management.

#### Scenario: Role picker lists all roles
- **WHEN** an Administrator edits a user in Employee Management
- **THEN** the role picker lists every system and custom role and saves the selection
