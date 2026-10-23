# Design

## Context

See proposal.md for motivation. Current state in the working tree (uncommitted, from `forgot-password-reset`):

- `AuthController.ForgotPassword` is anonymous, takes an `identifier`, mails a 5-minute token link to an Administrator (or auto-generates a password for an Administrator account). Token and rate-limit services exist (`PasswordResetTokenService`, in-memory singleton `PasswordResetRateLimiter`). The Mail entity carries a "password request" flag.
- `User` has no phone field. Sessions are invalidated through `User.SecurityStamp`.
- Configuration follows `Configure<TOptions>(GetSection(...))` in `Program.cs`; outbound HTTP uses `AddHttpClient`.
- Two reset migrations are not yet applied to a shared database.

## Goals / Non-Goals

**Goals:**
- Self-service password reset through SMS OTP with the limits in the spec.
- A provider-agnostic SMS gateway; Console for dev/demo, Twilio for real delivery.
- Leave the admin-mediated flow and the Mail feature behaviourally intact, reachable from Profile only.

**Non-Goals:**
- Email OTP, authenticator apps, or MFA at login.
- A UI for managing SMS provider settings.
- Fallback recovery for users with no number beyond an administrator setting one.
- Changing or archiving `forgot-password-reset`.

## Decisions

1. **New OTP table `PasswordResetOtp`** (user id, number hash, OTP HMAC, expires-at, attempt count, consumed-at, created-at, plus a reset-token hash and expiry set on verify). Throttling is computed from this table (cooldown from the latest row, hourly cap from rows in the last hour, daily cap from rows today). Rejected: reusing the in-memory `PasswordResetRateLimiter`, which resets on restart and does not count across instances. The same table stores the post-verify reset token so one record carries the whole attempt.
2. **OTP storage as HMAC-SHA256** keyed with `Sms:OtpHmacKey` over (OTP + record id), compared in constant time. A plain hash of a 6-digit code is brute-forceable from a database leak, so a server-side key is required. The key comes from configuration/secrets and startup fails outside Development if it is missing.
3. **Anti-enumeration**: lookup, throttle and SMS send all run behind one generic response. The SMS send is dispatched so response time does not differ between known and unknown numbers (unknown numbers take the same code path up to the send, which is skipped).
4. **Reset token**: 32 random bytes returned to the client once, stored only as a hash; valid ~10 minutes; consumed in the same transaction that changes the password. Weak/mismatched password errors do not consume it.
5. **Password update** reuses the existing hasher and password validator, then sets a new `SecurityStamp` and revokes refresh tokens, mirroring how change-password already ends sessions.
6. **`ISmsSender` abstraction** in Application with `ConsoleSmsSender` and `TwilioSmsSender` in Infrastructure, chosen at startup from `Sms:Provider`. Twilio is called through its REST API with a typed `HttpClient` (no SDK dependency). `AccountSid`, `AuthToken`, `From` come from environment variables (`Sms__Twilio__AuthToken`) or user-secrets; `appsettings.json` holds only non-secret defaults (provider name, caps). Console logs the OTP only when the environment is Development.
7. **Mobile number**: nullable `User.MobileNumber` normalized to E.164, unique filtered index on non-deleted users. Admin sets it through the employee create/update DTOs and form. Self-edit goes through a dedicated Profile endpoint that requires the current password (separate from the name/email update and not exposed to the assistant's `UpdateProfilePayload`).
8. **Endpoints** `auth/password-otp/request|verify|reset` are anonymous, rate-limited as above, and named to avoid any clash with `auth/forgot-password` and `auth/reset-password`.
9. **Re-scoping the old flow**: `POST auth/forgot-password` becomes `[Authorize]` and reads the user id from the caller's claims; `ForgotPasswordRequest` loses `identifier`. Profile's `requestPasswordReset()` calls it without a body. The public `/forgot-password` route and component are deleted and the login link targets the new flow route. Alternative considered: leave the endpoint anonymous and only unlink it; rejected because it keeps an anonymous way to mail administrators.
10. **Old admin auto-generate branch** (an Administrator asking for a reset) is kept as is, but it is now reached only from the Profile screen by a signed-in Administrator. It is no longer a recovery path for a locked-out Administrator: the SMS OTP flow is, which makes the seeded Administrator number important.
11. **Migrations**: the two unapplied reset migrations are untouched; one new migration adds `MobileNumber` and the OTP table. If a migration was already applied somewhere, nothing about it is rewritten.
12. **Seeding**: `DatabaseSeeder` sets a dev mobile number for the seeded administrator only when `Seed` configuration provides one, so no real number lands in source.

## Risks / Trade-offs

- **Admin cannot recover if their number is missing or wrong** → seed number from config; document that an administrator can fix it via the employee form or the database.
- **SIM swap / number takeover lets an attacker reset the password** → inherent to SMS OTP; mitigated by attempt limits, short expiry, session invalidation and requiring the current password to change a number.
- **SMS cost abuse** → per-number hourly cap, 60s cooldown and a configurable global daily cap; throttled requests send nothing.
- **Timing differences leak registered numbers** → unknown and throttled paths share the code path; verified by a test that compares behaviour, not exact timing.
- **Existing tests assume anonymous forgot-password** → tasks update them explicitly.
- **Twilio trial accounts only reach verified numbers** → Console is the default so demos work without a paid account.
- **Old flow's edits stay uncommitted and mixed with this change** → tasks do not touch the Mail edits, so they remain a separate commit decision for the user.
