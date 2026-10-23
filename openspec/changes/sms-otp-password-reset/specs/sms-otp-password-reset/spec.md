# Spec Delta

## Purpose

Lets a user who forgot their password regain access on their own by proving ownership of their registered mobile number with a one-time SMS code, without any password being sent by SMS or email.

## ADDED Requirements

### Requirement: Registered mobile number on user accounts

The system SHALL store an optional mobile number per user in international (E.164) format, unique among non-deleted users.

#### Scenario: Administrator sets a mobile number

- **WHEN** an administrator saves an employee with a valid E.164 mobile number
- **THEN** the number is stored on that account

#### Scenario: Invalid mobile number is rejected

- **WHEN** a mobile number that is not valid E.164 is submitted
- **THEN** the request is rejected with a validation error and nothing is stored

#### Scenario: Duplicate mobile number is rejected

- **WHEN** a number already held by another non-deleted user is submitted
- **THEN** the request is rejected with a validation error

#### Scenario: Account without a number

- **WHEN** a user has no mobile number
- **THEN** the account works normally but cannot use the SMS OTP reset

### Requirement: Users change their own mobile number only with their password

A user SHALL be able to change their own mobile number from the Profile screen only after supplying their current password. The assistant's profile-edit actions SHALL NOT read or change the mobile number.

#### Scenario: Change with correct password

- **WHEN** a user submits a new valid number with their correct current password
- **THEN** the number is updated

#### Scenario: Change with wrong password

- **WHEN** a user submits a new number with an incorrect current password
- **THEN** the number is not changed and an error is shown

### Requirement: Login page starts the SMS OTP reset

The login page's Forgot password link SHALL open a three-step flow: enter mobile number, enter the 6-digit code, then set and confirm a new password.

#### Scenario: Link opens the flow

- **WHEN** a user clicks Forgot password on the login page
- **THEN** the mobile-number step is shown and no administrator email is sent

#### Scenario: Successful reset

- **WHEN** the user completes all three steps with a valid number, correct code and matching new passwords
- **THEN** the password is changed and the user can sign in with the new password and no longer with the old one

### Requirement: OTP request does not reveal registered numbers

Requesting an OTP SHALL return the same response and the same status whether or not the number belongs to an account, and SHALL send an SMS only when it does.

#### Scenario: Unregistered number

- **WHEN** an OTP is requested for a number no account holds
- **THEN** the response is identical to the registered case and no SMS is sent

#### Scenario: Registered number

- **WHEN** an OTP is requested for a registered number
- **THEN** a 6-digit code is sent by SMS to that number

### Requirement: OTP expiry and attempt limits

An OTP SHALL expire 5 minutes after issue, be usable once, and be invalidated after 5 failed verification attempts.

#### Scenario: Expired code

- **WHEN** a code is submitted more than 5 minutes after issue
- **THEN** verification fails and the user must request a new code

#### Scenario: Too many wrong attempts

- **WHEN** a 6th verification is attempted after 5 wrong codes
- **THEN** it fails even if the code is now correct and a new code is required

#### Scenario: Newer code supersedes older

- **WHEN** a new OTP is issued for a number
- **THEN** any earlier unused OTP for that number can no longer be verified

### Requirement: OTP request throttling

The system SHALL enforce a 60-second cooldown between OTP requests for a number, a per-number hourly cap, and a configurable global daily cap. A throttled request SHALL look the same to the caller as an accepted one.

#### Scenario: Request inside cooldown

- **WHEN** a second OTP is requested for the same number within 60 seconds
- **THEN** no new SMS is sent and the response is the generic one

#### Scenario: Hourly cap reached

- **WHEN** the per-number hourly cap has been reached
- **THEN** further requests for that number send no SMS until the window passes

#### Scenario: Daily cap reached

- **WHEN** the global daily cap is reached
- **THEN** no further SMS are sent that day

### Requirement: Single-use reset token after verification

A successful OTP verification SHALL return a single-use reset token valid for a short time (about 10 minutes), required to set the new password.

#### Scenario: Reset without verification

- **WHEN** a new password is submitted without a valid reset token
- **THEN** it is rejected and the password is unchanged

#### Scenario: Token reuse

- **WHEN** a reset token is used a second time, or after it expires
- **THEN** the request is rejected

### Requirement: Secure password update

Setting the new password SHALL enforce the existing password rules and matching confirmation, store only a hash, and end all existing sessions of that user.

#### Scenario: Weak or mismatched password

- **WHEN** the new password breaks the rules or does not match its confirmation
- **THEN** it is rejected and the reset token remains usable until it expires

#### Scenario: Existing sessions end

- **WHEN** a reset succeeds
- **THEN** access and refresh tokens issued before it are rejected

### Requirement: No passwords or secrets sent or stored in the clear

The system SHALL never send a password by SMS or email as part of the OTP flow, and SHALL store OTP codes only in non-reversible form.

#### Scenario: Message content

- **WHEN** an SMS is sent for the flow
- **THEN** it contains only the OTP and its validity period

### Requirement: Configurable SMS gateway

SMS delivery SHALL go through a provider chosen by configuration, supporting at least a Console provider (default) and a Twilio provider. Credentials SHALL come from environment or secret configuration, not source-controlled files.

#### Scenario: Console provider

- **WHEN** the provider is Console and the app runs in Development
- **THEN** the OTP is written to the application log instead of being sent

#### Scenario: Console provider outside Development

- **WHEN** the provider is Console outside Development
- **THEN** the OTP is not logged and the app reports that no real SMS provider is configured

#### Scenario: Twilio provider

- **WHEN** the provider is Twilio with credentials configured
- **THEN** the OTP is sent through Twilio to the target number

#### Scenario: Missing credentials

- **WHEN** the provider is Twilio and a credential is missing
- **THEN** startup or the first send fails with a clear configuration error and no secret appears in logs

#### Scenario: Gateway failure

- **WHEN** the provider fails to send
- **THEN** the caller still receives the generic response and the failure is logged without the OTP

### Requirement: Admin-mediated reset is available from Profile only

The existing administrator-approved reset request SHALL remain available from the Profile screen, SHALL require a signed-in user, and SHALL act on the signed-in user's own account.

#### Scenario: Signed-in user requests from Profile

- **WHEN** a signed-in user requests a reset from Profile
- **THEN** the existing administrator reset-request mail is sent for that user

#### Scenario: Anonymous request

- **WHEN** an unauthenticated caller requests the admin-mediated reset
- **THEN** the request is rejected as unauthorized

#### Scenario: No public page

- **WHEN** a visitor opens the old public forgot-password page address
- **THEN** it is no longer available
