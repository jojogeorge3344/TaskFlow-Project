## Purpose

Employee Management enables Administrators to list, search, filter, create, update, and activate/deactivate employee accounts, assign employees to departments and roles, prevent duplicate email addresses, and avoid accidental deletion when related task records exist.

## ADDED Requirements

### Requirement: List employees with search and filters

The system SHALL allow Administrators to list employees and search/filter by employee name, email, department, role, and account status.

#### Scenario: Admin lists all employees

- **WHEN** an Administrator requests the employee list
- **THEN** the system returns all employees with their details

#### Scenario: Filter by name or email

- **WHEN** an Administrator searches by name or email
- **THEN** the system returns matching employees

#### Scenario: Filter by department, role, and status

- **WHEN** an Administrator applies department, role, and status filters
- **THEN** the system returns employees matching all applied filters

#### Scenario: Non-admin cannot list employees

- **WHEN** a non-Administrator attempts to list employees
- **THEN** the system responds as unauthorized or forbidden

### Requirement: View employee details

The system SHALL allow Administrators to view any employee's details, and allow users to view their own details.

#### Scenario: Admin views any employee

- **WHEN** an Administrator views another employee's details
- **THEN** the system returns the full employee details

#### Scenario: User views own details

- **WHEN** an authenticated user views their own details
- **THEN** the system returns their own details

### Requirement: Create employee account

The system SHALL allow Administrators to create employee accounts with full name, username, email, role, department assignment, and initial active status.

#### Scenario: Admin creates employee

- **WHEN** an Administrator creates an employee with valid required fields
- **THEN** the system creates the employee account

#### Scenario: Prevent duplicate email addresses

- **WHEN** an Administrator attempts to create an employee with an email that is already registered
- **THEN** the system rejects the creation with a validation error

#### Scenario: Prevent duplicate usernames

- **WHEN** an Administrator attempts to create an employee with a username that is already registered
- **THEN** the system rejects the creation with a validation error

#### Scenario: Non-admin cannot create employee

- **WHEN** a non-Administrator attempts to create an employee
- **THEN** the system responds as unauthorized or forbidden

### Requirement: Update employee details

The system SHALL allow Administrators to update employee details including full name, email, username, department, and role, while respecting that employees cannot change their own sensitive fields in certain contexts.

#### Scenario: Admin updates employee details

- **WHEN** an Administrator updates an employee's details
- **THEN** the system persists the changes

#### Scenario: Prevent duplicate email on update

- **WHEN** an Administrator updates an email to one that already exists
- **THEN** the system rejects the update with a validation error

#### Scenario: Prevent duplicate username on update

- **WHEN** an Administrator updates a username to one that already exists
- **THEN** the system rejects the update with a validation error

#### Scenario: Non-admin cannot update employee

- **WHEN** a non-Administrator attempts to update an employee
- **THEN** the system responds as unauthorized or forbidden

### Requirement: Activate or deactivate employee account

The system SHALL allow Administrators to activate or deactivate employee accounts. Deactivated accounts cannot log in.

#### Scenario: Admin activates employee

- **WHEN** an Administrator activates a deactivated employee account
- **THEN** the system marks the account as active

#### Scenario: Admin deactivates employee

- **WHEN** an Administrator deactivates an active employee account
- **THEN** the system marks the account as inactive

#### Scenario: Deactivated account cannot log in

- **WHEN** a deactivated account attempts to log in
- **THEN** the system rejects the login with a generic error (per auth requirements)

#### Scenario: Non-admin cannot change account status

- **WHEN** a non-Administrator attempts to change an employee's status
- **THEN** the system responds as unauthorized or forbidden

### Requirement: Safe handling of employee deletion

The system SHALL prevent accidental deletion of employees who have related task records. Employee records referenced by history SHALL remain protected through soft-delete semantics; hard deletion SHALL be blocked when related task records exist.

#### Scenario: Cannot delete employee with related tasks

- **WHEN** an attempt is made to delete an employee who has related task records
- **THEN** the system blocks the deletion

#### Scenario: Soft-delete semantics for historical integrity

- **WHEN** deletion is blocked due to related records
- **THEN** the system enforces referential integrity and preserves historical data
