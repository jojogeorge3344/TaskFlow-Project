## Purpose

Department Management enables Administrators to create, update, list, and safely delete departments, allows Admins and Managers to view departments and their employees, enforces unique department names, and prevents deletion of departments that still have associated employees.

## ADDED Requirements

### Requirement: List departments

The system SHALL allow Administrators and Managers to list departments, including each department's employee count.

#### Scenario: Admin lists departments

- **WHEN** an Administrator requests the departments list
- **THEN** the system returns all departments with employee counts

#### Scenario: Manager lists departments

- **WHEN** a Manager requests the departments list
- **THEN** the system returns all departments with employee counts

#### Scenario: Non-authorized user cannot list departments

- **WHEN** a user who is not Administrator or Manager requests departments
- **THEN** the system responds with an unauthorized or forbidden response

### Requirement: View department employees

The system SHALL allow Administrators and Managers to view employees associated with a specific department.

#### Scenario: Admin views department employees

- **WHEN** an Administrator requests employees for a department
- **THEN** the system returns the list of employees in that department

#### Scenario: Manager views department employees

- **WHEN** a Manager requests employees for a department
- **THEN** the system returns the list of employees in that department

### Requirement: Create department

The system SHALL allow only Administrators to create departments.

#### Scenario: Admin creates department

- **WHEN** an Administrator creates a department with valid name and optional description
- **THEN** the system creates the department

#### Scenario: Non-admin cannot create department

- **WHEN** a non-Administrator attempts to create a department
- **THEN** the system responds as unauthorized or forbidden

#### Scenario: Prevent duplicate department names

- **WHEN** an Administrator attempts to create a department with a name that already exists
- **THEN** the system rejects the creation with a validation error

### Requirement: Update department

The system SHALL allow only Administrators to update department details.

#### Scenario: Admin updates department

- **WHEN** an Administrator updates an existing department's name or description
- **THEN** the system persists the changes

#### Scenario: Prevent duplicate department names on update

- **WHEN** an Administrator updates a department name to one that already exists
- **THEN** the system rejects the update with a validation error

#### Scenario: Non-admin cannot update department

- **WHEN** a non-Administrator attempts to update a department
- **THEN** the system responds as unauthorized or forbidden

### Requirement: Safe delete of department

The system SHALL allow only Administrators to delete departments, but SHALL prevent deletion of departments that still have associated employees. Department records SHALL be soft-deleted to protect historical references.

#### Scenario: Admin deletes empty department

- **WHEN** an Administrator deletes a department with no associated employees
- **THEN** the system soft-deletes the department

#### Scenario: Prevent deletion if employees exist

- **WHEN** an Administrator attempts to delete a department that has associated employees
- **THEN** the system blocks deletion and returns an error

#### Scenario: Non-admin cannot delete department

- **WHEN** a non-Administrator attempts to delete a department
- **THEN** the system responds as unauthorized or forbidden
