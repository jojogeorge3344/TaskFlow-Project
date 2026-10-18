# Tasks

## 1. Application settings store (backend)

- [x] 1.1 Add `SettingKeys.Application.Name`, `Application.DefaultPriority` and `Notification.InAppEnabled` with their defaults, and seed them in `SettingsInitializer` alongside the existing rows. Verify by updating `SettingsInitializerTests` to assert the new keys are created with defaults and that an already-saved value is not overwritten.
- [x] 1.2 Add `ApplicationSettingsDto` and `UpdateApplicationSettingsRequest` (nullable fields) plus a FluentValidation validator that rejects a blank application name, an unknown timezone (via `TimeZoneInfo.FindSystemTimeZoneById`) and an out-of-range default priority. Verify with validator unit tests covering each rejection and a valid request.
- [x] 1.3 Add `IApplicationSettingsService` and `ApplicationSettingsService` mapping the `Application.*` / `Notification.InAppEnabled` rows to and from the DTO, persisting `UpdatedByUserId`/`UpdatedAt`. Verify with unit tests for get-with-fallback-to-default and a save that round-trips.
- [x] 1.4 Add `ApplicationSettingsController` at `api/v1/settings/application` with `GET` (`[Authorize]`) and `PUT` (`[Authorize(Policy = AdminOnly)]`). Verify with integration tests: a signed-in user can read, a Manager is forbidden from writing, a valid Admin write persists, and an unknown timezone is rejected with a validation problem.

## 2. In-app notification preference

- [x] 2.1 Make `NotificationPublisher` consult `Notification.InAppEnabled` and skip creating new in-app notification rows when it is false. Verify with unit tests showing notifications are created when enabled and suppressed when disabled.

## 3. Self-service profile (backend)

- [x] 3.1 Add `UpdateOwnProfileRequest` (full name, email only) and its validator. Verify with unit tests for valid input, malformed email and blank name.
- [x] 3.2 Add self-service methods to `IUserService`/`UserService` (read own profile, update own permitted fields, change own password) reusing `IPasswordHasher` and `EndSessionsAsync`. Verify with unit tests: duplicate email rejected, restricted fields (role/department/username) unchanged, wrong current password rejected, correct current password updates and ends other sessions.
- [x] 3.3 Add `GET`/`PUT /api/v1/users/me` and `POST /api/v1/users/me/password` on `UsersController` (`[Authorize]`). Verify with integration tests: a signed-in user reads only their own profile, updates name/email, cannot change role/status, and a password change signs other sessions out while returning 401 on the old token.

## 4. Frontend services and models

- [x] 4.1 Add models (`ApplicationSettings`, `UpdateApplicationSettings`, `UpdateOwnProfile`, `ChangePassword`) and services `application-settings.service.ts` and `profile.service.ts`. Verify with `npm run build` and service specs calling the expected endpoints.

## 5. Application settings screen

- [x] 5.1 Build `application-settings.component` (Administrator) with fields for application name, timezone, default priority and in-app notifications, a link to Reminder Settings for intervals/mail delivery, and register the route `/settings/application` plus the `ROLE_NAVIGATION` Administrator entry. Verify with a spec covering the admin-only render, validation errors and the successful save toast.

## 6. Profile screen and shell wiring

- [x] 6.1 Build `profile.component` (all roles) with read-only account fields, editable full name/email, and a change-password form requiring the current password; register route `/profile` and a link from the shell account block plus a `ROLE_NAVIGATION` entry for each role. Verify with specs for a self-edit save, a rejected duplicate email, and a password change that ends the session.
- [x] 6.2 Surface the application name in the shell brand (falling back to "TaskFlow") and pre-select the create-task form's priority from the application settings. Verify by building and observing the brand and initial priority reflect the stored values.

## 7. Integration verification

- [x] 7.1 Run the backend `dotnet test` suite and the frontend `npm test` and `npm run build`; confirm no new failures or warnings versus the pre-change baseline, and that the screen-13 read-only timezone now points at a working Application Settings screen.

## Workflow follow-up

- After code review, archive this change so its deltas sync into the main specs for `user-profile` and `application-settings`.
