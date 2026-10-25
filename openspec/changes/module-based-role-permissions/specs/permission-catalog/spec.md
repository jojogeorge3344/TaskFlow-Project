# Spec Delta

## Purpose

Defines the single module-based permission catalogue, how roles receive permissions from it, and how every protected action is enforced, so access can be granted per feature rather than per audience.

## ADDED Requirements

### Requirement: One module-based catalogue
The system SHALL expose one permission catalogue organised as Module, Screen, Tab or Section, Feature and Action. Each module SHALL appear exactly once, and no module, screen or feature SHALL be duplicated per role or audience.

#### Scenario: Dashboard appears once
- **WHEN** the catalogue is read
- **THEN** Dashboard is a single module whose features cover organization, team and personal views, with no Administrator, Manager or Employee dashboard sections

#### Scenario: Every real feature is listed
- **WHEN** the catalogue is compared with the application's protected API actions and screens
- **THEN** every protected action and every screen, tab and widget that can be restricted maps to exactly one catalogue entry under its module

#### Scenario: Only supported actions are offered
- **WHEN** a feature has no Create, Delete, Export or Approve behaviour in the application
- **THEN** the catalogue does not offer that action for the feature

#### Scenario: Catalogue is read-only
- **WHEN** any user, including an Administrator, tries to add, rename or delete a catalogue entry through the API
- **THEN** the request is rejected and the catalogue is unchanged

### Requirement: Scoped view permissions
The system SHALL let the Dashboard view permission and the task view permission carry a data scope chosen per role, with the levels Own, Team and Organization for the dashboard and Own and All for tasks.

#### Scenario: Scope limits data
- **WHEN** a role holds Dashboard view with scope Team
- **THEN** its users receive team-level dashboard data and are refused organization-level dashboard data

#### Scenario: Higher scope includes lower
- **WHEN** a role holds scope Organization
- **THEN** its users can also open the Team and Own views

### Requirement: Parent permissions expand to children
The system SHALL treat each pre-existing permission key as a parent that grants all of its child permissions, and SHALL expand every role's held parent keys into explicit child grants when the catalogue is introduced.

#### Scenario: No access is lost or widened
- **WHEN** the catalogue is introduced on a database with the existing roles
- **THEN** each role's effective access to every existing endpoint and screen is identical to before, and no role gains a permission it did not effectively hold

#### Scenario: Custom role assignments survive
- **WHEN** the catalogue is introduced
- **THEN** every custom role keeps its name, its users and its effective permissions

### Requirement: System role defaults are preserved and protected
The system SHALL keep Administrator holding every assignable permission, Manager and Employee holding their previous effective access, and SHALL reject any change to or deletion of a system role.

#### Scenario: System roles cannot be edited
- **WHEN** a request tries to change the permissions of, rename or delete Administrator, Manager or Employee
- **THEN** the request is rejected as read-only and the role is unchanged

#### Scenario: New permissions reach the Administrator
- **WHEN** a later release adds catalogue entries
- **THEN** the Administrator role holds them on the next start, and Manager and Employee receive them only if their defaults say so

#### Scenario: Role Configuration stays Administrator-only
- **WHEN** an Administrator edits a custom role
- **THEN** the Role Configuration permissions cannot be granted to that role

### Requirement: Custom roles are independent of system templates
The system SHALL let an Administrator create custom roles with any combination of assignable permissions, including by copying an existing role, and changes to a custom role SHALL NOT alter any system role.

#### Scenario: Copy a system role
- **WHEN** an Administrator duplicates Manager as a new custom role and removes a permission
- **THEN** the custom role lacks that permission and Manager still holds it

#### Scenario: Inconsistent selection is rejected
- **WHEN** a role is saved with a child permission whose required View permission is not selected
- **THEN** the request is rejected with the missing dependency named

### Requirement: Every protected action is enforced on the backend
The system SHALL check the caller's role permissions on each protected API action regardless of what the client shows, resolving them from the database on every request.

#### Scenario: Bypassing the UI is refused
- **WHEN** a user whose role lacks Delete tasks calls the delete task endpoint directly
- **THEN** the response is 403 and no task is deleted

#### Scenario: Change applies without sign-in
- **WHEN** an Administrator removes a permission from a role
- **THEN** a signed-in user in that role is refused that action on their next request

#### Scenario: Destructive actions have their own permissions
- **WHEN** the delete task and delete employee endpoints are called
- **THEN** they require Delete tasks and Delete employees respectively, not Role Configuration access

#### Scenario: Participation rules still apply
- **WHEN** a user holds View tasks with scope Own
- **THEN** they can read, comment on and change the status of only tasks they created or are assigned to
