# Spec Delta

## Purpose

Lets a custom role manage other custom roles through Role Configuration while stopping any role other than the Administrator from raising its own or another account's privileges to Administrator.

## ADDED Requirements

### Requirement: Role Configuration can be given to a custom role
The permission to open Role Configuration and manage roles SHALL be assignable to a custom role, and a user holding it SHALL be able to create, edit and delete custom roles.

#### Scenario: Custom role holds Role Configuration
- **WHEN** an administrator ticks Role Configuration for a custom role and saves
- **THEN** the role is saved and its users can open Role Configuration

#### Scenario: Delegated role manager creates a role
- **WHEN** a user whose role holds Role Configuration creates a custom role with ordinary permissions
- **THEN** the role is created

### Requirement: Privilege-raising permissions stay with the Administrator
Only the Administrator role SHALL be able to give a role the permissions Role Configuration, Assign roles, or Give the Administrator role. A user whose role is not Administrator SHALL NOT include them in a role they create or edit, nor edit a role that already holds them.

#### Scenario: Delegate tries to grant Assign roles
- **WHEN** a user who holds Role Configuration but is not an Administrator saves a custom role that includes Assign roles
- **THEN** the request is refused with a message that only the Administrator can grant it and the role is unchanged

#### Scenario: Delegate tries to grant Role Configuration
- **WHEN** the same user saves a role that includes Role Configuration
- **THEN** the request is refused and the role is unchanged

#### Scenario: Delegate edits a role that holds a restricted permission
- **WHEN** the same user edits a custom role that an administrator gave Assign roles
- **THEN** the request is refused

#### Scenario: Administrator grants them
- **WHEN** an Administrator saves a custom role that includes Role Configuration or Assign roles
- **THEN** the role is saved

#### Scenario: Restricted permissions are marked in Role Configuration
- **WHEN** a user who is not an Administrator edits a custom role in Role Configuration
- **THEN** Role Configuration, Assign roles and Give the Administrator role are not offered for ticking, and show as Administrator only when a role is viewed read-only

### Requirement: Giving the Administrator role has its own permission
Creating an employee as Administrator, or moving anyone into or out of the Administrator role, SHALL require a permission that only the Administrator role holds and that cannot be assigned to another role, in addition to the Assign roles permission.

#### Scenario: Assign without the permission
- **WHEN** a user with Assign roles but without the Administrator-only permission sets an employee's role to Administrator
- **THEN** the API returns 403 and the employee's role is unchanged

#### Scenario: Assign other roles
- **WHEN** the same user sets an employee's role to Manager, Employee or a custom role
- **THEN** the change is accepted as it is today

#### Scenario: Administrator assigns
- **WHEN** an Administrator sets an employee's role to Administrator
- **THEN** the change is accepted

#### Scenario: Administrator role not offered
- **WHEN** a user without the Administrator-only permission opens the employee form
- **THEN** the role list does not include Administrator

### Requirement: Employee update does not set passwords
Updating an employee through the employee API SHALL NOT change that employee's password, even when the request contains one. Passwords change only through the signed-in user's own password change or account recovery.

#### Scenario: Password field supplied
- **WHEN** a user with the edit employees permission submits an update that includes a password
- **THEN** the other fields are saved and the password is unchanged

### Requirement: Role dropdown works without role management
The employee form and role filter SHALL be able to list roles for a user who holds assign roles but not role management, without a failed request.

#### Scenario: Assign roles only
- **WHEN** a user holds assign roles and not role management and opens the employee form
- **THEN** the role list loads with all roles the user may assign and no error hint is shown
