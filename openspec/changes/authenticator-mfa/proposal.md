# Proposal

## Why

Sign-in is a password only, and a user who forgets their password cannot recover it from the login page: the administrator-approved reset needs a signed-in session. A time-based one-time code from an authenticator app (Google Authenticator, Microsoft Authenticator, Authy and similar) is free, works offline and needs no SMS gateway. It adds a second sign-in factor and gives users a no-cost way to recover their own password.

## What Changes

- Make **MFA with an authenticator app required** for every user. Anyone who has not set it up is taken to a full-page **setup screen** straight after sign-in, and the API refuses every other request until it is done: confirm their details and **mobile number**, confirm their password, scan a QR code (or type the key) and prove it works with one code.
- When MFA is on, **sign-in takes two steps**: password, then the current 6-digit code. The session is issued only after the second step. A user may instead enter one of their one-time **backup codes**.
- On enabling MFA the user is shown **10 single-use backup codes once**. They can be regenerated through the API, which invalidates the old set; no screen offers it.
- **Nobody can turn MFA off.** There is no switch for users or administrators. An administrator reset is the only removal, and it sends the person back to the setup screen.
- **Changing a password needs a fresh code** (an authenticator code or an unused backup code) as well as the current password, and **showing what was typed in a password box** needs a code check first (unlocked for about five minutes; the check does not spend the code).
- A user who does not remember their current password can **change it with just a 6-digit authenticator code**: the code replaces the current password (backup codes do not work on this path, the account must have MFA on, and the new password must still meet the rules). The trade-off is accepted: a stolen session plus the user's phone is now enough, whereas a stolen session alone, or a stolen backup code, is not.
- Users can keep an optional **contact mobile number** (international format). Email is the only unique identity, so a number may be shared; saving is refused only when another record already holds the same email and number together. Nothing is ever sent to it.
- Codes are accepted within a one-step clock tolerance, a code cannot be used twice, and repeated wrong codes **lock MFA verification temporarily**.
- An **administrator can reset a user's MFA** (for a lost phone and lost backup codes); the user's sessions end and they are notified.
- **Password recovery from the login page** uses the authenticator app only: the user enters their username or email and the current 6-digit code, then sets and confirms a new password with a single-use reset token (valid about 10 minutes). No text message is involved.
- The existing **administrator-approved reset endpoint now requires a signed-in user** and acts on the caller's own account, and the old public forgot-password page is removed. No screen offers it any more, since everyone has MFA and can recover on their own.
- Shared secrets are stored **encrypted at rest** and never returned after setup; no secret, code or backup code is ever logged.
- **BREAKING**: `POST auth/login` for a user with MFA on no longer returns a session; it returns an "MFA required" challenge, and a new `POST auth/mfa/verify` completes sign-in. Every user must set up MFA at their next sign-in; setting `Mfa:Required` to false is a break-glass switch.

Assumptions: MFA is required for all roles, with no per-role exceptions; the authenticator code is the only recovery factor on the login page (no SMS); no third-party service or paid dependency is used.

## Capabilities

### New Capabilities

- `authenticator-mfa`: requiring, enrolling, using, backing up and administratively resetting authenticator-app MFA (with no way to turn it off); the two-step sign-in; code validity, replay and lockout rules; a fresh code for password changes (or, when the current password is forgotten, an authenticator code alone) and for showing typed passwords; the optional contact mobile number; and using the authenticator code to recover a password.

### Modified Capabilities

_None._ `openspec/specs/` is empty, so nothing exists to delta against. The sign-in contract (`user-auth`) is extended without changing its own requirements; the recovery and Profile-only reset behaviour is specified here.

## Impact

- **Backend**: `User` (MFA secret, enabled-at, replay and lockout fields), a backup-code table, `AuthService.LoginAsync` and a new MFA verify step, a TOTP implementation, secret protection through the existing Data Protection key ring, new endpoints (`auth/mfa/verify`, `users/me/mfa/*`, an administrator reset endpoint, `users/me/mfa/check`, `auth/password-recovery/authenticator/verify` and `auth/password-recovery/reset`), a gate middleware plus a session claim that refuses everything else until setup is done, a code on the password-change request (and a `SkipCurrentPassword` form of it that takes an authenticator code instead of the current password), a `PasswordResetGrants` table for the single-use reset token, `auth/forgot-password` made authenticated, new EF migrations (`AddAuthenticatorMfa`; `RemoveSmsOtp`, which drops the abandoned text-message tables and old mobile numbers; and `AddUserMobileNumber`, a plain optional contact column), notification to the user on admin reset.
- **Frontend**: login page second step, a full-page setup screen (details and mobile number, authenticator app, backup codes) with route guards and a 403 handler, the Profile password card with a code-gated Show and a code on change, a restyled Profile page, authenticator-only recovery page, removal of the public forgot-password page, Employee list/details action for the administrator reset, `AuthService`/`ProfileService`/models. A small QR-code library (free, MIT) is added for the setup screen.
- **Tests**: TOTP against the RFC 6238 vectors, login/verify/lockout/replay, backup codes, recovery factor, admin reset, and component specs.
- **Configuration**: none required beyond the existing Data Protection key ring; `Mfa:Required` (default true, break-glass off switch) and optional `Mfa:Issuer` for the name shown in the authenticator app.
- **Prerequisite**: none. The abandoned text-message flow (`sms-otp-password-reset`) is not required and none of its code remains.
