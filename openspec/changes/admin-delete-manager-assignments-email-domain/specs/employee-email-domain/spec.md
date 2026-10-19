# Spec Delta

## Purpose

Gives every employee a company email on one fixed domain, so addresses are consistent and the same person or name cannot appear twice under different domains.

## ADDED Requirements

### Requirement: Fixed email domain

The system SHALL give every employee an email address ending in `@taskflow.com`, and SHALL reject any submitted address on a different domain.

#### Scenario: Valid address

- **WHEN** an Administrator creates an employee with local part `jane.doe`
- **THEN** the employee's email is `jane.doe@taskflow.com`

#### Scenario: Other domain submitted

- **WHEN** a request supplies an email whose domain is not `taskflow.com`
- **THEN** the system rejects the request with a validation message

#### Scenario: Case is ignored

- **WHEN** an Administrator enters `Jane.Doe` as the local part
- **THEN** the stored email is `jane.doe@taskflow.com`

### Requirement: Static suffix in the UI

The system SHALL show `@taskflow.com` as fixed, non-editable text after the email input wherever an employee email is entered or edited, and the user SHALL type only the part before it.

#### Scenario: Create form

- **WHEN** an Administrator opens the create-employee form
- **THEN** the email field accepts only the local part and displays `@taskflow.com` beside it

#### Scenario: Pasted full address

- **WHEN** a user pastes `jane@other.com` into the email field
- **THEN** the field keeps only the part before the `@`, and the suffix remains `@taskflow.com`

#### Scenario: Profile edit

- **WHEN** a user edits their own email on the profile screen
- **THEN** the same local-part input and static suffix are used

### Requirement: Local part rules

The system SHALL require the local part to be non-empty, at most 64 characters, and made only of letters, digits, dot, underscore and hyphen, not starting or ending with a dot and without consecutive dots.

#### Scenario: Invalid characters

- **WHEN** the local part contains a space or `@`
- **THEN** the system rejects it with a validation message

#### Scenario: Empty

- **WHEN** the local part is empty
- **THEN** the system rejects it with a validation message

### Requirement: Local-part uniqueness

The system SHALL treat two emails with the same local part, compared case-insensitively, as duplicates and SHALL reject creating or changing an email to a duplicate.

#### Scenario: Same email

- **WHEN** an Administrator creates an employee whose email already exists
- **THEN** the system rejects it with an "email already exists" message

#### Scenario: Same local part

- **WHEN** an Administrator creates an employee with local part `jane` and `jane@taskflow.com` exists
- **THEN** the system rejects it as a duplicate

#### Scenario: Own email unchanged

- **WHEN** an employee saves their profile without changing their email
- **THEN** the save is not rejected as a duplicate of themselves

#### Scenario: Deleted users

- **WHEN** an Administrator creates an employee whose local part belongs only to a deleted user
- **THEN** the system rejects it, because deleted accounts keep their email

### Requirement: Existing emails are converted

The system SHALL convert every existing employee email to the fixed domain by keeping its local part (dropping any character the local part rules do not allow) and replacing its domain with `taskflow.com`, and SHALL make any collision unique by appending a number to the local part of the later-created user.

#### Scenario: Different domain

- **WHEN** existing user `ravi@gmail.com` is migrated
- **THEN** their email becomes `ravi@taskflow.com`

#### Scenario: Seed accounts

- **WHEN** the seeded `admin@taskflow.local`, `manager@taskflow.local` and `employee@taskflow.local` accounts are migrated
- **THEN** they become `admin@taskflow.com`, `manager@taskflow.com` and `employee@taskflow.com`

#### Scenario: Characters that are not allowed

- **WHEN** existing user `o'brien@gmail.com` is migrated
- **THEN** their email becomes `obrien@taskflow.com`, which the local part rules accept

#### Scenario: Collision

- **WHEN** `jo@gmail.com` (created first) and `jo@yahoo.com` are migrated
- **THEN** they become `jo@taskflow.com` and `jo2@taskflow.com`

#### Scenario: Sign-in after conversion

- **WHEN** a converted user signs in with their new `@taskflow.com` address and unchanged password
- **THEN** sign-in succeeds
