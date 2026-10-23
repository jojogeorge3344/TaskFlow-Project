# Tasks

## 1. Mobile number on users

- [x] 1.1 Add nullable `User.MobileNumber` with E.164 normalization and a unique filtered index on non-deleted users; verify with a unit test for normalization and a persistence test for the duplicate rule
- [x] 1.2 Add `MobileNumber` to employee create/update DTOs, validators and `UserDto`; verify validator tests cover invalid format, duplicate and empty
- [x] 1.3 Add a Profile endpoint to change one's own mobile number requiring the current password, and keep the number out of the assistant's `UpdateProfilePayload` path; verify integration tests for correct and wrong password and a test that the assistant cannot change it
- [x] 1.4 Add the mobile number field to the employee form and the Profile screen (with current-password input) and the frontend user model; verify component specs
- [x] 1.5 Seed an admin mobile number from `Seed` configuration only when provided; verify a seeder test with and without the setting

## 2. SMS gateway

- [x] 2.1 Add `ISmsSender`, `SmsOptions` (provider, OTP HMAC key, caps) and register the provider from `Sms:Provider` in `Program.cs`; verify a DI test resolves the right sender per setting
- [x] 2.2 Implement `ConsoleSmsSender` that logs the OTP only in Development and reports misconfiguration otherwise; verify unit tests for both environments
- [x] 2.3 Implement `TwilioSmsSender` via typed `HttpClient` reading `AccountSid`, `AuthToken`, `From` from configuration; verify unit tests with a fake handler for success, failure, missing credentials, and that no secret or OTP appears in logs
- [x] 2.4 Add non-secret defaults to `appsettings.json` and document the environment variables / user-secrets keys in the project docs; verify the documented keys match the options class

## 3. OTP and reset-token backend

- [x] 3.1 Add the `PasswordResetOtp` entity, DbContext mapping and a single new EF migration for `MobileNumber` plus the OTP table (leaving the two existing reset migrations untouched); verify the migration applies to a fresh database
- [x] 3.2 Implement OTP issue with HMAC storage, 5-minute expiry, superseding earlier codes, 60-second cooldown, per-number hourly cap and global daily cap; verify unit tests per rule using a fake clock
- [x] 3.3 Implement OTP verify with constant-time compare, 5-attempt invalidation and single-use reset token (about 10 minutes, stored hashed); verify unit tests for expiry, attempts, reuse and supersession
- [x] 3.4 Implement reset: validate token, apply password rules and confirmation, hash, rotate `SecurityStamp`, revoke refresh tokens and consume the token; verify tests that a weak password keeps the token usable and that old tokens stop working
- [x] 3.5 Add `auth/password-otp/request`, `/verify`, `/reset` returning the generic response for unknown, throttled and gateway-failed requests; verify integration tests for generic parity, the full happy path and a login with the new password
- [x] 3.6 Update `PermissionAuthorizationTests` for the new anonymous endpoints; verify the suite passes

## 4. Re-scope the admin-mediated flow to Profile

- [x] 4.1 Make `POST auth/forgot-password` `[Authorize]`, take the user from claims and drop `identifier` from `ForgotPasswordRequest`; verify an anonymous call returns 401 and a signed-in call mails the administrator as before
- [x] 4.2 Update `ForgotPasswordEndpointTests`, `ForgotPasswordGenericResponseTests` and `ForgotPasswordThrottlingTests` to the authenticated contract; verify all pass
- [x] 4.3 Update `AuthService.requestPasswordReset` and Profile's request action to send no body; verify the Profile component spec
- [x] 4.4 Remove the public `/forgot-password` route and component and their specs; verify `app.routes.spec.ts` no longer lists them
- [x] 4.5 Update the login-page statements in `openspec/changes/forgot-password-reset/specs/password-reset/spec.md` (and its proposal/tasks where they say login page or anonymous) to match; verify `openspec validate forgot-password-reset` passes
- [x] 4.6 Confirm the Mail "password request" flag files are unmodified by this change; verify with `git diff` on `MailMessage.cs`, `MailDtos.cs`, `MailService.cs` showing no new edits from this work

## 5. Login-page OTP flow (frontend)

- [x] 5.1 Add `AuthService` methods for request, verify and reset against the new endpoints and the models they use; verify service specs with `HttpTestingController`
- [x] 5.2 Build the three-step flow component (mobile number, 6-digit code with resend countdown, new and confirm password) with generic messages and validation; verify component specs for each step and error state
- [x] 5.3 Add the route and re-point the login "Forgot password?" link; verify the login and routes specs
- [x] 5.4 Add the post-reset redirect to login with a success notice; verify the spec and a manual end-to-end run with the Console sender

## 6. Final integration checks

- [x] 6.1 Run the backend unit and integration suites and the frontend test suite; verify all green
- [x] 6.2 Manually run: set a number, request OTP (Console log), wrong codes until lockout, resend cooldown, successful reset, old password rejected; verify each against the spec scenarios
- [x] 6.3 Run `openspec validate sms-otp-password-reset --strict`; verify it passes

## Workflow follow-up

- Archive this change after review, then decide separately whether to archive `forgot-password-reset`.
