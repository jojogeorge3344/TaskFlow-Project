# Proposal

## Why

A user who forgets their TaskFlow password currently has no recovery path: the only password
change available is self-service on the Profile Screen, which requires the current password
(`settings-and-profile`), and the administrator has no way to see, let alone act on, a reset
request. Forgotten passwords therefore turn into manual out-of-band coordination or direct DB
edits, with no request record, no notification and no audit trail. Requirement 18 asks for a
complete admin-assisted reset workflow: request → notify administrator → set the
password → notify the user → clear the login-screen notice after the next successful sign-in.

## What Changes

- **Forgot Password entry points.** A Forgot Password link on the Login Screen (opens an
  identifier dialog asking for the registered email or username; the answer is always a generic
  confirmation, so account existence is never disclosed) and on the Profile Screen (identity taken
  from the signed-in session). Both create a password recovery request with its own unique
  identifier, status and timestamps.
- **Administrator notification.** Every new request sends a mail to the administrator from the
  "TaskFlow" system sender through the existing internal Mail module, containing the requesting
  user's identification (name, username, email, role), the request details and a deep link to that
  request on Employee Management (`/employees?resetRequest=<reference>`) — never an existing
  password. Decision recorded with the user: **"email" is delivered by the internal TaskFlow Mail
  inbox; SMTP/MailKit is not reintroduced** (consistent with `internal-mail`, screen 14 superseded
  and `project.md`).
- **Admin-assisted password update.** The administrator reviews pending requests on the
  Employee Management screen (the project's User Details Screen, `/employees`) and either enters a
  new password for requests of either origin; the system never generates one, and the administrator
  conveys the password to the user outside the application. The update
  goes through the existing admin password update path (`PUT /api/v1/users/{id}`), enforced by the
  configured password policy, stored only as a PBKDF2 hash, and rotates the security stamp so
  existing sessions end. Request status and the update timestamp are recorded.
- **User notification.** On a successful update the system writes a mail to the user's TaskFlow
  Inbox and creates an in-app notification, both saying a new password was set and to ask the
  administrator for it. No password is ever mailed, notified, stored, logged or returned by an API:
  a locked-out user cannot open their Inbox anyway, so the password travels outside the application.
  Any administrator-issued password forces a password change at the next sign-in.
- **Encrypted per-user login address.** `/login/<token>` carries an encrypted token that lasts five
  minutes and works once, instead of the email: the page resolves it, shows the email pre-filled and checks
  for the notice, while the address bar never shows the email. A bad, expired, used or superseded token
  behaves like plain `/login`, which falls back to the identifier last used on that device. The
  administrator generates the link in the Details panel when the user is ready.
- **Password update from the login page.** On a page opened through the encrypted link, the bell opens a
  screen with New password and Confirm password and no way to cancel; the link is the proof, so no temporary
  password is asked for. On plain `/login` the bell points to sign-in: the user signs in with the password the
  administrator gave them and lands on the same New/Confirm screen. A new unauthenticated endpoint accepts only
  the link; it and sign-in are rate limited.
- **Login Screen notice.** A completed reset, from the Login Screen or the Profile Screen, activates a
  notification bell icon with a badge on the Login Screen, scoped to that user and to the latest reset
  event; activating it opens the password update screen. It stays until that user signs in with the
  new credentials or updates the password there, with no time limit; the page checks every couple of
  seconds, so the bell appears by itself. The Inbox mail and in-app notification are never affected.
  Older reset events' notices never reappear, never leak across users or requests, and a failed mail or
  failed password update never marks a request completed.
- **Audit trail.** Reset requests, administrative actions, notification events and completion are
  recorded so the whole lifecycle can be reconstructed.

## Capabilities

### New Capabilities

- `password-reset`: the admin-assisted password recovery workflow — forgot-password entry points
  (Login Screen dialog and Profile Screen), password recovery requests with status and audit,
  administrator notification mail, administrator-chosen password update with policy enforcement, user
  notification mail and in-app notification, the per-reset-event Login Screen notice lifecycle
  (set on completion, cleared on the first successful login, never restored from older events or
  shown for another user), and the security rules that govern all of it.

### Modified Capabilities

None. `openspec/specs/` is still empty — no earlier change has been archived — so nothing exists to
modify, and the in-flight deltas of `user-auth` (Login Screen), `user-profile`, `notification-center`
and `internal-mail` are unaffected: this change adds requirements alongside them without altering
any requirement they state.

## Impact

- **Domain:** new `PasswordResetRequest` and `LoginPasswordResetNotice` entities (or an equivalent
  single-table model), a reset status enum (`Pending`, `Completed`, `Failed`, `Cancelled`), and
  optional `NotificationType` extension for reset notices.
- **Application:** new `IPasswordResetService` with request creation, request listing, completion
  and login-notice operations; FluentValidation validators for the unauthenticated identifier
  request; DTOs for the Login Screen notice endpoint.
- **Infrastructure:** EF Core implementation, a new migration for the request/notice tables,
  reuse of `MailService` for the "TaskFlow" system sender, reuse of `IPasswordHasher<User>` and the
  existing `UserService.UpdateAsync` password path (policy, hash, security stamp).
- **API:** new endpoints under `/api/v1` — unauthenticated request creation and login notice
  (generic responses), authenticated request listing and completion actions — each with an explicit
  policy; existing `PUT /api/v1/users/{id}` remains the password update path.
- **Frontend:** Forgot Password dialog on `features/auth/login`, link on `features/profile`, a
  pending-requests section and reset actions on `features/employees` (User Details view), the Login
  Screen notice indicator, the encrypted `/login/:token` route and the password update screen opened by the bell, the mandatory password reset screen after sign-in with an administrator-issued password (New and Confirm Password only, no way out), the bell icon on the Login Screen,
  and a `PasswordResetService`.
- **Tests:** unit/integration coverage for request lifecycle, policy enforcement, mail/notification
  side effects, failure handling, notice scoping and clearing, plus Playwright coverage for the
  Login Screen notice.
- **Docs:** `project.md` "Out of scope" no longer excludes this feature (email-token reset stays
  out of scope; admin-assisted reset is now in scope).
