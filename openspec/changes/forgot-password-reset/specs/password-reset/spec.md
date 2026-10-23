## Purpose

Lets a signed-in TaskFlow user recover access through an admin-assisted, one-time, time-limited password-reset request initiated from the profile screen. Signed-out users recover through the SMS OTP flow (see the `sms-otp-password-reset` change).

## ADDED Requirements

### Requirement: Forgot password link on the profile screen

The system SHALL present a Forgot password action on the profile screen for every signed-in user, and SHALL require a signed-in session to use it.

#### Scenario: Profile screen shows the Forgot password link

- **WHEN** a signed-in user opens the profile screen
- **THEN** the screen displays a Forgot password link

#### Scenario: Anonymous request is refused

- **WHEN** a caller who is not signed in requests an administrator-approved reset
- **THEN** the system rejects the request as unauthorized

#### Scenario: The request acts on the caller

- **WHEN** a signed-in user requests a reset
- **THEN** the reset is for that user's own account, regardless of any identifier in the request

### Requirement: Reset request email to the administrator

When a signed-in user clicks the Forgot password link on the profile screen, the system SHALL send a one-time, time-limited reset request email to the administrator requesting a password reset for that user.

#### Scenario: User clicks the link from the profile screen

- **WHEN** a signed-in user clicks Forgot password on the profile screen
- **THEN** the system sends a reset request email to the administrator for that user
- **AND** the email contains a one-time reset link valid for 5 minutes

### Requirement: Administrator generates a new password from the user details screen

The administrator who opens a valid reset link SHALL be taken to the user details screen for that user and SHALL be able to generate a new password for the user.

#### Scenario: Administrator opens a valid reset link

- **WHEN** the administrator opens the reset link from the email
- **THEN** the system navigates to the user details screen for the requesting user

#### Scenario: Administrator generates a password

- **WHEN** the administrator generates a new password on the user details screen
- **THEN** the system assigns the generated password to the user

### Requirement: Email and notification when the password is updated

When the administrator saves the new password, the system SHALL automatically send an email and an in-app notification to the user.

#### Scenario: Administrator saves the new password

- **WHEN** the administrator updates the user's password
- **THEN** the system sends a new-password email to the user
- **AND** the system creates an in-app notification for the user

### Requirement: Auto-generated password when an administrator requests it from the profile screen

When a signed-in administrator requests a password reset from the profile screen, the system SHALL automatically generate a password for that administrator and send a notification to them.

#### Scenario: Administrator triggers forgot password from the profile screen

- **WHEN** a signed-in administrator clicks Forgot password on the profile screen
- **THEN** the system automatically generates a password for the administrator
- **AND** the system sends a notification to the administrator

### Requirement: Reset link is one-time use only

A reset link SHALL be valid for a single use only; any second attempt to use the same link SHALL be rejected with a "link not valid" error.

#### Scenario: First use of a valid link succeeds

- **WHEN** the administrator uses a valid, unused reset link
- **THEN** the reset is accepted

#### Scenario: Second use of the same link is rejected

- **WHEN** a reset link that has already been used is presented again
- **THEN** the system rejects it with a "link not valid" error

### Requirement: Reset link expires after 5 minutes

A reset link SHALL expire 5 minutes after it is created; an attempt to use an expired link SHALL be rejected.

#### Scenario: Link used within 5 minutes is accepted

- **WHEN** a reset link is used before its 5-minute expiry
- **THEN** the reset is accepted

#### Scenario: Expired link is rejected

- **WHEN** a reset link older than 5 minutes is presented
- **THEN** the system rejects it as expired
