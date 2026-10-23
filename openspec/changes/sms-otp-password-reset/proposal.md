# Proposal

## Why

The current forgot-password flow needs an administrator to read a mail and generate a password, and it is reachable anonymously from the login page. A locked-out user cannot recover on their own, and an anonymous caller can flood administrator inboxes. A self-service recovery path that proves ownership of a registered mobile number (SMS OTP) lets users reset their own password without sending any password through email or SMS.

## What Changes

- Add a nullable, E.164 **mobile number** to users (unique among non-deleted users). Administrators set it on the employee form; users can change their own on Profile only after re-entering their current password. The assistant's profile-edit actions never touch it.
- Replace what the login page's **Forgot password?** link opens with a **3-step SMS OTP flow**: enter registered mobile number, enter the 6-digit OTP, set and confirm a new password. New endpoints: `auth/password-otp/request`, `auth/password-otp/verify`, `auth/password-otp/reset`.
- OTP rules: 6 digits, stored only as an HMAC, 5-minute expiry, 5 verification attempts then invalidated, 60-second resend cooldown, per-number hourly cap and a configurable global daily cap, all counted in the database. The request step returns the same response whether or not the number is registered.
- A successful verification issues a single-use reset token (about 10 minutes). The reset step hashes the new password, rotates the user's security stamp (ending existing sessions), and burns the token. No password is ever sent by SMS or email.
- Add an **SMS gateway abstraction** with a `Console` sender (default; logs the OTP only in Development) and a `Twilio` adapter. Provider selection and credentials come from configuration and environment variables or user-secrets, never from source or committed settings.
- **Keep** the existing admin-mediated reset flow (`forgot-password-reset` change), its Mail "password request" flag and the internal Mail feature **unchanged in behaviour**, but **re-scope it to the Profile screen only**:
  - **BREAKING**: `POST auth/forgot-password` becomes authenticated and uses the signed-in caller's identity; the `identifier` body field is dropped.
  - **BREAKING**: the public `/forgot-password` route and component are removed; the login link points to the new OTP flow.
  - The old flow's login-page administrator auto-generate branch can no longer be triggered from login and is superseded by the OTP flow.
- Dev seed gives the administrator account a mobile number so the flow is demonstrable.
- Known limitation: users with no mobile number cannot self-reset and need an administrator to set one.

## Capabilities

### New Capabilities

- `sms-otp-password-reset`: mobile number on user accounts, the 3-step OTP forgot-password flow from the login page, OTP and reset-token security rules (expiry, attempts, cooldown, caps, anti-enumeration), the configurable SMS gateway (Console and Twilio), and the rule that the older admin-mediated reset is available from Profile only.

### Modified Capabilities

_None._ `openspec/specs/` is empty, so there is no existing main spec to delta against. The `password-reset` requirements live in the still-open `forgot-password-reset` change, whose spec statements about the login page and anonymous access are superseded here and are updated as an explicit task.

## Impact

- **Backend**: `User` entity (+ `MobileNumber`), new OTP table, `AuthController` (new endpoints; `forgot-password` made `[Authorize]`), new OTP service and gateway abstraction with Console and Twilio implementations, options classes, `UserService`/profile and employee DTOs and validators, assistant profile actions (exclude mobile), `DatabaseSeeder`, one new EF migration (the two unapplied reset migrations stay as they are).
- **Frontend**: new OTP flow screens/route behind the login link, removal of the `/forgot-password` route and component, `AuthService` methods, employee form and Profile mobile-number fields, Profile reset action updated for the authenticated endpoint, user model.
- **Tests**: update `ForgotPasswordEndpointTests`, `ForgotPasswordGenericResponseTests`, `ForgotPasswordThrottlingTests`, `PermissionAuthorizationTests` and frontend route/login/forgot-password specs; add OTP service, gateway and endpoint tests.
- **Configuration**: new `Sms` options section (provider, OTP HMAC key, caps); Twilio credentials via environment variables or user-secrets only. SMS delivery through Twilio incurs per-message cost, bounded by the caps.
- **Prerequisite**: builds on the in-flight `forgot-password-reset` work in the working tree; does not archive or delete it.
