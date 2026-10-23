# Design

## Context

See proposal.md for motivation. Current state:

- `AuthService.LoginAsync` checks the password then immediately issues an access token and refresh token; `AuthController.Login` sets the refresh cookie. Sessions end by rotating `User.SecurityStamp` and revoking refresh tokens (already used by password change and reset).
- ASP.NET Data Protection with a persisted key ring is already configured and used for AI provider keys (`AiKeyProtector`).
- There was no recovery path from the login page: `POST auth/forgot-password` mailed an administrator and, being anonymous, was reachable by anyone. An earlier text-message design (the `sms-otp-password-reset` change) was abandoned, and its code and OTP tables were removed.
- `SessionValidation` already loads the user on every authenticated request (to check the security stamp and permissions), which makes a per-request "has MFA been set up?" check free.
- There are no existing TOTP or QR dependencies. The frontend is Angular; the database is SQLite through EF Core.

## Goals / Non-Goals

**Goals:**
- Required authenticator-app MFA for everyone, at no per-use cost and with no new paid service.
- Safe defaults: replay protection, lockout, encrypted secrets, hashed backup codes.
- Reuse the existing session-ending and reset-token machinery.

**Non-Goals:**
- Per-role exceptions to the MFA requirement.
- WebAuthn/passkeys, SMS or email as an MFA factor, remembered devices.
- Changing how the refresh cookie or access token work after sign-in.

## Decisions

1. **TOTP implemented in-house** (RFC 6238: HMAC-SHA1, 6 digits, 30-second step, 20-byte secret, Base32 for the key) using `HMACSHA1` and a small Base32 helper, tested against the RFC test vectors. Rejected: a NuGet TOTP package, since the algorithm is short and avoids a dependency. Constant-time comparison for codes.
2. **Window of ±1 step** to absorb clock drift, with replay protection: `User.MfaLastUsedStep` stores the highest accepted time step and a code is accepted only for a step greater than it. A code for the previous step is therefore accepted once, and never twice.
3. **Secret storage**: the key is protected with Data Protection (new purpose string `TaskFlow.MfaSecrets`) and stored in `User.MfaSecretProtected`. `User.MfaEnabledAt` is null while setup is pending, so an unconfirmed secret grants nothing and is overwritten by the next setup. The key is returned only by the setup call.
4. **Backup codes**: 10 random codes of 10 Base32 characters (about 50 bits) stored in a `MfaBackupCode` table as SHA-256 hashes with `UsedAt`. With that entropy a fast hash is adequate. Shown once; regeneration deletes the old rows.
5. **Two-step sign-in**: `LoginAsync` verifies the password; if `MfaEnabledAt` is set it returns an `MfaRequired` result carrying a **challenge token** instead of tokens. The token is a Data Protection time-limited protector payload (purpose `TaskFlow.MfaChallenge`, 5-minute life) holding the user id and security stamp, so it cannot be forged, expires quickly and dies if the stamp rotates. `POST auth/mfa/verify` takes the challenge token and a code or backup code, then issues the same tokens and refresh cookie as a normal login. `POST auth/login` returns `{ mfaRequired: true, challengeToken }` with 200 for that case; the frontend branches on the flag.
    The MFA-required answer is produced only after a correct password, so unknown users and wrong passwords still get the same 401.
6. **Lockout**: `User.MfaFailedAttempts` and `User.MfaLockedUntil`; 5 consecutive failures lock verification for 5 minutes (constants in `MfaOptions`); success resets the count. The counters are shared by sign-in, password change, the show-password check, backup-code regeneration and recovery so an attacker cannot multiply attempts across entry points.
7. **Profile endpoints** under `users/me/mfa`: `POST setup` (current password required, returns key and `otpauth://` URI), `POST enable` (code, returns backup codes), `POST check` (code; see decision 15), `POST backup-codes` (password + code, regenerates). There is deliberately **no disable endpoint** (decision 14). `UserDto` gains `MfaEnabled` and `BackupCodesRemaining`, never the key.
8. **Administrator reset**: `POST users/{id}/mfa/reset` guarded by the existing employee-management permission. It clears the secret, enabled flag, counters and backup codes, ends the user's sessions (stamp rotation and refresh revocation) and publishes a notification.
9. **Recovery factor**: `POST auth/password-recovery/authenticator/verify` takes an identifier (username or email) and a code. It applies the same lockout, never accepts a backup code, and every failure (unknown user, MFA off, wrong code, locked) returns the same 400. On success it writes a `PasswordResetGrant` (token stored as a SHA-256 hash, 10-minute life) and returns the token; `POST auth/password-recovery/reset` consumes it, rotates the security stamp, revokes refresh tokens and spends every other outstanding grant for that account. A rejected password does not spend the token. Rejected: reusing an OTP table, since there is no code to store.
10. **QR code** rendered client-side with the free MIT `qrcode` package so the key never leaves the browser to a third-party image service; the key is also shown as text.
11. **Issuer name** shown in the app comes from `Mfa:Issuer` (default `TaskFlow`). The account label is the user's email.
12. **Migrations**: `AddAuthenticatorMfa` adds the `User` columns and the `MfaBackupCodes` table; `RemoveSmsOtp` drops the old `User.MobileNumber` and the OTP table and adds `PasswordResetGrants`; `AddUserMobileNumber` adds a plain optional `MobileNumber` column with no index (decision 16).
13. **Administrator-approved reset needs a session, and has no screen**: `POST auth/forgot-password` is `[Authorize]` and reads the account from the caller's token; the `identifier` field and the public `/forgot-password` page are removed, and the Profile card that called it was removed too, since everyone now has MFA and recovers on their own. Its behaviour, the Mail flag and the administrator landing page are unchanged. Rejected: leaving it anonymous, which keeps an unauthenticated way to mail administrators.
14. **Mandatory MFA, enforced on the server**: `MfaOptions.Required` (default true). `SessionValidation` adds a `mfa_setup_required` claim when the user has no `MfaEnabledAt`, and `MfaSetupGateMiddleware` (after authentication) answers 403 `{ code: "mfa_setup_required" }` to every `/api` request except `auth/*`, `users/me` and `users/me/mfa/*`. Sign-in and `auth/me` return `mfaSetupRequired`, the client guards the shell route and sends such users to `/security-setup`, and an interceptor turns the 403 into that redirect for a stale session. The gate lifts the moment MFA is confirmed, with the same token. There is **no endpoint that clears `MfaEnabledAt` except the administrator reset**, so MFA cannot be switched off. Rejected: a client-only guard, which can be bypassed by calling the API. `Mfa__Required=false` is the break-glass off switch.
15. **Step-up for sensitive actions**: `POST users/me/mfa/check` verifies a code **without spending it** (the replay rule is skipped, backup codes are refused, wrong codes still count toward the lockout) and only unlocks showing typed passwords for about five minutes on the client. The password change itself is the real protection: `ChangePasswordRequest.MfaCode` is verified with the normal, spending check (authenticator or backup code) after the current password has been confirmed, so a typo does not burn the code, and because the unlock does not spend it the same code still works for the change. Rejected: a spending check for the unlock, which would force a wait of up to 30 seconds before the change.
16. **Contact mobile number**: `User.MobileNumber` (E.164, max 20, no index) is saved with the profile (`PUT users/me`, omitted keeps it, empty clears it) and entered on the setup screen. It is not unique, because email is the only unique identity. The only refusal is a second record holding the same email and number; the existing unique email index makes that rare, and the message is kept specific. Nothing is sent to it.
17. **Forgot-current-password path**: `ChangePasswordRequest.SkipCurrentPassword` makes `POST users/me/password` skip the current-password check and instead call `IMfaService.RequireAuthenticatorCodeAsync`, which needs MFA on, spends the code, refuses backup codes and counts failures toward the shared lockout. The password is then set by a separate `IUserService.SetOwnPasswordAsync`, so the flag cannot bypass anything inside the service, which has no way to skip the check on its own. Rejected: also accepting backup codes here, since a lost backup code should not be enough by itself; and trusting the flag inside `ChangeOwnPasswordAsync`.

## Risks / Trade-offs

- **Lost phone and lost backup codes lock the user out** → administrator reset (decision 8); documented in the README.
- **Clock drift beyond one step** → window of ±1; users are told to enable automatic time on their phone.
- **A stolen session could change the password** → changing it needs the current password and a fresh code, and there is no way to disable MFA at all.
- **Every user, including administrators, is held at the setup screen on their next sign-in** → announce it; `Mfa__Required=false` is the break-glass switch if every administrator is locked out, and should be turned back on afterwards.
- **A stolen session plus the phone can change a password without knowing it** → accepted for the "I do not remember it" path; a stolen session alone, or a stolen backup code, is not enough, and wrong codes lock after five tries.
- **A viewed password reveals only what was typed** → the application never stores or returns real passwords (only a hash), so the unlock is a precaution against shoulder-surfing, not a secret store.
- **Login contract change** (200 with `mfaRequired`) can confuse old clients → only users who turned MFA on are affected, and the Angular client is updated in the same change.
- **Challenge token theft** → five-minute life, bound to the security stamp, and useless without a valid code; verification attempts are rate-limited.
- **SQLite concurrency on counters** → acceptable at this scale; the lockout is advisory, not transactional.
- **Users without MFA cannot recover from the login page** → only possible while `Mfa:Required` is off or before first setup; an administrator sets a new password from the employee form.
- **The reset endpoint is anonymous** → it needs a valid single-use token that only a correct authenticator code can obtain, and tokens expire in 10 minutes.

## Migration Plan

Apply the migrations in order: `AddAuthenticatorMfa` (every existing user then has no MFA and is sent to the setup screen at their next sign-in), `RemoveSmsOtp` (drops the abandoned mobile numbers and OTP table, creates `PasswordResetGrants`) and `AddUserMobileNumber`. The drop in `RemoveSmsOtp` is permanent for those columns, so copy the database first. Rollback is `ef migrations remove` before release.

## Open Questions

- Whether to give users a screen to regenerate backup codes themselves (the endpoint exists; today an administrator reset is the route if they are lost); deferrable and does not change the specs.
