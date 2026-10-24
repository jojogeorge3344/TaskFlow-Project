# Spec Delta

## Purpose

Lets TaskFlow be run as one self-contained web app on a local PC in a QA configuration and be opened by others from a public HTTPS address at no cost.

## ADDED Requirements

### Requirement: Single-origin serving
In the QA configuration the system SHALL serve the web client and the API from the same address and port.

#### Scenario: Client loads from the API address
- **WHEN** a browser requests the root address of the running QA app
- **THEN** the TaskFlow sign-in page is returned and its API calls go to the same origin

#### Scenario: Deep link refresh
- **WHEN** a browser requests a client route such as `/admin` directly
- **THEN** the client application is returned instead of a not-found response

#### Scenario: Unknown API path stays an error
- **WHEN** a request is made to a non-existent path under `/api`
- **THEN** the response is a not-found error, not the client application

### Requirement: Demo accounts available
In the QA configuration the system SHALL create the Administrator, Manager and Employee demo sign-ins on first start.

#### Scenario: First start on empty data
- **WHEN** the QA app starts and no database exists in the data folder
- **THEN** the database is created and the three demo accounts can sign in

#### Scenario: Restart keeps data
- **WHEN** the QA app is stopped and started again with the same data folder
- **THEN** existing users, tasks and uploaded attachments are still present

### Requirement: Secrets supplied at run time
The system SHALL read the JWT signing key and the data folder location from environment variables in the QA configuration, and no signing key SHALL be stored in the repository.

#### Scenario: Missing signing key
- **WHEN** the QA app is started without a JWT signing key
- **THEN** startup fails with a message naming the missing setting

#### Scenario: Key supplied
- **WHEN** the signing key is set in the environment
- **THEN** sign-in succeeds and issued tokens validate

### Requirement: One-command public demo
The system SHALL provide a single script that builds the client, starts the API and exposes it at a public HTTPS URL without any cloud account or payment.

#### Scenario: Script succeeds
- **WHEN** the demo script is run on a PC with the required tools installed
- **THEN** it prints a public HTTPS URL at which the sign-in page loads

#### Scenario: Missing tool
- **WHEN** a required tool such as the tunnel binary is not installed
- **THEN** the script stops and tells the user what to install

#### Scenario: Sign-in over the public URL
- **WHEN** a demo user signs in through the public URL
- **THEN** the session works, including the secure refresh cookie, without cross-origin errors
