# Proposal

## Why

Two requirement screens depend on capabilities that do not exist yet. The reminder screen (13) already renders the application timezone as read-only and tells the user it is "Changed in Application Settings" — a screen that was never built, so the timezone can only change by editing configuration on the server. Separately, users can view their account in the shell header but cannot edit their own details or change their own password; the only update path is the Administrator-only `PUT /api/v1/users/{id}`. This change adds those two missing surfaces: Application Settings (screen 20) and self-service User Profile (screen 19).

## What Changes

- **Self-service profile API** — a signed-in user can read and update their own permitted fields (full name, email) and change their own password. Password change requires the current password and ends the user's other sessions via the existing security-stamp/refresh-token mechanism. Role, department, username and account status stay read-only to the user.
- **Application Settings API** — an Administrator can read and update application-level configuration: application name, default timezone, default task priority and notification preferences.
- **Editable timezone** — `Application.Timezone` becomes writable through Application Settings (validated against the system's known timezones). The reminder screen's read-only timezone and its hint text become accurate; `AppClock` already resolves the value from the settings store on every read.
- **New frontend screens** — `/profile` for all roles (view + edit own details, change password) and `/settings/application` for Administrators, plus their navigation entries.
- **Non-duplicating defaults** — the "default reminder intervals" from screen 20 remain owned by the reminder settings screen (13); Application Settings links to that screen rather than offering a second edit path for the same keys.

## Capabilities

### New Capabilities

- `user-profile`: A signed-in user can view their own account details and update the fields they are permitted to change, and securely change their own password; role, department, username and account status remain read-only to the user.
- `application-settings`: Administrator-managed application-level configuration — application name, default timezone, default task priority and notification preferences — stored in the application settings key/value store and applied consistently by the running application.

### Modified Capabilities

<!-- None: the reminder screen (13) keeps exposing the timezone as read-only, so no existing
     requirement changes. The timezone simply gains a writable owner in the new capability. -->

## Impact

- **Backend / API**: new self-service endpoints on `UsersController` (`GET`/`PUT /api/v1/users/me`, `POST /api/v1/users/me/password`) alongside the existing Administrator-only `PUT /users/{id}`; new `GET`/`PUT /api/v1/settings/application` (Administrator-only), mirroring the existing `/api/v1/settings/reminders`. New DTOs and FluentValidation validators. `IUserService`/`UserService` gain self-service methods reusing the existing password-hash and `EndSessionsAsync` logic.
- **Settings store**: new `Application.Name` (and default-priority / notification-preference) keys added to `SettingKeys` and seeded by `SettingsInitializer`. The store is a key/value table, so no EF Core migration is required; the timezone key already exists.
- **Frontend**: new `features/profile/` and `features/settings/application-settings.component` screens, `profile.service.ts` and `application-settings.service.ts`, new models, new routes as children of the shell, and `ROLE_NAVIGATION` entries. The screen-13 timezone hint text is unchanged and now correct.
- **Tests / Docs**: unit and integration coverage for the new endpoints; frontend specs for both screens. Requirement files `19-user-profile-screen.md` and `20-application-settings-screen.md` are the source of truth for scope; the broader stack notes embedded in file 20 are superseded by the implemented .NET + Angular stack and are out of scope here.
