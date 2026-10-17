# Design

## Context

See `proposal.md` for the motivation and `specs/email-delivery` and `specs/email-settings` for the
required behavior. The current code that shapes the approach:

- **Reminder engine (`deadline-reminders`).**
  - `ReminderService.GenerateDueAsync` computes the reminders due today, and resolves
    recipients as task recipients ∪ creator, active users only.
  - Per (task, recipient, reminder) it runs one transaction: a raw `INSERT … ON CONFLICT DO NOTHING`
    into `ReminderHistory`, guarded by an `EXISTS` check that the task is still open, not deleted and
    has the same deadline. Then it adds the `Notification` and commits.
  - History rows are written only as `Sent`. `ReminderStatus.Pending/Failed/Skipped` and
    `ReminderHistory.DeliveryAttempts` (always `0`) were reserved for this change.
- **Scheduler.** `ReminderSchedulerService` (`BackgroundService` + `PeriodicTimer`, 60 s) runs a
  pass in a new DI scope and catches and logs every exception. Integration tests set
  `Reminders:SchedulerEnabled=false` and call the services directly with a fake `TimeProvider`.
- **Settings.**
  - Settings live in the `application_settings` key/value table. Missing keys are inserted by
    `SettingsInitializer`, and existing rows are never overwritten.
  - `Notification.EmailEnabled` already exists. It is exposed and saved by
    `ReminderSettingsService` / `PUT /api/v1/settings/reminders`, which Managers can also call.
  - `ApplicationSetting`'s doc comment already states that secrets never live in that table.
- **Time.** `IAppClock` gives `UtcNow` and the application `TimeZoneInfo`.
  `ReminderCalculator` turns a local reminder day into the UTC instant of its local midnight
  (handling DST gaps and ambiguity), and the history row's `ScheduledFor` holds that value.
- **Users.** `User.Email` is a non-null string, but it can be empty.
- **Auth.** There is an `AuthPolicies.AdminOnly` policy. `SettingsController` sits on
  `api/v1/settings` with `ManagerOrAdmin` at class level. Validation failures return
  `ValidationProblemDetails` from FluentValidation results.
- **Frontend.**
  - Standalone Angular components under `features/`, with root-provided services and models
    under `core/`.
  - Routes are children of the shell with `roleGuard`, and nav entries are listed in
    `core/layout/nav-items.ts`.
  - The icon map has no `mail` icon.
  - `features/settings/reminder-settings` is the closest pattern to follow (reactive form,
    `.tf-*` primitives, server field errors).

## Goals / Non-Goals

**Goals:**
- Make email an **add-on outbox**. The reminder engine's in-app path and its dedup guarantees stay
  exactly as they are. Email is queued atomically with the notification and sent later, outside
  any database transaction.
- Make sending at-most-once per queued email across overlapping passes, and safe across crashes.
- Have one place (the dispatcher) decide send, retry, skip or expire, testable with a fake clock
  and a fake sender.
- Keep the password out of every persisted, returned or logged value, by design rather than by
  filtering.

**Non-Goals:**
- Emailing other notification types (task assigned, reassigned, comments). The outbox makes this
  easy to add later.
- HTML or templated emails, per-user email preferences or unsubscribe, bounce handling, and OAuth2
  SMTP authentication.
- Entering the password in the UI (decided: configuration only).
- Paging, filtering or a retention purge for the delivery log. The screen shows the latest 20.
- Recomputing already-queued emails when the send time or timezone changes. Only emails queued
  after the change use the new values.

## Decisions

**D1 - `email_delivery_logs` is both the outbox and the log (one row per email).**
New entity `EmailDeliveryLog`:

| Column | Notes |
|---|---|
| `Id` | Primary key |
| `Kind` | `EmailKind { Reminder, Test }`, stored as a string |
| `ReminderHistoryId?` | FK, `Restrict`, **unique** filtered index where not null. This is the at-most-one-email-per-reminder guarantee. |
| `TaskId?` | FK, `Restrict` |
| `RecipientUserId?` | FK, `Restrict` |
| `ToAddress` | Max 320 |
| `Subject` | Max 200 |
| `Body` | Text |
| `Status` | `EmailDeliveryStatus { Pending, Sent, Failed, Skipped }`, stored as a string |
| `Attempts` | |
| `NextAttemptAt?` | UTC |
| `ExpiresAt?` | UTC |
| `TaskDeadline?` | The deadline when the email was queued |
| `LastError?` | Max 1000 |
| `CreatedAt` | |
| `LastAttemptAt?` | |
| `SentAt?` | |
| `CreatedByUserId?` | Set for test emails |

Index `(Status, NextAttemptAt)` serves the dispatcher. Index `(CreatedAt)` serves the recent list.

The BRD says "record email delivery attempts and failures". A row per email with an attempt
counter and the last error covers that, and it doubles as the queue. Each attempt also increments
`ReminderHistory.DeliveryAttempts`.

Alternatives considered:
- A row per attempt plus a separate outbox table. Two tables for the same lifecycle, with no
  observable gain.
- Reusing `ReminderHistory.Status` (`Pending` until emailed). Rejected because it changes the
  `deadline-reminders` contract ("history records … a sent status") and mixes the in-app and email
  states. History keeps meaning "in-app reminder generated".

**D2 - Queue inside the existing reminder transaction.**
In `ReminderService.TryDeliverAsync`, after the history insert returns 1:
1. Read the new history `Id` by its unique key, inside the same transaction.
2. Add the `Notification`.
3. If email is enabled for this pass and the recipient's email is non-empty, add an
   `EmailDeliveryLog` (`Pending`, `Attempts = 0`).
4. Save and commit.

The pass reads the email settings once (enabled flag and send time) and loads recipients' emails
together with their ids, by extending `LoadRecipientsAsync`.

The queued row's times:
- `NextAttemptAt = max(nowUtc, ToUtc(reminder.Day + SendTime))`
- `ExpiresAt = ToUtc(reminder.Day + 1 day, 00:00)`
- `TaskDeadline` = the task's deadline from the pass

Both times use the same local-to-UTC conversion as the reminder slot. The subject and body are
built once here: plain text with the notification message, the deadline in local time and, when
`Email:AppBaseUrl` is configured, a link to `/tasks/{id}`.

Because the history insert is the dedup gate, a duplicate reminder never reaches the email insert.
The unique `ReminderHistoryId` index is a second line of defense.

**D3 - `EmailDispatcher` runs after each reminder pass.**
`ReminderSchedulerService.RunPassAsync` calls `IEmailDispatcher.DispatchDueAsync(ct)` after
`GenerateDueAsync`, in its own try/catch. A failure in one never affects the other. The dispatcher
runs even when reminders are disabled, so queued rows still get resolved. Per pass:

1. Load up to 50 `Pending` rows with `NextAttemptAt <= now`, oldest first, together with their
   task's current state.
2. Skip rules. Each sets `Status = Skipped` and `LastError` = the reason, and sends nothing:
   - `now >= ExpiresAt`: "Reminder day ended before the email could be sent".
   - The task was deleted, completed, cancelled, or its deadline is not `TaskDeadline`:
     "Task changed before sending".
   - Email is disabled (reminder emails only): "Email notifications were disabled".
3. **Claim** the row with a conditional update:
   `UPDATE … SET Attempts = Attempts + 1, LastAttemptAt = now, NextAttemptAt = now + 10 min
   WHERE Id = @id AND Status = 'Pending' AND Attempts = @seen`. Only the instance that gets one
   affected row sends. The 10-minute lease means a crash mid-send leads to one retry, not a lost
   email. Also increment `ReminderHistory.DeliveryAttempts`.
4. Check the configuration (D6). If it is incomplete, record the attempt as a failure with the
   configuration message. Otherwise call `IEmailSender.SendAsync`, outside any transaction.
5. Record the outcome:
   - Success: `Sent`, `SentAt`, `LastError = null`.
   - Failure with `Attempts < 3`: stays `Pending`, `NextAttemptAt` = now + 5 min after attempt 1,
     or + 15 min after attempt 2.
   - Failure with `Attempts = 3`: `Failed`.

Alternatives considered:
- Sending inside the reminder transaction. That holds the SQLite write lock across network I/O,
  and a slow server would roll back in-app notifications, which violates the spec.
- A separate hosted service. That adds a second timer and needs coordination for no benefit.

**D4 - `IEmailSender` (Application) with a MailKit implementation (Infrastructure).**
`SendAsync(EmailMessage, SmtpSettings, ct) → EmailSendResult(bool Success, string? Error)`. It
never throws for SMTP problems. `MailKitEmailSender` does the following:
- connects with the configured `SecureSocketOptions` (`None`, `StartTls`, `SslOnConnect`, or `Auto`
  mapped from `SmtpSecurity`)
- authenticates only when a username is set
- sends, and disconnects
- uses `Email:TimeoutSeconds` (default 30)

Exceptions are mapped to fixed, readable messages. Only MailKit's SMTP status code and the
exception type are appended, never `ex.Message` verbatim:

| Exception | Message |
|---|---|
| `AuthenticationException` | "Authentication failed: the SMTP server rejected the username or password." |
| `SocketException` / timeout | "Could not connect to {host}:{port}." |
| `SslHandshakeException` | "Secure connection failed; check the security mode and port." |
| `SmtpCommandException` | "Server rejected the message (code {StatusCode})." |
| anything else | "Email delivery failed ({ExceptionType})." |

The error is truncated to 1000 characters. Full exceptions are logged at Warning level with the
delivery id, never with the credentials.

MailKit was chosen because `System.Net.Mail.SmtpClient` is documented as not recommended for new
development and handles modern TLS and auth poorly. Tests replace `IEmailSender` with a recording
fake through `ConfigureTestServices`, so no test opens a socket.

**D5 - Settings storage: the database for non-secrets, configuration for the password.**

New `application_settings` keys, in category `Email`, with these defaults:

| Key | Default |
|---|---|
| `Email.SmtpHost` | `""` |
| `Email.SmtpPort` | `587` |
| `Email.SmtpSecurity` | `StartTls` |
| `Email.SenderAddress` | `""` |
| `Email.SenderName` | `TaskFlow` |
| `Email.SmtpUsername` | `""` |
| `Email.SendTime` | `09:00` |

They are added to `SettingKeys` and `SettingsInitializer.Defaults()`. When the `Email` configuration section sets
`SmtpHost`, `SmtpPort`, `SmtpSecurity`, `SenderAddress`, `SenderName`, `SmtpUsername` or `SendTime`, that value is
the first value on a new database instead of the default. Invalid values fall back to the default, and a value saved
on the screen is never overwritten.

The enable switch stays the existing `Notification.EmailEnabled` key, so Screens 13 and 14 share
it (spec "The email switch is shared").

`EmailOptions` binds the `Email` configuration section:
- `SmtpPassword` (secret: user-secrets or `Email__SmtpPassword`, never in `appsettings.json`)
- `TimeoutSeconds`
- `MaxAttempts = 3`
- `AppBaseUrl?`

`appsettings.json` gets an `Email` section **without** `SmtpPassword`.

The password exists only in `EmailOptions` and in the `SmtpSettings` value handed to the sender.
No DTO has a password property, and `SmtpSettings.ToString()` is overridden to redact it.

Alternative considered: encrypt the password with Data Protection in the database. The user
chose configuration-only.

**D6 - `IEmailSettingsService`.**
- `GetAsync()` returns `EmailSettingsDto`:
  - `emailEnabled, smtpHost, smtpPort, smtpSecurity, senderAddress, senderName, smtpUsername`
  - `sendTime` (`"HH:mm"`)
  - `passwordConfigured`
  - `issues: string[]`
  - `updatedAt?, updatedByName?` (the newest `UpdatedAt` among the email keys and that user's
    name)
- `UpdateAsync(userId, request)` writes the keys and stamps `UpdatedByUserId/UpdatedAt` on every
  key it writes, following the `ReminderSettingsService.Set` pattern.
- `GetSmtpSettingsAsync()` returns the internal `SmtpSettings`, password included, for the
  dispatcher and the test.

`issues` is computed by one pure function `EmailConfigurationCheck.Problems(settings,
passwordConfigured)`:
- "SMTP server is not set."
- "Sender address is not set."
- "A username is set but the SMTP password is not configured on the server. Set Email:SmtpPassword
  with dotnet user-secrets or the Email__SmtpPassword environment variable."

The same function gates sending in D3.

**D7 - API: a new `EmailSettingsController`** on `api/v1/settings/email` with
`[Authorize(Policy = AuthPolicies.AdminOnly)]`. It stays separate from `SettingsController`
because that controller's class-level policy is `ManagerOrAdmin`.

| Method and path | Behavior |
|---|---|
| `GET /` | Returns `EmailSettingsDto` |
| `PUT /` | Takes `UpdateEmailSettingsRequest`. All fields nullable but required by the validator, the same pattern as reminder settings. Returns the saved DTO, or a 400 `ValidationProblemDetails`. |
| `POST /test` | Takes `{ toAddress? }` and defaults to the caller's own email. Sends synchronously with the saved settings, even while email is disabled. Writes a `Test` log row (`Attempts = 1`, `Sent` or `Failed`). Returns **200** `{ success, message, deliveryId }` whether or not SMTP succeeded, because a failed test is a valid result, not a server error. An invalid address is a 400. |
| `GET /deliveries?take=20` | Take is clamped to 1–100. Returns the newest rows as `EmailDeliveryDto` with the task title when there is one. `Body` is not returned. |

`UpdateEmailSettingsRequestValidator` rules:
- port 1–65535
- security is a defined enum value
- `sendTime` matches `^([01]\d|2[0-3]):[0-5]\d$`
- sender address is a valid email of at most 320 characters when it is non-empty
- host at most 255 characters, username at most 320, sender name at most 100
- when `emailEnabled`, the host and sender address are required

`SendTestEmailRequestValidator`: the address is optional, and a valid email when present.

**D8 - Frontend.**
- **Service and models.** `core/services/email-settings.service.ts` provides `get`, `save`,
  `sendTest` and `deliveries`. The models go in `core/models/settings.model.ts`.
- **Screen.** `features/settings/email-settings.component` at route `settings/email`, with
  `roleGuard` limited to `['Administrator']`.
- **Form.** Uses the reminder-settings component's patterns:
  - a reactive form with an email switch, host, port, a security select, sender address, sender
    name, username and a `type="time"` send time
  - a read-only "SMTP password: Configured / Not configured" row with the user-secrets and
    environment-variable hint
  - a `.tf-alert` listing `issues` while email is enabled
  - server field errors mapped onto the controls, and a save toast
- **Test email.** An address input, prefilled with the logged-in user's email. "Send test email" is
  disabled while the form is dirty, with the hint "Save changes before testing". The result is
  shown inline.
- **Recent deliveries.** A `.tf-table` (kind, to, subject or task, status badge, attempts, last
  error, time) that turns into card rows below 768px. It reloads after a test or a save.
- **Navigation and icon.** `ROLE_NAVIGATION.Administrator` gets "Email Settings" with a new `mail`
  icon.
- **Screen 13 link.** The email toggle hint on Reminder Settings gets a "Configure email delivery"
  link, shown to Administrators only.

**D9 - Scheduler and options wiring.** `Program.cs` registers:
- `IEmailSettingsService`, `IEmailDispatcher`, and `IEmailSender → MailKitEmailSender`, all scoped
- `Configure<EmailOptions>`
- both new validators, through the existing assembly scan

`TaskFlowApiFactory` replaces `IEmailSender` with a `FakeEmailSender` (it records messages and can
be told to fail) and sets `Email:SmtpPassword` only in the tests that need it.

## Risks / Trade-offs

- **[Risk] A slow or unreachable SMTP server makes a scheduler pass long and delays the next
  reminder pass.** → Sends happen after generation, with a 30 s timeout and at most 50 per pass,
  and `PeriodicTimer` never overlaps passes. Repeated failures back off (5 → 15 min) and stop after
  3 attempts.
- **[Risk] A crash between a successful send and recording `Sent`** leads to one duplicate email
  after the 10-minute lease. → This is accepted, because at-least-once beats losing the email after
  a send. In-app dedup is unaffected.
- **[Risk] Emails queued while the configuration is incomplete burn their 3 attempts and fail.** →
  The issues banner makes this visible, and in-app notifications are unaffected. The next day's
  reminders are emailed once the configuration is fixed.
- **[Trade-off] Managers can switch email on through Screen 13** without seeing SMTP problems. →
  This keeps the existing contract. The Screen 13 hint links Administrators to Email Settings, and
  the problems are visible there.
- **[Trade-off] One SMTP connection per email.** This is simple and isolates failures. At office
  volume (tens per day) the overhead does not matter, and a pooled connection can come later.
- **[Risk] Gmail and Office 365 need an app password, and plain-password SMTP may be disabled.** →
  Document it in the README. smtp4dev or Papercut give a zero-credential local demo
  (`localhost:25`, security `None`).
- **[Risk] Secrets leaking through exception text.** → Errors are fixed mapped messages (D4), no
  DTO carries the password, `SmtpSettings.ToString()` redacts it, and a unit test asserts that the
  password string never appears in the DTO JSON or in `LastError`.

## Migration Plan

1. The `AddEmailDelivery` migration creates `EmailDeliveryLogs` with its FKs (`Restrict`), the
   unique filtered index on `ReminderHistoryId` and the two query indexes. It is additive only.
2. On startup `MigrateAsync` applies it, and `SettingsInitializer` adds the 7 `Email.*` keys with
   their defaults.
3. Email stays off (`Notification.EmailEnabled=false`) until an Administrator configures it and
   switches it on, so deploying changes nothing observable.
4. Rollback: switch email off (pending emails are then skipped), or revert the migration. The table
   is new and nothing else depends on it.
