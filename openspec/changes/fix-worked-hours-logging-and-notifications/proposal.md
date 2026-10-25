## Why

Logging worked hours on a task is broken: clicking **Log hours** flashes a blank screen and the hours never appear in the Worked hours field. Hour changes also notify nobody, so the people assigned to the task don't find out when time is added, corrected or removed.

## What Changes

- Fix the **Log hours** form on the task screen so it no longer triggers a native browser form submit (full page reload, the blank flash). Its `(ngSubmit)` binding is inert because the standalone component imports only `ReactiveFormsModule`, so the native submit goes through.
- After a successful log, update the displayed Worked and Remaining values and, when the edit form is open, its **Worked hours** field. Today the edit form is only filled when edit starts, so it stays stale.
- Let a user correct or remove logged time: setting Worked hours to `0` (or clearing the field) resets the total. The backend already accepts `0`, and the frontend will send `0` instead of `null` for a cleared field.
- On every add, update or delete of worked hours, publish an in-app notification and send an internal mail to the task's assigned members: the assignee plus the task recipients, including the user who made the change.
- Cover the frontend and backend changes with tests.

## Capabilities

### New Capabilities
- `task-worked-hours`: logging, editing and clearing a task's worked hours, how the screen reflects them, and the notification and mail sent to assigned members. It is separate from the in-flight `task-time-tracking` delta, which this builds on.

### Modified Capabilities
<!-- none: no specs exist yet under openspec/specs -->

## Impact

- Frontend: `frontend/src/app/features/tasks/task-details.component.{ts,html,spec.ts}`.
- Backend: `TaskService.UpdateAsync` in `TaskFlow.Infrastructure/Services/TaskService.cs`, plus a small hours-change notifier that uses `INotificationPublisher` and `IMailService`.
- No API contract change and no database migration. In-app notifications use the existing `System` notification type.
- Assumption: worked time stays a single running total (`Task.ActualHours`). There is no per-entry time-log table, so "update" and "delete" mean changing or zeroing that total.
