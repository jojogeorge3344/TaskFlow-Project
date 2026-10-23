# Forgot Password Feature — Documentation

## Overview

TaskFlow provides an admin-assisted, time-limited forgot-password flow so users who forget their password can regain access without administrator intervention outside the system.

## User flows

### User-initiated reset (from login or profile screen)

1. User clicks **Forgot password?** on the login screen or the profile screen.
2. The system sends a one-time, time-limited reset-request email to an Administrator. The email contains a single-use reset link valid for 5 minutes.
3. The Administrator opens the link and is taken to the user details screen for the requesting user.
4. The Administrator generates a new password for the user.
5. When the Administrator saves the new password, the system automatically sends:
   - An email to the user with the new password.
   - An in-app notification to the user.

### Administrator-initiated reset (from login page)

1. Administrator clicks **Forgot password?** on the login page.
2. The system automatically generates a new password for the Administrator's own account.
3. The system sends an email and an in-app notification to the Administrator.
4. No administrator-email step is involved — the system acts directly.

## Security properties

- **Single-use links:** Each reset link can be used exactly once. A second attempt to use the same link returns `"link not valid"`.
- **5-minute expiry:** Reset links expire 5 minutes after creation. An expired link is rejected as `"expired"`.
- **Hash storage:** Reset tokens are stored as SHA-256 hashes, never plaintext.
- **Expiry before single-use:** An expired link reports `"expired"` rather than `"link not valid"`, so an administrator never sees a confusing error for an aged-out link.
- **No user enumeration:** Forgot-password requests always return the same generic response, whether or not the account exists.
- **Session invalidation:** When a password is reset, all existing sessions for the user are ended (security stamp changed, refresh tokens revoked).
- **Rate limiting:** Forgot-password requests are rate-limited per requesting account and per Administrator recipient.

## API endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `POST /api/v1/auth/forgot-password` | POST | Anonymous | Requests a password reset. Generic response either way. |
| `GET /api/v1/auth/reset-password/{token}` | GET | Authenticated | Validates a reset token and returns user details. |
| `POST /api/v1/auth/reset-password` | POST | EmployeesManage | Administrator sets a new password using a one-time token. |

## Data model

`PasswordResetTokens` table:

| Column | Type | Description |
|--------|------|-------------|
| Id | int | Primary key |
| UserId | int | FK to Users |
| TokenHash | text | SHA-256 hash of the reset token |
| ExpiresAt | datetime | 5 minutes after creation |
| CreatedAt | datetime | Token creation time |
| Used | boolean | Whether the token has been consumed |
| ConsumedAt | datetime? | When the token was consumed |
