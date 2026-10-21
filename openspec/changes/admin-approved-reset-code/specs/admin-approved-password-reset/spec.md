# Spec Delta

## Purpose

Lets a user who cannot sign in ask for a password recovery, lets an Administrator verify and approve it without ever choosing or learning the new password, and lets the user redeem a one-time reset code to set their own password. The flow works entirely inside the application and needs no email delivery.

## ADDED Requirements

### Requirement: Forgot Password request from the Login Screen and Profile Screen

The Login Screen SHALL offer Forgot Password to every visitor, taking the account's email or username, and the Profile Screen SHALL offer it to a signed-in user for their own account. Both SHALL record a recovery request for a verified, active, non-deleted account, and the Login Screen SHALL always show the same generic confirmation whether or not the identifier matches an account.

#### Scenario: Request recorded from the Login Screen

- **WHEN** a visitor submits a registered email or username in Forgot Password
- **THEN** a recovery request is recorded for that account and a generic confirmation is shown

#### Scenario: Unknown or inactive identifier

- **WHEN** the identifier matches no active, non-deleted account
- **THEN** the same generic confirmation is shown, nothing is recorded and no detail about account existence is revealed

#### Scenario: Profile request uses the session

- **WHEN** a signed-in user activates Forgot Password on the Profile Screen
- **THEN** the request is recorded for that user's own account, taken from the session and not from input

#### Scenario: Missing identifier

- **WHEN** the Login Screen dialog is submitted with an empty identifier
- **THEN** submission is blocked and the field is marked required

### Requirement: Repeated requests collapse into one open request

An account SHALL have at most one open (not yet approved or expired) recovery request. Further requests for that account SHALL NOT create new requests; they SHALL increase the open request's request count and refresh its last-requested time, and SHALL receive the same confirmation as the first.

#### Scenario: Ten requests, one open request

- **WHEN** the same user submits ten forgot-password requests before an administrator acts
- **THEN** exactly one open request exists for that account, with a request count of ten
- **AND** every submission received the same confirmation

#### Scenario: Request after approval or use

- **WHEN** the user submits a request after the previous one was approved and redeemed or has expired
- **THEN** a new open request is created

### Requirement: Forgot Password requests are throttled

The unauthenticated request endpoint SHALL limit submissions per client and per target account, and a limited caller SHALL receive a retry-after response. Throttling MUST NOT change what a non-limited caller learns about whether an account exists.

#### Scenario: Client over the limit

- **WHEN** one client exceeds the permitted submissions within the window
- **THEN** further submissions are refused with a retry-after response until the window passes

#### Scenario: Account flooded from many clients

- **WHEN** many clients submit for the same account within the window
- **THEN** administrators are not notified more than once per window for that account and the request count still records the submissions

### Requirement: Administrators are told of an open request

When an account's open request is created, the system SHALL deliver one internal mail to every active Administrator other than the requester, naming the requester and request and linking to it, and SHALL NOT include any password, code or token. A failed delivery MUST leave the request open and visible on Employee Management.

#### Scenario: Administrators receive the request mail

- **WHEN** a new open request is created
- **THEN** each other active Administrator has one new unread mail from the system sender with the requester's identification and a link to the request
- **AND** the mail contains no password, code or token

#### Scenario: Mail failure does not lose the request

- **WHEN** delivering the administrator mail fails
- **THEN** the request is still open and listed on Employee Management

### Requirement: Administrator reviews open requests on Employee Management

An Administrator (holder of the employee-management permission) SHALL see on Employee Management the latest request for an employee with its status, request count and last-requested time, and SHALL open it directly from the mail link. Everyone else MUST be refused.

#### Scenario: Request opened from the mail link

- **WHEN** an Administrator opens Employee Management through the mail link
- **THEN** the requester's details open with that request shown and its Approve action available if it is still open

#### Scenario: Link reached while signed out

- **WHEN** an Administrator who is not signed in opens the link and then signs in
- **THEN** they are returned to the same request rather than to the default page

#### Scenario: Request no longer open or newer exists

- **WHEN** the linked request was already approved, expired, or a newer one exists
- **THEN** the screen says so, shows the employee's latest request and changes nothing

#### Scenario: Non-administrator refused

- **WHEN** a caller without the employee-management permission lists or acts on requests
- **THEN** the call is rejected as forbidden and nothing changes

### Requirement: Administrator approves a request without choosing a password

An Administrator SHALL approve an open request after verifying the requester's identity. Approving SHALL NOT take or set any password; it SHALL create a one-time reset ticket for the account, replace any earlier ticket for it, and show the administrator the reset code once.

#### Scenario: Approval issues a code

- **WHEN** an Administrator approves an open request
- **THEN** a reset ticket with a new random code is created and the code is displayed to that Administrator once
- **AND** the account's current password is unchanged

#### Scenario: Code is not shown again

- **WHEN** the Administrator leaves the screen or reloads after approval
- **THEN** the code is no longer retrievable by any call

#### Scenario: Re-issue

- **WHEN** an Administrator approves again while the ticket is unused
- **THEN** a new code is issued and the earlier code stops working

#### Scenario: Unavailable account

- **WHEN** the requesting account is deactivated or deleted before approval
- **THEN** approval is refused and no ticket is created

### Requirement: Reset code is a protected one-time secret

A reset code SHALL be random with at least 40 bits of entropy, stored only as a hash, valid for a limited time (default 30 minutes), usable once, and limited to a fixed number of wrong attempts (default 5) after which the ticket is dead. The code MUST NOT appear in any mail, notification, log, audit record or API response other than the single approval response.

#### Scenario: Expired code

- **WHEN** a code is submitted after the ticket expired
- **THEN** it is refused and the password is unchanged

#### Scenario: Wrong attempts exhaust the ticket

- **WHEN** the permitted number of wrong codes has been submitted for a ticket
- **THEN** even the correct code is refused and the Administrator must approve again

#### Scenario: Used code

- **WHEN** a code that already completed a reset is submitted again
- **THEN** it is refused

#### Scenario: Code never stored in the clear

- **WHEN** the ticket is stored or an event is logged or audited
- **THEN** only a hash of the code is persisted and no code appears

### Requirement: User redeems the code and sets their own password

A user with no session SHALL redeem a ticket by entering their email or username, the reset code, a new password and its confirmation. On success the system SHALL store the new password as a salted hash, end all of the account's sessions, burn the ticket, and show the user the sign-in screen. Every failure of proof MUST be refused with the same generic message and without revealing whether the account exists.

#### Scenario: Successful reset

- **WHEN** the user submits a matching, policy-compliant new password with the correct unexpired code for their account
- **THEN** the password is replaced, existing sessions end, the ticket is burned and the user is taken to sign in with the new password

#### Scenario: Wrong account or wrong code

- **WHEN** the code does not belong to the identified account, or the account has no live ticket
- **THEN** the same generic refusal is returned and nothing changes

#### Scenario: Mismatch or weak password

- **WHEN** New and Confirm differ or the new password fails the password policy
- **THEN** a field error is shown, the ticket is not burned and the attempt does not count as a wrong code

#### Scenario: Administrator never learns the password

- **WHEN** a reset completes
- **THEN** no administrator, mail, notification, log or API response has the new password

### Requirement: Login Screen bell signals an approval without a credential

When an account has a live, unredeemed ticket, the Login Screen SHALL show a bell for that identifier that tells the user a reset was approved and opens the redeem screen. The notice response MUST carry no code, token or account detail and SHALL have the same shape whether or not a ticket exists.

#### Scenario: Bell appears after approval

- **WHEN** the Login Screen knows an account and an Administrator approves its request
- **THEN** the bell appears within a few seconds without a reload

#### Scenario: Bell opens redeem screen, not the code

- **WHEN** the user activates the bell
- **THEN** the redeem screen opens asking for identifier, code, new password and confirmation, with nothing pre-filled from the server

#### Scenario: No ticket, no signal

- **WHEN** an identifier has no live ticket
- **THEN** no bell is shown and the response is indistinguishable in shape from one with a ticket

#### Scenario: Bell clears

- **WHEN** the ticket is redeemed, expires, is exhausted or is replaced by a newer approval
- **THEN** the bell is no longer shown for the earlier ticket

### Requirement: Redemption attempts are rate limited

The redeem endpoint SHALL limit attempts per client and per account and answer a limited caller with a retry-after response, so a code cannot be guessed by trial.

#### Scenario: Attempts limited

- **WHEN** one client exceeds the permitted redeem attempts within the window
- **THEN** further attempts are refused with a retry-after response until the window passes

### Requirement: User is told after a reset

After a successful reset the system SHALL create an in-app notification and internal mail for the user stating that the password was changed and to contact an administrator if it was not them. They MUST NOT contain a password or code.

#### Scenario: User notified

- **WHEN** a reset completes
- **THEN** the user has an in-app notification and an Inbox mail about the change, containing no password or code

### Requirement: Audit trail of the reset lifecycle

The system SHALL record, without any password or code, each request, approval (with the approving administrator), re-issue, expiry or exhaustion, redemption and failed redemption, so a reset can be reconstructed.

#### Scenario: Lifecycle reconstructable

- **WHEN** an Administrator reviews a completed reset
- **THEN** the request, the approval and acting administrator, and the redemption are all present in the audit record
