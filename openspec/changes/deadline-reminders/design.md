# Design

## Context

See `proposal.md` for motivation and `specs/` for the required behavior. The current state that
shapes the approach:

- **Task** already stores `Deadline` (UTC), `Status`, `IsDeleted`, `CreatedByUserId`,
  `AssignedToUserId`, and the per-task `FirstReminderDays`, `SecondReminderDays` and
  `OverdueReminderFrequency` (`Daily | Weekly`), all from `task-management-impl` D4.
  `CreateTaskRequest` gives these defaults of `2 / 1 / Daily` as non-nullable parameters.
  `CreateTaskRequestValidator` allows 0–30 for both intervals. The Create Task form offers first
  ∈ {3, 2, 1} and second ∈ {1, 0}, where `0` is labelled "On the deadline day".
- **Recipients:** `task_recipients` always contains the assignee (BR-5) plus any additional
  recipients. Reassignment adds the new assignee and keeps the old one. The creator is *not* a
  row unless chosen. BRD §3.1/§3.3 says the creator is also reminded.
- **Notifications:** `INotificationPublisher.PublishAsync` adds a row and calls `SaveChanges`
  immediately. The `NotificationType` enum already has `DeadlineReminder` and `Overdue`, and it is
  stored as a string, so adding values is safe. The Notification Center (Screen 12) lists every
  type and links rows that have a `TaskId`.
- **Time:** `TimeProvider` is registered as a singleton (`TimeProvider.System`) and every service
  takes it. All timestamps are UTC. `TaskStageCalculator` and `DashboardService` currently
  compare **UTC** dates for "due today" and "overdue".
- **Hosting/tests:** `Program.cs` migrates and seeds at startup. Integration tests use
  `TaskFlowApiFactory` (a `WebApplicationFactory`) with in-memory config and a temp SQLite file.
  There is no hosted service yet. The TDD (§11) and `project.md` D4 prescribe a `BackgroundService`
  plus a DB dedup index `(TaskId, RecipientUserId, ReminderType, ScheduledFor)`, and an
  `application_settings` key/value table (TDD §7.2).
- **Target:** .NET 10 on Windows. `TimeZoneInfo.FindSystemTimeZoneById` accepts IANA ids there
  through ICU.

## Goals / Non-Goals

**Goals:**
- A stateless, restart-safe engine. Each tick computes what is due *now* from the current task
  rows, and the database alone guarantees at-most-once delivery per (task, recipient, type, slot).
- Settings that are runtime-editable through a small typed service over a generic key/value
  table. Screens 14 and 20 can then add keys without new tables.
- Reminder-day logic as a pure Domain function, testable without a database or clock.

**Non-Goals:**
- Pre-materialising future reminder rows (`Pending`) and rewriting them on every edit (see D2).
- Email sending, SMTP and `email_delivery_logs` (Screen 14). `ReminderHistory` gets a
  `DeliveryAttempts` column now so Screen 14 needs no schema change to it.
- A specific time of day for sending (for example 09:00). Reminders become eligible from local
  midnight of their day (see Open Questions).
- Changing day boundaries in `TaskStageCalculator` or the dashboards (see Risks).

## Decisions

**D1 - Pure Domain rule `ReminderCalculator`.** Signature:
`DueReminders(task snapshot, nowUtc, TimeZoneInfo tz, deadlineDayEnabled) → IReadOnlyList<(ReminderType, DateOnly day, DateTime scheduledForUtc)>`.
It returns only the reminders whose **day is today in `tz`**, which is how the no-backfill rule
works: a day that has ended can never be "today" again. The rules are:
- `deadlineDay = LocalDate(task.Deadline)`. Return nothing if the status is Completed or
  Cancelled.
- First reminder: today = `deadlineDay − First`, when `First > 0`. Second reminder: today =
  `deadlineDay − Second`, when `Second > 0`. When First equals Second, only `SecondReminder` is
  emitted.
- DueToday: today = `deadlineDay` **and** `nowUtc < Deadline` **and** (`deadlineDayEnabled` or
  `First == 0` or `Second == 0`).
- Overdue: today > `deadlineDay` and `(today − deadlineDay) % step == 0`, where the step is 1 day
  for Daily and 7 days for Weekly.
- `scheduledForUtc` = local midnight of `day` converted to UTC (`TimeZoneInfo.ConvertTimeToUtc`,
  handling a DST gap by taking the earliest valid local time, and an ambiguous midnight by taking the
  earlier, daylight-time instant). This is the
  deterministic slot used in the dedup key.

Alternative: put the logic in the Infrastructure service with LINQ date math. Rejected because
SQLite date translation is weak, and the rule is the heart of the feature, so it deserves fast
table-driven unit tests.

**D2 - Compute on each tick, persist on send. No `Pending` rows.** `reminder_history` rows are
written only when a reminder is generated, with status `Sent`. A deadline change needs no
"recalculate" hook: the next tick derives slots from the new deadline, and old slots simply stop
being computed. Completion and cancellation stop reminders the same way, through the status
filter. Alternative: TDD §11.5 materialises future `Pending` rows and marks them `Skipped` on
edits. Rejected because it couples `TaskService.UpdateAsync`, `UpdateStatusAsync` and
`ReassignAsync` to the engine and adds failure modes (stale pending rows) without changing any
observable behavior. `ReminderStatus` keeps `Pending/Sent/Failed/Skipped` for Screen 14 and
future use, but this change writes only `Sent`.

**D3 - Dedup with the unique index and insert-or-ignore inside one transaction per
(task, recipient, reminder).** For each due reminder and recipient, `ReminderService`:
1. skips the pair if a history row for today's slot was already loaded at the start of the pass (one
   read per pass), so reminders delivered earlier in the day do not take the SQLite write lock again;
2. begins a transaction;
3. runs `INSERT INTO ReminderHistory (...) SELECT ... WHERE EXISTS (task still not deleted, not
   Completed/Cancelled, same Deadline) ON CONFLICT (TaskId, RecipientUserId, ReminderType, ScheduledFor) DO NOTHING`
   through `ExecuteSqlInterpolatedAsync`, which returns the affected row count. The `EXISTS` guard
   closes the window where a task is completed, cancelled or rescheduled after the pass read it;
4. only when the row count is 1, adds the `Notification` and saves, then commits.

This makes concurrent ticks, overlapping instances and restarts idempotent at the database level.
Alternative: check-then-insert with a `DbUpdateException` catch. That works too, but a failed
`SaveChanges` poisons the change tracker for the rest of the batch. Raw insert-or-ignore is
explicit and cheap. The notification is written on the service's own `AppDbContext`, not through
`INotificationPublisher.PublishAsync` (which saves on its own), so that the history row and the
notification commit together.

**D4 - Recipient resolution.** recipients = `task_recipients.UserId` ∪ {`CreatedByUserId`},
filtered to users with `IsActive && !IsDeleted`, and de-duplicated. This follows the existing
recipient model, so a reassigned-away assignee stays a recipient until a manager removes them on
the recipients screen. That is consistent with their continued task visibility.

**D5 - `ReminderType` vs `NotificationType`.** Add a Domain enum `ReminderType { FirstReminder,
SecondReminder, DueToday, Overdue }` for history and dedup. Notifications reuse the existing
`NotificationType.DeadlineReminder` (first, second, due-today) and `NotificationType.Overdue`, so
the Notification Center's type filter needs no change. Messages are built in one place, for
example "Reminder: "{Title}" is due in 2 days ({deadline local, dd MMM yyyy HH:mm})",
""{Title}" is due today", or ""{Title}" is overdue since {date}". `project.md`'s different enum
list stays a documented discrepancy, as noted in `notification-center`.

**D6 - Scheduler as a `BackgroundService` using `PeriodicTimer`.** `ReminderSchedulerService`
reads `ReminderEngineOptions` (`Reminders:SchedulerEnabled`, default `true`;
`Reminders:TickSeconds`, default `60`). It runs one pass immediately at startup, then one pass per
tick. Each pass creates a DI scope and calls `IReminderService.GenerateDueAsync(ct)`, catching and
logging exceptions so that one bad pass never stops the loop. `PeriodicTimer` never overlaps
passes inside a single process. Integration tests set `SchedulerEnabled=false` and call
`GenerateDueAsync` directly with a fake `TimeProvider`, which keeps them deterministic.
Alternatives: Hangfire or Quartz. Rejected as new dependencies that the TDD explicitly avoids.

**D7 - Candidate query.** `GenerateDueAsync` loads open, non-deleted tasks whose `Deadline` is at
most `nowUtc + 32 days`, using `AsNoTracking()` and selecting only the needed columns. The upper
bound covers the maximum interval of 30 days plus timezone slack. There is no lower bound, so overdue
tasks keep receiving reminders for as long as they stay open, as the spec requires. The
`Deadline` index serves this query. It reads `ReminderSettings` once per pass and resolves
`TimeZoneInfo` once.

**D8 - `application_settings` key/value table plus a typed settings service.** The table has
`Id, Key (unique), Value, Category, UpdatedByUserId?, UpdatedAt`, matching TDD §7.2.
`IReminderSettingsService` maps these keys to a typed DTO:
`Reminder.Enabled`, `Reminder.DeadlineDayEnabled`, `Reminder.DefaultFirstDays`,
`Reminder.DefaultSecondDays`, `Reminder.DefaultOverdueFrequency`, `Notification.EmailEnabled`,
and the read-only `Application.Timezone`. Values are stored as invariant-culture strings. A
`SettingsInitializer`, run in `Program.cs` right after `MigrateAsync`, inserts any **missing** key
with its default. The timezone comes from `Application:Timezone` in configuration
(`appsettings.json` gets `"Asia/Kolkata"`; the code fallback is `"UTC"`). Existing rows are never
overwritten. The initializer runs regardless of `Seed:Enabled`, because these are required
configuration, not demo data. Alternatives: seeding through EF `HasData`, which cannot read
configuration or a dynamic `UpdatedAt`; or a typed single-row settings table, which would need a
migration for every new setting that Screens 14 and 20 add.

**D9 - API.** A `SettingsController` at `/api/v1/settings/reminders` exposes `GET` and `PUT`.
Both use `[Authorize(Policy = AuthPolicies.ManagerOrAdmin)]`, and the controller stays thin. `PUT`
takes `UpdateReminderSettingsRequest(Enabled, DeadlineDayEnabled, DefaultFirstDays,
DefaultSecondDays, DefaultOverdueFrequency, EmailEnabled)` and returns the saved DTO. The fields are
nullable and the validator requires each one, so an omitted field is a 400 rather than silently
binding to `false`/`0`. The
timezone is simply not part of the request, so it cannot be changed here. A FluentValidation
`UpdateReminderSettingsRequestValidator` enforces: first 1–30, second 0–30, first > second, and a
valid enum. Errors return as `ValidationProblemDetails`, matching the other controllers. The save
records `UpdatedByUserId` (taken from the token) and `UpdatedAt`.

**D10 - Task-creation defaults.** Make `FirstReminderDays`, `SecondReminderDays` and
`OverdueReminderFrequency` on `CreateTaskRequest` **nullable**. `TaskService.CreateAsync` fills
nulls from `IReminderSettingsService.GetAsync()`. The validator rules apply only to non-null
values. This is backward compatible: clients that send values keep their exact values. After the
defaults are applied, the second interval may not exceed a non-zero first interval
(`ReminderIntervals.AreOrdered`, enforced by the validator for explicit pairs and by `CreateAsync`
for mixed explicit/default pairs). On the frontend, `TaskCreateComponent` fetches the reminder
settings when it loads and patches the three controls. If that fetch fails, the form stays usable,
shows a hint, and sends untouched reminder fields as `null` so the server still applies the
configured defaults.

**D11 - Frontend.** Add a `ReminderSettings` model and a root-provided `ReminderSettingsService`
in `core/` (`get()` and `save()`), plus a lazy-loaded `features/settings/reminder-settings.component`
at `/settings/reminders`, guarded by `roleGuard` for `['Administrator', 'Manager']`. The screen
is a reactive form that uses the existing `.tf-*` primitives and shows the current values, a
read-only timezone, the predefined dropdowns (the option constants move from `task-create` into a
shared `features/tasks/reminder-options.ts` and are reused), server-side field errors and a save
confirmation. A nav entry "Reminder Settings" with an existing settings-style icon is added for
Administrator and Manager in `ROLE_NAVIGATION`. If the icon map has no suitable icon, add a `clock`
entry, the same way `bell` was added.

## Risks / Trade-offs

- **[Risk] Day boundaries disagree between the UI and reminders.** `TaskStageCalculator` and
  `DashboardService` use UTC dates, while reminders use the application timezone. Between 00:00
  and 05:30 IST a task can be "due today" in a reminder and "upcoming" on its card. → This change
  adds an `IAppClock` used by the engine, and task 7.3 moves `TaskStageCalculator` and the
  dashboard "due today" / "upcoming" boundaries onto it too, so cards, dashboards and reminders agree.
- **[Risk] Midnight delivery.** Reminders fire just after local midnight, which is odd for email.
  → It is harmless for in-app notifications. A send-time setting can be added with Screen 14
  without changing the dedup key, because the slot stays the local-midnight instant.
- **[Risk] A long outage loses reminders.** This is a direct consequence of the no-backfill
  requirement (Screen 13 FR-6). → Reminders resume on the next due day, and overdue reminders
  continue daily.
- **[Trade-off] Scanning every open task each minute.** At office scale (hundreds to low thousands
  of tasks) this is a cheap indexed query. Most passes insert nothing because the
  insert-or-ignore is a no-op. → If needed later, tick less often or cache "already processed
  today".
- **[Risk] Invalid timezone id in configuration or the database.** → `IAppClock` resolves the
  zone once per pass. On `TimeZoneNotFoundException` it logs an error and falls back to UTC rather
  than stopping reminders. The settings `GET` reports the stored value as-is.
- **[Trade-off] Raw SQL for insert-or-ignore** ties this one statement to SQLite or PostgreSQL
  `ON CONFLICT` syntax. → It is isolated in one method of `ReminderService`, and an integration
  test covers it.

## Migration Plan

1. Migration `AddReminderEngine` creates `reminder_history` (FKs to `tasks` and `users` with
   `ON DELETE RESTRICT`, the unique dedup index, and an index on `(TaskId)`) and
   `application_settings` (unique `Key`). It is additive only.
2. On startup, `MigrateAsync` applies it and `SettingsInitializer` inserts the default keys.
3. The scheduler starts with the app. Existing tasks immediately receive any reminder that is due
   *today*. No historical reminders are generated, because there is no backfill.
4. Rollback: set `Reminders:SchedulerEnabled=false` to stop generation at once, or revert the
   migration. Both tables are new and nothing existing depends on them.

## Open Questions

- Should a configurable send time of day (for example 09:00 local) be added? This does not change
  the specs or the dedup key, and can be decided with Screen 14.
- Should reminder history get a read-only UI (for example on Task Activity or an admin page)? The
  data is retained either way.
