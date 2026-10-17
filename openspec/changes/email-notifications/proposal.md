# Proposal

## Why

Deadline reminders are TaskFlow's main feature (BRD §3), but they currently reach people only as
in-app notifications. The "Also send reminders by email" toggle on Screen 13 saves a preference
that nothing uses yet. Screen 14 (Email Notification Settings) and acceptance criterion #8
require email delivery over SMTP, so reminders reach people who are not logged in. Email failures
must be recorded and must never stop in-app notifications. The reminder engine was merged in PR #14
and already reserves room for this (`ReminderStatus.Pending/Failed`,
`ReminderHistory.DeliveryAttempts`, the `Notification.EmailEnabled` key), so this is the natural
next step.

## What Changes

- **Email channel for deadline reminders.**
  - When email is enabled and a reminder is generated for a recipient who has an email address,
    an email is queued in the same transaction as the in-app notification.
  - A dispatcher sends queued emails over SMTP on each scheduler pass.
  - Failed sends are retried a limited number of times, and then the email is marked failed.
  - In-app notifications are created exactly as before, whatever happens to the email.
- **Email send time.** A new setting, default `09:00` in the application timezone. Reminders
  generated at local midnight hold their email until the send time, while the in-app notification
  appears at once. An email that has not been sent by the end of its reminder day is skipped, not
  sent late.
- **Email delivery log (`email_delivery_logs`).** One row per email (reminder or test) records the
  recipient address, subject, status (`Pending`, `Sent`, `Failed`, `Skipped`), attempt count, last
  error and timestamps. `ReminderHistory.DeliveryAttempts` is incremented for each attempt.
- **SMTP configuration in the database.** The non-secret values live in `application_settings`:
  host, port, security mode, sender address and name, username and send time.
- **The SMTP password is configuration-only.** It is read from `Email:SmtpPassword` (user-secrets
  or an environment variable). It is never stored in the database, returned by the API, shown in
  the UI or written to logs. The screen shows only whether a password is configured.
- **Admin-only Email Configuration API** under `/api/v1/settings/email`:
  - get and save the configuration, including the shared `Notification.EmailEnabled` switch
  - list the configuration problems
  - send a test email
  - list recent delivery attempts
- **Screen 14 UI** at `/settings/email` (Administrator only):
  - the email switch and SMTP fields, a read-only "password configured" indicator, and the send time
  - a configuration-problem banner, a "Send test email" button and a recent-deliveries table
  - an "Email Settings" entry in the side menu
- **New dependency:** MailKit, the SMTP client recommended in place of the obsolete
  `System.Net.Mail.SmtpClient`. It is added to `TaskFlow.Infrastructure`.

## Capabilities

### New Capabilities

- `email-delivery`: how deadline reminders are sent by email. Covers queueing alongside in-app
  notifications, the send time, retries and expiry, the in-app fallback, delivery logging and the
  rule that credentials are never exposed.
- `email-settings`: the Administrator's Email Configuration screen and API. Covers access control,
  the stored SMTP fields, the configuration-only password indicator, validation and configuration
  errors, the test email and the recent delivery log.

### Modified Capabilities

None. `openspec/specs/` is still empty: the earlier changes, including `deadline-reminders`, have
not been archived. This change keeps every behavior in `deadline-reminders` the same:
- in-app reminders are generated exactly as before
- history rows still record a sent status
- turning email on still never stops or delays in-app reminders

The requirement title "Email preference is recorded only" in `deadline-reminders/reminder-settings`
becomes out of date. Its scenario still holds.

## Impact

- **Domain:**
  - a new `EmailDeliveryLog` entity
  - new enums `EmailDeliveryStatus` and `EmailKind`
  - a new `SmtpSecurity` enum
- **Infrastructure:**
  - an `AddEmailDelivery` migration
  - the `ReminderService` transaction also queues the email
  - new `EmailDispatcher`, `IEmailSender` with a MailKit implementation, `EmailSettingsService`
    and `EmailOptions`
  - `SettingsInitializer` adds the new defaults
  - `ReminderSchedulerService` runs the dispatcher after each reminder pass
- **Application:** email settings DTOs, a validator and service interfaces.
- **API:**
  - a new `EmailSettingsController` with the `AdminOnly` policy
  - `appsettings.json` gets an `Email` section with no secret in it
  - the integration test factory disables real sending
- **Frontend:**
  - a new `features/settings/email-settings` component and route
  - `EmailSettingsService` and models in `core/`
  - a nav entry and a `mail` icon
  - an admin link from the Screen 13 email toggle hint
- **Operations:**
  - set the SMTP password with `dotnet user-secrets set Email:SmtpPassword ...` or the
    `Email__SmtpPassword` environment variable
  - for local demos, a capture server such as smtp4dev or Papercut works with no credentials
