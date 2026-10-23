# Tasks

## 1. TOTP core and secret protection

- [x] 1.1 Add Base32 encode/decode and an RFC 6238 TOTP generator/validator (HMAC-SHA1, 6 digits, 30 s, ±1 step, constant-time compare, returns the matched step); verify unit tests pass the RFC 6238 SHA-1 test vectors and cover drift, old codes and malformed input
- [x] 1.2 Add `MfaOptions` (issuer default `TaskFlow`, 5 failures, 5-minute lock, 10 backup codes) bound from `Mfa`, and an `IMfaSecretProtector` over Data Protection (purpose `TaskFlow.MfaSecrets`) that never returns or logs plaintext on failure; verify a round-trip test and that a tampered value returns null
- [x] 1.3 Add `User.MfaSecretProtected`, `MfaEnabledAt`, `MfaLastUsedStep`, `MfaFailedAttempts`, `MfaLockedUntil`, a `MfaBackupCode` entity and DbContext mapping, plus one EF migration; verify the migration applies to a fresh database and the model snapshot builds

## 2. Enrolment and backup codes (backend)

- [x] 2.1 Implement `IMfaService` setup (current password required, returns key and `otpauth://` URI, overwrites any unconfirmed secret) and enable (valid code turns MFA on and returns 10 backup codes once, stored as SHA-256 hashes); verify unit tests for wrong password, wrong code, abandoned setup and that enabling leaves the stored key encrypted
- [x] 2.2 Implement code verification shared by all entry points: step greater than `MfaLastUsedStep`, backup-code fallback, failure counter and 5-minute lockout that blocks even a correct code, reset on success; verify unit tests for replay, previous-step acceptance, lockout and counter reset using a fake clock
- [x] 2.3 Implement regenerate backup codes (password + valid code, invalidates the old set); there is intentionally no disable (see 10.4); verify unit tests for each success and failure case
- [x] 2.4 Add `users/me/mfa/setup`, `enable` and `backup-codes` endpoints, and `MfaEnabled` plus `BackupCodesRemaining` on `UserDto` (never the key); verify integration tests for the full enrolment lifecycle, unauthenticated refusal and that no response after setup contains the key

## 3. Two-step sign-in (backend)

- [x] 3.1 Change `LoginAsync` to return an `MfaRequired` result with a 5-minute challenge token (Data Protection, bound to the security stamp) when MFA is on, issuing no session; keep wrong-password and unknown-user responses identical; verify unit tests for MFA on, MFA off and wrong password
- [x] 3.2 Add `POST auth/mfa/verify` (challenge token + code or backup code) that issues the normal tokens and refresh cookie, honouring replay and lockout; verify integration tests for success, wrong code, expired or tampered challenge, a rotated security stamp and backup-code use
- [x] 3.3 Update `PermissionAuthorizationTests` for the new anonymous endpoint and existing login tests for the response shape; verify the suite passes

## 4. Administrator reset (backend)

- [x] 4.1 Add `POST users/{id}/mfa/reset` guarded by the employee-management permission that clears MFA state and backup codes, ends the user's sessions and publishes a notification; verify integration tests for success, a user without the permission, an unknown user and that old sessions are rejected afterwards

## 5. Authenticator code as a recovery factor (backend)

- [x] 5.1 Add `POST auth/password-recovery/authenticator/verify` (identifier + code) that applies the shared lockout, never accepts a backup code, returns the same 400 for unknown user, MFA off, wrong code and locked, and on success returns a single-use, 10-minute reset token stored hashed in `PasswordResetGrants`; add `POST auth/password-recovery/reset` that spends it, ends sessions and keeps the token usable after a rejected password; verify integration and unit tests for the full recovery ending in a login with the new password, response parity across failure causes, lockout shared with sign-in, token reuse, expiry and other tokens dying with a reset
- [x] 5.2 Add the new anonymous endpoint to `PermissionAuthorizationTests`; verify the suite passes

## 6. Frontend: sign-in and Profile

- [x] 6.1 Extend `AuthService` and models for the MFA challenge, verify, setup, enable and admin reset calls; verify service specs with `HttpTestingController`
- [x] 6.2 Add the second step to the login page (6-digit code input, "use a backup code" toggle, back link, API error messages) and route the session through the existing success path; verify login component specs for MFA on, wrong code, backup code and MFA off
- [x] 6.3 Add `qrcode` and the QR, key and backup-code steps (first built as a Profile card, now the setup screen in 10.2; the Profile card was removed in 10.4); verify component specs for each step and error
- [x] 6.4 Add an MFA status badge and a "Reset MFA" action with confirmation to the employee details for users holding the permission; verify component specs including the hidden state without the permission

## 7. Frontend: recovery factor and docs

- [x] 7.1 Build the authenticator-only account-recovery page (identifier + code, then new and confirm password) with generic error messages and a "start over" path when the token is refused; verify component specs for each step and error
- [x] 7.2 Document MFA setup, backup codes, lost-phone administrator reset, the `Mfa:Issuer` setting and the time-sync tip in the README; verify the documented setting name matches `MfaOptions`

## 8. Final integration checks

- [x] 8.1 Run the backend unit and integration suites and the frontend suite; verify all green
- [ ] 8.2 Manually run with a real authenticator app: enrol, sign out and in with a code, replay the same code, five wrong codes to lock, backup code sign-in, regenerate, admin reset, recover a password by authenticator code; verify each against the spec scenarios
- [x] 8.3 Run `openspec validate authenticator-mfa --strict`; verify it passes

## 9. Remove the text-message flow and re-scope the administrator reset

- [x] 9.1 Remove the SMS gateway, OTP service, the old mobile numbers (entity, DTOs, validators, seeder, endpoints) and their settings and tests (a plain contact number returns in 10.3), and add the `RemoveSmsOtp` migration; verify the backend suites pass and the migration applies to a copy of the development database
- [x] 9.2 Make `POST auth/forgot-password` authenticated and account-from-token, remove the public forgot-password page, and drop the Profile request (the card was removed in 10.5); verify the endpoint tests return 401 anonymously and act on the caller
- [x] 9.3 Remove the mobile number fields from Profile and the employee form and redesign Profile (header, Profile and Security sections, show/hide passwords, strength meter, responsive layout); verify the Profile specs and a visual check at desktop, tablet and phone widths
- [x] 9.4 Replace the README text-message section with the authenticator recovery description; verify no SMS settings remain documented

## 10. Mandatory MFA, setup screen, step-up and contact number

- [x] 10.1 Add `Mfa:Required` (default true), `mfaSetupRequired` on the sign-in and `auth/me` responses, the `mfa_setup_required` session claim and the gate middleware that refuses everything except `auth/*`, `users/me` and `users/me/mfa/*`; verify integration tests for the 403 and its code, the allowed setup calls, the gate lifting with the same token after setup, and an administrator reset putting it back
- [x] 10.2 Build the full-page setup screen (employee details and mobile number, authenticator app with QR code, backup codes with a saved-them checkbox), the route guards and the 403 interceptor that send users there; verify component, guard and interceptor specs and a screenshot walk-through at desktop and phone widths
- [x] 10.3 Add the optional contact mobile number (E.164, not unique, refuse only the same email and number, omitted keeps and empty clears) with the `AddUserMobileNumber` migration, and show it on the setup screen and Profile; verify unit, integration and component tests
- [x] 10.4 Remove the disable endpoint and the Profile Two-step verification card so nobody can turn MFA off; verify a test that no disable method or route exists
- [x] 10.5 Remove the Profile "Locked out of your password?" card and its request code; verify the Profile specs show no reset request
- [x] 10.6 Require a fresh code on password change (`MfaCode`, current password checked first) and gate **Show** on password boxes behind a non-spending `users/me/mfa/check` with a five-minute unlock; verify unit, integration and component tests including that a mistyped password does not spend the code
- [x] 10.7 Update the README for mandatory setup, the code on password change, the Show unlock, `Mfa:Required` and the contact number; verify the documented setting matches `MfaOptions`

- [x] 10.8 Let a user who has MFA on change their password with only a 6-digit authenticator code (`SkipCurrentPassword`, `RequireAuthenticatorCodeAsync`, `SetOwnPasswordAsync`), with an "I don't remember my current password" switch on Profile; verify unit and integration tests (valid code, backup code refused, wrong or missing code, user without MFA, weak new password), component specs and the README line

## Workflow follow-up

- Archive this change after review.
