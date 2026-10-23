# Design

## Context

See `proposal.md` — Why. Current state and constraints that shape the approach:

- `user-auth` (login/session) and `user-profile` (self-service password change requiring the current password) are already specified and neither covers password reset, so this change adds a distinct admin-mediated reset capability without altering those capabilities' requirements.
- `internal-mail` provides the email transport and `notification-center` provides in-app notifications; this change reuses both rather than introducing new channels.
- An administrator user-details screen exists (from `app-shell-and-admin-ui` / employee management), which is where the administrator generates the new password after opening a reset link.
- The forgot-password action goes on the profile screen (`user-profile`) and requires a signed-in session. Signed-out recovery from the login screen is handled by the SMS OTP flow in the `sms-otp-password-reset` change.

## Goals / Non-Goals

**Goals:**

- A Forgot password action on the profile screen, available to every signed-in user.
- A one-time, time-limited reset link emailed to the administrator when a user requests a reset.
- The administrator opens the link, reaches the user details screen, and generates a new password for the user.
- On saving the new password, an email and an in-app notification are sent automatically to the user.
- An administrator-triggered reset from the profile screen auto-generates a password and notifies the administrator.
- Reset links are single-use ("link not valid" on reuse) and expire after 5 minutes.

**Non-Goals:**

- Self-service password reset without administrator involvement (reset remains admin-mediated by design).
- Self-service password change with the current password — that stays in `user-profile`.
- Account lockout, throttling beyond link invalidation, MFA, external identity providers, or user registration.
- Changing `user-auth` or `user-profile` requirements.

## Decisions

**D1 — Password-reset tokens stored as hashes with a 5-minute TTL and a used flag.**
The reset token is generated crypto-random, stored as a hash (never plaintext, consistent with `user-auth`'s "never store plaintext" rule), with `expiresAt = createdAt + 5 minutes` and a `used` boolean. Single-use is enforced by rejecting any token already marked used. The 5-minute window limits exposure while leaving the administrator enough time to act.
_Alternative considered:_ storing the plaintext token — rejected, since tokens function like passwords and must not be recoverable from the data store.
_Alternative considered:_ a longer expiry (e.g., 24 hours) — rejected; the request explicitly requires 5 minutes.

**D2 — Two entry points with different outcomes.**
- User-initiated (profile screen, signed in): sends a reset-request email to the administrator containing the single-use link; the administrator then generates the password.
- Administrator-initiated (profile screen, signed in): skips the admin-email step, auto-generates the password directly, and sends a notification to the user.
_Alternative considered:_ always auto-generating the password — rejected, because the request distinguishes the user-initiated flow (admin generates after opening the link) from the admin-initiated flow (system auto-generates).

**D3 — Reuse the `internal-mail` and `notification-center` transports.**
The reset-request email goes to the administrator via `internal-mail`; the new-password email and the in-app notification go to the user via `internal-mail` and `notification-center` respectively. No new messaging infrastructure is introduced.
_Alternative considered:_ adding a dedicated reset-mail service — rejected as duplicate infrastructure.

**D4 — Generic, non-enumerating responses.**
Whether or not a user exists, a failed reset attempt returns the same generic outcome, and the distinction (if any) is logged server-side only. This mirrors `user-auth`'s single generic failure response and prevents user enumeration; since the request is now authenticated and acts on the caller, there is no identifier to probe.

**D5 — Link validation order: expiry first, then single-use.**
An expired link is rejected as "expired" before the used check, so an administrator never gets a confusing "link not valid" for a link that simply aged out.

## Risks / Trade-offs

- [Reset-request emails could be abused to spam the administrator] → Rate-limit reset requests per user and per recipient, and require an authenticated session where applicable.
- [A generated password is sensitive] → Deliver it by email and in-app notification only; never return the generated plaintext password in an API response to the administrator after creation.
- [Administrator may not act within 5 minutes] → The link expires and the user must request a new reset; this is acceptable given the short, explicitly required window.
- [Link reuse after the administrator has acted] → The used flag makes a second attempt return "link not valid"; the user must request a fresh reset.
