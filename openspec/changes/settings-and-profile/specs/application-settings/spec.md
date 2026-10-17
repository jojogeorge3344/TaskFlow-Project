# Spec Delta

## Purpose

Provides the application-level configuration surface an Administrator uses to set the application name, default timezone, default task priority and notification preferences, and ensures those values are validated, persisted and applied consistently by the running application.

## ADDED Requirements

### Requirement: View application settings
A signed-in user SHALL be able to read the non-secret application configuration — application name, default timezone, default task priority and notification preferences — so screens can reflect the configured values. These values contain no secrets.

#### Scenario: Signed-in user reads settings
- **WHEN** a signed-in user requests the application settings
- **THEN** the system returns the application name, default timezone, default task priority and notification preferences

#### Scenario: Unauthenticated request denied
- **WHEN** the application settings are requested without a valid session
- **THEN** the system responds with 401 Unauthorized

### Requirement: Update application settings with validation
An Administrator SHALL be able to update the application settings. The system MUST reject a timezone it does not recognize, a blank application name, or a task priority outside the defined set, and MUST persist valid changes so they survive a restart.

#### Scenario: Valid update persisted
- **WHEN** an Administrator submits a valid application name, timezone, default priority and notification preferences
- **THEN** the system saves them and returns the updated configuration

#### Scenario: Unknown timezone rejected
- **WHEN** an Administrator submits a timezone the server does not recognize
- **THEN** the system rejects the request and saves nothing

#### Scenario: Blank application name rejected
- **WHEN** an Administrator submits a blank application name
- **THEN** the system rejects the request and saves nothing

#### Scenario: Invalid default priority rejected
- **WHEN** an Administrator submits a default priority outside the defined set
- **THEN** the system rejects the request and saves nothing

### Requirement: Only administrators change application settings
The system MUST restrict changing application settings to Administrators. Managers and Employees MUST NOT be able to change any application-level configuration.

#### Scenario: Non-administrator cannot change settings
- **WHEN** a Manager or Employee submits an application settings change
- **THEN** the system denies the request and saves nothing

### Requirement: Timezone change applies consistently
After the default timezone changes, the system SHALL use the new timezone when determining the local calendar day for deadline reminders and dashboard figures, without requiring a restart.

#### Scenario: New timezone used without restart
- **WHEN** an Administrator changes the default timezone
- **THEN** deadline reminders and dashboard day boundaries use the new timezone

### Requirement: Settings have safe defaults
The system SHALL ensure every application-level setting exists with a documented default before it serves requests, and MUST NOT overwrite a value an Administrator has already saved.

#### Scenario: Missing settings are seeded
- **WHEN** the application starts and an application setting row is missing
- **THEN** the system creates it with its default value

#### Scenario: Saved values are preserved
- **WHEN** the application starts and an Administrator has already changed a setting
- **THEN** the system leaves that value unchanged
