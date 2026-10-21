# Tasks

## 1. Domain and persistence

- [x] 1.1 Add the `PasswordResetRequest` and `LoginPasswordResetNotice` entities, the
  `PasswordResetOrigin` (`LoginScreen`, `ProfileScreen`) and `PasswordResetStatus` (`Pending`,
  `Completed`, `Failed`) enums, and `User.MustChangePassword` to `TaskFlow.Domain`; verify with
  `dotnet build` from `backend/` (compiles, no other layer referenced).
- [x] 1.2 Register the two `DbSet`s and their fluent configuration in `AppDbContext` (FKs to `Users`,
  unique index on `LoginPasswordResetNotices.UserId`, index on `PasswordResetRequests.Status`,
  `ReferenceId` unique); verify with an integration test that inserting a second notice row for the
  same user is rejected by the unique index.
- [x] 1.3 Create and apply the EF migration `AddPasswordReset` (tables + `Users.MustChangePassword`
  defaulting to `false`); verify `dotnet ef database update
  --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.Api` succeeds and the two
  tables and the unique index exist in `database/taskflow.db`.
- [x] 1.4 Update `TaskFlow Proj/project.md` §6 (entity list, new enums) and `Artifacts/TDD-TaskFlow.md`
  table list with the new tables; verify both documents name `password_reset_requests`,
  `login_password_reset_notices` and `MustChangePassword` exactly as the migration creates them.

## 2. Recovery request creation and administrator notification

- [x] 2.1 Add `IPasswordResetService` (Application) with the unauthenticated request-creation
  operation covering both origins, recording `ReferenceId`, `UserId`, `Origin`, `Status = Pending`
  and `CreatedAt`; verify with integration tests that a registered active identifier creates exactly
  one request and that an unknown, inactive or deleted identifier creates none while returning the
  same generic result.
- [x] 2.2 Add the FluentValidation validator for the identifier submission (required, length-bounded,
  trimmed); verify with unit tests that an empty or oversized identifier is rejected with a field
  error and no request is written.
- [x] 2.3 Write the administrator notification as an `IsSystem` mail from the "TaskFlow" sender to
  every active, non-deleted Administrator except the requester, containing name, username, email, role
  and request reference and no password, setting `AdminNotifiedAt`; verify with an integration test
  that each eligible administrator has one new Inbox mail, that the requester-admin does not, and
  that forcing the mail write to fail still creates the request with `AdminNotifiedAt` null.
- [x] 2.4 Add `POST /api/v1/auth/password-reset-requests` (anonymous, uniform `200` generic
  confirmation) and `GET /api/v1/password-reset-requests` (`RequirePermission(PermissionKeys.EmployeesManage)`);
  verify with integration tests that the anonymous endpoint returns an identical response body for
  known and unknown identifiers, and that the list endpoint answers `403` for Manager and Employee
  callers.

## 3. Administrator processing, user notification and completion

- [x] 3.1 Add `IUserService.SetPasswordAsync(userId, newPassword, actingAdminId)` reusing the existing
  policy checks, `IPasswordHasher<User>` hashing and `EndSessionsAsync`/security-stamp rotation;
  verify with integration tests that a compliant password updates the stored hash, ends the target
  user's sessions, and a too-short password is refused leaving the hash unchanged.
- [x] 3.2 Implement the temporary-password generator (`RandomNumberGenerator`, 16 characters,
  lower/upper/digit/symbol, regenerated until it passes the policy validators) and set
  `MustChangePassword` when it is issued; verify with unit tests that generated values always satisfy
  the policy, never repeat across a large sample, and are never written to any log or DTO.
- [x] 3.3 Implement the process operation behind `POST /api/v1/password-reset-requests/{referenceId}/process`
  (`employees.manage`, accepts supplied password or auto-generates for `LoginScreen` origin) using the
  two-transaction order from design D8: password update first, then user recovery mail
  (with the temporary password only there), `INotificationPublisher` notification of type
  `PasswordReset`, login-notice upsert for Login-Screen origins, and `Completed` only after the
  notification exists; verify with integration tests the happy path, weak-password rejection,
  mail-write failure leaving `Failed` with `FailureReason`, and a second administrator's concurrent
  process being refused as already processed.
- [x] 3.4 Add the retry-notification operation for `Failed` requests (re-runs notification and
  completion only); verify with an integration test that retrying after a simulated mail failure does
  not change the stored password hash or re-issue a temporary password, then marks the request
  `Completed` with `CompletedAt` and `ProcessedByUserId`.
- [x] 3.5 Record the audit trail (Serilog events for request created, admin notified, password set,
  user notified, notice set, notice cleared, denied attempts - identifiers only, no secrets); verify
  with a unit test that the captured log events for a full lifecycle contain no password or hash
  substring.
- [x] 3.6 Add `NotificationType.PasswordReset` and expose it in the Notification Center type filter;
  verify with a backend test that the new notification is returned with `type: "PasswordReset"` and
  with a frontend spec test that the filter lists and applies it.

## 4. Login Screen notice and forced password change

- [x] 4.1 Add `GET /api/v1/auth/password-reset-notice?identifier=...` (anonymous) returning the single
  uniform field `noticeAvailable`; verify with integration tests that it is `true` only after a
  completed Login-Screen-originated reset for that identifier, `false` otherwise, byte-identical in
  shape in both cases, and never shows another account's notice.
- [x] 4.2 Clear the account's active notice inside a successful login (and only there); verify with
  integration tests that a successful sign-in with the new password clears the notice so the next
  notice read returns `false`, while a failed sign-in leaves it, and that a later completed reset
  rebinds the notice so the earlier event stays cleared.
- [x] 4.3 Enforce `MustChangePassword` after authentication as a `403` ProblemDetails titled
  "Password change required", exempting `POST /api/v1/users/me/password`, `POST /api/v1/auth/logout`
  and `GET /api/v1/users/me`, and clear the flag on a successful self-service change; verify with
  integration tests that each exempt route works while every other authenticated route is refused,
  and that after the change the previously refused route succeeds.

## 5. Login Screen and Profile Screen entry points

- [x] 5.1 Add `PasswordResetService` in the Angular `core` layer (create request, read notice) with
  typed models; verify with a service spec that the request posts the trimmed identifier and the
  notice call maps `noticeAvailable` to a signal.
- [x] 5.2 Add the Forgot Password dialog to `features/auth/login` (identifier field with required
  validation, generic confirmation rendered for every submission, dialog closes without revealing
  account existence); verify with a component spec covering empty submit blocked, known and unknown
  identifier showing the same confirmation text, and the link being visible for every visitor.
- [x] 5.3 Add the Forgot Password link to `features/profile` submitting with the session identity;
  verify with a component spec that the confirmation appears and that no identifier is collected from
  the signed-in user.

## 6. User Details Screen (Employee Management) processing UI

- [x] 6.1 Add the "Password reset requests" section to `features/employees/employee-details-panel`
  showing pending requests (requesting user, origin, status, created time) with Generate/Process and
  Retry-notification actions; verify with component specs for rendering the list, the generate flow
  posting to the process endpoint, the retry action, and the password never being displayed after
  processing.
- [x] 6.2 Gate the section on `employees.manage`; verify with a component spec that a caller without
  the permission never renders the section and that the API `403` is surfaced as an error message
  rather than a silent failure.

## 7. Login notice indicator and forced change screen

- [x] 7.1 Show the notice indicator on the login screen for the identifier being entered and remove
  it after a successful sign-in; verify with a component spec that the indicator appears when
  `noticeAvailable` is true, disappears after `login()` succeeds, and stays after a failed login.
- [x] 7.2 Add the `force-password-change` route with its screen (current/new/confirm fields, policy
  mirror validation, submits `POST /users/me/password`) and map the "Password change required" 403 in
  the auth interceptor to that route; verify with specs that the route is reachable with a valid
  token, the submit clears the flag, and the interceptor redirects on that 403 but still signs out on
  ordinary 401s.
- [ ] 7.3 Add a Playwright end-to-end test for the full loop: request from the login dialog →
  administrator generates the password on the Employee Management screen → user returns to login,
  sees the indicator, signs in with the temporary password → forced change screen → new password →
  indicator gone on next visit; verify with `npx playwright test` from `frontend/`.

## 8. Integration verification

- [x] 8.1 Run `dotnet build` and `dotnet test` from `backend/` and fix anything this change broke;
  verify both complete with no failing tests.
- [x] 8.2 Run `npx ng build` and `ng test` from `frontend/` and fix anything this change broke;
  verify both complete with no failing tests.
- [x] 8.3 Run `openspec validate --strict --change forgot-password-reset` from the workspace root;
  verify it reports no errors or warnings for this change.
- [ ] 8.4 Walk acceptance criteria 1-10 of Requirement 18 manually against the running app
  (`dotnet run --project src/TaskFlow.Api` + `npm start`) and record the result for each criterion
  in the change folder; verify every criterion is marked pass with the observed behaviour.

## 9. Revision: administrator-chosen password and per-user login address

Supersedes the generated-password behaviour of 3.2 and 3.3 and the Inbox-delivery assumption of D4
(tasks 3.2, 3.3, 3.4, 4.1 and 7.1 stay ticked as built; this section carries the delta).

- [x] 9.1 Make the process operation (`POST /api/v1/password-reset-requests/{referenceId}/process`)
  require an administrator-supplied password for both origins, refuse a missing or policy-violating one
  with `400` leaving the request `Pending`, and stop calling `ITemporaryPasswordGenerator`; verify with
  integration tests that a Login-Screen request now needs a password, that a missing or too-short one
  is refused with the hash unchanged, and that a compliant one completes the request.
- [x] 9.2 Set `MustChangePassword` for every administrator-issued password regardless of origin
  (today only generated ones set it); verify with an integration test that a Profile-Screen request
  processed with a supplied password forces the change at next sign-in.
- [x] 9.3 Rewrite the user recovery mail and in-app notification text to say a new password was set
  and to ask the administrator for it, containing no password, and make the retry path identical;
  verify with integration tests that no mail body, notification message, API response or captured log
  line contains the supplied password and that the mail contains the "ask your administrator" text.
- [x] 9.4 Remove or retire `ITemporaryPasswordGenerator`, its DI registration and
  `TemporaryPasswordGeneratorTests` once nothing references them; verify `dotnet build` and
  `dotnet test` from `backend/` pass.
- [x] 9.5 Add the `/login/:identifier` route to the Angular app: the login component pre-fills the
  identifier from the route parameter and runs the notice lookup on load; plain `/login` pre-fills from
  the identifier last used to sign in (`localStorage`, try/catch, written after a successful sign-in
  only); verify with component specs that the named address shows the indicator on load, that an
  unknown account shows nothing and looks identical, that the remembered identifier is used on
  `/login`, and that a blocked `localStorage` does not break the page.
- [x] 9.6 Change the indicator text to say a new password was set and to ask the administrator for
  it, and change the Employee Management processing UI so every pending request has a required
  password field (no "Generate" action); verify with component specs that processing without a typed
  password is blocked client-side, that the typed password is sent once and never displayed afterwards,
  and that the indicator text matches.
- [x] 9.7 Update `Artifacts/TDD-TaskFlow.md` and `project.md` where they describe generated
  temporary passwords or Inbox delivery; verify neither document still says a password is mailed or
  generated.
- [ ] 9.8 Re-run `dotnet test`, `ng test`, `ng build` and `openspec validate --strict`, then walk
  acceptance criteria 1-10 of Requirement 18 against the running app including the `/login/<id>`
  address, recording each result in the change folder.

## 10. Revision: login notice for every request origin

Supersedes the Login-Screen-only notice of 2.x/3.3 (those tasks stay ticked as built).

- [x] 10.1 Upsert the login notice when a request of either origin completes (drop the
  `origin == LoginScreen` condition in `NotifyUserAndCompleteAsync`); verify with integration tests that
  a Profile-Screen request processed by an administrator makes `GET /auth/password-reset-notice` return
  `true` for that account, that a sign-in clears it, and that the retry path also sets it.
- [x] 10.2 Update the tests that assert "Profile-Screen origins never write a login notice" to the new
  rule; verify `dotnet test` from `backend/` passes.
- [x] 10.3 Re-run `ng test`, `ng build` and `openspec validate --strict`; verify all pass (no frontend
  code change is expected, the notice read is origin-agnostic).

## 11. Revision: mandatory reset screen (Requirement 20) and expiring bell icon

Supersedes the Current/Temporary field of the force-password-change screen (7.2) and the login
banner indicator (5.2/7.1); those tasks stay ticked as built.

- [x] 11.1 Add `FirstShownAt` (nullable) to `LoginPasswordResetNotice` with an EF migration; verify the
  migration applies and the column exists in `database/taskflow.db`.
- [x] 11.2 Make the notice lookup expire: available only while `ClearedAt` is null and `FirstShownAt` is
  null or under 60 s old; stamp `FirstShownAt` once on the first lookup that returns true; reset it when
  a later reset rebinds the notice. Use `TimeProvider` so it is testable; verify with integration tests
  that the icon is available, stays for a reload within the minute, is gone after 60 s (advance the fake
  clock), is cleared at once by sign-in, gets a fresh window on a later reset, and that the Inbox mail
  and in-app notification remain after expiry.
- [x] 11.3 Let `POST /api/v1/users/me/password` accept no `currentPassword` when the caller has
  `MustChangePassword`, keeping it required for everyone else; verify with integration tests that a
  flagged user can change with New and Confirm only, that the temporary password no longer signs in
  afterwards, that the flag is cleared and sessions end, and that an unflagged user without a current
  password is still refused.
- [x] 11.4 Rebuild the force-password-change screen with New Password, Confirm Password and Update
  Password only: no current-password field and no cancel/close/back control; the server gate plus the auth
  interceptor return the user to it from any other route (the SPA cannot know the flag); verify with component specs for the two fields, the mismatch and
  policy errors, that submit sends no current password, and that no escape control exists.
- [x] 11.5 Replace the login banner with the bell icon and unread badge opening the notice popover
  (keyboard operable, reduced-motion safe, mirroring `Artifacts/mockups/login-reset-notice.html`);
  verify with component specs that the icon appears on load for a notice, opens the text on activation,
  and disappears when the lookup stops returning it or after a sign-in.
- [x] 11.6 Update `Artifacts/mockups/login-reset-notice.html` if the final design differs, and update
  `project.md`/TDD where they describe the notice; verify `openspec validate --strict` and all
  test suites pass.
- [ ] 11.7 Re-walk acceptance criteria 1-10 plus Requirement 20 against the running app (with 8.4
  and 9.8), recording each result in the change folder.

## 12. Revision: administrator mail deep-links to the request

Supersedes the generic "Open Employee Management" link of 2.3 (stays ticked as built).

- [x] 12.1 Change the administrator mail link to `<AppBaseUrl>/employees?resetRequest=<ReferenceId>`
  (omitted when `AppBaseUrl` is unset); verify with integration tests that the mail body contains that
  exact link when `AppBaseUrl` is set, contains no link when it is not, and still contains no password.
- [x] 12.2 Make the employee list read `resetRequest`, resolve the request and open its requesting
  employee's Details panel, and handle an unknown reference with a message; verify with component specs
  that the panel opens for a known reference, that an unknown one shows the message and opens nothing,
  and that a non-administrator never reaches it (route guard unchanged).
- [x] 12.3 Make the Details panel highlight the referenced request and focus its password field when it
  is pending, and show its status plus a message when it is not; verify with component specs for the
  highlighted, focused, already-completed and no-reference cases.
- [ ] 12.4 Update `project.md`/TDD where they describe the administrator mail; run all test suites and
  `openspec validate --strict`, then walk it against the running app (with 8.4, 9.8 and 11.7): raise a
  request from the Login Screen, open the mail link as administrator, set a password from there, and see
  the bell on the user's login address.

- [x] 12.5 Make web addresses in a mail body clickable in the mail reader (same-app links navigate inside
  the app, others open in a new tab, markup never rendered), and make the password forms show errors only
  after a field is touched or typed in, with "Passwords do not match." appearing as Confirm Password is
  typed (Profile change-password form and the mandatory reset screen); verify with specs for the link
  rendering, in-app navigation, external links, markup safety, the untouched forms showing no errors, the
  live mismatch message and the submit-shows-everything case.

- [x] 12.6 Show only the employee's latest password reset request in the Details panel (no history, newest
  by creation time), with a note when a mail link names an older request; verify with component specs for
  several requests showing one, the newest chosen, and the older-link note.

## 13. Revision: encrypted login link and password update from the login page

Supersedes the plain `/login/:identifier` address (9.5), the bell notice popover (11.5) and the open
"no rate limiting" gap; 9.5/11.5 stay ticked as built.

- [x] 13.1 Add a login-link token service (Data Protection, time-limited protector, own purpose, payload =
  user id + request id, configurable lifetime) with `GET .../password-reset-requests/{ref}/login-link`
  (`employees.manage`, latest Completed request only); verify with unit/integration tests that the token
  round-trips, contains neither the email nor any password, expires, is refused when tampered with, and
  that a Manager/Employee gets 403 and a non-latest or non-Completed request gets 404/409.
- [x] 13.2 Add anonymous `GET /api/v1/auth/password-reset-link?token=` returning the email and
  `noticeAvailable` for a valid token and the uniform empty response otherwise; verify with integration
  tests that a valid token returns the email, that bad, expired and superseded (later reset) tokens are
  indistinguishable from an unknown account, and that the response never contains a password.
- [x] 13.3 Add anonymous `POST /api/v1/auth/complete-password-reset` (token or identifier +
  `temporaryPassword` + `newPassword`): verify the temporary password like sign-in, require
  `MustChangePassword`, apply the policy, replace the hash, clear the flag, end sessions, clear the notice;
  one generic 401 for any wrong input; verify with integration tests for success (old password dead, notice
  cleared, flag lifted), wrong temporary password, unknown account/bad token (identical response), mismatch
  or weak password, and an account without the flag.
- [x] 13.4 Add a fixed-window rate limiter (configurable permits per minute, `429` + `Retry-After`) to
  sign-in and the update endpoint, with the test factory raising the limit; verify with an integration
  test that the (n+1)th attempt in a window is refused with `Retry-After` and that other endpoints are
  unaffected.
- [x] 13.5 Replace the `/login/:identifier` route with `/login/:token`: resolve the token, show the email
  pre-filled (read-only look not required), keep the remembered-identifier fallback, treat any failure as
  plain `/login`; verify with component specs that the address never holds the email, that a valid token
  fills the email and shows the bell, and that a bad token shows the plain page.
- [x] 13.6 Make the bell open the update screen (route `/reset-password`, carrying the token or the typed
  identifier in router state, never in the address) with Temporary password, New password, Confirm password,
  the email shown read-only and no cancel/close/link out; verify with component specs for the three empty
  fields, the live mismatch error, a generic error on a wrong temporary password, success redirecting to
  sign-in with the credentials-changed message, and no escape control.
- [x] 13.7 Show the user's login link with a copy action on a Completed request in the Details panel;
  verify with component specs that it appears only for a Completed latest request, copies the link, and
  carries no password.
- [ ] 13.8 Update `Artifacts/mockups/login-reset-notice.html` for the new bell behaviour, then re-run all
  suites, `openspec validate --strict`, and walk it on the running app (with 8.4, 9.8, 11.7 and 12.4).

## 14. Revision: the bell stays until sign-in or update, and appears by itself

Supersedes the one-minute expiry of 11.1/11.2 and the countdown of 11.5 (those stay ticked as built; this
section removes what they added).

- [x] 14.1 Remove the expiry: `HasPasswordResetNoticeAsync` answers true while the notice is not cleared
  (no `FirstShownAt`, no 60-second window), drop `FirstShownAt` from the entity, the rebinding code and the
  model, and add an EF migration that drops the column; verify the migration applies, and with integration
  tests that the notice stays available across repeated lookups and after the notice is old, is cleared by
  sign-in and by the update screen, and that a later reset rebinds it.
- [x] 14.2 Make the Login Screen check for the notice every ~2 seconds while it knows the account (typed,
  remembered or resolved from the link) and the tab is visible, without overlapping requests; remove the
  one-minute countdown; verify with component specs that the bell appears on a later check without a
  reload, disappears when a check stops returning it, stops checking when the page is destroyed, and no
  longer disappears by itself after a minute.
- [x] 14.3 Update `Artifacts/TDD-TaskFlow.md`, `project.md` and the mockup for the notice that stays;
  verify no document still mentions a one-minute expiry or `FirstShownAt`.
- [ ] 14.4 Re-run all suites and `openspec validate --strict`, then walk it on the running app with the
  other open walk-throughs.

## 15. Revision: the link is the proof (five minutes, one use) and the temporary password box goes

Supersedes the always-typed Temporary password of 13.3/13.6 and the 7-day link of 13.1 (those stay ticked as
built).

- [x] 15.1 Shorten the login-link lifetime to five minutes (`PasswordReset:LinkLifetimeMinutes`, default 5)
  and make it single-use: the token is accepted by resolve, create and update only while the account's notice
  is the same event and not cleared, and `GET .../login-link` answers 409 once the notice is cleared; verify
  with unit tests that a token expires after five minutes and not before, and integration tests that a used
  link no longer resolves or updates and that a new link can be generated after an unused one expires.
- [x] 15.2 Let `POST /auth/complete-password-reset` accept a token without a temporary password (the link is
  the proof) and keep requiring the temporary password on the identifier path; validate accordingly; verify
  with integration tests that the token path updates with only New password, the identifier path still
  refuses a wrong or missing temporary password, a used/expired/superseded token gets the generic 401, and
  nothing in any response reveals a password.
- [x] 15.3 Show the Temporary password field on the update screen only when it was not opened through a
  link, and send no temporary password on the link path; verify with component specs for both variants.
- [x] 15.4 Change the Details-panel note to say the link lasts five minutes and works once, and show the
  409 as an error message; update the TDD, `project.md` and the mockup; verify with component specs.
- [ ] 15.5 Re-run all suites and `openspec validate --strict`, then walk it on the running app with the
  other open walk-throughs.

## 16. Revision: one update screen - the plain login bell points to sign-in

Supersedes the Temporary/New/Confirm variant and the identifier path of 13.3/13.6/15.2/15.3 (those stay ticked
as built; this section removes them).

- [x] 16.1 Make `POST /auth/complete-password-reset` accept only the token and the new password: drop
  `identifier` and `temporaryPassword` from the request, the validator and `PasswordResetService`, requiring
  a token; verify with integration tests that the link path updates, that a request without a token, or with a
  bad, used, expired or superseded one, gets the generic 401/400 and changes nothing, and that nothing reveals
  a password.
- [x] 16.2 Remove the Temporary password field and the identifier path from the update screen (it needs the
  token from the link, otherwise it returns to the login page) and from `PasswordResetService`; verify with
  component and service specs that the screen has only New and Confirm and sends only the token and the new
  password.
- [x] 16.3 Make the bell on a page without a link show a sign-in hint and focus the password field instead of
  opening a screen, and keep opening the update screen when a link was used; verify with component specs for
  both behaviours.
- [ ] 16.4 Update the TDD, `project.md` and the mockup; run all suites and `openspec validate --strict`,
  then walk it on the running app with the other open walk-throughs.

## Workflow follow-up

- Archive the change after the project's review requirements are satisfied (`openspec archive
  --change forgot-password-reset`), which folds `password-reset` into `openspec/specs/`.
- Verify the archived result (`openspec list --specs` shows the capability).
