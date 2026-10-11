## Purpose

Authenticate registered TaskFlow users by email or username, establish and maintain a secure session, and route each user to the dashboard that matches their role.

## ADDED Requirements

### Requirement: Credential login by email or username

The system SHALL authenticate a registered user using either their registered email address or their username, together with their password.

#### Scenario: Login with email

- **WHEN** a user submits their registered email address and correct password
- **THEN** the system authenticates the user and establishes a session

#### Scenario: Login with username

- **WHEN** a user submits their registered username and correct password
- **THEN** the system authenticates the user and establishes a session

#### Scenario: Unknown identifier or incorrect password

- **WHEN** the submitted identifier is not registered, or the password is incorrect
- **THEN** the system rejects the attempt with a single generic "invalid credentials" error
- **AND** the response does not reveal whether the identifier exists

### Requirement: Inactive or deleted accounts cannot log in

The system MUST refuse authentication for accounts that are inactive (`IsActive` is false) or soft-deleted (`IsDeleted`), and MUST NOT disclose that such an account exists.

#### Scenario: Inactive account

- **WHEN** an inactive account submits correct credentials
- **THEN** authentication is rejected with the same generic error used for invalid credentials
- **AND** no session is established

#### Scenario: Soft-deleted account

- **WHEN** a soft-deleted account submits correct credentials
- **THEN** authentication is rejected with the generic error
- **AND** no session is established

### Requirement: Secure password storage

The system SHALL store user passwords only as PBKDF2 hashes and MUST never store, log, or return a plaintext password or its hash.

#### Scenario: No password in any response

- **WHEN** any authentication or user response is produced
- **THEN** it contains no password or password hash

#### Scenario: Verification uses the stored hash

- **WHEN** a submitted password is verified
- **THEN** verification is performed against the stored PBKDF2 hash and never against plaintext

### Requirement: Successful authentication result and role redirect

On successful authentication the system SHALL return the authenticated user's identity, role, and access token, SHALL record the login time, and the client SHALL route the user to the dashboard for their role.

#### Scenario: Administrator lands on the admin dashboard

- **WHEN** an Administrator authenticates successfully
- **THEN** the response identifies role Administrator
- **AND** the client navigates to `/admin`

#### Scenario: Manager lands on the manager dashboard

- **WHEN** a Manager authenticates successfully
- **THEN** the response identifies role Manager
- **AND** the client navigates to `/manager`

#### Scenario: Employee lands on the employee dashboard

- **WHEN** an Employee authenticates successfully
- **THEN** the response identifies role Employee
- **AND** the client navigates to `/me`

#### Scenario: Login time recorded

- **WHEN** authentication succeeds
- **THEN** the user's last login time is updated

### Requirement: Secure session handling

The system SHALL issue a short-lived access token and a refresh token. The access token SHALL be held only in client memory, and the refresh token SHALL be stored hashed on the server, rotated on each use, and revoked on logout.

#### Scenario: Access token is not persisted

- **WHEN** a session is established
- **THEN** the access token is kept in memory only and is not written to browser persistent storage

#### Scenario: Refresh rotates the token

- **WHEN** a valid refresh token is used to obtain a new access token
- **THEN** a new refresh token is issued and the previous refresh token is no longer accepted

#### Scenario: Logout revokes the refresh token

- **WHEN** a user logs out
- **THEN** the refresh token is revoked and cannot be used to obtain a new access token

#### Scenario: Session restored after reload

- **WHEN** the SPA reloads and a valid refresh token is present
- **THEN** the session is restored without the user re-entering credentials

#### Scenario: Expired or revoked credentials rejected

- **WHEN** a request presents an expired access token or a revoked refresh token
- **THEN** the system rejects it with an unauthenticated response

### Requirement: Current user profile for an authenticated session

The system SHALL expose the current authenticated user's identity and role to an authenticated caller.

#### Scenario: Authenticated caller reads own profile

- **WHEN** an authenticated user requests the current user
- **THEN** the system returns that user's identity and role

#### Scenario: Unauthenticated caller rejected

- **WHEN** a request without a valid access token requests the current user
- **THEN** the system responds as unauthenticated

### Requirement: Login screen

The login screen SHALL present the application logo and name, a single field accepting an email address or username, a password field with a show/hide control, a submit button, and an area for error messages. It MUST display an error when authentication fails without revealing whether an account exists.

#### Scenario: Show or hide the password

- **WHEN** the user activates the show/hide control
- **THEN** the entered password is displayed as plain text
- **AND** activating it again masks the password

#### Scenario: Failed login shows a generic error

- **WHEN** authentication is rejected
- **THEN** the screen displays a generic error message in the error area
- **AND** the user remains on the login screen

#### Scenario: Missing input is blocked before submission

- **WHEN** the user submits with an empty identifier or password
- **THEN** the screen prevents submission and indicates the missing field
