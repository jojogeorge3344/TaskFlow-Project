# Proposal

## Why

The BRD calls the automatic deadline notification workflow (§3) "the main feature of TaskFlow",
and acceptance criteria 6, 7, 9, 10 and 15 depend on it. None of it exists yet. Tasks already
store per-task reminder intervals (`FirstReminderDays`, `SecondReminderDays`,
`OverdueReminderFrequency`, from `task-management-impl` D4). The `DeadlineReminder` and `Overdue`
notification types also exist, and the Notification Center (Screen 12) already shows whatever is
written. Nothing reads the reminder fields, so no reminder is ever produced. This change adds the
reminder engine and the Screen 13 Reminder Settings screen that controls it.

## What Changes

Backend:
- Domain: add `ReminderType` (FirstReminder, SecondReminder, DueToday, Overdue), a
  `ReminderHistory` entity, and an `ApplicationSetting` key/value entity. Add a pure, unit-tested
  rule that computes which reminders a task is due for at a given moment in the application timezone.
- Infrastructure: add the migration `AddReminderEngine`, which creates the `reminder_history` table
  with a unique dedup index `(TaskId, RecipientUserId, ReminderType, ScheduledFor)`. The same
  migration creates the `application_settings` table with a unique `Key`. The seeded keys are
  `Application.Timezone` (taken from configuration), `Reminder.Enabled`, `Reminder.DeadlineDayEnabled`,
  `Reminder.DefaultFirstDays`, `Reminder.DefaultSecondDays`, `Reminder.DefaultOverdueFrequency` and
  `Notification.EmailEnabled`.
- Infrastructure: add a `ReminderSchedulerService` (`BackgroundService`) that ticks on a configurable
  interval (default 1 minute). On each tick it opens a scope and asks `IReminderService` to
  generate the reminders that are due. Each generated reminder writes one `reminder_history`
  row and one in-app notification per recipient. Recipients are the assignee, the creator and
  any additional recipients. Completed, cancelled and deleted tasks are skipped, and so are
  inactive or deleted recipients. A reminder slot that has already passed is never backfilled.
  The dedup index makes repeated or restarted ticks idempotent. A deadline change is picked up
  on the next tick because slots are computed from the task's current deadline.
- Application/API: add `IReminderSettingsService` with `GET` and `PUT /api/v1/settings/reminders`
  under the `ManagerOrAdmin` policy. The endpoints read and validate the global switch,
  the deadline-day toggle, default intervals, default overdue frequency and email toggle, and
  return the configured timezone read-only.
- Task creation: when the request omits reminder intervals, the defaults now come from the
  reminder settings instead of hard-coded `2 / 1 / Daily`. Only Managers and Administrators can
  create tasks, so the Create Task form pre-fills from the same `GET /api/v1/settings/reminders`.

Frontend:
- A `ReminderSettingsService` (core) and a lazy-loaded `features/settings/reminder-settings`
  screen at `/settings/reminders` for Administrator and Manager. The screen has the
  enable/disable switch, first and second interval, deadline-day reminder, overdue frequency,
  email setting, a read-only timezone and a Save button. It shows the current configuration
  and validation errors.
- A "Reminder Settings" nav entry for Administrator and Manager. The Create Task form pre-fills
  its reminder fields from the configured defaults.

Out of scope:
- Actual email delivery, SMTP configuration and `email_delivery_logs` belong to Screen 14. The
  `Notification.EmailEnabled` flag is stored and shown here, but nothing is sent yet.
- Editing the timezone and the other application settings belongs to Screen 20.
- Moving the existing UTC day boundaries in `TaskStageCalculator` and `DashboardService` onto the
  application timezone is a follow-up; see the design's Risks section.
- Reminder history UI. The history is stored and queryable, but no screen lists it.

## Capabilities

### New Capabilities
- `deadline-reminders`: automatic generation of first, second, due-today and overdue reminders
  for open tasks. This covers recipient resolution, stop rules for completed or cancelled
  tasks, no backfill of past slots, duplicate prevention through reminder history,
  restart safety, and one consistent application timezone.
- `reminder-settings`: global reminder configuration (Screen 13). This covers reading and
  saving the switch, toggles, default intervals and email flag; validation; role access;
  supplying defaults to task creation; and the Reminder Settings screen.

### Modified Capabilities
- None under `openspec/specs/`, which is still empty because no change has been archived. The
  in-flight `task-management-impl` change hard-codes the reminder defaults for task creation.
  The `reminder-settings` spec here takes over that defaulting behavior.

## Impact

- **Backend:** new entities, enum and migration in Domain and Infrastructure. Also new:
  `IReminderService`/`ReminderService`, `IReminderSettingsService`, a hosted service, a
  `SettingsController`, and registrations in `Program.cs`. `TaskService.CreateAsync` gains a
  defaults lookup. New `Reminders` options (tick interval, enabled flag) and
  `Application:Timezone` go in `appsettings.json`.
- **Frontend:** a new settings feature, route, nav entry and core service. `task-create`
  pre-fills from the defaults.
- **Data:** two new tables and seeded settings rows. Existing data is unchanged.
- **Tests:** unit tests for the reminder rule and the generator (dedup, stop rules, recipients,
  timezone, no backfill). Integration tests for the settings endpoints and for scheduler
  idempotency. Frontend specs for the settings screen and service.
- **Dependencies:** none new. The engine uses the built-in `BackgroundService` and
  `TimeZoneInfo`.
