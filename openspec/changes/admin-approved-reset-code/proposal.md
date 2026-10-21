# Proposal

## Why

The current forgot-password flow (change `forgot-password-reset`) has the administrator choose and type the user's new password, then hand it over outside the app along with a 1-minute login link. The administrator therefore knows the user's password, the temporary password travels by voice or chat, and the 1-minute link forces the admin and user to act in lockstep. The backend has no real email delivery, and a user who forgot their password has no session to read in-app mail, so a standard email-link reset is not available. We need a recovery flow that keeps the administrator as the gate, works entirely inside the app, and never lets anyone but the user know the new password.

## What Changes

- Replace the admin-typed temporary password with an **admin-approved reset**: the administrator verifies the requester and clicks Approve; no password is chosen or typed by the administrator.
- Approving creates a **one-time reset ticket** with a random code, stored hashed, single-use, short-lived (default 30 minutes) and attempt-limited (default 5 wrong tries kills it). The code is shown to the administrator once, to be passed to the user outside the app; it is never put in mail, notifications or logs.
- The user redeems the ticket on the Login Screen / reset page by entering identifier + code, then **chooses and confirms their own new password**. A successful reset ends all sessions, burns the ticket and notifies the user.
- The Login Screen bell only signals that a reset was approved; it never carries the code or any token.
- **Repeated forgot-password requests** (10 or more) collapse into one open request per account with a counter, are throttled per IP and per account, and always receive the same generic response so account existence is not revealed. A new request or new approval retires any earlier ticket.
- **BREAKING**: remove the administrator "Set password" action, the `MustChangePassword` temporary-password step for recovery, and the 1-minute stateless Data Protection login link.

## Capabilities

### New Capabilities
- `admin-approved-password-reset`: request collapsing and throttling, administrator approval, hashed one-time reset ticket with expiry and attempt limit, user redemption with self-chosen password, session revocation and audit.

### Modified Capabilities
<!-- None in openspec/specs/ (main specs are empty). The in-flight change `forgot-password-reset` defines the `password-reset` capability this change supersedes; it is not yet archived, so it is addressed under Impact rather than as a delta. -->

## Impact

- **Backend**: `PasswordResetService` (processing, link creation and login-link resolution replaced), `PasswordResetLinkTokens` (removed), `PasswordResetRequestsController`, `AuthController` / `AuthService` reset endpoints, `UserService` (no admin-set password for recovery), new EF entity, migration and rate limiting for reset tickets, adjustments to `LoginPasswordResetNotice`, request and notification enums.
- **Frontend**: Employee details panel (Approve and show-code-once replaces Set password and Generate login link), Login Screen bell and reset page (identifier, code, new and confirm password), `password-reset.service.ts`, models, routes.
- **Tests**: unit, integration and component specs for the old flow are rewritten or removed.
- **Relationship to `forgot-password-reset`** (65 of 74 tasks done): this change supersedes its admin-typed-password and login-link design. That change should be closed out as-is or trimmed once this one is applied; the overlap is to be settled in design.
- **Dependencies**: none added. No email or SMTP infrastructure.
- **Open questions for design**: whether the admin can re-issue a code for the same request, and whether redemption binds the code to the account's email or username.
