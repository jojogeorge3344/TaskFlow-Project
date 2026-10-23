# Proposal

## Why

TaskFlow has no way to recover a forgotten password. The profile screen lacks a "Forgot password" action, and the existing `user-auth` capability explicitly scopes out password reset, so a user who forgets their password is permanently locked out with no recovery path. This increases support burden and account abandonment. Adding an admin-assisted, time-limited forgot-password flow gives every user a supported way to regain access.

## What Changes

- Add a **Forgot password** action to the profile screen for signed-in users. (The login page now uses the SMS OTP flow from the `sms-otp-password-reset` change; this admin-mediated flow is profile-only and requires a signed-in session.)
- When a signed-in user clicks the link on the profile screen, send a **one-time, time-limited reset request email to the administrator** (requesting a password reset for that user; the account comes from the session, not from the request).
- The administrator opens the link, is taken to the **user details screen**, and **generates a new password for the user**.
- When the administrator updates/saves the new password, automatically send an **email and an in-app notification to the user**.
- When a signed-in administrator triggers forgot password from the **profile screen**, the system **automatically generates a password** and sends a notification to them.
- Reset links are **one-time-use only**: a second attempt to use the same link shows "link not valid".
- Reset links are valid for **5 minutes only**, then expire.

## Capabilities

### New Capabilities

- `password-reset`: forgot-password request from the profile screen (signed-in users only), admin-mediated one-time time-limited password-reset links, email plus in-app notification to the user when the password is updated, auto-generated password when an administrator initiates the reset from the profile screen, and enforcement of single-use and 5-minute expiry with a "link not valid" error on reuse.

### Modified Capabilities

_None_ — `user-auth` (login/session) and `user-profile` (self-service password change requiring the current password) keep their existing requirements; password reset is a distinct, admin-mediated behavior that neither owns. No existing capability's requirements change.

## Impact

- **Backend / API**: new forgot-password reset-request endpoint; password-reset token generation/validation (single-use, 5-minute TTL, "link not valid" on reuse); administrator password-update endpoint that triggers email + notification to the user; auto-generate-password path for admin-initiated resets from the profile screen. The reset-request endpoint requires a signed-in user.
- **Email service**: reset-request email to the administrator; new-password email to the user.
- **Notification service**: in-app notification to the user when their password is updated.
- **Frontend**: forgot-password action on the profile screen; administrator user-details screen password-generation action; reset-link error and expiry handling.
- **Data**: password-reset tokens store (user, token, expiresAt, used flag).
- **Prerequisite:** `user-auth` (authentication foundation) must be applied first; `internal-mail` / `notification-center` provide the email and notification transport.
