# Tasks

## 1. Domain, persistence and configuration

- [x] 1.1 Add the enums `EmailKind` (Reminder, Test), `EmailDeliveryStatus` (Pending, Sent, Failed, Skipped) and `SmtpSecurity` (None, StartTls, SslOnConnect, Auto) to `TaskFlow.Domain`. Add the `EmailDeliveryLog` entity with the columns listed in design D1. Update the `ReminderStatus` doc comment so it no longer says the reserved states are for Screen 14. Verify `dotnet build` succeeds.
- [x] 1.2 Configure `EmailDeliveryLog` in `AppDbContext` (D1):
  - string conversions for `Kind` and `Status`
  - column max lengths
  - `Restrict` FKs to `ReminderHistory`, `Tasks` and `Users`
  - a unique filtered index on `ReminderHistoryId`, and indexes on `(Status, NextAttemptAt)` and `(CreatedAt)`

  Add the migration `AddEmailDelivery`. Verify that the migration creates only the new table and its indexes, and that the app starts and migrates the existing `database/taskflow.db` without errors.
- [x] 1.3 Add the `Email.*` keys, the `Email` category and their defaults to `SettingKeys`, and insert them in `SettingsInitializer.Defaults()` (D5). Add `EmailOptions` (`SmtpPassword`, `TimeoutSeconds = 30`, `MaxAttempts = 3`, `AppBaseUrl`). Bind it in `Program.cs` and add an `Email` section to `appsettings.json` with no password in it. Verify by extending `SettingsInitializerTests`: the 7 keys are inserted once with their defaults, and existing values are kept.

## 2. Settings service, configuration check and sender

- [x] 2.1 Add the following to `TaskFlow.Application/Settings`:
  - `EmailSettingsDto`, `UpdateEmailSettingsRequest`, `SendTestEmailRequest`, `TestEmailResultDto` and `EmailDeliveryDto`
  - the internal `SmtpSettings` record, whose `ToString()` redacts the password
  - the pure `EmailConfigurationCheck.Problems(...)`, returning the three messages from D6
  - `UpdateEmailSettingsRequestValidator` and `SendTestEmailRequestValidator` (D7 rules)

  Verify with unit tests:
  - the validators: port range, the `HH:mm` regex, invalid sender, and host and sender required only when enabled
  - each problem message from `EmailConfigurationCheck`
  - the redacted `ToString()`
- [x] 2.2 Implement `EmailSettingsService` (D6). It provides:
  - `GetAsync`, including `passwordConfigured`, `issues`, `updatedAt` and `updatedByName`
  - `UpdateAsync`, which writes the `Email.*` keys plus `Notification.EmailEnabled` and stamps `UpdatedByUserId`/`UpdatedAt`
  - `GetSmtpSettingsAsync`

  Register it in `Program.cs`. Verify with unit tests:
  - defaults on a fresh database
  - a save round-trip
  - an email switch saved here is read back by `ReminderSettingsService`, and the reverse
  - the serialized DTO JSON never contains the configured password string
- [x] 2.3 Add the MailKit package to `TaskFlow.Infrastructure`. Add `IEmailSender` and `EmailMessage` to Application, and implement `MailKitEmailSender` (D4):
  - the security-mode mapping
  - authentication only when a username is set
  - the timeout
  - exceptions mapped to the fixed messages, truncated to 1000 characters
  - Warning-level logging with no credentials

  Register it in `Program.cs`. Verify with unit tests for the exception-to-message mapping, including that a password present in an exception's text does not appear in the mapped error. Verify `dotnet build` succeeds.

## 3. Reminder engine queues emails

- [x] 3.1 Extend `ReminderService` (D2):
  - read the email settings once per pass
  - make `LoadRecipientsAsync` return the recipients' emails too
  - in `TryDeliverAsync`, after the history insert, read the history id and add a `Pending` `EmailDeliveryLog` (subject, plain-text body with an optional task link, `NextAttemptAt` at the local send time or now, `ExpiresAt` at the next local midnight, `TaskDeadline`) in the same transaction as the notification, only when email is enabled and the address is non-empty

  Verify by extending `ReminderServiceTests` and `ReminderIdempotencyTests`:
  - email disabled queues nothing
  - enabled queues one row per recipient that has an email address, and none for an empty address
  - a reminder generated at 00:01 local time gets `NextAttemptAt` at 09:00 local time, and one generated at 14:00 gets now
  - repeated and concurrent passes leave exactly one row per reminder
  - in-app notifications are unchanged in every case

## 4. Email dispatcher

- [x] 4.1 Implement `EmailDispatcher.DispatchDueAsync` (D3):
  - batch of 50 due `Pending` rows
  - skip rules: expired, task deleted, completed, cancelled or rescheduled, or email disabled (reminder emails only), each with its reason
  - the conditional-update claim with the 10-minute lease, plus incrementing `ReminderHistory.DeliveryAttempts`
  - the configuration check
  - the send
  - the outcome: `Sent`, or retry at +5/+15 min, or `Failed` after 3 attempts

  Verify with unit tests using a fake clock and a recording/failing `IEmailSender` for each `email-delivery` scenario:
  - sent on the first attempt, with the log and history counts
  - a temporary failure then success, with 2 attempts
  - a permanent failure after 3 attempts with no fourth
  - the configuration-error attempt
  - an email waiting for the send time is not sent before it
  - the application down all day leads to skipped
  - task completed before the send time leads to skipped
  - email disabled leads to skipped
  - a row already claimed by another worker is not sent twice
  - in-app notifications are untouched by every failure
- [x] 4.2 Call the dispatcher from `ReminderSchedulerService.RunPassAsync` after `GenerateDueAsync`, in its own try/catch so that one failing never blocks the other. Verify with a unit test where the dispatcher throws: the reminder pass still completes and the next tick runs. Also start the app and confirm the scheduler log shows both steps without errors.

## 5. Email settings API

- [x] 5.1 Add `EmailSettingsController` on `api/v1/settings/email` with `AuthPolicies.AdminOnly` (D7):
  - `GET` and `PUT /`
  - `POST /test`: defaults to the caller's email, uses the saved settings, works while email is disabled, writes a `Test` log row, and always returns 200 with `success` and `message` unless the address is invalid (400)
  - `GET /deliveries?take=`: clamped to 1–100, newest first, with task title, no body

  In `TaskFlowApiFactory`, replace `IEmailSender` with a `FakeEmailSender`. Verify with integration tests:
  - Admin gets the defaults, and a save persists and is returned with `updatedAt`
  - an invalid port, or enabling without a host, returns 400 with field errors and nothing changes
  - a Manager gets 403 on every endpoint, and an unauthenticated caller gets 401
  - a successful test returns `success: true` and lists a sent test row
  - a failing fake returns `success: false` with the mapped message and a failed row
  - the test address "abc" returns 400 with no row written
  - no response body contains the configured password

## 6. Frontend Email Settings screen

- [x] 6.1 Add the email settings models to `core/models/settings.model.ts`, and `core/services/email-settings.service.ts` with `get`, `save`, `sendTest` and `deliveries`. Verify with a service spec using `HttpTestingController` that checks each URL, method and body.
- [x] 6.2 Build `features/settings/email-settings.component` (D8):
  - the reactive form with the stored values
  - the read-only password status and its hint
  - the issues alert while email is enabled
  - server field errors and a save toast
  - test email with a prefilled address, disabled while the form is dirty with "Save changes before testing", and an inline result
  - the recent-deliveries table with status badges, turning into card rows below 768px

  Add the `settings/email` route with `roleGuard` for `['Administrator']`, a `mail` icon, and an "Email Settings" nav entry for Administrator. Verify with a component spec covering: values loaded into the form, a save calling the service and showing a toast, field errors mapped, issues shown only when enabled, test disabled while dirty, the test result rendered, and deliveries rendered. Verify `npm run build` passes and `npm test` is green.
- [x] 6.3 On Reminder Settings, add a "Configure email delivery" link to `/settings/email` under the email toggle hint, shown to Administrators only. Verify with a reminder-settings spec that the link appears for an Administrator and not for a Manager.

## 7. Documentation and end-to-end check

- [x] 7.0 Seed the SMTP fields from the `Email` configuration section on a new database (added on request): `EmailOptions` gains the SMTP fields, `SettingsInitializer` uses valid configured values and falls back to the defaults, and `appsettings.json` sets the Gmail account. Verify with `SettingsInitializerTests` (seeded, invalid fallback, never overwritten) and a fresh-database API check.

- [x] 7.1 Document email setup in `README.md`:
  - setting the password with `dotnet user-secrets set Email:SmtpPassword …` or `Email__SmtpPassword`
  - Gmail and Office 365 app-password notes
  - a credential-free local demo with smtp4dev or Papercut (`localhost`, port 25, security None)
  - the send time and retry behavior

  Verify that the documented user-secrets command sets the value and that the screen then shows "Configured".
- [ ] 7.2 Manual check in `npm start` as Administrator with smtp4dev running:
  - configure SMTP, save, and send a test email (it arrives in smtp4dev and shows as Sent)
  - enable email, set the send time a minute ahead, and create a task due tomorrow with a first reminder of 1 day: the in-app notification appears at once, the email arrives at the send time, and the delivery row shows as Sent
  - stop smtp4dev and trigger another reminder: the in-app notification still appears, and the delivery retries and then shows as Failed with a readable error
  - as Manager and as Employee, `/settings/email` is denied and the menu entry is hidden
  - the page works at 390px with no horizontal scroll
