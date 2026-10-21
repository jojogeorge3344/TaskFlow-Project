# Design

## Context

See proposal.md - Why. Today `PasswordResetService` has the administrator type a password (`ProcessAsync` -> `SetPasswordAsync`, `MustChangePassword = true`), then mint a stateless Data Protection token (`PasswordResetLinkTokens`, `/login/:token`, lifetime default 1 minute) that `CompleteFromLoginAsync` accepts. Single use is enforced only through `LoginPasswordResetNotice.ClearedAt`. The existing pieces worth keeping: `PasswordResetRequest` (with `ReferenceId`), the one-notice-per-account `LoginPasswordResetNotice` and the Login Screen bell and polling, `NotifyAdminsAsync` (internal mail), `ChangeOwnPasswordAsync` (replaces the hash and ends sessions), the `/reset-password` component (new + confirm), the `PasswordAttemptsPolicy` rate limiter on `AuthController`, and the `/employees?resetRequest=` deep link. There is no SMTP; mail is internal Inbox rows that need a session.

## Goals / Non-Goals

**Goals:**
- The administrator is the gate but never chooses, sees or knows the new password.
- Possession of a server-side approval plus a code given out-of-band is required to reset.
- Every secret is hashed at rest, single-use, short-lived and attempt-limited.

**Non-Goals:**
- Email or SMS delivery, self-service reset without an administrator, MFA.
- Changing the normal sign-in or the Profile "change password" flow.
- Migrating tickets or in-flight requests of the old flow (see Migration Plan).

## Decisions

**D1 - Server-side ticket instead of a stateless token.** A new `PasswordResetTicket` row (id, `RequestId`, `UserId`, `CodeHash`, `Salt`, `ExpiresAt`, `FailedAttempts`, `UsedAt`, `RevokedAt`, `CreatedByAdminId`, `CreatedAt`). Alternatives: keep Data Protection tokens - rejected because attempt limits, revocation and burn-on-use need state, and the token would also have to be the secret that reaches the user. The ticket row is the state; the code is the secret.

**D2 - Code format and storage.** 10 characters from an unambiguous 32-symbol alphabet (50 bits) from `RandomNumberGenerator`, displayed grouped (`XXXXX-XXXXX`) and normalised (uppercase, no dashes/spaces) before hashing. Stored as an HMAC-SHA256 (server key from configuration) or salted SHA-256 of the normalised code, compared with `CryptographicOperations.FixedTimeEquals`. Alternatives: 6-8 digit code - rejected, too guessable once an attacker can reach the endpoint from many clients. Argon/bcrypt for the code - unnecessary at 50 bits with 5 attempts.

**D3 - Binding the code to the account (resolves the open question).** Redeem takes `identifier` (email or username) + `code`. The ticket is looked up by the account, never by the code, so a code is only ever checked against its own account's single live ticket. A wrong or missing account/ticket, a wrong code, an expired, used or dead ticket all return the same 401 generic message.

**D4 - Re-issue (resolves the open question).** Approve on a request that already has an unused ticket revokes the old ticket and creates a new one with a new code. Administrators do not wait for the user to submit again. The old code stops working immediately.

**D5 - Request lifecycle.** Replace `Pending/Completed/Failed` semantics with `Open`, `Approved`, `Redeemed`, `Expired`, `Cancelled`. `PasswordResetRequest` gains `RequestCount`, `LastRequestedAt`. One `Open`/`Approved` request per account (partial unique index). `CreateRequestAsync` on an existing open request increments the count; it does not insert. Admin mail is re-sent at most once per window per account (default 15 minutes) regardless of count, so a flood does not mail every administrator ten times. Approval does not complete the request; redemption does.

**D6 - Approval response is the only place the code exists.** `POST /api/v1/password-reset-requests/{ref}/approve` (`EmployeesManage`) returns `{ code, expiresAt }` once. Nothing logs, audits or notifies the code; the audit entry stores the ticket id and acting administrator only. The Employee panel shows it in a read-only field with a copy button and clears it from component state on leave. Alternative: send the code to the user through internal mail - rejected, the user has no session; and mailing it would also put the secret in a store administrators can read.

**D7 - Redeem endpoint.** `POST /api/v1/auth/redeem-password-reset` (anonymous) with `identifier`, `code`, `newPassword`. Order: validate the password (policy, max length) and return field errors **before** touching the ticket, so a typo does not burn an attempt; then verify the code; wrong code increments `FailedAttempts` and revokes at the limit. On success, in one transaction: `ChangeOwnPasswordAsync`-style hash replacement with `MustChangePassword = false`, revoke all refresh tokens/sessions, set `UsedAt`, mark the request `Redeemed`, clear the `LoginPasswordResetNotice`. The `ChangeOwnPasswordAsync` path currently requires the current password unless null is passed; the redeem path passes null, as `CompleteFromLoginAsync` does today. Replaces `complete-password-reset`.

**D8 - Throttling.** Request endpoint: existing `PasswordAttemptsPolicy` style per-IP limiter plus a per-account in-memory/DB window. Redeem endpoint: per-IP and per-account (by normalised identifier) windows; account-keyed limiting also blocks distributed guessing against one account, and the 5-attempt ticket limit bounds it absolutely (5 guesses of a 50-bit code). Limited callers get `429` + `Retry-After`. Request-endpoint responses stay identical for known and unknown identifiers; the throttle keys on the submitted string, not on account existence, so it does not leak existence.

**D9 - Login Screen bell.** `GET /auth/password-reset-notice?identifier=` now returns `{ available }` where available means a live ticket (not used, revoked, expired or exhausted) exists; same shape always, no token, no email. The bell opens `/reset-password`, which now shows four fields (identifier pre-filled from what the Login Screen already holds, code, new, confirm). Router state no longer carries a token or a temporary password; nothing credential-like enters history. `password-reset-link` endpoint and `/login/:token` route are removed.

**D10 - Deep link survives sign-in.** The auth guard stores the intended URL (`returnUrl`, same-origin relative paths only) and the Login component navigates there after sign-in, so the admin's mail link lands on the request. This also fixes the cause found while exploring why the mail link did not show the password box.

**D11 - Old flow removal.** Remove `SetPasswordAsync` use in `ProcessAsync`, `PasswordResetLinkTokens`, `PasswordResetOptions.LinkLifetimeMinutes` (replaced by `TicketLifetimeMinutes`, `MaxCodeAttempts`), the force-password-change use for recovery, the `TemporaryPasswordIssued` flag, `UserService` admin set-password for recovery, and the user "ask your administrator for it" mail/notification (replaced by a post-reset notification, D12). `force-password-change` stays for the existing first-login case if other code paths set `MustChangePassword`.

**D12 - Post-reset notification.** After redemption, one in-app notification and one internal mail to the user ("your password was changed; contact an administrator if it was not you"). Both carry no secret. Their failure does not undo the reset (the password change has already committed) and is logged.

**D13 - Relationship to `forgot-password-reset`.** That change is 65/74 tasks done and defines `password-reset`, which overlaps in purpose. This change supersedes its admin-typed-password and login-link requirements. The two are not archived together: `forgot-password-reset` is archived first (or abandoned at the user's choice), so `password-reset` reaches main specs, and this change's apply step then deletes the old behaviour. Spec removals of old requirements are left to that archive step, since the main specs are empty today and a REMOVED delta would have nothing to remove.

## Risks / Trade-offs

- [The code is passed outside the app, so it can be overheard or misdirected] -> 30-minute life, 5 attempts, single use, bound to one account, administrator verifies identity before approving.
- [A malicious or compromised administrator can approve and redeem for any account] -> Inherent to an admin-gated flow; mitigated by the audit trail of approver and redemption, user notification after every reset, and sessions ended on reset. They cannot learn an existing password, only take over via the code, which is visible to the user as a notification.
- [Anyone can type an identifier into the bell check to learn that a reset is pending] -> Same-shape response and rate limits; reveals only that an approval exists for that string, not whether it is a valid account, since unknown identifiers answer identically.
- [Attempt counter can be used to lock out a legitimate user (grief)] -> Bounded by per-IP/account throttles; administrator re-approves, which is a deliberate human step.
- [Larger rewrite than a patch: removes a mostly finished flow] -> Reuse of request entity, notice, panel and reset screen limits new code to the ticket, approve/redeem endpoints and UI wiring.
- [`ChangeOwnPasswordAsync` has a null-current-password path that must not be reachable from any endpoint except redeem] -> Keep it internal to the service call and cover with an integration test that the profile endpoint still requires the current password.

## Migration Plan

1. Add migration: `PasswordResetTickets` table; `PasswordResetRequests.RequestCount/LastRequestedAt`; status values mapped (`Pending` -> `Open`, `Completed` -> `Redeemed`, `Failed` -> `Cancelled`); drop `TemporaryPasswordIssued`.
2. Existing `LoginPasswordResetNotice` rows are cleared (their temporary passwords are obsolete); users with `MustChangePassword = true` from the old flow keep the existing forced-change screen until they change it.
3. Deploy backend and frontend together; the old `/login/:token` and `complete-password-reset` routes are removed in the same release.
4. Rollback: revert the release and the migration; no password data is lost because passwords are never stored in tickets.

## Open Questions

- Whether the code displayed to the administrator should also offer a QR or read-aloud grouping format; cosmetic, no effect on specs or tasks.
