# Design

## Context

See `proposal.md` for motivation. The relevant current state:

- **Settings store**: `ApplicationSetting` is a key/value table (`Key`, `Value`, `Category`, `UpdatedByUserId`, `UpdatedAt`). `SettingKeys` defines keys/categories/defaults; `SettingsInitializer` seeds missing rows on startup and never overwrites existing ones. `ReminderSettingsService` maps the `Reminder.*` + `Notification.MailEnabled` + `Application.Timezone` rows to the screen-13 DTO. `AppClock.GetTimeZoneAsync` reads `Application.Timezone` from the database on every call, falling back to configuration then UTC.
- **Reminder settings API**: `SettingsController` is `[Route("api/v1/settings")]`, class-level `ManagerOrAdmin`; `GET`/`PUT /reminders` expose the timezone as read-only (deliberately absent from the update request).
- **Users**: `UsersController` (`api/v1/users`) is `[Authorize]` for reads, `ManagerOrAdmin` for the list and `AdminOnly` for create/update/status. `UserService` already holds the password hashing, uniqueness checks and the `EndSessionsAsync` (security-stamp + refresh-token revocation) logic. `IUserService.GetOwnAsync` already exists as a self-service read hook.
- **Auth policies** available: `AdminOnly`, `ManagerOrAdmin`, `TaskAccess`.

## Goals / Non-Goals

**Goals:**
- Give a signed-in user a self-service read/update of their own profile and a secure own-password change.
- Give Administrators a validated, persisted application-settings surface that makes the timezone writable and adds application name, default task priority and an in-app notification preference.
- Reuse the existing settings store and session/security machinery rather than inventing parallel mechanisms.

**Non-Goals:**
- SMTP / outbound email configuration — retired in favour of the internal Mail module.
- Moving ownership of the default reminder intervals or the mail-delivery preference off screen 13.
- Per-user notification preferences (only a single global in-app preference).
- Wiring the application name into the internal-mail sender identity.
- Changing the reminder settings API's read-only timezone (screen 13 behaviour is unchanged).

## Decisions

### 1. Self-service endpoints live on `UsersController` under `/me`
Add `GET /api/v1/users/me`, `PUT /api/v1/users/me`, `POST /api/v1/users/me/password` (all `[Authorize]`), resolving the caller id from the `NameIdentifier` claim. The `me` literal is a more specific match than the int-constrained `{id}` route, so there is no collision, and `GetOwnAsync` was already added for this purpose.
*Alternatives:* a new `ProfileController` (rejected — `UserDto` and `UserService` already model the resource); reusing `auth/me` (rejected — that endpoint is identity/session, not profile mutation).

### 2. A dedicated self-service request contract, separate from `UpdateUserRequest`
`PUT /users/me` accepts a new `UpdateOwnProfileRequest(FullName, Email)` with its own validator; role, department, username, employee code and status are absent from the contract, so they cannot be escalated by construction. `UpdateUserRequest` (Admin) stays as-is.
*Alternatives:* reusing `UpdateUserRequest` and ignoring extra fields (rejected — easy to regress into privilege escalation).

### 3. Password change is its own endpoint with current-password verification
`POST /users/me/password` takes `{ CurrentPassword, NewPassword }`. The service verifies `CurrentPassword` with the injected `IPasswordHasher<User>` before applying the new hash, then calls the existing `EndSessionsAsync` so other sessions die. This satisfies "secure password change; require current password" without coupling password changes to ordinary profile edits.
*Alternatives:* an optional password field on `PUT /users/me` (rejected — cannot express the current-password requirement cleanly for self-service).

### 4. New `ApplicationSettingsController` at `api/v1/settings/application`
A separate controller keeps the Admin-write boundary in one class rather than mixing policies on `SettingsController` (which is `ManagerOrAdmin`). Read is `[Authorize]` (non-secret values); write is `AdminOnly`. The route does not collide with `api/v1/settings/reminders`.
*Alternatives:* adding methods to `SettingsController` with method-level `AdminOnly` (workable but muddier).

### 5. Read is broad, write is Admin-only
Application name, timezone, default priority and the notification preference are non-secret and are needed by non-admin screens (the create-task form uses the default priority as its initial value; the shell can show the app name). Read therefore requires only authentication; only Administrators write. Secrets never enter this surface.

### 6. Application settings own name, timezone, default priority and an in-app preference; intervals stay on screen 13
New keys: `Application.Name` (Category `Application`), `Application.DefaultPriority` (Category `Application`), `Notification.InAppEnabled` (Category `Notification`), all seeded by `SettingsInitializer`. `Application.Timezone` already exists and simply becomes writable. Screen 20's "default reminder intervals" and the mail-delivery preference stay owned by the reminder screen (13); Application Settings links to it, so the same keys never have two editors.
*Alternatives:* duplicating interval editing on both screens (rejected — conflicting writes to one key).

### 7. Timezone validated via `TimeZoneInfo`, applied with no restart
The update validator resolves the submitted value with `TimeZoneInfo.FindSystemTimeZoneById` and rejects unknown ids. Because `AppClock` reads the store per call, a saved change takes effect for reminders and dashboard day boundaries without a restart.
*Alternatives:* validating against a fixed allow-list (rejected — brittle across OS timezone databases).

### 8. Default task priority is a client initial value, not a backend fallback
The create-task form (Manager/Admin) reads the setting and pre-selects it; the task create/update contract still requires an explicit priority, so `TaskService` and its validators are untouched.

### 9. In-app notification preference gates new bell notifications
`NotificationPublisher` consults `Notification.InAppEnabled`; when false it skips creating new in-app notification rows. Existing rows are unaffected. This is the one behavioural hook the notification preference needs.
*Alternatives:* leaving the preference inert/unused (rejected — requirement 20 lists notification preferences explicitly).

## Risks / Trade-offs

- **`SettingsInitializer` seeding new keys may break existing tests that assert the exact default set** → update `SettingsInitializerTests` / `ReminderSettingsServiceTests` expectations as part of the change.
- **`TimeZoneInfo.FindSystemTimeZoneById` has OS-specific ids** (Windows vs IANA) → the validator accepts whatever the host recognises and rejects the rest with a clear validation error; `AppClock` already falls back to UTC for a stored value it cannot resolve.
- **Allowing authenticated read of settings widens surface slightly** → only non-secret values are exposed; no credentials or SMTP config live here.
- **Password change ends other sessions by design** → the current session also carries the old stamp; the client must handle the resulting 401 by re-authenticating (the existing interceptor already redirects to login with the `session-ended` notice).
- **Suppressing in-app notifications globally could surprise users** → default is enabled; the toggle is Administrator-only.

## Migration Plan

- No EF Core migration: the settings table already exists and rows are seeded at startup, so new keys appear automatically on the next boot. `SettingsInitializer` runs whether or not demo seeding is enabled.
- Deploy backend and frontend together; the frontend falls back to sensible defaults (brand "TaskFlow", a fixed priority) if the settings call fails. Rollback is a normal redeploy — no schema or data reversal required beyond the added setting rows.

## Open Questions

None that affect the specs, approach or task breakdown.
