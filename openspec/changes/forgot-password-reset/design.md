# Design

## Context

Facts that shape this design (motivation is in `proposal.md`, behavior in
`specs/password-reset/spec.md`):

- **Auth today:** `AuthService.LoginAsync` verifies with `IPasswordHasher<User>` (PBKDF2);
  `SessionValidation` rejects any access token whose security stamp no longer matches the user, and
  `UserService.UpdateAsync` already hashes an admin-supplied password and calls `EndSessionsAsync`
  when the password changes. The authenticated-request seam for a "you must change your password
  first" gate therefore already exists.
- **Password policy today:** `UpdateUserRequestValidator` / `ChangePasswordRequestValidator`
  (min length 6, max 128, non-blank) plus the `MinPasswordLength` check in `UserService`. There is
  no settings row for password complexity; screen 20 exposes application settings only.
- **Messaging today:** outbound SMTP was removed by `internal-mail` (MailKit, `EmailDeliveryLogs`,
  `EmailSettingsController` and the `Email` config section are gone; screen 14 is superseded).
  `ReminderService` shows the established way to write a system mail: insert `MailMessage`
  (`IsSystem = true`), a `MailRecipient` row per recipient and a `MailboxItem` in each Inbox. In-app
  bell notifications are written only through `INotificationPublisher`.
- **Admin surface today:** the User Details Screen from the requirement is the Employee Management
  screen (`/employees`, `features/employees/employee-details-panel.component.ts`). Password changes
  already go through `PUT /api/v1/users/{id}` guarded by `RequirePermission(PermissionKeys.EmployeesManage)`.
- **Mail privacy today:** `internal-mail` guarantees a user's mailbox is visible only to them;
  Administrators get no special access to other users' mail. That is why no password is ever placed in
  the Inbox: a locked-out user cannot open it, so the password travels outside the app (decision D4).

## Goals / Non-Goals

**Goals:**

- A request → admin notification → admin processing → user notification → login-notice lifecycle
  that is per-request, per-user, auditable and failure-honest.
- Reuse of the existing seams (system mail, notification publisher, password hashing/session ending,
  `employees.manage` permission, Employee Management screen) instead of new subsystems.
- No password in any log, API response, in-app notification or non-recipient-facing record.

**Non-Goals:**

- Tokenized self-service reset links (already out of scope in `project.md`); this is admin-assisted.
- Reintroducing SMTP or any outbound external email.
- A configurable password-complexity settings screen.
- Reset requests for inactive or soft-deleted accounts.

## Decisions

### D1 - "Email" is delivered by the internal TaskFlow Mail module

Every message in this flow (admin notification, user recovery mail) is written as an `IsSystem`
`MailMessage` from the "TaskFlow" sender to the recipients' Inboxes, exactly as `ReminderService`
does for deadline reminders.

*Why:* chosen by the user with the project's `internal-mail` decision; no SMTP subsystem exists to
call, and `IMailService.SendAsync` is caller-scoped (it would need a fake "sender user" and would
write a Sent item for someone). *Alternative:* reintroduce MailKit + delivery logs - rejected as it
reverses a deliberate project decision and widens scope by a whole subsystem.

### D2 - Two new entities: the request, and the per-user login notice

`PasswordResetRequests`: `Id`, `ReferenceId` (Guid, unique - the identifier used by admin UI and
audit so unauthenticated callers never see internal ids), `UserId`, `Origin`
(`LoginScreen | ProfileScreen`), `Status` (`Pending | Completed | Failed`), `CreatedAt`,
`CompletedAt`, `ProcessedByUserId`, `FailureReason`, `AdminNotifiedAt`, `UserNotifiedAt`,
`TemporaryPasswordIssued`.

`LoginPasswordResetNotices`: `Id`, `UserId` (**unique index**), `PasswordResetRequestId`,
`CreatedAt`, `ClearedAt`.

*Why:* the unique index on `UserId` gives exactly the requirement's semantics for free - one active
notice per account, always bound to the latest completed reset event, upserted (`INSERT` or
`UPDATE ... SET PasswordResetRequestId = @latest, ClearedAt = NULL`) when a later reset completes, so
older events cannot reappear and notices cannot cross accounts. *Alternative:* store notice columns
on the request row - rejected: it needs a scan to find "the" notice, cannot express "superseded by a
later event" cleanly, and leaves stale rows that a later request could resurrect.

**Notice for every origin.** The login notice is upserted when a request of either origin completes.
Setting the password ends the requester's sessions (D5), so a Profile-Screen requester is signed out
and lands on the Login Screen just like a Login-Screen requester; both need to learn that a new
password was set. The notice stays bound to the latest event and is cleared only by a successful
sign-in, so no per-origin rule is needed. *Alternative:* notice for Login-Screen requests only -
rejected: it left Profile-Screen requesters signed out with no indication on the screen they return to.

### D3 - The Login Screen notice is read through a uniform, unauthenticated endpoint

`GET /api/v1/auth/password-reset-notice?identifier=<email-or-username>` always answers `200` with the
same shape, `{ "noticeAvailable": true|false }` and nothing else: no name, no request id, no
timestamps, no difference in shape or error path between an existing and a non-existing account.
The Forgot Password submission endpoint behaves the same way (one generic confirmation).

*Why:* the requirement puts the indicator on the Login Screen, before any session exists, and the
user may be on a different device from the one where they clicked the link. *Alternative:*
device-scoped marker in `localStorage` set when the link is clicked - rejected as the notice itself: it
cannot work when the administrator processes the request later or elsewhere, which is the normal case.

**Per-user login address.** Superseded by D14: the address carries an encrypted token, not the identifier.
Plain `/login` still falls back to the identifier last used to sign in on that device (`localStorage`,
wrapped in try/catch, written only after a successful sign-in) for the same pre-fill and lookup; that
path adds no identifier to the address bar.

*Why:* the user cannot be expected to type their identifier before they learn a reset was processed.
*Alternative:* the remembered identifier alone - kept as the fallback only, because it cannot help on a
new device.

### D4 - The password is chosen by the administrator and passed on outside the application

The administrator types the new password into the processing action. It is hashed immediately by
`SetPasswordAsync` and never persisted, logged, mailed, put in a notification or returned by an API
response - only its PBKDF2 hash is stored. The user's recovery mail and in-app notification say that a
new password was set and to ask the administrator for it; they carry no password.

*Why:* the original design put a generated password in the user's Inbox, but a user who forgot their
password cannot sign in to read their Inbox, so that channel could never reach the people it was for.
Choosing the administrator as the relay is the smallest change that makes the flow work, and it needs
no token-link flow, which stays out of scope. *Alternatives:* show a generated password once to the
administrator (rejected: it still hands the administrator a secret to transcribe, with no benefit over
choosing one); keep Inbox-only delivery (rejected: dead end for a locked-out user).

### D5 - Password change reuses `UserService` logic through a focused method

A new `SetPasswordAsync(userId, newPassword, actingAdminId)` on `IUserService` reuses the existing
policy checks, `IPasswordHasher<User>` hashing, `EndSessionsAsync` / security-stamp rotation and the
audit stamp - the same code `UpdateAsync` uses today. `PasswordResetService` calls it; it does not
hash passwords itself.

*Why:* one implementation of "a password was changed, kill the sessions"; the admin UI keeps using
`PUT /api/v1/users/{id}` for full edits, while the reset flow needs a password-only action.
*Alternative:* call `UpdateAsync` with a full `UpdateUserRequest` - rejected: the reset flow would
have to round-trip role/department/project fields it has no business touching.

### D6 - Administrator-issued passwords are policy-checked and force a change at next sign-in

The administrator's password is validated against the existing policy (length 6-128, non-blank) for
both origins; a missing password is refused. Issuing one sets `MustChangePassword` on the user for
either origin, because the administrator now knows the password. A successful self-service change
(`POST /api/v1/users/me/password`) clears it. The server-side random generator is no longer used by
this flow.

*Why:* a password someone else chose must not survive the first sign-in. *Alternative:* keep random
generation for Login-Screen requests and have the administrator read it from the screen - rejected
with D4. The `ITemporaryPasswordGenerator` code may be removed later; it carries no behaviour here.

### D7 - The forced-change gate runs after authentication, as a 403 - not as a token failure

A small check on the authenticated pipeline rejects requests with
`ProblemDetails` `title: "Password change required"` (`403`) when `MustChangePassword` is true,
exempting `POST /api/v1/users/me/password`, `POST /api/v1/auth/logout` and `GET /users/me`. The
Angular auth interceptor maps that problem title to a redirect to a `force-password-change` route.

*Why:* failing in `SessionValidation` (i.e. `401`) would sign the user out - the interceptor's
documented reaction to `401` - so the user could never reach the change-password step the requirement
mandates. *Alternative:* enforce only in the SPA - rejected: server-side authorization is
authoritative in this codebase (`agent.md` §4).

### D11 - The mandatory screen asks for New and Confirm only

For a session whose user has `MustChangePassword`, `POST /api/v1/users/me/password` accepts a request
with no `currentPassword`: the user signed in with the temporary password moments ago, so that proof
already happened, and the screen never displays the temporary password (D4). For every other user the
current password stays required, unchanged. The change replaces the hash, clears the flag and ends
the user's sessions as today, so the temporary password stops working at once. The screen has no
cancel or close control, and D7's server gate (not the SPA) is what stops the user going elsewhere.

*Why:* the requirement wants a screen with New and Confirm fields and no way out; asking for the
temporary password again adds nothing once the user has just signed in with it. *Alternative:* keep a
Current/Temporary field - rejected as redundant friction; *alternative:* auto-fill the temporary
password from the sign-in form - rejected, it would keep a password in client memory for no benefit.
*Requirement 20 as first worded wanted the screen to display the temporary password after clicking the
icon on the login page: rejected - the login page is public, so anyone who knew the account email could
read the password and take the account.*

### D12 - The login notice is a bell icon that stays until sign-in or update

The Login Screen shows a bell with an unread badge instead of a banner; activating it opens the password
update screen (D14). The notice has no time limit: `noticeAvailable` is true while the account's notice row
has `ClearedAt` null, and a successful sign-in or a successful update from the Login Screen clears it. A later
reset rebinds the row to the newer event. The page checks for the notice every ~2 seconds while it knows the
account (typed, remembered or resolved from the encrypted link) and the tab is visible, so the bell appears
soon after the administrator finishes and disappears once it is cleared. The Inbox mail and in-app
notification are untouched.

*Why:* an earlier version expired the bell one minute after its first lookup. The page never refreshed
itself, so the first lookup was often a stranger's, an administrator's test or a blur nobody noticed, and by
the time the real user looked the bell was gone with no way back to the update screen. A notice that stays
until it is used is predictable. *Alternatives rejected:* a server-stamped first-view expiry (any anonymous
lookup could start the clock); a client-only per-visit timer (still gone after a minute of looking away).
*Trade-off:* the anonymous notice lookup is now polled; it stays uniform and reveals nothing about existence,
and polling stops when the tab is hidden.

### D8 - Ordering: change the password first, then notify, then mark completed

1. **Transaction A:** validate policy → `SetPasswordAsync` (hash + sessions ended) → set
   `MustChangePassword` (always, for an administrator-issued password) → save the request as `Pending` with
   `ProcessedByUserId`.
2. **Transaction B:** write the user's recovery mail and in-app notification → set
   `UserNotifiedAt` → upsert the login notice (every origin) → `Status = Completed`,
   `CompletedAt` set.
3. If step 2 fails, `Status = Failed` with `FailureReason`; the password stays changed (it must -
   reverting it would restore the forgotten password). The admin gets a **Retry notification**
   action that re-runs step 2 only, never re-hashing the password.

*Why:* matches the requirement that a mail failure must not fake completion, and makes the retry
idempotent and cheap. The admin-side notification mail is written when the request is created; if
that write fails, the request is still created with `AdminNotifiedAt = null` so the administrator
sees it in the UI even though the mail failed - the UI list, not the mail, is the source of truth.

### D9 - Admin recipients and authorization

The request notification mail goes to every active, non-deleted Administrator except the requesting
user, as one mail with one `MailRecipient` row per administrator. Request listing, processing and
retry are guarded by `RequirePermission(PermissionKeys.EmployeesManage)` - the same permission that
already guards password changes on `UsersController`. The unauthenticated endpoints (request
creation, notice read) are `[AllowAnonymous]` and never mutate anything but the request table.

*Why:* no new permission key, no new policy surface, and the admin screen already lives behind that
permission.

### D14 - Encrypted login link and the update-from-login-page endpoint

**Token.** The link is `/login/<token>`. The token is the output of the existing ASP.NET Data Protection
setup (persisted keys) through a time-limited protector with its own purpose string, over a payload of
the user id and the reset request id, so it is encrypted and tamper-evident, expires (default 5 minutes,
configurable), and is url-safe. It contains no password and no readable identifier. It is minted on demand
by `GET /api/v1/password-reset-requests/{referenceId}/login-link` (`employees.manage`, Completed requests
only, latest event only) and shown to the administrator with a copy action; nothing stores it.

**Resolving.** `GET /api/v1/auth/password-reset-link?token=` (anonymous) unprotects the token and checks
that its request id is still the user's current notice event. Valid: returns the email to pre-fill and
the usual `noticeAvailable`. Anything else: the same response as an unknown account (no email, `false`),
so a bad token reveals nothing and an earlier reset's link dies when a later reset completes. The page
shows the email, never the token's contents, and the address bar shows only the token.

**Proof, single use and expiry.** The link is the proof of who the user is when the update screen was opened
through it: the screen then asks only for New and Confirm, and the temporary password is never needed,
shown or pre-filled. The link lasts five minutes (configurable) and works once: it is accepted only while the
account's notice row is still the same event and not cleared, and the update (or any sign-in) clears the row,
so a used, leaked-then-used or forwarded-later link is dead. The administrator generates a link when the user
is ready and can generate another at any time; none is stored. *Why:* the user asked to avoid typing the
temporary password. Populating it was refused (the system holds only a hash, and the page is public); the link,
being a short-lived single-use secret given only to that user, is the standard safe substitute. *Residual risk:*
whoever holds a live link within its five minutes can set that account's password, so it must be sent privately
and only when the user is ready.

**Update from the login page.** `POST /api/v1/auth/complete-password-reset` (anonymous) accepts only the
`token` (the link is the proof) and `newPassword`. It requires the token's request to be the account's current,
uncleared event and the user to have `MustChangePassword`, applies the password policy, then replaces the hash,
clears the flag, ends sessions and clears the notice. Any failure of proof - missing, bad, expired, used or
superseded token - returns one generic 401. It never returns or accepts anything that reveals the stored password.
The earlier identifier-plus-temporary-password path is removed: with only a typed or remembered email nothing is
proven, and the user instead signs in with the temporary password, which leads to the mandatory screen (D11) -
the same New and Confirm screen the link gives. *Why:* two different update screens confused users, and the
sign-in path needs no new unauthenticated endpoint that checks passwords.


**Rate limiting.** Both password-checking anonymous endpoints (sign-in and this one) get a fixed-window
limiter keyed by client address (permits per minute configurable, `429` with `Retry-After`). Login has no
limiter today, so this also hardens it; the test factory raises the limit because every test client shares
one address.

*Why:* a link carrying the email puts an identifier in history and referrers; an encrypted token does not,
and still lets the page show the email once resolved. Requiring the typed temporary password keeps the
screen safe on a public page. *Alternative rejected:* populating the Temporary password box - the system
only holds a hash, and doing it would hand the password to anyone with the email (see D11).
*Known limit:* before sign-in the screen cannot stop someone closing the tab; the server's forced-change
gate (D7) still blocks every other route after sign-in.

### D13 - The administrator mail deep-links to the request

The mail's "Open Employee Management" line becomes `<Mail:AppBaseUrl>/employees?resetRequest=<ReferenceId>`
(still omitted when `AppBaseUrl` is unset). The employee list reads the `resetRequest` query parameter,
finds the request in the administrator's request list, opens the requesting employee's Details panel and
passes the reference to the panel, which highlights that request and focuses its password field when it
is still pending. An unknown or already-handled reference shows a message and the employee's current
requests; nothing is changed by opening a link. The route keeps its existing `employees.manage` guard, so
a non-administrator following the link is redirected as for any other forbidden screen.

*Why:* the mail told administrators to "open Employee Management" without saying where the action is, so
they reached for the Edit form, which changes the password but never completes the request - leaving no
notice for the user. The reference is already printed in the mail and is not a credential. *Alternative:*
make the Edit form complete pending requests too - not chosen here; it can be a separate change.
*Latest request only:* the Details panel shows just the employee's newest request (by creation time) and
no history, so repeated clicks on Forgot Password never pile up on screen. Older Pending rows stay in the
table and in the audit trail; they are simply not offered. *Alternative:* supersede older Pending requests
when the latest is processed - not done here.

*Mail rendering:* mail bodies are plain text, so the mail reader splits a body into text and `http(s)`
links and renders them through Angular bindings (never as HTML). A link to this application navigates in
the app (no reload, so the in-memory session survives); other addresses open in a new tab with
`noopener noreferrer`; other schemes are never linked.
*Known limitation:* the auth guard sends a signed-out visitor to `/login` without a return address, so an
administrator who follows the link while signed out lands on their dashboard after signing in and must
click the link again.

### D10 - Notification type and audit

Reset notifications use a new `NotificationType.PasswordReset` (additive to the enum, serialized via
`ToString()`), and the Notification Center filter gains that option. Audit rows reuse the
`PasswordResetRequests` history plus Serilog events for request created, admin notified, password
set (identifier only), user notified, notice set/cleared, denied attempts - with no password or hash
in any message.

## Risks / Trade-offs

- [The notice endpoint reveals that *some* account has a pending notice] → minimal uniform payload,
  no account details, generic Forgot Password confirmation, per-IP throttling following the existing
  assistant rate-limiter pattern; accepted residual risk because the requirement mandates a
  pre-login indicator.
- [Password changed but notification failed] → `Failed` status with `FailureReason` plus a retry that
  only re-runs notification; audited, never reported as completed (D8).
- [Administrator relays the password out of band] → the password never enters mail, notifications or
  logs; the flag forces the user to replace it at first sign-in (D6/D7), so the window in which the
  administrator-known value works is one sign-in.
- [Identifier in the login address] → shows in history and referrers; accepted, and the response
  stays uniform so it reveals nothing about account existence (D3).
- [Multi-admin races: two administrators process the same request] → completion is guarded by a
  conditional update (`WHERE Status = Pending`); the loser gets a "already processed" error and the
  request keeps the first `ProcessedByUserId`.
- [Two in-flight changes touch the Login Screen (`user-login`)] → this delta only *adds* requirements
  (link, notice) and never reworded the login requirements of `user-auth`, so archive merges cleanly.
- [`MustChangePassword` gate locks out a user if the exemption list is wrong] → exemptions limited
  to the change-password, logout and current-user endpoints, covered by integration tests for each.

## Migration Plan

1. EF Core migration `AddPasswordReset` creating `PasswordResetRequests` and
   `LoginPasswordResetNotices` (unique index on `UserId`, index on `Status`, FKs to `Users`), plus
   the `PasswordResetOrigin`/`PasswordResetStatus` enums stored as integers and the
   `Users.MustChangePassword` column. Additive only; existing rows default to
   `MustChangePassword = false`, so no data backfill and no behavior change for current users.
2. Deploy backend, then frontend (the frontend tolerates a missing endpoint by hiding the link only
   if the call fails - it is shipped together, so the link is simply present).
3. Rollback: revert the frontend, then `dotnet ef database update <previous-migration>` - dropping
   two unused tables and the column is lossless for unrelated features.

## Open Questions

- Whether password complexity should become an administrator-configurable setting (a
  `Security.*` settings row on screen 20). The spec says "the configured password policy", which
  today means the existing validator constants; adding a settings row later changes no requirement
  here and can be its own change.
- Whether recovery requests should expire after N days of admin inactivity. Not required by
  Requirement 18; if wanted, it is a new requirement with its own scenarios rather than a silent
  behavior change.
