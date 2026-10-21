# Spec Delta

## Purpose

Lets a user who cannot sign in request a password recovery, lets an Administrator see and process
that request by setting a new password, notifies both sides through the internal TaskFlow Mail and
in-app notifications, and keeps a per-reset-event notice on the Login Screen - reachable from a
per-user login address - until the user signs in with the newly issued credentials. The administrator
chooses the password and passes it to the user outside the application; no password is ever mailed,
notified, logged or returned by an API.

## ADDED Requirements

### Requirement: Forgot Password entry point on the Login Screen

The Login Screen SHALL offer a Forgot Password link to every visitor. Activating it SHALL open a
dialog in which the visitor submits the registered email address or username of the account that
needs recovery, and submitting SHALL create a password recovery request for that account. The
dialog SHALL always show the same generic confirmation, whether or not the identifier matches an
account, and MUST NOT disclose whether an account exists.

#### Scenario: Request created from the Login Screen

- **WHEN** a visitor opens Forgot Password, enters a registered email address or username and submits
- **THEN** a password recovery request is recorded for that account, marked as originating from the
  Login Screen
- **AND** the dialog shows a generic confirmation

#### Scenario: Identifier does not match an account

- **WHEN** a visitor submits an identifier that matches no active account
- **THEN** the dialog still shows the same generic confirmation
- **AND** no request is created and no detail about account existence is revealed

#### Scenario: Missing identifier

- **WHEN** the visitor submits the dialog with an empty identifier
- **THEN** the submission is blocked and the field is marked as required

### Requirement: Forgot Password entry point on the Profile Screen

A signed-in user SHALL be able to start a password recovery from the Profile Screen. The request
SHALL be associated with the signed-in user's own account, taken from the session rather than from
input, and the user SHALL see a confirmation that the request was recorded.

#### Scenario: Signed-in user requests recovery

- **WHEN** a signed-in user activates Forgot Password on the Profile Screen
- **THEN** a password recovery request is recorded for that user's own account
- **AND** a confirmation is shown

#### Scenario: Profile entry point requires a session

- **WHEN** the Profile Screen entry point is used without a valid session
- **THEN** no request is created and the request is rejected as unauthorized

### Requirement: Password recovery request record

Every password recovery request SHALL have its own unique identifier, a status, the requesting
account, its origin (Login Screen or Profile Screen), a creation timestamp and, when processed, a
completion timestamp and the acting administrator. A request SHALL be associated only with a verified,
active, non-deleted account.

#### Scenario: Each request is distinct

- **WHEN** a user submits two separate recovery requests
- **THEN** each is stored with its own identifier, status and timestamps

#### Scenario: Inactive or deleted account cannot raise a request

- **WHEN** an identifier belonging to a deactivated or soft-deleted account is submitted
- **THEN** no request is created, and the response is the same generic confirmation

### Requirement: Administrator notification mail

When a recovery request is created, the system SHALL deliver a mail to the administrator's mailbox
from the "TaskFlow" system sender, containing the requesting user's identification (full name,
username, email address and role), the request details and a link that opens that request for
processing. The mail MUST NOT contain any current or new password.

#### Scenario: Mail links straight to the request

- **WHEN** the administrator opens the link in the mail while signed in
- **THEN** Employee Management opens the requesting user's details with that request shown and its
  password field ready, rather than only the employee list
- **AND** the link contains the request reference and no password or other account detail

#### Scenario: Link in the mail is clickable

- **WHEN** the administrator reads the mail in TaskFlow Mail
- **THEN** a web address in the body is shown as a link, and clicking one that points at the
  application opens it inside the application without a page reload
- **AND** an address that points elsewhere opens in a new tab, and markup in a body is never rendered

#### Scenario: Link without an application address

- **WHEN** no application base address is configured
- **THEN** the mail is still delivered, without a link

#### Scenario: Administrator receives the request mail

- **WHEN** a recovery request is created successfully
- **THEN** the administrator has a new unread Inbox mail from "TaskFlow" identifying the requesting
  user and the request
- **AND** the mail contains no password

#### Scenario: Mail delivery failure does not fake success

- **WHEN** delivering the administrator notification fails
- **THEN** the request is recorded with a failed notification state and is not reported as notified
- **AND** no password is changed

### Requirement: Administrator reviews requests on the User Details Screen

An Administrator SHALL be able to open the User Details Screen (Employee Management) and see the
password recovery requests awaiting processing with the requesting user's identification, request
status and creation time. Only Administrators SHALL see or process them; every other role SHALL be
refused.

#### Scenario: Administrator sees pending requests

- **WHEN** an Administrator opens the request list on the User Details Screen
- **THEN** each pending request shows its requesting user, status and creation time

#### Scenario: Only the latest request is shown

- **WHEN** an employee has raised several recovery requests
- **THEN** the User Details Screen shows only the most recent one, with no list of earlier requests
- **AND** a mail link that names an older request says a newer one exists and shows the latest

#### Scenario: Request opened from its mail link

- **WHEN** an Administrator opens Employee Management with a request reference in the address
- **THEN** the requesting user's details open with that request highlighted and, if it is still
  pending, its password field focused

#### Scenario: Unknown or already-handled reference

- **WHEN** the reference in the address matches no request, or the request is no longer pending
- **THEN** the screen says so and shows the employee's current requests, changing nothing

#### Scenario: Non-administrator is refused

- **WHEN** a Manager or Employee requests the recovery request list or a processing action
- **THEN** the request is rejected as forbidden and nothing changes

### Requirement: Administrator sets the new password

An Administrator SHALL be able to set a new password for the requesting user, for a request of either
origin, on the User Details Screen. The administrator chooses it; the system does not generate one.
It MUST satisfy the password policy, be stored only as a salted hash, and end the account's existing
sessions. The administrator conveys it to the user outside the application.

#### Scenario: Password set successfully

- **WHEN** an Administrator supplies a policy-compliant new password for the requesting user, whether
  the request came from the Login Screen or the Profile Screen
- **THEN** the account's stored password is replaced by the new hash
- **AND** the account's existing sessions are no longer usable

#### Scenario: Password violating the policy is rejected

- **WHEN** the submitted password fails the configured complexity policy
- **THEN** the request is rejected with a validation error, the password is unchanged and no
  notification is sent

#### Scenario: Missing password is rejected

- **WHEN** an Administrator processes a request without supplying a password
- **THEN** the request is rejected with a validation error and remains pending

#### Scenario: Password never exposed

- **WHEN** any API response, log entry, mail or notification is produced during processing
- **THEN** it contains no password and no password hash

### Requirement: User notification after the password is updated

After the password is successfully updated, the system SHALL deliver a mail to the user's registered
mailbox from the "TaskFlow" system sender stating that an administrator has set a new password and
that the user should obtain it from their administrator, and SHALL create an in-app notification to
the same effect. Neither the mail nor the in-app notification may contain the password, and neither
MAY be created when the password update failed.

#### Scenario: User is notified by mail and in-app notification

- **WHEN** an Administrator's password update for a recovery request succeeds
- **THEN** the user has a new Inbox mail from "TaskFlow" telling them to ask their administrator for
  the new password
- **AND** the user has an in-app notification about the new password
- **AND** neither contains the password

#### Scenario: Failed update sends nothing

- **WHEN** the password update fails
- **THEN** no user mail and no in-app notification are created
- **AND** the request is not marked completed

### Requirement: Mandatory password reset screen after sign-in

When a user signs in with a password an Administrator set for a recovery request, the application
SHALL show a dedicated screen with New Password, Confirm Password and Update Password. The user SHALL
NOT be able to cancel, skip or leave it until the password is updated. No current password is asked
for, and the screen MUST NOT display the temporary password.

#### Scenario: Issued password forces the screen at sign-in

- **WHEN** a user signs in with a password an Administrator set for a recovery request
- **THEN** the dedicated screen is shown before any other screen
- **AND** it offers New Password, Confirm Password and Update Password and no cancel or close control

#### Scenario: No way around the screen

- **WHEN** the user tries to open another screen or call another API route before updating
- **THEN** the route is refused with "Password change required" and the user is returned to the screen

#### Scenario: Mismatched or non-compliant password is rejected

- **WHEN** the two fields differ or the new password fails the password policy
- **THEN** the update is rejected with a field error and the screen stays

#### Scenario: Update invalidates the temporary password

- **WHEN** the user submits a matching, policy-compliant new password
- **THEN** the new hash replaces the temporary one, the temporary password no longer signs in, the
  must-change requirement is lifted and the user's other sessions end
- **AND** the user then continues using the application

### Requirement: Encrypted per-user login address

The application SHALL accept a per-user login address `/login/<token>`, where the token is encrypted,
expires five minutes after the administrator generated it, works only once, and does not contain the
account's email or username in readable form. Opening it SHALL resolve the account and show its email
on the Login Screen, pre-filled, and check for the notice. An invalid, expired, used or superseded token
SHALL behave exactly like plain `/login`.

#### Scenario: Address shows the email only after it is resolved

- **WHEN** a visitor opens `/login/<token>` for an account whose completed reset has not been cleared
- **THEN** the address bar shows only the token, never the email
- **AND** the page shows that account's email pre-filled and the bell icon

#### Scenario: Bad token behaves like the plain login page

- **WHEN** the token is tampered with, expired, already used, or belongs to an earlier reset of that account
- **THEN** the plain Login Screen is shown with nothing pre-filled and no indication of why

#### Scenario: Link expires after five minutes

- **WHEN** more than five minutes have passed since the administrator generated the link
- **THEN** opening it behaves like plain `/login`, and the administrator can generate a new one

#### Scenario: Plain address may remember the last identifier

- **WHEN** a visitor opens `/login` on a device where an identifier was previously used to sign in
- **THEN** the identifier is pre-filled and the notice for it is shown if one exists

### Requirement: Bell opens the password update screen

Activating the bell icon on a Login Screen that was opened through a valid encrypted link SHALL open a
dedicated password update screen with New password and Confirm password only. The screen MUST NOT ask
for, pre-fill or reveal the temporary password, and SHALL offer no cancel, close or skip control. On a
Login Screen opened without a link, activating the bell SHALL NOT open that screen; it SHALL tell the user
to sign in with the password the administrator gave them.

#### Scenario: Screen opened through the link asks only for the new password

- **WHEN** the user opened the Login Screen through a valid encrypted link and activates the bell
- **THEN** the screen shows New password, Confirm password and one Update password action, with the
  account's email displayed read-only
- **AND** no control on the screen leaves it without updating

#### Scenario: Bell without a link points to sign-in

- **WHEN** the user reaches the bell through a typed or remembered identifier and activates it
- **THEN** no update screen opens; the page says a new password was set and to sign in with the password
  the administrator gave them, and moves focus to the password field
- **AND** signing in with it leads to the mandatory password reset screen

#### Scenario: Temporary password is never shown

- **WHEN** the screen is displayed for any account
- **THEN** no password, hash or hint of one appears in the page, its address or any response

### Requirement: Password update from the login page

Submitting the update screen SHALL check that New and Confirm match and meet the password policy and
that the caller holds a valid encrypted link, then replace the password, end the account's sessions,
lift the must-change requirement and clear the notice. The link is the only proof accepted on this
path. Any failure of proof MUST be refused without saying whether the account exists.

#### Scenario: Update through the link succeeds

- **WHEN** the user submits matching, policy-compliant New and Confirm passwords from the screen opened
  through a valid link
- **THEN** the password is replaced, the temporary password no longer signs in, the notice is cleared,
  the link no longer works and the user is taken to sign in with the new password

#### Scenario: Used, expired or invalid link cannot update

- **WHEN** a link that was already used, has expired, was superseded or is invalid is submitted
- **THEN** nothing changes and the answer is the same generic refusal

#### Scenario: No account is named without a link

- **WHEN** an update is submitted with no link
- **THEN** it is rejected and nothing changes

#### Scenario: Mismatch or weak password

- **WHEN** New and Confirm differ or the new password fails the policy
- **THEN** the screen shows a field error as Confirm is typed and nothing changes

### Requirement: Administrator shares the login link

For a completed request the administrator SHALL be able to generate the user's encrypted login link
from the User Details Screen, to pass to the user when they are ready. The link MUST NOT contain the
password, is valid for five minutes and one use, and can be generated again at any time. Only a caller
with the administrator password-management permission SHALL be able to obtain it, and only for the
latest completed request whose notice has not been cleared.

#### Scenario: Administrator copies the link

- **WHEN** an Administrator views a completed request on the User Details Screen
- **THEN** the screen offers to generate the user's login link to copy, with a note that it lasts five
  minutes and works once, and which carries no password

#### Scenario: Link is for the latest event only

- **WHEN** a later reset completes for the same account
- **THEN** links from the earlier reset no longer work

#### Scenario: No link once the user has updated

- **WHEN** the user has already signed in or updated their password after that reset
- **THEN** the administrator is told no link is available

### Requirement: Login-page password attempts are rate limited

The endpoints that verify a password from the unauthenticated Login Screen, the sign-in and the
password update, SHALL limit repeated attempts from one client and answer a limited client with a
retry-after response, so the temporary password cannot be guessed by trial.

#### Scenario: Repeated attempts are limited

- **WHEN** one client exceeds the permitted attempts within the window
- **THEN** further attempts are refused with a retry-after response until the window passes

### Requirement: Login Screen notice for a completed reset

When a recovery request of either origin has been completed, the Login Screen SHALL show a
notification bell icon with an unread badge for the account on that screen, however the identifier
got there. Activating it SHALL open the password update screen. The notice response MUST NOT include
the password or any account detail beyond the email resolved from a valid link, and SHALL have the
same shape whether or not a notice exists.

#### Scenario: Bell icon opens the update screen

- **WHEN** the notification bell is displayed and the user activates it
- **THEN** the password update screen opens, which says a new password has been set and to ask the
  administrator for it
- **AND** the icon is reachable and operable from the keyboard

#### Scenario: Notice shown after the reset completes

- **WHEN** a request from the Login Screen or the Profile Screen has been completed and the identifier of that account is
  present on the Login Screen
- **THEN** the notice indicator is displayed

#### Scenario: Profile-Screen reset also shows the notice

- **WHEN** a request raised from the Profile Screen has been completed, which ended the requester's
  sessions, and that account's identifier is present on the Login Screen
- **THEN** the notice indicator is displayed, exactly as for a Login-Screen request

#### Scenario: No notice for an identifier without a completed reset

- **WHEN** an identifier with no completed reset of either origin is present
- **THEN** no notice indicator is shown and the response is indistinguishable in shape

### Requirement: Login Screen notice stays until sign-in or update

The Login Screen notice for a completed reset SHALL stay available, with no time limit, until that
user signs in with the new credentials or updates the password through the update screen. The Login
Screen SHALL check periodically while it knows the account, so the bell appears without a reload.

#### Scenario: Bell appears without a reload

- **WHEN** the login page is open for an account and the administrator then completes that account's reset
- **THEN** the bell icon appears within a few seconds without the user reloading

#### Scenario: Bell stays until it is used

- **WHEN** any amount of time passes after the reset completed and the user has not signed in or updated
- **THEN** the bell icon is still shown on a reload and through the encrypted login link

#### Scenario: Sign-in clears the notice

- **WHEN** the user signs in successfully with the new password
- **THEN** the bell icon is cleared and is no longer shown for that account

#### Scenario: Failed sign-in keeps the notice

- **WHEN** the user attempts to sign in with the old or an incorrect password
- **THEN** the bell icon remains

#### Scenario: Update through the screen clears the notice

- **WHEN** the user updates the password from the update screen
- **THEN** the bell icon is cleared and is no longer shown for that account

### Requirement: Login Screen notice tracks only the latest reset event

A Login Screen notice SHALL be associated with one specific user account and one specific reset
event. Completing a later reset request SHALL bind the notice to that latest event, and notices of
previously completed reset events SHALL NOT reappear. A notice raised for one account or request
MUST NEVER be shown for another account or request.

#### Scenario: Later reset supersedes the earlier notice

- **WHEN** a second recovery request for the same account is completed
- **THEN** only the notice of the latest reset event is shown
- **AND** the earlier event's notice does not reappear

#### Scenario: Notices are isolated per account

- **WHEN** one account has a completed reset and another account's identifier is entered
- **THEN** no notice is shown for the other account

### Requirement: Request status reflects real outcomes

Each request SHALL carry a status that distinguishes pending, completed and failed processing. A
request SHALL be marked completed only after the password is stored and the user notification has
been created; a mail failure SHALL leave the request in a failed state that the administrator can
retry, and MUST NOT be reported as completed.

#### Scenario: Completed only when everything succeeded

- **WHEN** the password is stored and the user's mail and in-app notification are created
- **THEN** the request status becomes completed with its completion timestamp and acting administrator

#### Scenario: Retry after a failed notification

- **WHEN** the password was stored but the user's mail delivery failed
- **THEN** the request status is failed, not completed
- **AND** the administrator can retry the notification without resetting the password again

### Requirement: Password and request security rules

Password reset operations SHALL never store, log, mail, notify or return any password in plain text;
the only plain-text password is the one the administrator types into the processing action, which is
hashed immediately and then discarded. They MUST validate the acting
administrator's identity and authorization before any password change, and MUST reject reset actions
against accounts that are not verified, active and non-deleted.

#### Scenario: Existing passwords are never disclosed

- **WHEN** any reset-related mail, notification or API response is produced
- **THEN** it contains no existing password and no password hash

#### Scenario: Unauthorized change attempt

- **WHEN** a caller without the administrator password-management permission attempts to change
  another user's password through this workflow
- **THEN** the change is refused and recorded as a denied action

### Requirement: Audit trail of the reset lifecycle

The system SHALL record password reset requests, administrative actions on them, notification events
and successful completion, so the full lifecycle of a request can be reconstructed.

#### Scenario: Lifecycle reconstructable

- **WHEN** an Administrator reviews the history of a completed request
- **THEN** the request, its notification events, the password update action and its completion are
  all present in the audit record
