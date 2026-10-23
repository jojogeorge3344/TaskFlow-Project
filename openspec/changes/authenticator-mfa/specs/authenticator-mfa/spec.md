# Spec Delta

## Purpose

Adds a required second sign-in factor from a free authenticator app, with backup codes and administrator reset, and lets the same app code be used to recover a forgotten password from the login page, at no cost.

## ADDED Requirements

### Requirement: Mandatory two-step setup before using the app

Until a signed-in user has confirmed an authenticator app, the system SHALL show them a setup screen first and SHALL refuse every other request, except sign-in, reading and saving their own details and the setup calls. Setup SHALL confirm their details and mobile number and their password, and accept one valid code before MFA turns on.

#### Scenario: Setup screen comes first

- **WHEN** a user without MFA signs in
- **THEN** the setup screen is shown instead of the app

#### Scenario: The API refuses everything else

- **WHEN** a user without MFA calls any other endpoint
- **THEN** it answers 403 with the code `mfa_setup_required`

#### Scenario: Successful enrolment

- **WHEN** the user confirms their details and password, scans the QR code (or types the key) and enters a valid current code
- **THEN** MFA is turned on, backup codes are shown once, and the app opens

#### Scenario: Wrong password at the start

- **WHEN** the current password is wrong
- **THEN** no key is issued and MFA stays off

#### Scenario: Wrong confirmation code

- **WHEN** the confirmation code is wrong
- **THEN** MFA stays off and the user can try again

#### Scenario: Abandoned setup

- **WHEN** a user starts setup but never confirms
- **THEN** they remain on the setup screen and cannot use the app

#### Scenario: Break-glass switch

- **WHEN** the setting `Mfa:Required` is turned off
- **THEN** nobody is held on the setup screen until it is turned back on

### Requirement: Two-step sign-in when MFA is on

For a user with MFA on, sign-in SHALL require the password and then a valid authenticator code or unused backup code. No session SHALL be issued after the password alone.

#### Scenario: Password correct, MFA on

- **WHEN** the password is correct for a user with MFA on
- **THEN** the system asks for a code and issues no session yet

#### Scenario: Valid code completes sign-in

- **WHEN** the user then submits a valid current code
- **THEN** the session is issued as for a normal sign-in

#### Scenario: Wrong password, MFA on

- **WHEN** the password is wrong
- **THEN** sign-in fails with the same response as for any account, without mentioning MFA

#### Scenario: Users without MFA

- **WHEN** a user with MFA off signs in with a correct password
- **THEN** the session is issued as before

### Requirement: Code validity and replay protection

An authenticator code SHALL be a 6-digit time-based code (30-second step) accepted within one step either side of the server's time, and a code SHALL NOT be accepted a second time.

#### Scenario: Code from the previous step

- **WHEN** a code from the immediately preceding 30-second step is submitted
- **THEN** it is accepted

#### Scenario: Old code

- **WHEN** a code from more than one step away is submitted
- **THEN** it is rejected

#### Scenario: Code reused

- **WHEN** a code that already completed a verification is submitted again
- **THEN** it is rejected

### Requirement: Lockout after repeated wrong codes

After 5 consecutive wrong codes for an account, the system SHALL refuse further code attempts for that account for a short period, even if a correct code is then entered.

#### Scenario: Lockout

- **WHEN** 5 consecutive wrong codes are submitted
- **THEN** the next attempt, even with a correct code, is refused until the lock period ends

#### Scenario: Counter resets

- **WHEN** a correct code is accepted
- **THEN** the consecutive-failure count returns to zero

### Requirement: Backup codes

When MFA is turned on the system SHALL show 10 single-use backup codes once. A backup code SHALL be accepted in place of an authenticator code at sign-in and then be spent. Regenerating SHALL invalidate all earlier codes.

#### Scenario: Backup code signs in

- **WHEN** a user submits an unused backup code at the second step
- **THEN** sign-in completes and that code cannot be used again

#### Scenario: Regenerate through the API

- **WHEN** a user regenerates backup codes with their password and a valid code
- **THEN** a new set is shown once and the old set stops working

#### Scenario: Codes are not retrievable

- **WHEN** a user later reads their profile
- **THEN** existing backup codes are not shown, only how many remain

### Requirement: MFA cannot be turned off

The system SHALL NOT offer any way for a user or an administrator to switch MFA off. The only removal is the administrator reset, after which the user SHALL set it up again before using the app.

#### Scenario: No disable route

- **WHEN** any caller tries to turn MFA off through the API or the app
- **THEN** there is no such route or button, and MFA stays on

#### Scenario: Setup is required again after a reset

- **WHEN** an administrator resets a user's MFA
- **THEN** that user is held on the setup screen at their next request

### Requirement: Password change needs a fresh code

Changing a password SHALL need the current password and a valid authenticator code or unused backup code. A code SHALL be spent only after the current password has been found correct.

#### Scenario: Missing or wrong code

- **WHEN** the code is missing, wrong or already used
- **THEN** the change is refused and the password is unchanged

#### Scenario: Mistyped current password

- **WHEN** the current password is wrong
- **THEN** the change is refused and the code is not spent, so it can be used again

#### Scenario: Successful change

- **WHEN** the current password and a valid code are given with a new password
- **THEN** the password changes and every session of that user ends

### Requirement: Password change without the current password

A user who has MFA on and does not remember their current password SHALL be able to change it with a valid authenticator code alone. Only an authenticator code counts, never a backup code, and the new password SHALL still meet the password rules.

#### Scenario: Code replaces the current password

- **WHEN** a user with MFA on submits a new password and a valid authenticator code without a current password
- **THEN** the password changes and every session of that user ends

#### Scenario: Backup code refused

- **WHEN** a backup code is submitted on this path
- **THEN** the change is refused and the password is unchanged

#### Scenario: Missing or wrong code

- **WHEN** the code is missing, wrong or already used
- **THEN** the change is refused, and wrong codes count toward the lockout

#### Scenario: User without MFA

- **WHEN** a user who has no MFA tries this path
- **THEN** it is refused and they are told to use their current password

### Requirement: Showing a typed password needs a code

The Profile page SHALL keep what was typed in a password box hidden until a valid authenticator code has been checked, then allow viewing for about five minutes. The check SHALL NOT spend the code or accept a backup code, and wrong codes SHALL count toward the lockout.

#### Scenario: Show asks first

- **WHEN** a user presses Show on a password box and viewing is locked
- **THEN** a code prompt appears and the text stays hidden

#### Scenario: Correct code unlocks

- **WHEN** a valid code is entered
- **THEN** the boxes can be shown for about five minutes and then lock again

#### Scenario: Wrong code

- **WHEN** a wrong code is entered
- **THEN** the text stays hidden and the failure counts toward the lockout

#### Scenario: Same code still serves the change

- **WHEN** the code that unlocked viewing is used for the password change in the same 30-second window
- **THEN** the change is accepted

### Requirement: Contact mobile number

A user SHALL be able to give an optional mobile number in international format. Email is the only unique identity, so the same number MAY appear on several accounts, but saving SHALL be refused when another record already holds the same email and number. Nothing is ever sent to the number.

#### Scenario: Valid number

- **WHEN** a user saves a well-formed number, with or without spaces and dashes
- **THEN** it is stored in international form

#### Scenario: Malformed number

- **WHEN** the number is not in international format
- **THEN** saving is refused with an explanation

#### Scenario: Shared number

- **WHEN** two users with different emails save the same number
- **THEN** both saves succeed

#### Scenario: Omitted or empty

- **WHEN** a save leaves the number out
- **THEN** the current number is kept, and an empty value clears it

### Requirement: Administrator reset

A user holding the employee-management permission SHALL be able to remove another user's MFA. The affected user's sessions SHALL end and they SHALL be notified.

#### Scenario: Administrator resets MFA

- **WHEN** an administrator resets a user's MFA
- **THEN** the user's key and backup codes are removed, their sessions end, and they receive a notification

#### Scenario: Without permission

- **WHEN** a caller without the permission attempts a reset
- **THEN** the request is refused

### Requirement: Password recovery with an authenticator code

From the login page a user with MFA on SHALL be able to recover a password with their username or email and a valid authenticator code, then set a new password with a single-use reset token. The response SHALL NOT reveal whether the account exists or has MFA.

#### Scenario: Successful recovery

- **WHEN** a user enters their identifier and a valid code, then sets and confirms a new password
- **THEN** the password is changed, existing sessions end, and they can sign in with the new password

#### Scenario: Unknown account or MFA off

- **WHEN** the identifier is unknown or the account has MFA off
- **THEN** the failure is the same as for a wrong code

#### Scenario: Counts toward lockout

- **WHEN** wrong codes are submitted in recovery
- **THEN** they count toward the same lockout as sign-in codes

#### Scenario: Backup code is not a recovery factor

- **WHEN** a backup code is submitted in recovery
- **THEN** it is refused like a wrong code

### Requirement: Reset token is single-use and short-lived

The reset token issued after a correct code SHALL work once, expire about 10 minutes after it is issued, and be stored only in hashed form. A rejected new password SHALL NOT use the token up.

#### Scenario: Token reuse or expiry

- **WHEN** a reset token is used a second time, or after it expires
- **THEN** the request is rejected and the password is unchanged

#### Scenario: Weak or mismatched password

- **WHEN** the new password breaks the rules or does not match its confirmation
- **THEN** it is rejected and the same token can be used again before it expires

#### Scenario: Other tokens die with a reset

- **WHEN** a reset succeeds
- **THEN** any other outstanding reset token for that account stops working

### Requirement: Administrator-approved reset needs a signed-in user

The existing administrator-approved password reset request SHALL require a signed-in user, SHALL act on that user's own account regardless of any identifier in the request, and SHALL NOT be offered on a public page or on the Profile screen.

#### Scenario: Anonymous request

- **WHEN** a caller who is not signed in requests the administrator-approved reset
- **THEN** the request is rejected as unauthorized

#### Scenario: Acts on the caller

- **WHEN** a signed-in user makes the request with another person's identifier
- **THEN** only the caller's own account is affected

#### Scenario: No public page and no Profile card

- **WHEN** a visitor opens the old public forgot-password address, or a user opens Profile
- **THEN** neither offers a password reset request

### Requirement: Secrets are protected

The shared key SHALL be stored encrypted, SHALL be shown only during setup, and SHALL NOT appear in logs or API responses afterwards. Codes and backup codes SHALL NOT be logged, and backup codes SHALL be stored only in hashed form.

#### Scenario: Key after setup

- **WHEN** the user's profile or any later response is read
- **THEN** it contains only whether MFA is on, never the key

#### Scenario: Logs

- **WHEN** setup, sign-in or recovery runs
- **THEN** no key, code or backup code appears in the logs
