## ADDED Requirements

### Requirement: Change own password with an authenticator code
A signed-in user SHALL be able to change their own password from the Profile page by supplying a new password and a valid authenticator code (or an unused backup code). The system SHALL NOT ask for or verify the current password for this action.

#### Scenario: Successful change
- **WHEN** a user supplies a valid code and a new password that meets the length rules
- **THEN** the password changes and every session of that user ends

#### Scenario: Missing or wrong code
- **WHEN** the code is missing, wrong or already used
- **THEN** the change is refused and the password is unchanged

#### Scenario: Weak new password
- **WHEN** the new password is shorter than the minimum or longer than the maximum length
- **THEN** the change is refused and the password is unchanged

### Requirement: Change password screen
Profile SHALL offer a Change password button that opens a separate screen with a Back button returning to Profile. The screen SHALL show only the new password and its confirmation, and SHALL NOT include a current-password field or an authenticator-code box.

#### Scenario: Open and leave
- **WHEN** a user clicks Change password on Profile and then Back
- **THEN** the screen opens and Back returns to Profile with nothing changed

#### Scenario: Form fields
- **WHEN** the screen is shown
- **THEN** only New password and Confirm new password are listed

#### Scenario: Confirmation must match
- **WHEN** the confirmation differs from the new password
- **THEN** the screen refuses to continue and shows a mismatch message

### Requirement: Show or hide a typed password needs no code
The eye icon on the new and confirmation fields SHALL show or hide what was typed without asking for an authenticator code.

#### Scenario: Eye icon
- **WHEN** a user clicks the eye icon
- **THEN** the typed text is shown or hidden immediately

### Requirement: Code is asked when the change is submitted
Clicking Change password on a valid form SHALL open a popup asking only for the authenticator code. The password SHALL be saved only when the server accepts the code.

#### Scenario: Valid code
- **WHEN** the user enters a valid code in the popup
- **THEN** the password is saved and the user is signed out

#### Scenario: Wrong or missing code
- **WHEN** the code is wrong, expired, already used or empty
- **THEN** the change is rejected, the password is unchanged, and the user can retry or cancel

#### Scenario: Cancel
- **WHEN** the user cancels the popup
- **THEN** nothing is sent and the form keeps its values
