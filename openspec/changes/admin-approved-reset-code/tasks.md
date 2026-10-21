# Tasks

## 1. Data model and migration

- [ ] 1.1 Add `PasswordResetTicket` entity and DbSet (code hash, salt, expiry, failed attempts, used/revoked times, creating admin) and verify the model snapshot builds
- [ ] 1.2 Extend `PasswordResetRequest` with `RequestCount` and `LastRequestedAt`, replace status values with Open/Approved/Redeemed/Expired/Cancelled, drop `TemporaryPasswordIssued`, add the one-open-request-per-account partial unique index
- [ ] 1.3 Generate the EF migration mapping old statuses (Pending->Open, Completed->Redeemed, Failed->Cancelled) and clearing obsolete `LoginPasswordResetNotice` rows; verify it applies to a database seeded with old-flow rows
- [ ] 1.4 Replace `PasswordResetOptions` settings with `TicketLifetimeMinutes` (30), `MaxCodeAttempts` (5), request-mail window (15) and a code-hashing key; verify defaults via a unit test

## 2. Code generation and ticket service

- [ ] 2.1 Implement code generator (10 chars, 32-symbol alphabet, `RandomNumberGenerator`) with normalisation and keyed hash with fixed-time compare; verify unit tests for entropy length, normalisation, wrong/right code and no plaintext in the stored value
- [ ] 2.2 Implement ticket create/revoke/verify in the service (revoke previous ticket on re-issue, attempt counting, exhaustion, expiry, single use); verify unit tests for each outcome including the 5th wrong attempt killing a correct code

## 3. Request, approve and redeem services

- [ ] 3.1 Rework `CreateRequestAsync` to collapse repeats into the open request (count, last-requested) and mail administrators at most once per window; verify an integration test where ten submissions yield one request with count 10 and one admin mail
- [ ] 3.2 Add `ApproveAsync` (admin permission, account active check, creates ticket, returns code once, audit entry without code); verify integration tests for approve, re-issue invalidating the old code, and refused approval for an inactive account
- [ ] 3.3 Add `RedeemAsync` (validate password before touching the ticket, verify code by account, replace hash, revoke sessions, burn ticket, mark request Redeemed, clear notice) in one transaction; verify integration tests for success, mismatch/weak password not burning an attempt, wrong account, expired, used and exhausted tickets returning the same generic refusal
- [ ] 3.4 Add post-reset user notification and mail with no secret and verify its failure does not undo the reset
- [ ] 3.5 Make `GetNotice` return availability from a live ticket only, same response shape always; verify tests for live, expired, exhausted, replaced and unknown identifiers

## 4. API and rate limiting

- [ ] 4.1 Add `POST password-reset-requests/{ref}/approve` (EmployeesManage) and verify the code appears only in that response and the endpoint is forbidden for non-admins
- [ ] 4.2 Add anonymous `POST auth/redeem-password-reset` and per-IP plus per-account limiters for it and for the request endpoint, answering 429 with Retry-After; verify integration tests that limits trip and that unknown and known identifiers get identical request responses
- [ ] 4.3 Remove `process`, `retry-notification`, `login-link`, `password-reset-link` and `complete-password-reset` endpoints and `PasswordResetLinkTokens`; verify the solution builds and no references remain
- [ ] 4.4 Confirm the profile change-password endpoint still requires the current password; verify an integration test

## 5. Frontend

- [ ] 5.1 Add `returnUrl` capture in the auth guard (same-origin relative only) and navigation after sign-in; verify a guard/login spec that a signed-out admin lands on `/employees?resetRequest=<ref>` after signing in and an off-site URL is ignored
- [ ] 5.2 Employee details panel: replace Set password and Generate login link with Approve and a show-once code with copy, cleared on leave, plus request count and last-requested; verify component specs for open, approved, redeemed and re-issue states
- [ ] 5.3 Reset-password screen: identifier, code, new and confirm; call redeem, show the generic refusal, and send nothing through router state; verify component specs for success, mismatch, refusal and 429 messages
- [ ] 5.4 Login Screen bell and Forgot Password dialog: bell reflects live-ticket availability with polling, opens the redeem screen; remove `/login/:token` route and link resolution; verify specs and remove obsolete old-flow specs
- [ ] 5.5 Update `password-reset.service.ts`, models and notification text for the new endpoints and statuses; verify service specs against the new routes

## 6. Cleanup and verification

- [ ] 6.1 Remove old-flow code and tests (admin-set password for recovery, temporary-password flag use, link-lifetime option, force-password-change for recovery) and verify full backend and frontend test suites pass
- [ ] 6.2 End-to-end check in the running app: request x3, admin opens mail link signed out and signs in, approves, user redeems with the code, old password fails and new one signs in, reused code is refused; record the result
- [ ] 6.3 Update the TDD and docs for the new flow and verify they no longer describe an admin-typed password or the login link

## Workflow follow-up

- Decide with the user how `forgot-password-reset` is closed out before archiving this change (see design D13).
- Archive this change after review and verify the archived `admin-approved-password-reset` spec.
