# Spec Delta

## Purpose

Lets Administrators record which departments a manager oversees and which employees report to a manager, so the organization structure is captured in the system.

## ADDED Requirements

### Requirement: Administrator-only manager assignment

The system SHALL allow only Administrators to assign departments and employees to a manager, and to change or remove those assignments.

#### Scenario: Administrator assigns

- **WHEN** an Administrator saves department or employee assignments for a manager
- **THEN** the assignments are stored and shown on later views

#### Scenario: Non-administrator cannot assign

- **WHEN** a non-Administrator attempts to change manager assignments
- **THEN** the system rejects the request as forbidden and nothing changes

### Requirement: Multiple departments per manager

The system SHALL allow a manager to be assigned to any number of departments, and a department to have any number of managers.

#### Scenario: Several departments

- **WHEN** an Administrator assigns three departments to one manager
- **THEN** all three are stored and listed for that manager

#### Scenario: Replace the set

- **WHEN** an Administrator saves a manager with a different set of departments
- **THEN** the stored set equals the submitted set, with omitted departments removed

#### Scenario: Deleted department

- **WHEN** a department assigned to a manager is deleted
- **THEN** it no longer appears in that manager's departments

### Requirement: Only managers receive department assignments

The system SHALL accept department assignments only for users holding the Manager role.

#### Scenario: Non-manager target

- **WHEN** an Administrator assigns departments to an Employee or Administrator
- **THEN** the system rejects the request with an explanatory message

#### Scenario: Role changes away from Manager

- **WHEN** a manager's role is changed to a non-manager role
- **THEN** their department assignments are removed and employees assigned to them have no manager

### Requirement: One manager per employee

The system SHALL allow an employee to be assigned to at most one manager, and a manager to have any number of employees.

#### Scenario: Assign an employee

- **WHEN** an Administrator sets a manager on an employee
- **THEN** the employee shows that manager

#### Scenario: Reassign an employee

- **WHEN** an Administrator sets a different manager on an employee who already has one
- **THEN** the employee has only the new manager

#### Scenario: Clear a manager

- **WHEN** an Administrator clears an employee's manager
- **THEN** the employee has no manager

#### Scenario: Manager must be an active manager

- **WHEN** an Administrator sets as manager a user who is not an active Manager, or is deleted
- **THEN** the system rejects the request with an explanatory message

#### Scenario: No self-management

- **WHEN** an Administrator sets a user as their own manager
- **THEN** the system rejects the request

### Requirement: Assignments are visible to administrators and the people involved

The system SHALL show a manager's departments and employees, and an employee's manager, on the employee screens, and SHALL show a user their own manager and, for managers, their own departments and employees.

#### Scenario: Admin views a manager

- **WHEN** an Administrator opens a manager's details
- **THEN** the assigned departments and employees are listed

#### Scenario: Employee views own profile

- **WHEN** an employee opens their profile
- **THEN** their manager's name is shown, or none if unassigned

### Requirement: Assignments do not change access

The system SHALL NOT change what a manager can view or do based on these assignments.

#### Scenario: Visibility unchanged

- **WHEN** a manager is assigned departments or employees
- **THEN** their task, workload, report and dashboard visibility is the same as before the assignment
