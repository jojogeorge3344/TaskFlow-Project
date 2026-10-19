# Spec Delta

## Purpose

Defines where TaskFlow keeps its data on a user's machine and which accounts exist on a fresh install, so the application is usable immediately after its first run.

## ADDED Requirements

### Requirement: Data folder is created on startup
The system SHALL create the folder `C:\TaskFlow` on startup if it does not exist, and SHALL keep the database file, the AI key ring and mail attachments inside it.

#### Scenario: First run creates the folder and database
- **WHEN** the application starts and `C:\TaskFlow` does not exist
- **THEN** the folder is created and a database file `taskflow.db` is created inside it with the current schema applied

#### Scenario: Later runs reuse existing data
- **WHEN** the application starts and `C:\TaskFlow\taskflow.db` already exists
- **THEN** the existing database is opened, pending schema changes are applied, and existing rows are not duplicated or overwritten

#### Scenario: Folder cannot be created
- **WHEN** the application starts and `C:\TaskFlow` cannot be created or written to
- **THEN** startup fails with a message that names the folder and the cause, and no database is created elsewhere

### Requirement: Data folder location is configurable
The system SHALL read the data folder from configuration, defaulting to `C:\TaskFlow`, so automated tests and other hosts can use a different location.

#### Scenario: Override replaces the default
- **WHEN** configuration sets a different storage root
- **THEN** the database, key ring and mail attachments are all created under that root and `C:\TaskFlow` is not touched

### Requirement: Default accounts are seeded on first run
The system SHALL create one Administrator, one Manager and one Employee account with sign-in credentials when they do not already exist, and SHALL store only password hashes.

#### Scenario: Fresh database has three accounts
- **WHEN** the application starts against an empty database
- **THEN** accounts `admin@taskflow.local`, `manager@taskflow.local` and `employee@taskflow.local` exist, each assigned the matching system role and able to sign in with its configured password

#### Scenario: Seeding is idempotent
- **WHEN** the application starts and one of the seeded accounts already exists
- **THEN** that account is left unchanged, including any changed password or role, and only missing accounts are created

#### Scenario: Passwords are not stored in plain text
- **WHEN** the seeded accounts are inspected in the database
- **THEN** each password is stored only as a hash
