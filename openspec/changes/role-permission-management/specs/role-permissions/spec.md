# Spec Delta

## Purpose

Defines roles, the fixed permission catalog and how permissions grant access, including which roles and grants are locked and what an administrator may change.

## ADDED Requirements

### Requirement: Fixed permission catalog
The system SHALL provide a fixed catalog of permissions, seeded on first run, that no user can add, rename, update or delete through the application.

#### Scenario: Catalog is present after first run
- **WHEN** the application starts against an empty database
- **THEN** every permission in the catalog exists with a stable key and a display name

#### Scenario: Catalog cannot be modified
- **WHEN** any user, including an Administrator, tries to create, update or delete a permission through the API
- **THEN** the request is rejected and the catalog is unchanged

### Requirement: System roles are locked
The system SHALL provide the system roles Administrator, Manager and Employee, whose permission grants SHALL NOT be changed and which SHALL NOT be renamed or deleted by anyone.

#### Scenario: System role permissions are fixed
- **WHEN** a request tries to add or remove a permission on Administrator, Manager or Employee
- **THEN** the request is rejected with a message that system roles are read-only and the grants are unchanged

#### Scenario: System role cannot be deleted or renamed
- **WHEN** a request tries to delete or rename a system role
- **THEN** the request is rejected and the role is unchanged

#### Scenario: Administrator holds every permission
- **WHEN** the Administrator system role's permissions are read
- **THEN** it holds every permission in the catalog, including ones added in later releases

### Requirement: Administrator manages custom roles
The system SHALL let an Administrator create custom roles with a unique name, choose their permissions from the catalog, rename them, and delete them.

#### Scenario: Create a custom role
- **WHEN** an Administrator creates a role named "Team Lead" with a chosen set of permissions
- **THEN** the role is stored as a custom role holding exactly those permissions

#### Scenario: Role names are unique
- **WHEN** an Administrator creates or renames a role to a name already in use, ignoring case
- **THEN** the request is rejected with a duplicate-name error

#### Scenario: Update a custom role's permissions
- **WHEN** an Administrator saves a changed permission set for a custom role
- **THEN** the role holds exactly the saved set and users in that role are affected from their next request

#### Scenario: Delete an unused custom role
- **WHEN** an Administrator deletes a custom role that no user holds
- **THEN** the role and its permission grants are removed

#### Scenario: Role in use cannot be deleted
- **WHEN** an Administrator deletes a custom role that one or more users hold
- **THEN** the request is rejected with the number of users affected and the role is unchanged

### Requirement: Only Administrators manage roles
The system SHALL allow only users in the Administrator system role to create, change or delete roles and to change a user's role, and SHALL NOT allow this permission to be granted to custom roles.

#### Scenario: Non-administrator is refused
- **WHEN** a Manager, an Employee or a user in a custom role calls a role-management endpoint
- **THEN** the response is forbidden and nothing changes

#### Scenario: Role management is not assignable
- **WHEN** an Administrator edits a custom role's permissions
- **THEN** the role-management permission is not available to tick

### Requirement: One role per user
The system SHALL assign each user exactly one role, and an Administrator SHALL be able to change a user's role to any system or custom role.

#### Scenario: Assign a custom role
- **WHEN** an Administrator sets a user's role to a custom role
- **THEN** the user holds that role's permissions from their next request and not those of their previous role

#### Scenario: Last administrator is protected
- **WHEN** an Administrator tries to change the role of the only active Administrator
- **THEN** the request is rejected and the user stays an Administrator

### Requirement: Access is decided by permissions
The system SHALL grant or refuse each protected operation based on whether the caller's role holds the required permission, evaluated against current stored data on every request.

#### Scenario: Permission granted
- **WHEN** a user whose role holds the permission for an operation calls it
- **THEN** the operation proceeds

#### Scenario: Permission missing
- **WHEN** a user whose role lacks the permission for an operation calls it
- **THEN** the response is forbidden

#### Scenario: Change applies without sign-in
- **WHEN** an Administrator removes a permission from a custom role while a user in that role is signed in
- **THEN** that user's next request for the affected operation is refused without signing in again

### Requirement: Signed-in user can read own permissions
The system SHALL return the signed-in user's role and permission keys so the client can show only what the user may use.

#### Scenario: Permissions returned after sign-in
- **WHEN** a signed-in user requests their profile
- **THEN** the response includes the role name and the full list of permission keys held by that role

### Requirement: Existing access is preserved for system roles
The system SHALL grant each system role the access it had before this change, so Administrator, Manager and Employee behave as they did.

#### Scenario: Manager keeps manager access
- **WHEN** a Manager uses reports, workload, task creation and reassignment, and reminder settings
- **THEN** each works as before, and Administrator-only screens still refuse the Manager

#### Scenario: Employee keeps employee access
- **WHEN** an Employee signs in
- **THEN** they land on their own dashboard and see only their own tasks, and manager and administrator screens are refused
